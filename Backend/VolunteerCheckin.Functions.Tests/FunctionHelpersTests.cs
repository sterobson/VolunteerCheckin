using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions.Helpers;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class FunctionHelpersTests
    {
        #region GetFrontendUrl Tests

        [TestMethod]
        public void GetFrontendUrl_RefererWithHash_ExtractsBaseUrl()
        {
            // Arrange - GitHub Pages style URL with hash routing
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://sterobson.github.io/VolunteerCheckin/testing/#/admin/login" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://sterobson.github.io/VolunteerCheckin/testing");
        }

        [TestMethod]
        public void GetFrontendUrl_RefererWithHashAtRoot_ExtractsBaseUrl()
        {
            // Arrange - Local development URL with hash at root
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "http://192.168.1.207:5174/#/" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("http://192.168.1.207:5174");
        }

        [TestMethod]
        public void GetFrontendUrl_RefererWithHashAndDeepPath_ExtractsBaseUrl()
        {
            // Arrange - Deep route in hash
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://example.com/app/v2/#/events/123/marshals/456" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com/app/v2");
        }

        [TestMethod]
        public void GetFrontendUrl_RefererWithoutHash_ReturnsFullPath()
        {
            // Arrange - URL without hash (non-SPA or server-side routing)
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://example.com/app/page/" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com/app/page");
        }

        [TestMethod]
        public void GetFrontendUrl_RefererWithTrailingSlash_TrimsSlash()
        {
            // Arrange
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://example.com/app/" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com/app");
        }

        [TestMethod]
        public void GetFrontendUrl_LocalhostWithPort_PreservesPort()
        {
            // Arrange
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "http://localhost:5174/#/admin" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("http://localhost:5174");
        }

        [TestMethod]
        public void GetFrontendUrl_NoReferer_FallsBackToOrigin()
        {
            // Arrange - Only Origin header (CORS request)
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Origin", "https://example.com" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com");
        }

        [TestMethod]
        public void GetFrontendUrl_OriginWithTrailingSlash_TrimsSlash()
        {
            // Arrange
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Origin", "https://example.com/" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com");
        }

        [TestMethod]
        public void GetFrontendUrl_BothRefererAndOrigin_PrefersReferer()
        {
            // Arrange - Both headers present
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://full-path.example.com/app/#/page" },
                { "Origin", "https://origin-only.example.com" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert - Should use Referer because it has more path information
            result.ShouldBe("https://full-path.example.com/app");
        }

        [TestMethod]
        public void GetFrontendUrl_NoHeaders_FallsBackToDefault()
        {
            // Arrange - No Referer or Origin headers
            HttpRequest request = TestHelpers.CreateEmptyHttpRequest();

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert - Falls back to localhost default
            result.ShouldBe("http://localhost:5174");
        }

        [TestMethod]
        public void GetFrontendUrl_HttpsGitHubPages_CorrectUrl()
        {
            // Arrange - Real GitHub Pages deployment scenario
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://sterobson.github.io/VolunteerCheckin/testing/#/admin/events" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://sterobson.github.io/VolunteerCheckin/testing");
        }

        [TestMethod]
        public void GetFrontendUrl_EmptyHashFragment_ExtractsBaseUrl()
        {
            // Arrange - Hash with nothing after it
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://example.com/app/#" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("https://example.com/app");
        }

        [TestMethod]
        public void GetFrontendUrl_MultipleHashes_ExtractsBeforeFirstHash()
        {
            // Arrange - Edge case: multiple # characters (shouldn't happen, but handle gracefully)
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "https://example.com/app/#/page#section" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert - Takes everything before the first #
            result.ShouldBe("https://example.com/app");
        }

        [TestMethod]
        public void GetFrontendUrl_IpAddressWithPath_PreservesPath()
        {
            // Arrange - IP address with subpath
            HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithHeaders(new Dictionary<string, string>
            {
                { "Referer", "http://192.168.1.100:8080/myapp/#/dashboard" }
            });

            // Act
            string result = FunctionHelpers.GetFrontendUrl(request);

            // Assert
            result.ShouldBe("http://192.168.1.100:8080/myapp");
        }

        #endregion
    }
}
