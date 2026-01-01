using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class RateLimitServiceTests
    {
        private RateLimitService _rateLimitService = null!;

        [TestInitialize]
        public void Setup()
        {
            _rateLimitService = new RateLimitService();
        }

        [TestMethod]
        public void IsAllowed_FirstRequest_ReturnsTrue()
        {
            // Arrange
            string key = "test-key-1";

            // Act
            bool result = _rateLimitService.IsAllowed(key, maxRequests: 5, windowSeconds: 60);

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void IsAllowed_UnderLimit_ReturnsTrue()
        {
            // Arrange
            string key = "test-key-2";

            // Act & Assert
            for (int i = 0; i < 5; i++)
            {
                bool result = _rateLimitService.IsAllowed(key, maxRequests: 5, windowSeconds: 60);
                result.ShouldBeTrue();
            }
        }

        [TestMethod]
        public void IsAllowed_AtLimit_ReturnsFalse()
        {
            // Arrange
            string key = "test-key-3";

            // Use up all allowed requests
            for (int i = 0; i < 5; i++)
            {
                _rateLimitService.IsAllowed(key, maxRequests: 5, windowSeconds: 60);
            }

            // Act - 6th request should be rejected
            bool result = _rateLimitService.IsAllowed(key, maxRequests: 5, windowSeconds: 60);

            // Assert
            result.ShouldBeFalse();
        }

        [TestMethod]
        public void IsAllowed_DifferentKeys_Independent()
        {
            // Arrange
            string key1 = "test-key-4a";
            string key2 = "test-key-4b";

            // Use up all allowed requests for key1
            for (int i = 0; i < 5; i++)
            {
                _rateLimitService.IsAllowed(key1, maxRequests: 5, windowSeconds: 60);
            }

            // Act - key2 should still be allowed
            bool result = _rateLimitService.IsAllowed(key2, maxRequests: 5, windowSeconds: 60);

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void IsAllowedMagicLinkRequest_UnderLimit_ReturnsTrue()
        {
            // Arrange
            string email = "test@example.com";

            // Act
            bool result = _rateLimitService.IsAllowedMagicLinkRequest(email);

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void IsAllowedMagicLinkRequest_AtLimit_ReturnsFalse()
        {
            // Arrange
            string email = "ratelimit-test@example.com";

            // Use up all allowed requests (5 per hour)
            for (int i = 0; i < 5; i++)
            {
                _rateLimitService.IsAllowedMagicLinkRequest(email);
            }

            // Act - 6th request should be rejected
            bool result = _rateLimitService.IsAllowedMagicLinkRequest(email);

            // Assert
            result.ShouldBeFalse();
        }

        [TestMethod]
        public void IsAllowedMagicLinkRequest_CaseInsensitive()
        {
            // Arrange
            string email1 = "Test@Example.Com";
            string email2 = "test@example.com";

            // Use up all requests with uppercase
            for (int i = 0; i < 5; i++)
            {
                _rateLimitService.IsAllowedMagicLinkRequest(email1);
            }

            // Act - lowercase should also be rejected (same email)
            bool result = _rateLimitService.IsAllowedMagicLinkRequest(email2);

            // Assert
            result.ShouldBeFalse();
        }

        [TestMethod]
        public void IsAllowedMarshalCodeAttempt_UnderLimit_ReturnsTrue()
        {
            // Arrange
            string ipAddress = "192.168.1.1";

            // Act
            bool result = _rateLimitService.IsAllowedMarshalCodeAttempt(ipAddress);

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void IsAllowedMarshalCodeAttempt_AtLimit_ReturnsFalse()
        {
            // Arrange
            string ipAddress = "192.168.1.2";

            // Use up all allowed requests (10 per minute)
            for (int i = 0; i < 10; i++)
            {
                _rateLimitService.IsAllowedMarshalCodeAttempt(ipAddress);
            }

            // Act - 11th request should be rejected
            bool result = _rateLimitService.IsAllowedMarshalCodeAttempt(ipAddress);

            // Assert
            result.ShouldBeFalse();
        }

        [TestMethod]
        public void IsAllowedMarshalCodeAttemptForEvent_UnderLimit_ReturnsTrue()
        {
            // Arrange
            string eventId = "event-123";

            // Act
            bool result = _rateLimitService.IsAllowedMarshalCodeAttemptForEvent(eventId);

            // Assert
            result.ShouldBeTrue();
        }

        [TestMethod]
        public void GetRemainingRequests_NoRequests_ReturnsMax()
        {
            // Arrange
            string key = "remaining-test-1";

            // Act
            int remaining = _rateLimitService.GetRemainingRequests(key, maxRequests: 5, windowSeconds: 60);

            // Assert
            remaining.ShouldBe(5);
        }

        [TestMethod]
        public void GetRemainingRequests_SomeRequests_ReturnsCorrectCount()
        {
            // Arrange
            string key = "remaining-test-2";

            // Make 3 requests
            for (int i = 0; i < 3; i++)
            {
                _rateLimitService.IsAllowed(key, maxRequests: 5, windowSeconds: 60);
            }

            // Act
            int remaining = _rateLimitService.GetRemainingRequests(key, maxRequests: 5, windowSeconds: 60);

            // Assert
            remaining.ShouldBe(2);
        }
    }
}
