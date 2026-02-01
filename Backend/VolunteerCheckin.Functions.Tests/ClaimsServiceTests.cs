using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for ClaimsService - user authentication claims resolution.
/// </summary>
[TestClass]
public class ClaimsServiceTests
{
    private Mock<IAuthSessionRepository> _mockSessionRepository = null!;
    private Mock<IPersonRepository> _mockPersonRepository = null!;
    private Mock<IEventRoleRepository> _mockRoleRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private ISampleEventService _mockSampleEventService = null!;
    private Mock<IEventDeletionRepository> _mockEventDeletionRepository = null!;
    private ClaimsService _claimsService = null!;

    private const string SessionToken = "test-session-token-12345";
    private const string PersonId = "person-123";
    private const string EventId = "event-456";
    private const string MarshalId = "marshal-789";
    private const string Email = "test@example.com";
    private const string IpAddress = "192.168.1.1";

    [TestInitialize]
    public void Setup()
    {
        _mockSessionRepository = new Mock<IAuthSessionRepository>();
        _mockPersonRepository = new Mock<IPersonRepository>();
        _mockRoleRepository = new Mock<IEventRoleRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockSampleEventService = CreateMockSampleEventService();
        _mockEventDeletionRepository = new Mock<IEventDeletionRepository>();

        // By default, no events are pending deletion
        _mockEventDeletionRepository
            .Setup(r => r.IsDeletionPendingAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _claimsService = new ClaimsService(
            _mockSessionRepository.Object,
            _mockPersonRepository.Object,
            _mockRoleRepository.Object,
            _mockMarshalRepository.Object,
            _mockSampleEventService,
            _mockEventDeletionRepository.Object
        );
    }

    private AuthSessionEntity CreateValidSession(
        string personId = PersonId,
        string? eventId = null,
        string? marshalId = null,
        string authMethod = Constants.AuthMethodSecureEmailLink,
        DateTime? expiresAt = null)
    {
        return new AuthSessionEntity
        {
            PartitionKey = "SESSION",
            RowKey = "token-hash",
            SessionId = "session-123",
            SessionTokenHash = "token-hash",
            PersonId = personId,
            EventId = eventId,
            MarshalId = marshalId,
            AuthMethod = authMethod,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddHours(23),
            LastAccessedAt = DateTime.UtcNow.AddMinutes(-5),
            IsRevoked = false,
            IpAddress = IpAddress
        };
    }

    private PersonEntity CreatePerson(string personId = PersonId, bool isSystemAdmin = false)
    {
        return new PersonEntity
        {
            PartitionKey = "PERSON",
            RowKey = personId,
            PersonId = personId,
            Name = "Test User",
            Email = Email,
            IsSystemAdmin = isSystemAdmin,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
    }

    private MarshalEntity CreateMarshal(string marshalId = MarshalId, string? personId = PersonId)
    {
        return new MarshalEntity
        {
            PartitionKey = EventId,
            RowKey = marshalId,
            MarshalId = marshalId,
            Name = "Test Marshal",
            Email = Email,
            PersonId = personId ?? string.Empty,
            LastAccessedDate = DateTime.UtcNow.AddDays(-1)
        };
    }

    #region GetClaimsAsync Tests

    [TestMethod]
    public async Task GetClaimsAsync_ValidSession_ReturnsUserClaims()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();
        PersonEntity person = CreatePerson();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldNotBeNull();
        claims.PersonId.ShouldBe(PersonId);
        claims.PersonName.ShouldBe("Test User");
        claims.PersonEmail.ShouldBe(Email);
        claims.IsSystemAdmin.ShouldBeFalse();
    }

    [TestMethod]
    public async Task GetClaimsAsync_InvalidToken_ReturnsNull()
    {
        // Arrange
        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((AuthSessionEntity?)null);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetClaimsAsync_ExpiredSession_ReturnsNull()
    {
        // Arrange
        AuthSessionEntity expiredSession = CreateValidSession(expiresAt: DateTime.UtcNow.AddHours(-1));

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(expiredSession);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetClaimsAsync_RevokedSession_ReturnsNull()
    {
        // Arrange
        AuthSessionEntity revokedSession = CreateValidSession();
        revokedSession.IsRevoked = true;
        revokedSession.RevokedAt = DateTime.UtcNow.AddMinutes(-10);

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(revokedSession);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetClaimsAsync_PersonNotFound_ReturnsNull()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync((PersonEntity?)null);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldBeNull();
    }

    [TestMethod]
    public async Task GetClaimsAsync_SystemAdmin_ReturnsIsSystemAdminTrue()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();
        PersonEntity adminPerson = CreatePerson(isSystemAdmin: true);

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(adminPerson);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldNotBeNull();
        claims.IsSystemAdmin.ShouldBeTrue();
    }

    [TestMethod]
    public async Task GetClaimsAsync_MarshalSession_UsesMarshalIdFromSession()
    {
        // Arrange
        AuthSessionEntity marshalSession = CreateValidSession(
            eventId: EventId,
            marshalId: MarshalId,
            authMethod: Constants.AuthMethodMarshalMagicCode);
        marshalSession.ExpiresAt = null; // Marshal sessions don't expire
        PersonEntity person = CreatePerson();
        MarshalEntity marshal = CreateMarshal();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(marshalSession);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);
        _mockMarshalRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken);

        // Assert
        claims.ShouldNotBeNull();
        claims.MarshalId.ShouldBe(MarshalId);
        claims.EventId.ShouldBe(EventId);
    }

    [TestMethod]
    public async Task GetClaimsAsync_MarshalSession_UpdatesLastAccessedDate()
    {
        // Arrange
        AuthSessionEntity marshalSession = CreateValidSession(
            eventId: EventId,
            marshalId: MarshalId,
            authMethod: Constants.AuthMethodMarshalMagicCode);
        marshalSession.ExpiresAt = null;
        PersonEntity person = CreatePerson();
        MarshalEntity marshal = CreateMarshal();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(marshalSession);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        MarshalEntity? updatedMarshal = null;
        _mockMarshalRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()))
            .Callback<MarshalEntity>(m => updatedMarshal = m)
            .Returns(Task.CompletedTask);

        // Act
        await _claimsService.GetClaimsAsync(SessionToken);

        // Assert
        updatedMarshal.ShouldNotBeNull();
        updatedMarshal.LastAccessedDate.ShouldNotBeNull();
        (DateTime.UtcNow - updatedMarshal.LastAccessedDate.Value).TotalMinutes.ShouldBeLessThan(2);
    }

    [TestMethod]
    public async Task GetClaimsAsync_AdminSession_SlidingExpiration()
    {
        // Arrange
        AuthSessionEntity adminSession = CreateValidSession(
            authMethod: Constants.AuthMethodSecureEmailLink);
        adminSession.ExpiresAt = DateTime.UtcNow.AddHours(5); // Will be extended
        PersonEntity person = CreatePerson();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(adminSession);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        AuthSessionEntity? updatedSession = null;
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Callback<AuthSessionEntity>(s => updatedSession = s)
            .Returns(Task.CompletedTask);

        // Act
        await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        updatedSession.ShouldNotBeNull();
        updatedSession.ExpiresAt.ShouldNotBeNull();
        // Should be extended to now + AdminSessionExpiryHours
        updatedSession.ExpiresAt!.Value.ShouldBeGreaterThan(DateTime.UtcNow.AddHours(Constants.AdminSessionExpiryHours - 1));
    }

    [TestMethod]
    public async Task GetClaimsAsync_WithEventRoles_ReturnsRolesInClaims()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();
        PersonEntity person = CreatePerson();

        EventRoleEntity adminRole = new EventRoleEntity
        {
            PartitionKey = PersonId,
            RowKey = $"{EventId}|role-1",
            PersonId = PersonId,
            EventId = EventId,
            Role = Constants.RoleEventAdmin,
            AreaIdsJson = "[]"
        };

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([adminRole]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldNotBeNull();
        claims.EventRoles.Count.ShouldBe(1);
        claims.EventRoles[0].Role.ShouldBe(Constants.RoleEventAdmin);
        claims.HasRole(Constants.RoleEventAdmin).ShouldBeTrue();
    }

    [TestMethod]
    public async Task GetClaimsAsync_AdminLookupMarshalByPersonId()
    {
        // Arrange - Admin session (no MarshalId in session) but person is a marshal
        AuthSessionEntity adminSession = CreateValidSession(
            authMethod: Constants.AuthMethodSecureEmailLink);
        PersonEntity person = CreatePerson();
        MarshalEntity marshal = CreateMarshal(personId: PersonId);

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(adminSession);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, EventId);

        // Assert
        claims.ShouldNotBeNull();
        claims.MarshalId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public async Task GetClaimsAsync_NoEventId_ReturnsEmptyEventRoles()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();
        PersonEntity person = CreatePerson();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        // Act - No eventId parameter
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken);

        // Assert
        claims.ShouldNotBeNull();
        claims.EventRoles.ShouldBeEmpty();
        claims.EventId.ShouldBeNull();
    }

    #endregion

    #region CreateSessionAsync Tests

    [TestMethod]
    public async Task CreateSessionAsync_AdminAuth_ReturnsSessionToken()
    {
        // Arrange
        AuthSessionEntity? capturedSession = null;
        _mockSessionRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthSessionEntity>()))
            .Callback<AuthSessionEntity>(s => capturedSession = s)
            .ReturnsAsync((AuthSessionEntity s) => s);

        // Act
        string token = await _claimsService.CreateSessionAsync(
            PersonId,
            Constants.AuthMethodSecureEmailLink,
            null,
            IpAddress);

        // Assert
        token.ShouldNotBeNullOrEmpty();
        capturedSession.ShouldNotBeNull();
        capturedSession.PersonId.ShouldBe(PersonId);
        capturedSession.AuthMethod.ShouldBe(Constants.AuthMethodSecureEmailLink);
        capturedSession.EventId.ShouldBeNull();
        capturedSession.ExpiresAt.ShouldNotBeNull();
        capturedSession.IpAddress.ShouldBe(IpAddress);
    }

    [TestMethod]
    public async Task CreateSessionAsync_MarshalAuth_SetsEventIdAndMarshalId()
    {
        // Arrange
        AuthSessionEntity? capturedSession = null;
        _mockSessionRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthSessionEntity>()))
            .Callback<AuthSessionEntity>(s => capturedSession = s)
            .ReturnsAsync((AuthSessionEntity s) => s);

        // Act
        string token = await _claimsService.CreateSessionAsync(
            PersonId,
            Constants.AuthMethodMarshalMagicCode,
            EventId,
            IpAddress,
            MarshalId);

        // Assert
        token.ShouldNotBeNullOrEmpty();
        capturedSession.ShouldNotBeNull();
        capturedSession.PersonId.ShouldBe(PersonId);
        capturedSession.AuthMethod.ShouldBe(Constants.AuthMethodMarshalMagicCode);
        capturedSession.EventId.ShouldBe(EventId);
        capturedSession.MarshalId.ShouldBe(MarshalId);
        capturedSession.ExpiresAt.ShouldBeNull(); // Marshal sessions don't expire
    }

    [TestMethod]
    public async Task CreateSessionAsync_GeneratesSecureToken()
    {
        // Arrange
        _mockSessionRepository
            .Setup(r => r.AddAsync(It.IsAny<AuthSessionEntity>()))
            .ReturnsAsync((AuthSessionEntity s) => s);

        // Act
        string token1 = await _claimsService.CreateSessionAsync(PersonId, Constants.AuthMethodSecureEmailLink, null, IpAddress);
        string token2 = await _claimsService.CreateSessionAsync(PersonId, Constants.AuthMethodSecureEmailLink, null, IpAddress);

        // Assert
        token1.ShouldNotBe(token2); // Tokens should be unique
        token1.Length.ShouldBeGreaterThan(50); // Should be sufficiently long
    }

    #endregion

    #region RevokeSessionAsync Tests

    [TestMethod]
    public async Task RevokeSessionAsync_ValidToken_RevokesSession()
    {
        // Arrange
        AuthSessionEntity session = CreateValidSession();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _mockSessionRepository
            .Setup(r => r.RevokeAsync(session.SessionTokenHash))
            .Returns(Task.CompletedTask);

        // Act
        await _claimsService.RevokeSessionAsync(SessionToken);

        // Assert
        _mockSessionRepository.Verify(
            r => r.RevokeAsync(session.SessionTokenHash),
            Times.Once);
    }

    [TestMethod]
    public async Task RevokeSessionAsync_InvalidToken_DoesNotThrow()
    {
        // Arrange
        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((AuthSessionEntity?)null);

        // Act & Assert - Should not throw
        await _claimsService.RevokeSessionAsync(SessionToken);

        _mockSessionRepository.Verify(
            r => r.RevokeAsync(It.IsAny<string>()),
            Times.Never);
    }

    #endregion

    #region RevokeAllSessionsForPersonAsync Tests

    [TestMethod]
    public async Task RevokeAllSessionsForPersonAsync_RevokesAllSessions()
    {
        // Arrange
        _mockSessionRepository
            .Setup(r => r.RevokeAllForPersonAsync(PersonId))
            .Returns(Task.CompletedTask);

        // Act
        await _claimsService.RevokeAllSessionsForPersonAsync(PersonId);

        // Assert
        _mockSessionRepository.Verify(
            r => r.RevokeAllForPersonAsync(PersonId),
            Times.Once);
    }

    #endregion


    #region Edge Cases

    [TestMethod]
    public async Task GetClaimsAsync_MarshalNotFound_UpdatesLastAccessStillSucceeds()
    {
        // Arrange - Marshal session but marshal record doesn't exist
        AuthSessionEntity marshalSession = CreateValidSession(
            eventId: EventId,
            marshalId: MarshalId,
            authMethod: Constants.AuthMethodMarshalMagicCode);
        marshalSession.ExpiresAt = null;
        PersonEntity person = CreatePerson();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(marshalSession);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync((MarshalEntity?)null);

        // Act
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken);

        // Assert - Should still return claims despite marshal not found
        claims.ShouldNotBeNull();
        claims.MarshalId.ShouldBe(MarshalId); // From session
    }

    [TestMethod]
    public async Task GetClaimsAsync_MarshalSessionUsesSessionEventId()
    {
        // Arrange - Marshal session should use EventId from session, not parameter
        AuthSessionEntity marshalSession = CreateValidSession(
            eventId: EventId, // Session is locked to this event
            marshalId: MarshalId,
            authMethod: Constants.AuthMethodMarshalMagicCode);
        marshalSession.ExpiresAt = null;
        PersonEntity person = CreatePerson();
        MarshalEntity marshal = CreateMarshal();

        _mockSessionRepository
            .Setup(r => r.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(marshalSession);
        _mockSessionRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<AuthSessionEntity>()))
            .Returns(Task.CompletedTask);
        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);
        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);
        _mockMarshalRepository
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        // Act - Pass a different eventId parameter
        UserClaims? claims = await _claimsService.GetClaimsAsync(SessionToken, "different-event-id");

        // Assert - Should use session's EventId, not parameter
        claims.ShouldNotBeNull();
        claims.EventId.ShouldBe(EventId);
    }

    #endregion

    private static ISampleEventService CreateMockSampleEventService()
    {
        return Mock.Of<ISampleEventService>();
    }
}
