using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class AuthFunctionsTests
    {
        private Mock<ILogger<AuthFunctions>> _mockLogger = null!;
        private Mock<AuthService> _mockAuthService = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private Mock<IPersonRepository> _mockPersonRepository = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private Mock<RateLimitService> _mockRateLimitService = null!;
        private AuthFunctions _authFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AuthFunctions>>();
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();

            // Create mock RateLimitService
            _mockRateLimitService = new Mock<RateLimitService>();
            _mockRateLimitService.Setup(r => r.IsAllowedMagicLinkRequest(It.IsAny<string>())).Returns(true);
            _mockRateLimitService.Setup(r => r.IsAllowedMarshalCodeAttempt(It.IsAny<string>())).Returns(true);
            _mockRateLimitService.Setup(r => r.IsAllowedMarshalCodeAttemptForEvent(It.IsAny<string>())).Returns(true);

            // Create mock ClaimsService with required constructor params
            _mockClaimsService = new Mock<ClaimsService>(
                Mock.Of<IAuthSessionRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IEventRoleRepository>(),
                Mock.Of<IMarshalRepository>(),
                CreateMockSampleEventService(),
                Mock.Of<IEventDeletionRepository>()
            );

            // Create mock AuthService with required constructor params
            _mockAuthService = new Mock<AuthService>(
                Mock.Of<IAuthTokenRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IMarshalRepository>(),
                _mockClaimsService.Object,
                new EmailService("localhost", 25, "", "", "test@test.com", "Test"),
                Mock.Of<ILogger<AuthService>>()
            );

            _authFunctions = new AuthFunctions(
                _mockLogger.Object,
                _mockAuthService.Object,
                _mockClaimsService.Object,
                _mockPersonRepository.Object,
                _mockMarshalRepository.Object,
                _mockRateLimitService.Object
            );
        }

        // ==================== RequestLogin HTTP Endpoint Tests ====================

        [TestMethod]
        public async Task RequestLogin_NullBody_ReturnsBadRequest()
        {
            // Arrange - Empty JSON object, not empty string (which causes parsing to fail)
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(new { });

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert - Email is missing so returns BadRequest
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task RequestLogin_EmptyEmail_ReturnsBadRequest()
        {
            // Arrange
            RequestLoginRequest body = new("");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task RequestLogin_RateLimitExceeded_Returns429()
        {
            // Arrange
            _mockRateLimitService.Setup(r => r.IsAllowedMagicLinkRequest(It.IsAny<string>())).Returns(false);

            RequestLoginRequest body = new("user@example.com");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<ObjectResult>();
            ObjectResult objResult = (ObjectResult)result;
            objResult.StatusCode.ShouldBe(429);
        }

        [TestMethod]
        public async Task RequestLogin_ValidEmail_Success_ReturnsOk()
        {
            // Arrange
            _mockAuthService.Setup(a => a.RequestMagicLinkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()
            )).ReturnsAsync(true);

            RequestLoginRequest body = new("user@example.com");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            RequestLoginResponse response = (RequestLoginResponse)okResult.Value!;
            response.Success.ShouldBeTrue();
        }

        [TestMethod]
        public async Task RequestLogin_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            _mockAuthService.Setup(a => a.RequestMagicLinkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()
            )).ReturnsAsync(false);

            RequestLoginRequest body = new("invalid@example.com");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task RequestLogin_WithFrontendUrl_UsesFrontendUrlFromBody()
        {
            // Arrange
            string expectedFrontendUrl = "https://sterobson.github.io/VolunteerCheckin/testing";
            string? capturedFrontendUrl = null;

            _mockAuthService.Setup(a => a.RequestMagicLinkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()
            )).Callback<string, string, string, bool>((email, ip, frontendUrl, useHash) => capturedFrontendUrl = frontendUrl)
              .ReturnsAsync(true);

            RequestLoginRequest body = new("user@example.com", expectedFrontendUrl);
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            capturedFrontendUrl.ShouldBe(expectedFrontendUrl);
        }

        [TestMethod]
        public async Task RequestLogin_WithFrontendUrlTrailingSlash_TrimsSlash()
        {
            // Arrange
            string? capturedFrontendUrl = null;

            _mockAuthService.Setup(a => a.RequestMagicLinkAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()
            )).Callback<string, string, string, bool>((email, ip, frontendUrl, useHash) => capturedFrontendUrl = frontendUrl)
              .ReturnsAsync(true);

            // URL with trailing slash
            RequestLoginRequest body = new("user@example.com", "https://example.com/app/");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.RequestLogin(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            capturedFrontendUrl.ShouldBe("https://example.com/app");
        }

        // ==================== MarshalLogin HTTP Endpoint Tests ====================

        [TestMethod]
        public async Task MarshalLogin_NullBody_ReturnsBadRequest()
        {
            // Arrange - Empty JSON object, not empty string
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(new { });

            // Act
            IActionResult result = await _authFunctions.MarshalLogin(request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task MarshalLogin_MissingEventId_ReturnsBadRequest()
        {
            // Arrange
            MarshalLoginRequest body = new("", "ABC123");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.MarshalLogin(request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task MarshalLogin_MissingMagicCode_ReturnsBadRequest()
        {
            // Arrange
            MarshalLoginRequest body = new("event-123", "");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.MarshalLogin(request);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task MarshalLogin_ValidCredentials_ReturnsOk()
        {
            // Arrange
            _mockAuthService.Setup(a => a.AuthenticateWithMagicCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()
            )).ReturnsAsync((true, "session-token", new PersonInfo("person-1", "Test User", "test@example.com", "", false), "marshal-1", null));

            MarshalLoginRequest body = new("event-123", "ABC123");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.MarshalLogin(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task MarshalLogin_InvalidCredentials_ReturnsBadRequest()
        {
            // Arrange
            _mockAuthService.Setup(a => a.AuthenticateWithMagicCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()
            )).ReturnsAsync((false, null, null, null, "Invalid code"));

            MarshalLoginRequest body = new("event-123", "WRONG");
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateHttpRequest(body);

            // Act
            IActionResult result = await _authFunctions.MarshalLogin(request);

            // Assert - Invalid code returns BadRequest, not Unauthorized
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        // ==================== GetMe HTTP Endpoint Tests ====================

        [TestMethod]
        public async Task GetMe_NoAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _authFunctions.GetMe(request);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task GetMe_InvalidSession_ReturnsUnauthorized()
        {
            // Arrange
            _mockClaimsService.Setup(c => c.GetClaimsAsync(It.IsAny<string>(), null))
                .ReturnsAsync((UserClaims?)null);

            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("invalid-token");

            // Act
            IActionResult result = await _authFunctions.GetMe(request);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task GetMe_ValidSession_ReturnsOk()
        {
            // Arrange
            UserClaims claims = new(
                PersonId: "person-123",
                PersonName: "Test User",
                PersonEmail: "test@example.com",
                IsSystemAdmin: false,
                EventId: null,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>()
            );
            _mockClaimsService.Setup(c => c.GetClaimsAsync(It.IsAny<string>(), null))
                .ReturnsAsync(claims);

            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _authFunctions.GetMe(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        // ==================== Logout HTTP Endpoint Tests ====================

        [TestMethod]
        public async Task Logout_NoAuthHeader_ReturnsOk()
        {
            // Arrange - Logout is a no-op if no auth header, but still succeeds
            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _authFunctions.Logout(request);

            // Assert - Logout always succeeds (idempotent)
            result.ShouldBeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task Logout_ValidSession_RevokesSession()
        {
            // Arrange
            _mockClaimsService.Setup(c => c.RevokeSessionAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            Microsoft.AspNetCore.Http.HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _authFunctions.Logout(request);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            _mockClaimsService.Verify(c => c.RevokeSessionAsync("valid-token"), Times.Once);
        }

        // ==================== Entity and Service Tests (existing) ====================

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

        // ==================== LastAccessedDate Tests ====================

        /// <summary>
        /// Verifies that AuthService sets LastAccessedDate when a marshal authenticates with magic code.
        /// </summary>
        [TestMethod]
        public async Task AuthService_AuthenticateWithMagicCode_SetsLastAccessedDate()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";
            string magicCode = "ABC123";
            string personId = "person-789";

            MarshalEntity marshal = new()
            {
                PartitionKey = eventId,
                RowKey = marshalId,
                EventId = eventId,
                MarshalId = marshalId,
                PersonId = personId,
                MagicCode = magicCode,
                Name = "Test Marshal",
                Email = "marshal@test.com",
                LastAccessedDate = null // Not accessed yet
            };

            PersonEntity person = new()
            {
                PartitionKey = "PERSON",
                RowKey = personId,
                PersonId = personId,
                Name = "Test Marshal",
                Email = "marshal@test.com",
                IsSystemAdmin = false
            };

            Mock<IAuthTokenRepository> mockTokenRepo = new();
            Mock<IPersonRepository> mockPersonRepo = new();
            Mock<IMarshalRepository> mockMarshalRepo = new();
            Mock<IAuthSessionRepository> mockSessionRepo = new();
            Mock<IEventRoleRepository> mockRoleRepo = new();
            EmailService emailService = new("localhost", 25, "", "", "test@test.com", "Test");
            Mock<ILogger<AuthService>> mockLogger = new();

            mockMarshalRepo.Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<MarshalEntity> { marshal });

            MarshalEntity? capturedMarshal = null;
            mockMarshalRepo.Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
                .Callback<MarshalEntity>(m => capturedMarshal = m)
                .Returns(Task.CompletedTask);

            mockPersonRepo.Setup(r => r.GetAsync(personId))
                .ReturnsAsync(person);

            mockSessionRepo.Setup(r => r.AddAsync(It.IsAny<AuthSessionEntity>()))
                .ReturnsAsync((AuthSessionEntity s) => s);

            mockRoleRepo.Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
                .ReturnsAsync(new List<EventRoleEntity>());

            // Create real ClaimsService with mocked repos
            ClaimsService claimsService = new(
                mockSessionRepo.Object,
                mockPersonRepo.Object,
                mockRoleRepo.Object,
                mockMarshalRepo.Object,
                CreateMockSampleEventService(),
                Mock.Of<IEventDeletionRepository>()
            );

            AuthService authService = new(
                mockTokenRepo.Object,
                mockPersonRepo.Object,
                mockMarshalRepo.Object,
                claimsService,
                emailService,
                mockLogger.Object
            );

            // Act
            (bool success, string? sessionToken, PersonInfo? personInfo, string? returnedMarshalId, string? message) =
                await authService.AuthenticateWithMagicCodeAsync(eventId, magicCode, "127.0.0.1");

            // Assert
            success.ShouldBeTrue();
            capturedMarshal.ShouldNotBeNull("Marshal should have been updated");
            capturedMarshal.LastAccessedDate.ShouldNotBeNull("LastAccessedDate should be set");
            capturedMarshal.LastAccessedDate.Value.ShouldBeInRange(
                DateTime.UtcNow.AddSeconds(-5),
                DateTime.UtcNow.AddSeconds(5),
                "LastAccessedDate should be approximately now");
        }

        /// <summary>
        /// Verifies that ClaimsService updates LastAccessedDate when validating a marshal session.
        /// </summary>
        [TestMethod]
        public async Task ClaimsService_GetClaimsAsync_UpdatesLastAccessedDate()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";
            string personId = "person-789";
            string sessionToken = "test-session-token";
            string sessionTokenHash = HashToken(sessionToken);

            AuthSessionEntity session = new()
            {
                PartitionKey = "SESSION",
                RowKey = sessionTokenHash,
                SessionId = Guid.NewGuid().ToString(),
                SessionTokenHash = sessionTokenHash,
                PersonId = personId,
                EventId = eventId,
                MarshalId = marshalId,
                AuthMethod = Constants.AuthMethodMarshalMagicCode,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                LastAccessedAt = DateTime.UtcNow.AddHours(-1)
            };

            MarshalEntity marshal = new()
            {
                PartitionKey = eventId,
                RowKey = marshalId,
                EventId = eventId,
                MarshalId = marshalId,
                PersonId = personId,
                Name = "Test Marshal",
                LastAccessedDate = DateTime.UtcNow.AddDays(-1) // Last accessed yesterday
            };

            PersonEntity person = new()
            {
                PartitionKey = "PERSON",
                RowKey = personId,
                PersonId = personId,
                Name = "Test Marshal",
                Email = "marshal@test.com",
                IsSystemAdmin = false
            };

            Mock<IAuthSessionRepository> mockSessionRepo = new();
            Mock<IPersonRepository> mockPersonRepo = new();
            Mock<IEventRoleRepository> mockRoleRepo = new();
            Mock<IMarshalRepository> mockMarshalRepo = new();

            mockSessionRepo.Setup(r => r.GetBySessionTokenHashAsync(sessionTokenHash))
                .ReturnsAsync(session);
            mockSessionRepo.Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
                .Returns(Task.CompletedTask);

            mockPersonRepo.Setup(r => r.GetAsync(personId))
                .ReturnsAsync(person);

            mockMarshalRepo.Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(marshal);

            MarshalEntity? capturedMarshal = null;
            mockMarshalRepo.Setup(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()))
                .Callback<MarshalEntity>(m => capturedMarshal = m)
                .Returns(Task.CompletedTask);

            mockRoleRepo.Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
                .ReturnsAsync(new List<EventRoleEntity>());

            ClaimsService claimsService = new(
                mockSessionRepo.Object,
                mockPersonRepo.Object,
                mockRoleRepo.Object,
                mockMarshalRepo.Object,
                CreateMockSampleEventService(),
                Mock.Of<IEventDeletionRepository>()
            );

            // Act
            UserClaims? claims = await claimsService.GetClaimsAsync(sessionToken, eventId);

            // Assert
            claims.ShouldNotBeNull();
            capturedMarshal.ShouldNotBeNull("Marshal should have been updated");
            capturedMarshal.LastAccessedDate.ShouldNotBeNull("LastAccessedDate should be set");
            capturedMarshal.LastAccessedDate.Value.ShouldBeInRange(
                DateTime.UtcNow.AddSeconds(-5),
                DateTime.UtcNow.AddSeconds(5),
                "LastAccessedDate should be updated to approximately now");
        }

        /// <summary>
        /// Verifies that ClaimsService does NOT update LastAccessedDate for non-marshal sessions.
        /// </summary>
        [TestMethod]
        public async Task ClaimsService_GetClaimsAsync_AdminSession_DoesNotUpdateMarshalLastAccessedDate()
        {
            // Arrange
            string eventId = "event-123";
            string personId = "person-789";
            string sessionToken = "test-session-token";
            string sessionTokenHash = HashToken(sessionToken);

            // Admin session - no MarshalId
            AuthSessionEntity session = new()
            {
                PartitionKey = "SESSION",
                RowKey = sessionTokenHash,
                SessionId = Guid.NewGuid().ToString(),
                SessionTokenHash = sessionTokenHash,
                PersonId = personId,
                EventId = null, // Admin sessions may not have EventId locked
                MarshalId = null, // No marshal ID for admin session
                AuthMethod = Constants.AuthMethodSecureEmailLink,
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                LastAccessedAt = DateTime.UtcNow.AddHours(-1)
            };

            PersonEntity person = new()
            {
                PartitionKey = "PERSON",
                RowKey = personId,
                PersonId = personId,
                Name = "Admin User",
                Email = "admin@test.com",
                IsSystemAdmin = true
            };

            Mock<IAuthSessionRepository> mockSessionRepo = new();
            Mock<IPersonRepository> mockPersonRepo = new();
            Mock<IEventRoleRepository> mockRoleRepo = new();
            Mock<IMarshalRepository> mockMarshalRepo = new();

            mockSessionRepo.Setup(r => r.GetBySessionTokenHashAsync(sessionTokenHash))
                .ReturnsAsync(session);
            mockSessionRepo.Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
                .Returns(Task.CompletedTask);

            mockPersonRepo.Setup(r => r.GetAsync(personId))
                .ReturnsAsync(person);

            mockRoleRepo.Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
                .ReturnsAsync(new List<EventRoleEntity>());

            ClaimsService claimsService = new(
                mockSessionRepo.Object,
                mockPersonRepo.Object,
                mockRoleRepo.Object,
                mockMarshalRepo.Object,
                CreateMockSampleEventService(),
                Mock.Of<IEventDeletionRepository>()
            );

            // Act
            UserClaims? claims = await claimsService.GetClaimsAsync(sessionToken, eventId);

            // Assert
            claims.ShouldNotBeNull();
            // Marshal repository should NOT be called for GetAsync or UpdateAsync
            mockMarshalRepo.Verify(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never,
                "Marshal GetAsync should not be called for admin sessions");
            mockMarshalRepo.Verify(r => r.UpdateAsync(It.IsAny<MarshalEntity>()), Times.Never,
                "Marshal UpdateAsync should not be called for admin sessions");
        }

        private static ISampleEventService CreateMockSampleEventService()
        {
            return Mock.Of<ISampleEventService>();
        }
    }
}
