using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class AuthFunctionsTests
    {
        private Mock<ILogger<AuthFunctions>> _mockLogger = null!;
        private Mock<IAdminUserRepository> _mockAdminUserRepository = null!;
        private AuthFunctions _authFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AuthFunctions>>();
            _mockAdminUserRepository = new Mock<IAdminUserRepository>();

            _authFunctions = new AuthFunctions(
                _mockLogger.Object,
                _mockAdminUserRepository.Object
            );
        }

        #region InstantLogin Tests

        [TestMethod]
        public async Task InstantLogin_ExistingAdmin_ReturnsSuccess()
        {
            // Arrange
            string email = "existing@example.com";

            AdminUserEntity existingAdmin = new()
            {
                RowKey = email,
                Email = email
            };

            _mockAdminUserRepository
                .Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(existingAdmin);

            InstantLoginRequest request = new(email);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.InstantLogin(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            InstantLoginResponse? response = okResult.Value as InstantLoginResponse;
            response.ShouldNotBeNull();
            response.Success.ShouldBeTrue();
            response.Email.ShouldBe(email);

            // Verify no new admin was created
            _mockAdminUserRepository.Verify(
                r => r.AddAsync(It.IsAny<AdminUserEntity>()),
                Times.Never
            );
        }

        [TestMethod]
        public async Task InstantLogin_NewAdmin_CreatesAdminAndReturnsSuccess()
        {
            // Arrange
            string email = "new@example.com";

            _mockAdminUserRepository
                .Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync((AdminUserEntity?)null);

            InstantLoginRequest request = new(email);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.InstantLogin(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            InstantLoginResponse? response = okResult.Value as InstantLoginResponse;
            response.ShouldNotBeNull();
            response.Success.ShouldBeTrue();
            response.Email.ShouldBe(email);

            // Verify new admin was created
            _mockAdminUserRepository.Verify(
                r => r.AddAsync(It.Is<AdminUserEntity>(a =>
                    a.Email == email &&
                    a.RowKey == email
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task InstantLogin_NullEmail_ReturnsBadRequest()
        {
            // Arrange
            InstantLoginRequest request = new(null!);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.InstantLogin(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task InstantLogin_EmptyEmail_ReturnsBadRequest()
        {
            // Arrange
            InstantLoginRequest request = new("");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.InstantLogin(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task InstantLogin_WhitespaceEmail_ReturnsBadRequest()
        {
            // Arrange
            InstantLoginRequest request = new("   ");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.InstantLogin(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region CreateAdmin Tests

        [TestMethod]
        public async Task CreateAdmin_ValidEmail_CreatesAdmin()
        {
            // Arrange
            string email = "admin@example.com";

            InstantLoginRequest request = new(email);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.CreateAdmin(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAdminUserRepository.Verify(
                r => r.AddAsync(It.Is<AdminUserEntity>(a =>
                    a.Email == email &&
                    a.RowKey == email
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateAdmin_NullEmail_ReturnsBadRequest()
        {
            // Arrange
            InstantLoginRequest request = new(null!);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.CreateAdmin(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task CreateAdmin_EmptyEmail_ReturnsBadRequest()
        {
            // Arrange
            InstantLoginRequest request = new("");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _authFunctions.CreateAdmin(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion
    }
}
