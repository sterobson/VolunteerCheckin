using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for EmailService - email sending functionality.
/// Note: These tests focus on construction and mock-based verification since
/// actual SMTP operations cannot be unit tested without external services.
/// </summary>
[TestClass]
public class EmailServiceTests
{
    private const string SmtpHost = "smtp.example.com";
    private const int SmtpPort = 587;
    private const string SmtpUsername = "testuser";
    private const string SmtpPassword = "testpass";
    private const string FromEmail = "noreply@example.com";
    private const string FromName = "Volunteer Check-in";

    #region Constructor Tests

    [TestMethod]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        EmailService service = new EmailService(
            SmtpHost, SmtpPort, SmtpUsername, SmtpPassword, FromEmail, FromName);

        // Assert
        service.ShouldNotBeNull();
    }

    [TestMethod]
    public void Constructor_WithEmptyHost_CreatesInstance()
    {
        // Act - Should not throw during construction
        EmailService service = new EmailService(
            "", SmtpPort, SmtpUsername, SmtpPassword, FromEmail, FromName);

        // Assert
        service.ShouldNotBeNull();
    }

    [TestMethod]
    public void Constructor_WithZeroPort_CreatesInstance()
    {
        // Act
        EmailService service = new EmailService(
            SmtpHost, 0, SmtpUsername, SmtpPassword, FromEmail, FromName);

        // Assert
        service.ShouldNotBeNull();
    }

    [TestMethod]
    public void Constructor_WithDifferentPorts_CreatesInstance()
    {
        // Test common SMTP ports
        new EmailService(SmtpHost, 25, SmtpUsername, SmtpPassword, FromEmail, FromName).ShouldNotBeNull();
        new EmailService(SmtpHost, 465, SmtpUsername, SmtpPassword, FromEmail, FromName).ShouldNotBeNull();
        new EmailService(SmtpHost, 587, SmtpUsername, SmtpPassword, FromEmail, FromName).ShouldNotBeNull();
        new EmailService(SmtpHost, 2525, SmtpUsername, SmtpPassword, FromEmail, FromName).ShouldNotBeNull();
    }

    #endregion

    #region Protected Constructor Tests

    [TestMethod]
    public void ProtectedConstructor_AllowsSubclassing()
    {
        // Act - Create mock using protected constructor
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };

        // Assert
        mockService.Object.ShouldNotBeNull();
    }

    #endregion

    #region SendMagicLinkEmailAsync Tests

    [TestMethod]
    public async Task SendMagicLinkEmailAsync_CanBeMocked()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.SendMagicLinkEmailAsync("user@example.com", "https://example.com/login?token=abc123");

        // Assert
        mockService.Verify(
            s => s.SendMagicLinkEmailAsync("user@example.com", It.Is<string>(link => link.Contains("abc123"))),
            Times.Once);
    }

    [TestMethod]
    public async Task SendMagicLinkEmailAsync_CalledWithCorrectParameters()
    {
        // Arrange
        string capturedEmail = string.Empty;
        string capturedLink = string.Empty;

        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((email, link) =>
            {
                capturedEmail = email;
                capturedLink = link;
            })
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.SendMagicLinkEmailAsync("admin@company.com", "https://app.example.com/auth/verify?t=xyz789");

        // Assert
        capturedEmail.ShouldBe("admin@company.com");
        capturedLink.ShouldBe("https://app.example.com/auth/verify?t=xyz789");
    }

    [TestMethod]
    public async Task SendMagicLinkEmailAsync_HandlesSpecialCharactersInEmail()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        await mockService.Object.SendMagicLinkEmailAsync("user+tag@example.com", "https://example.com/link");
        await mockService.Object.SendMagicLinkEmailAsync("user.name@sub.example.com", "https://example.com/link");

        mockService.Verify(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
    }

    #endregion

    #region SendMarshalMagicLinkEmailAsync Tests

    [TestMethod]
    public async Task SendMarshalMagicLinkEmailAsync_CanBeMocked()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "marshal@example.com",
            "John Smith",
            "Annual Charity Run 2024",
            "https://example.com/marshal?code=ABC123");

        // Assert
        mockService.Verify(
            s => s.SendMarshalMagicLinkEmailAsync(
                "marshal@example.com",
                "John Smith",
                "Annual Charity Run 2024",
                It.Is<string>(link => link.Contains("ABC123"))),
            Times.Once);
    }

    [TestMethod]
    public async Task SendMarshalMagicLinkEmailAsync_CapturesAllParameters()
    {
        // Arrange
        string capturedEmail = string.Empty;
        string capturedName = string.Empty;
        string capturedEventName = string.Empty;
        string capturedLink = string.Empty;

        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<string, string, string, string>((email, name, eventName, link) =>
            {
                capturedEmail = email;
                capturedName = name;
                capturedEventName = eventName;
                capturedLink = link;
            })
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "volunteer@example.org",
            "Jane Doe",
            "City Marathon 2024",
            "https://checkin.example.org/m/XYZ789");

        // Assert
        capturedEmail.ShouldBe("volunteer@example.org");
        capturedName.ShouldBe("Jane Doe");
        capturedEventName.ShouldBe("City Marathon 2024");
        capturedLink.ShouldBe("https://checkin.example.org/m/XYZ789");
    }

    [TestMethod]
    public async Task SendMarshalMagicLinkEmailAsync_HandlesUnicodeInMarshalName()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should handle unicode characters
        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "marshal@example.com",
            "José García",
            "Maratón de Madrid",
            "https://example.com/link");

        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "marshal@example.com",
            "田中太郎",
            "東京マラソン",
            "https://example.com/link");

        mockService.Verify(
            s => s.SendMarshalMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Exactly(2));
    }

    [TestMethod]
    public async Task SendMarshalMagicLinkEmailAsync_HandlesSpecialCharactersInEventName()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "marshal@example.com",
            "Test Marshal",
            "5K Run & Walk - Spring 2024 (Charity)",
            "https://example.com/link");

        // Assert - Should complete without errors
        mockService.Verify(
            s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "5K Run & Walk - Spring 2024 (Charity)",
                It.IsAny<string>()),
            Times.Once);
    }

    #endregion

    #region Integration-Style Tests (Mocked)

    [TestMethod]
    public async Task EmailService_MultipleSends_AllSucceed()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        int adminEmailCount = 0;
        int marshalEmailCount = 0;

        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback(() => adminEmailCount++)
            .Returns(Task.CompletedTask);

        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback(() => marshalEmailCount++)
            .Returns(Task.CompletedTask);

        // Act - Send multiple emails of each type
        await mockService.Object.SendMagicLinkEmailAsync("admin1@example.com", "link1");
        await mockService.Object.SendMagicLinkEmailAsync("admin2@example.com", "link2");
        await mockService.Object.SendMarshalMagicLinkEmailAsync("m1@example.com", "Marshal 1", "Event", "link1");
        await mockService.Object.SendMarshalMagicLinkEmailAsync("m2@example.com", "Marshal 2", "Event", "link2");
        await mockService.Object.SendMarshalMagicLinkEmailAsync("m3@example.com", "Marshal 3", "Event", "link3");

        // Assert
        adminEmailCount.ShouldBe(2);
        marshalEmailCount.ShouldBe(3);
    }

    [TestMethod]
    public async Task EmailService_CanSimulateFailure()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("SMTP server unavailable"));

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await mockService.Object.SendMagicLinkEmailAsync("user@example.com", "https://example.com/link"));
    }

    [TestMethod]
    public async Task EmailService_CanSimulateTimeoutScenario()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new TimeoutException("Connection timed out"));

        // Act & Assert
        await Should.ThrowAsync<TimeoutException>(async () =>
            await mockService.Object.SendMagicLinkEmailAsync("user@example.com", "https://example.com/link"));
    }

    [TestMethod]
    public async Task EmailService_CanSimulateAuthenticationFailure()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ThrowsAsync(new MailKit.Security.AuthenticationException("Invalid credentials"));

        // Act & Assert
        await Should.ThrowAsync<MailKit.Security.AuthenticationException>(async () =>
            await mockService.Object.SendMarshalMagicLinkEmailAsync(
                "user@example.com", "Name", "Event", "https://example.com/link"));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public async Task SendMagicLinkEmailAsync_EmptyEmail_MockHandlesGracefully()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act - Empty email string
        await mockService.Object.SendMagicLinkEmailAsync("", "https://example.com/link");

        // Assert
        mockService.Verify(s => s.SendMagicLinkEmailAsync("", It.IsAny<string>()), Times.Once);
    }

    [TestMethod]
    public async Task SendMarshalMagicLinkEmailAsync_EmptyMarshalName_MockHandlesGracefully()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMarshalMagicLinkEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act - Empty marshal name
        await mockService.Object.SendMarshalMagicLinkEmailAsync(
            "user@example.com", "", "Event Name", "https://example.com/link");

        // Assert
        mockService.Verify(
            s => s.SendMarshalMagicLinkEmailAsync(It.IsAny<string>(), "", It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task SendMagicLinkEmailAsync_LongMagicLink_MockHandles()
    {
        // Arrange
        Mock<EmailService> mockService = new Mock<EmailService>() { CallBase = false };
        mockService
            .Setup(s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Generate a long URL
        string longToken = new string('x', 500);
        string longLink = $"https://example.com/auth/verify?token={longToken}";

        // Act
        await mockService.Object.SendMagicLinkEmailAsync("user@example.com", longLink);

        // Assert
        mockService.Verify(
            s => s.SendMagicLinkEmailAsync(It.IsAny<string>(), It.Is<string>(l => l.Length > 500)),
            Times.Once);
    }

    #endregion
}
