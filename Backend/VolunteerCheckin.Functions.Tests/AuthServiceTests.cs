using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for AuthService - authentication operations.
/// </summary>
[TestClass]
public class AuthServiceTests
{
    private Mock<IAuthTokenRepository> _mockTokenRepository = null!;
    private Mock<IPersonRepository> _mockPersonRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private Mock<EmailService> _mockEmailService = null!;
    private Mock<ILogger<AuthService>> _mockLogger = null!;
    private AuthService _authService = null!;

    private const string EventId = "event123";
    private const string PersonId = "person123";
    private const string MarshalId = "marshal456";
    private const string Email = "test@example.com";
    private const string IpAddress = "192.168.1.1";
    private const string BaseUrl = "https://example.com";
    private const string SessionToken = "session-token-123";
    private const string MagicCode = "ABC123";

    [TestInitialize]
    public void Setup()
    {
        _mockTokenRepository = new Mock<IAuthTokenRepository>();
        _mockPersonRepository = new Mock<IPersonRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _mockClaimsService = new Mock<ClaimsService>(
            Mock.Of<IAuthSessionRepository>(),
            Mock.Of<IPersonRepository>(),
            Mock.Of<IEventRoleRepository>(),
            Mock.Of<IMarshalRepository>(),
            Mock.Of<ISampleEventService>(),
            Mock.Of<IEventDeletionRepository>()
        );

        // EmailService has a protected parameterless constructor for testing
        _mockEmailService = new Mock<EmailService>() { CallBase = false };

        _authService = new AuthService(
            _mockTokenRepository.Object,
            _mockPersonRepository.Object,
            _mockMarshalRepository.Object,
            _mockClaimsService.Object,
            _mockEmailService.Object,
            _mockLogger.Object
        );
    }

    private PersonEntity CreatePersonEntity(string personId, string email)
    {
        return new PersonEntity
        {
            PartitionKey = "PERSON",
            RowKey = personId,
            PersonId = personId,
            Name = "Test User",
            Email = email,
            Phone = "555-1234",
            IsSystemAdmin = false,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
    }

    private MarshalEntity CreateMarshalEntity(string marshalId, string magicCode, string personId = "")
    {
        return new MarshalEntity
        {
            PartitionKey = EventId,
            RowKey = marshalId,
            MarshalId = marshalId,
            Name = "Test Marshal",
            Email = Email,
            PhoneNumber = "555-1234",
            MagicCode = magicCode,
            PersonId = personId
        };
    }

    #region RequestMagicLinkAsync Tests

    [TestMethod]
    public async Task RequestMagicLinkAsync_ExistingPerson_SendsEmail()
    {
        // Arrange
        PersonEntity existingPerson = CreatePersonEntity(PersonId, Email);

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync(Email))
            .ReturnsAsync(existingPerson);

        AuthTokenEntity? capturedToken = null;
        _mockTokenRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthTokenEntity>()))
            .Callback<AuthTokenEntity>(t => capturedToken = t)
            .ReturnsAsync((AuthTokenEntity t) => t);

        _mockEmailService
            .Setup(e => e.SendMagicLinkEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        bool result = await _authService.RequestMagicLinkAsync(Email, IpAddress, BaseUrl, true);

        // Assert
        result.ShouldBeTrue();
        capturedToken.ShouldNotBeNull();
        capturedToken.PersonId.ShouldBe(PersonId);
        capturedToken.RequestIpAddress.ShouldBe(IpAddress);
        capturedToken.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow);
        capturedToken.LoginCode.ShouldNotBeNullOrEmpty();
        capturedToken.LoginCode.Length.ShouldBe(6);

        _mockEmailService.Verify(
            e => e.SendMagicLinkEmailAsync(Email, It.Is<string>(s => s.Contains(BaseUrl)), It.IsAny<string>()),
            Times.Once);
    }

    [TestMethod]
    public async Task RequestMagicLinkAsync_NewPerson_CreatesPersonAndSendsEmail()
    {
        // Arrange
        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync(Email))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedPerson = null;
        _mockPersonRepository
            .Setup(r => r.AddAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(p => capturedPerson = p)
            .ReturnsAsync((PersonEntity p) => p);

        _mockTokenRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthTokenEntity>()))
            .ReturnsAsync((AuthTokenEntity t) => t);

        _mockEmailService
            .Setup(e => e.SendMagicLinkEmailAsync(Email, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        bool result = await _authService.RequestMagicLinkAsync(Email, IpAddress, BaseUrl, true);

        // Assert
        result.ShouldBeTrue();
        capturedPerson.ShouldNotBeNull();
        capturedPerson.Email.ShouldBe(Email);
        capturedPerson.Name.ShouldBe(string.Empty); // New user hasn't filled in name
    }

    [TestMethod]
    public async Task RequestMagicLinkAsync_InvalidEmail_ReturnsFalse()
    {
        // Act
        bool result = await _authService.RequestMagicLinkAsync("not-an-email", IpAddress, BaseUrl, true);

        // Assert
        result.ShouldBeFalse();
        _mockPersonRepository.Verify(r => r.GetByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [TestMethod]
    public async Task RequestMagicLinkAsync_EmptyEmail_ReturnsFalse()
    {
        // Act
        bool result = await _authService.RequestMagicLinkAsync("", IpAddress, BaseUrl, true);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public async Task RequestMagicLinkAsync_NormalizesEmail()
    {
        // Arrange
        string uppercaseEmail = "TEST@EXAMPLE.COM";

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedPerson = null;
        _mockPersonRepository
            .Setup(r => r.AddAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(p => capturedPerson = p)
            .ReturnsAsync((PersonEntity p) => p);

        _mockTokenRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthTokenEntity>()))
            .ReturnsAsync((AuthTokenEntity t) => t);

        _mockEmailService
            .Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        bool result = await _authService.RequestMagicLinkAsync(uppercaseEmail, IpAddress, BaseUrl, true);

        // Assert
        result.ShouldBeTrue();
        capturedPerson.ShouldNotBeNull();
        capturedPerson.Email.ShouldBe("test@example.com"); // Lowercase
    }

    #endregion

    #region VerifyMagicLinkAsync Tests

    [TestMethod]
    public async Task VerifyMagicLinkAsync_ValidToken_ReturnsSuccess()
    {
        // Arrange
        string token = "valid-token";
        PersonEntity person = CreatePersonEntity(PersonId, Email);

        AuthTokenEntity authToken = new AuthTokenEntity
        {
            TokenId = "token123",
            PartitionKey = "AUTHTOKEN",
            RowKey = "token-hash",
            TokenHash = "token-hash",
            PersonId = PersonId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt = null
        };

        _mockTokenRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(authToken);

        _mockTokenRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AuthTokenEntity>()))
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(PersonId, Constants.AuthMethodSecureEmailLink, null, IpAddress, null))
            .ReturnsAsync(SessionToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync(token, IpAddress);

        // Assert
        success.ShouldBeTrue();
        sessionToken.ShouldBe(SessionToken);
        personInfo.ShouldNotBeNull();
        personInfo.PersonId.ShouldBe(PersonId);
        personInfo.Email.ShouldBe(Email);
    }

    [TestMethod]
    public async Task VerifyMagicLinkAsync_EmptyToken_ReturnsFailure()
    {
        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync("", IpAddress);

        // Assert
        success.ShouldBeFalse();
        sessionToken.ShouldBeNull();
        message.ShouldNotBeNull();
        message!.ShouldContain("Invalid");
    }

    [TestMethod]
    public async Task VerifyMagicLinkAsync_TokenNotFound_ReturnsFailure()
    {
        // Arrange
        _mockTokenRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((AuthTokenEntity?)null);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync("unknown-token", IpAddress);

        // Assert
        success.ShouldBeFalse();
        sessionToken.ShouldBeNull();
    }

    [TestMethod]
    public async Task VerifyMagicLinkAsync_ExpiredToken_ReturnsFailure()
    {
        // Arrange
        AuthTokenEntity expiredToken = new AuthTokenEntity
        {
            TokenId = "token123",
            PartitionKey = "AUTHTOKEN",
            RowKey = "token-hash",
            TokenHash = "token-hash",
            PersonId = PersonId,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
            UsedAt = null
        };

        _mockTokenRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(expiredToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync("expired-token", IpAddress);

        // Assert
        success.ShouldBeFalse();
    }

    [TestMethod]
    public async Task VerifyMagicLinkAsync_AlreadyUsedToken_ReturnsFailure()
    {
        // Arrange
        AuthTokenEntity usedToken = new AuthTokenEntity
        {
            TokenId = "token123",
            PartitionKey = "AUTHTOKEN",
            RowKey = "token-hash",
            TokenHash = "token-hash",
            PersonId = PersonId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt = DateTime.UtcNow.AddMinutes(-1) // Already used
        };

        _mockTokenRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(usedToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync("used-token", IpAddress);

        // Assert
        success.ShouldBeFalse();
    }

    [TestMethod]
    public async Task VerifyMagicLinkAsync_PersonNotFound_ReturnsFailure()
    {
        // Arrange
        AuthTokenEntity authToken = new AuthTokenEntity
        {
            TokenId = "token123",
            PartitionKey = "AUTHTOKEN",
            RowKey = "token-hash",
            TokenHash = "token-hash",
            PersonId = PersonId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            UsedAt = null
        };

        _mockTokenRepository
            .Setup(r => r.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(authToken);

        _mockTokenRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AuthTokenEntity>()))
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync((PersonEntity?)null);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? message) =
            await _authService.VerifyMagicLinkAsync("valid-token", IpAddress);

        // Assert
        success.ShouldBeFalse();
        message.ShouldNotBeNull();
        message!.ShouldContain("Person not found");
    }

    #endregion

    #region AuthenticateWithMagicCodeAsync Tests

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_ValidCode_ReturnsSuccess()
    {
        // Arrange
        MarshalEntity marshal = CreateMarshalEntity(MarshalId, MagicCode, PersonId);
        PersonEntity person = CreatePersonEntity(PersonId, Email);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(PersonId, Constants.AuthMethodMarshalMagicCode, EventId, IpAddress, MarshalId))
            .ReturnsAsync(SessionToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync(EventId, MagicCode, IpAddress);

        // Assert
        success.ShouldBeTrue();
        sessionToken.ShouldBe(SessionToken);
        personInfo.ShouldNotBeNull();
        marshalId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_InvalidCode_ReturnsFailure()
    {
        // Arrange
        MarshalEntity marshal = CreateMarshalEntity(MarshalId, MagicCode, PersonId);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync(EventId, "WRONG1", IpAddress);

        // Assert
        success.ShouldBeFalse();
        message.ShouldNotBeNull();
        message!.ShouldContain("Invalid magic code");
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_EmptyEventId_ReturnsFailure()
    {
        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync("", MagicCode, IpAddress);

        // Assert
        success.ShouldBeFalse();
        message.ShouldNotBeNull();
        message!.ShouldContain("required");
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_EmptyMagicCode_ReturnsFailure()
    {
        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync(EventId, "", IpAddress);

        // Assert
        success.ShouldBeFalse();
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_LegacyMarshal_GeneratesPersonId()
    {
        // Arrange - Marshal without PersonId (legacy)
        MarshalEntity marshal = CreateMarshalEntity(MarshalId, MagicCode, ""); // Empty PersonId

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        MarshalEntity? capturedMarshal = null;
        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Callback<MarshalEntity>(m => capturedMarshal = m)
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(It.IsAny<string>()))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedPerson = null;
        _mockPersonRepository
            .Setup(r => r.AddAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(p => capturedPerson = p)
            .ReturnsAsync((PersonEntity p) => p);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(It.IsAny<string>(), Constants.AuthMethodMarshalMagicCode, EventId, IpAddress, MarshalId))
            .ReturnsAsync(SessionToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync(EventId, MagicCode, IpAddress);

        // Assert
        success.ShouldBeTrue();
        capturedMarshal.ShouldNotBeNull();
        capturedMarshal.PersonId.ShouldNotBeNullOrEmpty(); // New PersonId generated
        capturedPerson.ShouldNotBeNull();
        capturedPerson.Name.ShouldBe(marshal.Name); // Name copied from marshal
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_NormalizesCode()
    {
        // Arrange - lowercase magic code
        MarshalEntity marshal = CreateMarshalEntity(MarshalId, MagicCode, PersonId);
        PersonEntity person = CreatePersonEntity(PersonId, Email);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(PersonId, Constants.AuthMethodMarshalMagicCode, EventId, IpAddress, MarshalId))
            .ReturnsAsync(SessionToken);

        // Act - Use lowercase code
        (bool success, string? sessionToken, PersonInfo? personInfo, string? marshalId, string? message) =
            await _authService.AuthenticateWithMagicCodeAsync(EventId, "abc123", IpAddress);

        // Assert
        success.ShouldBeTrue();
    }

    [TestMethod]
    public async Task AuthenticateWithMagicCodeAsync_UpdatesLastAccessedDate()
    {
        // Arrange
        MarshalEntity marshal = CreateMarshalEntity(MarshalId, MagicCode, PersonId);
        marshal.LastAccessedDate = DateTime.UtcNow.AddDays(-7);
        PersonEntity person = CreatePersonEntity(PersonId, Email);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        MarshalEntity? capturedMarshal = null;
        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Callback<MarshalEntity>(m => capturedMarshal = m)
            .Returns(Task.CompletedTask);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(PersonId, Constants.AuthMethodMarshalMagicCode, EventId, IpAddress, MarshalId))
            .ReturnsAsync(SessionToken);

        // Act
        await _authService.AuthenticateWithMagicCodeAsync(EventId, MagicCode, IpAddress);

        // Assert
        capturedMarshal.ShouldNotBeNull();
        capturedMarshal.LastAccessedDate.ShouldNotBeNull();
        capturedMarshal.LastAccessedDate!.Value.ShouldBeInRange(DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    #endregion

    #region InstantLoginAsync Tests

    [TestMethod]
    public async Task InstantLoginAsync_ExistingPerson_ReturnsSuccess()
    {
        // Arrange
        PersonEntity existingPerson = CreatePersonEntity(PersonId, Email);

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync(Email))
            .ReturnsAsync(existingPerson);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(PersonId, Constants.AuthMethodSecureEmailLink, null, IpAddress, null))
            .ReturnsAsync(SessionToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo) =
            await _authService.InstantLoginAsync(Email, IpAddress);

        // Assert
        success.ShouldBeTrue();
        sessionToken.ShouldBe(SessionToken);
        personInfo.ShouldNotBeNull();
        personInfo.Email.ShouldBe(Email);
    }

    [TestMethod]
    public async Task InstantLoginAsync_NewPerson_CreatesPersonAndReturnsSuccess()
    {
        // Arrange
        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync(Email))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedPerson = null;
        _mockPersonRepository
            .Setup(r => r.AddAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(p => capturedPerson = p)
            .ReturnsAsync((PersonEntity p) => p);

        _mockClaimsService
            .Setup(c => c.CreateSessionAsync(It.IsAny<string>(), Constants.AuthMethodSecureEmailLink, null, IpAddress, null))
            .ReturnsAsync(SessionToken);

        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo) =
            await _authService.InstantLoginAsync(Email, IpAddress);

        // Assert
        success.ShouldBeTrue();
        capturedPerson.ShouldNotBeNull();
        capturedPerson.Email.ShouldBe(Email);
    }

    [TestMethod]
    public async Task InstantLoginAsync_InvalidEmail_ReturnsFailure()
    {
        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo) =
            await _authService.InstantLoginAsync("not-an-email", IpAddress);

        // Assert
        success.ShouldBeFalse();
        sessionToken.ShouldBeNull();
    }

    [TestMethod]
    public async Task InstantLoginAsync_EmptyEmail_ReturnsFailure()
    {
        // Act
        (bool success, string? sessionToken, PersonInfo? personInfo) =
            await _authService.InstantLoginAsync("", IpAddress);

        // Assert
        success.ShouldBeFalse();
    }

    #endregion

    #region GenerateMagicCode Tests

    [TestMethod]
    public void GenerateMagicCode_ReturnsCorrectLength()
    {
        // Act
        string code = AuthService.GenerateMagicCode();

        // Assert
        code.Length.ShouldBe(Constants.MagicCodeLength);
    }

    [TestMethod]
    public void GenerateMagicCode_ReturnsOnlyValidCharacters()
    {
        // Arrange
        HashSet<char> validChars = new HashSet<char>("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789");

        // Act
        string code = AuthService.GenerateMagicCode();

        // Assert
        foreach (char c in code)
        {
            validChars.ShouldContain(c);
        }
    }

    [TestMethod]
    public void GenerateMagicCode_GeneratesUniqueCodesAcrossMultipleCalls()
    {
        // Arrange
        HashSet<string> codes = new HashSet<string>();

        // Act - Generate 100 codes
        for (int i = 0; i < 100; i++)
        {
            codes.Add(AuthService.GenerateMagicCode());
        }

        // Assert - Should have 100 unique codes (extremely unlikely to have collisions)
        codes.Count.ShouldBe(100);
    }

    #endregion
}
