using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for LogoFunctions - event logo upload and delete operations.
/// </summary>
[TestClass]
public class LogoFunctionsTests
{
    private Mock<ILogger<LogoFunctions>> _mockLogger = null!;
    private Mock<IEventRepository> _mockEventRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private Mock<BlobStorageService> _mockBlobStorageService = null!;
    private LogoFunctions _functions = null!;

    private const string EventId = "event123";
    private const string AdminEmail = "admin@test.com";
    private const string AdminPersonId = "person-123";
    private const string LogoVersion = "1234567890";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<LogoFunctions>>();
        _mockEventRepository = new Mock<IEventRepository>();
        _mockClaimsService = new Mock<ClaimsService>(
            Mock.Of<IAuthSessionRepository>(),
            Mock.Of<IPersonRepository>(),
            Mock.Of<IEventRoleRepository>(),
            Mock.Of<IMarshalRepository>(),
            Mock.Of<ISampleEventService>(),
            Mock.Of<IEventDeletionRepository>()
        );

        // Create mock for BlobStorageService - we need a valid connection string for the constructor
        // but we'll override the virtual methods
        _mockBlobStorageService = new Mock<BlobStorageService>("UseDevelopmentStorage=true") { CallBase = false };

        _functions = new LogoFunctions(
            _mockLogger.Object,
            _mockEventRepository.Object,
            _mockClaimsService.Object,
            _mockBlobStorageService.Object
        );
    }

    private UserClaims CreateAdminClaims()
    {
        return new UserClaims(
            PersonId: AdminPersonId,
            PersonName: "Admin User",
            PersonEmail: AdminEmail,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null,
            EventRoles: new List<EventRoleInfo> { new(Constants.RoleEventAdmin, new List<string>()) }
        );
    }

    private void SetupAuthorizedUser()
    {
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string?>(), EventId))
            .ReturnsAsync(CreateAdminClaims());
    }

    private void SetupUnauthorizedUser()
    {
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string?>(), EventId))
            .ReturnsAsync((UserClaims?)null);
    }

    private EventEntity CreateEventEntity()
    {
        return new EventEntity
        {
            PartitionKey = Constants.EventPartitionKey,
            RowKey = EventId,
            Name = "Test Event",
            EventDate = DateTime.UtcNow.AddDays(30),
            BrandingLogoUrl = string.Empty
        };
    }

    private HttpRequest CreateUploadRequest(string contentType, long contentLength = 1024, bool withAuth = true)
    {
        DefaultHttpContext context = new();
        HttpRequest request = context.Request;

        if (withAuth)
        {
            request.Headers["Authorization"] = "Bearer valid-session-token";
        }
        request.ContentType = contentType;
        request.ContentLength = contentLength;
        request.Body = new MemoryStream(new byte[contentLength]);

        return request;
    }

    #region UploadEventLogo Tests

    [TestMethod]
    public async Task UploadEventLogo_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.UploadLogoAsync(EventId, It.IsAny<Stream>(), "image/png"))
            .ReturnsAsync(LogoVersion);

        EventEntity? capturedEntity = null;
        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Callback<EventEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = CreateUploadRequest("image/png");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        capturedEntity.ShouldNotBeNull();
        // Logo URL is now API path with version query string for cache-busting
        capturedEntity.BrandingLogoUrl.ShouldBe($"/api/events/{EventId}/logo?v={LogoVersion}");
    }

    [TestMethod]
    public async Task UploadEventLogo_JpegImage_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.UploadLogoAsync(EventId, It.IsAny<Stream>(), "image/jpeg"))
            .ReturnsAsync(LogoVersion);

        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = CreateUploadRequest("image/jpeg");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_SvgImage_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.UploadLogoAsync(EventId, It.IsAny<Stream>(), "image/svg+xml"))
            .ReturnsAsync(LogoVersion);

        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = CreateUploadRequest("image/svg+xml");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_NoAuth_ReturnsUnauthorized()
    {
        // Arrange - no authorization header
        HttpRequest request = CreateUploadRequest("image/png", 1024, withAuth: false);

        // Act
        IActionResult result = await _functions.UploadEventLogo(request, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthorizedUser();

        HttpRequest httpRequest = CreateUploadRequest("image/png");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_EventNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupAuthorizedUser();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync((EventEntity?)null);

        HttpRequest httpRequest = CreateUploadRequest("image/png");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        HttpRequest httpRequest = CreateUploadRequest("text/plain");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_FileTooLarge_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        // 6MB - exceeds 5MB limit
        HttpRequest httpRequest = CreateUploadRequest("image/png", 6 * 1024 * 1024);

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_NoContentType_ReturnsBadRequest()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        DefaultHttpContext context = new();
        HttpRequest request = context.Request;
        request.Headers["Authorization"] = "Bearer valid-session-token";
        request.ContentType = null;
        request.Body = new MemoryStream(new byte[1024]);

        // Act
        IActionResult result = await _functions.UploadEventLogo(request, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_GifImage_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.UploadLogoAsync(EventId, It.IsAny<Stream>(), "image/gif"))
            .ReturnsAsync(LogoVersion);

        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = CreateUploadRequest("image/gif");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [TestMethod]
    public async Task UploadEventLogo_WebpImage_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.UploadLogoAsync(EventId, It.IsAny<Stream>(), "image/webp"))
            .ReturnsAsync(LogoVersion);

        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = CreateUploadRequest("image/webp");

        // Act
        IActionResult result = await _functions.UploadEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region DeleteEventLogo Tests

    [TestMethod]
    public async Task DeleteEventLogo_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupAuthorizedUser();
        EventEntity eventEntity = CreateEventEntity();
        eventEntity.BrandingLogoUrl = $"/api/events/{EventId}/logo?v={LogoVersion}";

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync(eventEntity);

        _mockBlobStorageService
            .Setup(s => s.DeleteLogoAsync(EventId))
            .Returns(Task.CompletedTask);

        EventEntity? capturedEntity = null;
        _mockEventRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventEntity>()))
            .Callback<EventEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-session-token");

        // Act
        IActionResult result = await _functions.DeleteEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        capturedEntity.ShouldNotBeNull();
        capturedEntity.BrandingLogoUrl.ShouldBe(string.Empty);

        _mockBlobStorageService.Verify(s => s.DeleteLogoAsync(EventId), Times.Once);
    }

    [TestMethod]
    public async Task DeleteEventLogo_NoAuth_ReturnsUnauthorized()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.DeleteEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task DeleteEventLogo_UnauthorizedUser_ReturnsUnauthorized()
    {
        // Arrange
        SetupUnauthorizedUser();

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("invalid-token");

        // Act
        IActionResult result = await _functions.DeleteEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task DeleteEventLogo_EventNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupAuthorizedUser();

        _mockEventRepository
            .Setup(r => r.GetAsync(EventId))
            .ReturnsAsync((EventEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-session-token");

        // Act
        IActionResult result = await _functions.DeleteEventLogo(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion
}
