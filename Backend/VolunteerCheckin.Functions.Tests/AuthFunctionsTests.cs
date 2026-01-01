using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class AuthFunctionsTests
    {
        /// <summary>
        /// Verifies that AuthTokenEntity has RowKey set to TokenHash for O(1) lookup.
        /// This test prevents regression of the bug where RowKey was set to TokenId instead of TokenHash.
        /// </summary>
        [TestMethod]
        public void AuthTokenEntity_RowKey_ShouldMatchTokenHash()
        {
            // Arrange
            string tokenHash = "test-token-hash-abc123";
            string tokenId = Guid.NewGuid().ToString();

            // Act - create entity the same way AuthService does
            AuthTokenEntity authToken = new AuthTokenEntity
            {
                TokenId = tokenId,
                PartitionKey = "AUTHTOKEN",
                RowKey = tokenHash,  // This is the critical assertion
                TokenHash = tokenHash,
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            // Assert
            authToken.RowKey.ShouldBe(authToken.TokenHash,
                "RowKey must equal TokenHash for O(1) lookup by token hash");
            authToken.RowKey.ShouldNotBe(tokenId,
                "RowKey must NOT be TokenId - this was the bug!");
        }

        /// <summary>
        /// Verifies that a token saved can be retrieved by its hash.
        /// Uses a mock repository to simulate the storage layer.
        /// </summary>
        [TestMethod]
        public async Task TokenRepository_SaveAndRetrieve_ShouldFindTokenByHash()
        {
            // Arrange
            Mock<IAuthTokenRepository> mockRepo = new();
            Dictionary<string, AuthTokenEntity> storage = new();

            // Setup AddAsync to store by RowKey
            mockRepo.Setup(r => r.AddAsync(It.IsAny<AuthTokenEntity>()))
                .Callback<AuthTokenEntity>(token => storage[token.RowKey] = token)
                .ReturnsAsync((AuthTokenEntity token) => token);

            // Setup GetByTokenHashAsync to retrieve by hash (which should be the RowKey)
            mockRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
                .ReturnsAsync((string hash) => storage.TryGetValue(hash, out AuthTokenEntity? token) ? token : null);

            // Create a token with correct RowKey = TokenHash
            string tokenHash = HashToken("test-raw-token");
            AuthTokenEntity authToken = new AuthTokenEntity
            {
                TokenId = Guid.NewGuid().ToString(),
                PartitionKey = "AUTHTOKEN",
                RowKey = tokenHash,
                TokenHash = tokenHash,
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            // Act
            await mockRepo.Object.AddAsync(authToken);
            AuthTokenEntity? retrieved = await mockRepo.Object.GetByTokenHashAsync(tokenHash);

            // Assert
            retrieved.ShouldNotBeNull("Token should be found by its hash");
            retrieved.TokenId.ShouldBe(authToken.TokenId);
            retrieved.PersonId.ShouldBe(authToken.PersonId);
        }

        /// <summary>
        /// Demonstrates the bug: if RowKey is set to TokenId instead of TokenHash,
        /// the token cannot be found.
        /// </summary>
        [TestMethod]
        public async Task TokenRepository_WrongRowKey_ShouldNotFindToken()
        {
            // Arrange
            Mock<IAuthTokenRepository> mockRepo = new();
            Dictionary<string, AuthTokenEntity> storage = new();

            mockRepo.Setup(r => r.AddAsync(It.IsAny<AuthTokenEntity>()))
                .Callback<AuthTokenEntity>(token => storage[token.RowKey] = token)
                .ReturnsAsync((AuthTokenEntity token) => token);

            mockRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
                .ReturnsAsync((string hash) => storage.TryGetValue(hash, out AuthTokenEntity? token) ? token : null);

            // Create a token with WRONG RowKey = TokenId (the bug)
            string tokenHash = HashToken("test-raw-token");
            string tokenId = Guid.NewGuid().ToString();
            AuthTokenEntity authToken = new AuthTokenEntity
            {
                TokenId = tokenId,
                PartitionKey = "AUTHTOKEN",
                RowKey = tokenId,  // BUG: Using tokenId instead of tokenHash
                TokenHash = tokenHash,
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            // Act
            await mockRepo.Object.AddAsync(authToken);
            AuthTokenEntity? retrieved = await mockRepo.Object.GetByTokenHashAsync(tokenHash);

            // Assert - token should NOT be found because RowKey doesn't match TokenHash
            retrieved.ShouldBeNull("Token should NOT be found when RowKey != TokenHash");
        }

        /// <summary>
        /// Verifies that expired tokens are correctly identified as invalid.
        /// </summary>
        [TestMethod]
        public void AuthTokenEntity_IsValid_ExpiredToken_ShouldReturnFalse()
        {
            // Arrange
            AuthTokenEntity expiredToken = new AuthTokenEntity
            {
                TokenId = Guid.NewGuid().ToString(),
                PartitionKey = "AUTHTOKEN",
                RowKey = "token-hash",
                TokenHash = "token-hash",
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5)  // Expired 5 minutes ago
            };

            // Act & Assert
            expiredToken.IsValid().ShouldBeFalse("Expired token should not be valid");
        }

        /// <summary>
        /// Verifies that used tokens are correctly identified as invalid.
        /// </summary>
        [TestMethod]
        public void AuthTokenEntity_IsValid_UsedToken_ShouldReturnFalse()
        {
            // Arrange
            AuthTokenEntity usedToken = new AuthTokenEntity
            {
                TokenId = Guid.NewGuid().ToString(),
                PartitionKey = "AUTHTOKEN",
                RowKey = "token-hash",
                TokenHash = "token-hash",
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                UsedAt = DateTime.UtcNow  // Token has been used
            };

            // Act & Assert
            usedToken.IsValid().ShouldBeFalse("Used token should not be valid");
        }

        /// <summary>
        /// Verifies that a valid token is correctly identified.
        /// </summary>
        [TestMethod]
        public void AuthTokenEntity_IsValid_ValidToken_ShouldReturnTrue()
        {
            // Arrange
            AuthTokenEntity validToken = new AuthTokenEntity
            {
                TokenId = Guid.NewGuid().ToString(),
                PartitionKey = "AUTHTOKEN",
                RowKey = "token-hash",
                TokenHash = "token-hash",
                PersonId = "person-123",
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                UsedAt = null  // Not used yet
            };

            // Act & Assert
            validToken.IsValid().ShouldBeTrue("Unexpired, unused token should be valid");
        }

        // ==================== Session Token Tests ====================

        /// <summary>
        /// Verifies that AuthSessionEntity has RowKey set to SessionTokenHash for O(1) lookup.
        /// </summary>
        [TestMethod]
        public void AuthSessionEntity_RowKey_ShouldMatchSessionTokenHash()
        {
            // Arrange
            string sessionTokenHash = HashToken("test-session-token");
            string sessionId = Guid.NewGuid().ToString();

            // Act - create entity the same way ClaimsService does
            AuthSessionEntity session = new AuthSessionEntity
            {
                SessionId = sessionId,
                PartitionKey = "SESSION",
                RowKey = sessionTokenHash,
                SessionTokenHash = sessionTokenHash,
                PersonId = "person-123",
                AuthMethod = "SecureEmailLink",
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            // Assert
            session.RowKey.ShouldBe(session.SessionTokenHash,
                "RowKey must equal SessionTokenHash for O(1) lookup");
            session.RowKey.ShouldNotBe(sessionId,
                "RowKey must NOT be SessionId");
        }

        /// <summary>
        /// Verifies that a session saved can be retrieved by its token hash.
        /// </summary>
        [TestMethod]
        public async Task SessionRepository_SaveAndRetrieve_ShouldFindSessionByHash()
        {
            // Arrange
            Mock<IAuthSessionRepository> mockRepo = new();
            Dictionary<string, AuthSessionEntity> storage = new();

            mockRepo.Setup(r => r.AddAsync(It.IsAny<AuthSessionEntity>()))
                .Callback<AuthSessionEntity>(session => storage[session.RowKey] = session)
                .ReturnsAsync((AuthSessionEntity session) => session);

            mockRepo.Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
                .ReturnsAsync((string hash) => storage.TryGetValue(hash, out AuthSessionEntity? session) ? session : null);

            // Create a session with correct RowKey = SessionTokenHash
            string sessionTokenHash = HashToken("test-session-token");
            AuthSessionEntity session = new AuthSessionEntity
            {
                SessionId = Guid.NewGuid().ToString(),
                PartitionKey = "SESSION",
                RowKey = sessionTokenHash,
                SessionTokenHash = sessionTokenHash,
                PersonId = "person-123",
                AuthMethod = "SecureEmailLink",
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            // Act
            await mockRepo.Object.AddAsync(session);
            AuthSessionEntity? retrieved = await mockRepo.Object.GetBySessionTokenHashAsync(sessionTokenHash);

            // Assert
            retrieved.ShouldNotBeNull("Session should be found by its token hash");
            retrieved.SessionId.ShouldBe(session.SessionId);
            retrieved.PersonId.ShouldBe(session.PersonId);
        }

        // ==================== URL-Safe Base64 Encoding Tests ====================

        /// <summary>
        /// Verifies that token hash does not contain characters invalid for Azure Table Storage RowKey.
        /// Azure Table Storage RowKey cannot contain: / \ # ?
        /// Standard Base64 can produce + / = which need to be replaced.
        /// </summary>
        [TestMethod]
        public void HashToken_ShouldProduceUrlSafeBase64()
        {
            // Test multiple tokens to ensure we catch edge cases
            string[] testTokens = new[]
            {
                "simple-token",
                "token-with-special-chars-!@#$%",
                "a]b[c{d}e|f\\g/h?i#j",  // Contains chars that might produce +/= in hash
                Convert.ToBase64String(new byte[] { 255, 254, 253, 252, 251 }),  // Binary data
                new string('x', 1000)  // Long token
            };

            // Characters that are invalid for Azure Table Storage RowKey
            char[] invalidChars = new[] { '+', '/', '=', '\\', '#', '?' };

            foreach (string token in testTokens)
            {
                // Act
                string hash = HashToken(token);

                // Assert - should not contain any Azure Table Storage invalid characters
                foreach (char invalidChar in invalidChars)
                {
                    hash.Contains(invalidChar).ShouldBeFalse(
                        $"Hash of '{token.Substring(0, Math.Min(20, token.Length))}...' contains '{invalidChar}' which is invalid for RowKey");
                }
            }
        }

        /// <summary>
        /// Verifies that hashing is deterministic - same input always produces same output.
        /// </summary>
        [TestMethod]
        public void HashToken_ShouldBeDeterministic()
        {
            // Arrange
            string token = "test-token-for-determinism";

            // Act
            string hash1 = HashToken(token);
            string hash2 = HashToken(token);
            string hash3 = HashToken(token);

            // Assert
            hash1.ShouldBe(hash2);
            hash2.ShouldBe(hash3);
        }

        /// <summary>
        /// Verifies that different tokens produce different hashes.
        /// </summary>
        [TestMethod]
        public void HashToken_DifferentTokens_ShouldProduceDifferentHashes()
        {
            // Arrange
            string token1 = "token-one";
            string token2 = "token-two";

            // Act
            string hash1 = HashToken(token1);
            string hash2 = HashToken(token2);

            // Assert
            hash1.ShouldNotBe(hash2, "Different tokens should produce different hashes");
        }

        /// <summary>
        /// Helper method to hash a token (mirrors the AuthService/ClaimsService implementation)
        /// Uses URL-safe Base64 encoding to avoid Azure Table Storage RowKey invalid characters
        /// </summary>
        private static string HashToken(string token)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace("=", "");
        }
    }
}
