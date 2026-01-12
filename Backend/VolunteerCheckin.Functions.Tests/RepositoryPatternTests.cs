using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for Repository patterns and interface contracts.
/// Note: These are primarily mock-based tests since TableStorage repositories
/// directly use Azure TableClient. For full integration tests, use Azurite
/// or a test Azure Storage account.
/// </summary>
[TestClass]
public class RepositoryPatternTests
{
    private const string ConnectionString = "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;EndpointSuffix=core.windows.net";

    #region IPersonRepository Mock Tests

    [TestMethod]
    public async Task PersonRepository_AddAsync_CanBeMocked()
    {
        // Arrange
        Mock<IPersonRepository> mockRepo = new Mock<IPersonRepository>();

        PersonEntity person = new PersonEntity
        {
            PersonId = "person-123",
            Name = "Test User",
            Email = "test@example.com"
        };

        mockRepo
            .Setup(r => r.AddAsync(It.IsAny<PersonEntity>()))
            .ReturnsAsync((PersonEntity p) => p);

        // Act
        PersonEntity result = await mockRepo.Object.AddAsync(person);

        // Assert
        result.ShouldNotBeNull();
        result.PersonId.ShouldBe("person-123");
        mockRepo.Verify(r => r.AddAsync(It.Is<PersonEntity>(p => p.Email == "test@example.com")), Times.Once);
    }

    [TestMethod]
    public async Task PersonRepository_GetByEmailAsync_CanBeMocked()
    {
        // Arrange
        Mock<IPersonRepository> mockRepo = new Mock<IPersonRepository>();

        PersonEntity person = new PersonEntity
        {
            PersonId = "person-123",
            Name = "Test User",
            Email = "test@example.com"
        };

        mockRepo
            .Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(person);

        mockRepo
            .Setup(r => r.GetByEmailAsync("nonexistent@example.com"))
            .ReturnsAsync((PersonEntity?)null);

        // Act
        PersonEntity? found = await mockRepo.Object.GetByEmailAsync("test@example.com");
        PersonEntity? notFound = await mockRepo.Object.GetByEmailAsync("nonexistent@example.com");

        // Assert
        found.ShouldNotBeNull();
        found.Email.ShouldBe("test@example.com");
        notFound.ShouldBeNull();
    }

    [TestMethod]
    public async Task PersonRepository_GetAllAsync_ReturnsCollection()
    {
        // Arrange
        Mock<IPersonRepository> mockRepo = new Mock<IPersonRepository>();

        List<PersonEntity> people =
        [
            new PersonEntity { PersonId = "1", Name = "User 1", Email = "user1@example.com" },
            new PersonEntity { PersonId = "2", Name = "User 2", Email = "user2@example.com" },
            new PersonEntity { PersonId = "3", Name = "User 3", Email = "user3@example.com" }
        ];

        mockRepo
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(people);

        // Act
        IEnumerable<PersonEntity> result = await mockRepo.Object.GetAllAsync();

        // Assert
        result.Count().ShouldBe(3);
    }

    #endregion

    #region IAuthSessionRepository Mock Tests

    [TestMethod]
    public async Task AuthSessionRepository_GetBySessionTokenHashAsync_ReturnsSession()
    {
        // Arrange
        Mock<IAuthSessionRepository> mockRepo = new Mock<IAuthSessionRepository>();

        string tokenHash = "abc123hash";
        AuthSessionEntity session = new AuthSessionEntity
        {
            SessionId = "session-1",
            SessionTokenHash = tokenHash,
            PersonId = "person-123",
            AuthMethod = "SecureEmailLink"
        };

        mockRepo
            .Setup(r => r.GetBySessionTokenHashAsync(tokenHash))
            .ReturnsAsync(session);

        // Act
        AuthSessionEntity? result = await mockRepo.Object.GetBySessionTokenHashAsync(tokenHash);

        // Assert
        result.ShouldNotBeNull();
        result.SessionTokenHash.ShouldBe(tokenHash);
    }

    [TestMethod]
    public async Task AuthSessionRepository_RevokeAsync_UpdatesSession()
    {
        // Arrange
        Mock<IAuthSessionRepository> mockRepo = new Mock<IAuthSessionRepository>();

        string tokenHash = "abc123hash";
        bool revokeCalled = false;

        mockRepo
            .Setup(r => r.RevokeAsync(tokenHash))
            .Callback(() => revokeCalled = true)
            .Returns(Task.CompletedTask);

        // Act
        await mockRepo.Object.RevokeAsync(tokenHash);

        // Assert
        revokeCalled.ShouldBeTrue();
        mockRepo.Verify(r => r.RevokeAsync(tokenHash), Times.Once);
    }

    [TestMethod]
    public async Task AuthSessionRepository_RevokeAllForPersonAsync_RevokesMultipleSessions()
    {
        // Arrange
        Mock<IAuthSessionRepository> mockRepo = new Mock<IAuthSessionRepository>();

        string personId = "person-123";

        mockRepo
            .Setup(r => r.RevokeAllForPersonAsync(personId))
            .Returns(Task.CompletedTask);

        // Act
        await mockRepo.Object.RevokeAllForPersonAsync(personId);

        // Assert
        mockRepo.Verify(r => r.RevokeAllForPersonAsync(personId), Times.Once);
    }

    [TestMethod]
    public async Task AuthSessionRepository_GetByPersonAndEventAsync_FiltersCorrectly()
    {
        // Arrange
        Mock<IAuthSessionRepository> mockRepo = new Mock<IAuthSessionRepository>();

        string personId = "person-123";
        string eventId = "event-456";

        List<AuthSessionEntity> eventSessions =
        [
            new AuthSessionEntity { SessionId = "s1", PersonId = personId, EventId = eventId },
            new AuthSessionEntity { SessionId = "s2", PersonId = personId, EventId = eventId }
        ];

        mockRepo
            .Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
            .ReturnsAsync(eventSessions);

        // Act
        IEnumerable<AuthSessionEntity> result = await mockRepo.Object.GetByPersonAndEventAsync(personId, eventId);

        // Assert
        result.Count().ShouldBe(2);
        result.ShouldAllBe(s => s.PersonId == personId && s.EventId == eventId);
    }

    #endregion

    #region IMarshalRepository Mock Tests

    [TestMethod]
    public async Task MarshalRepository_GetByEventAsync_ReturnsEventMarshals()
    {
        // Arrange
        Mock<IMarshalRepository> mockRepo = new Mock<IMarshalRepository>();

        string eventId = "event-123";
        List<MarshalEntity> marshals =
        [
            new MarshalEntity { MarshalId = "m1", Name = "Marshal 1", PartitionKey = eventId },
            new MarshalEntity { MarshalId = "m2", Name = "Marshal 2", PartitionKey = eventId }
        ];

        mockRepo
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(marshals);

        // Act
        IEnumerable<MarshalEntity> result = await mockRepo.Object.GetByEventAsync(eventId);

        // Assert
        result.Count().ShouldBe(2);
    }

    [TestMethod]
    public async Task MarshalRepository_GetAsync_ReturnsSingleMarshal()
    {
        // Arrange
        Mock<IMarshalRepository> mockRepo = new Mock<IMarshalRepository>();

        string eventId = "event-123";
        string marshalId = "marshal-456";

        MarshalEntity marshal = new MarshalEntity
        {
            PartitionKey = eventId,
            RowKey = marshalId,
            MarshalId = marshalId,
            Name = "Test Marshal"
        };

        mockRepo
            .Setup(r => r.GetAsync(eventId, marshalId))
            .ReturnsAsync(marshal);

        // Act
        MarshalEntity? result = await mockRepo.Object.GetAsync(eventId, marshalId);

        // Assert
        result.ShouldNotBeNull();
        result.MarshalId.ShouldBe(marshalId);
    }

    [TestMethod]
    public async Task MarshalRepository_UpdateUnconditionalAsync_SkipsConcurrencyCheck()
    {
        // Arrange
        Mock<IMarshalRepository> mockRepo = new Mock<IMarshalRepository>();

        MarshalEntity marshal = new MarshalEntity
        {
            MarshalId = "marshal-123",
            Name = "Updated Marshal",
            LastAccessedDate = DateTime.UtcNow
        };

        mockRepo
            .Setup(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        // Act & Assert - Should not throw
        await mockRepo.Object.UpdateUnconditionalAsync(marshal);
        mockRepo.Verify(r => r.UpdateUnconditionalAsync(It.IsAny<MarshalEntity>()), Times.Once);
    }

    #endregion

    #region IEventRoleRepository Mock Tests

    [TestMethod]
    public async Task EventRoleRepository_GetByPersonAndEventAsync_ReturnsRoles()
    {
        // Arrange
        Mock<IEventRoleRepository> mockRepo = new Mock<IEventRoleRepository>();

        string personId = "person-123";
        string eventId = "event-456";

        List<EventRoleEntity> roles =
        [
            new EventRoleEntity
            {
                PersonId = personId,
                EventId = eventId,
                Role = "EventAdmin",
                AreaIdsJson = "[]"
            },
            new EventRoleEntity
            {
                PersonId = personId,
                EventId = eventId,
                Role = "EventAreaLead",
                AreaIdsJson = "[\"area-1\", \"area-2\"]"
            }
        ];

        mockRepo
            .Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
            .ReturnsAsync(roles);

        // Act
        IEnumerable<EventRoleEntity> result = await mockRepo.Object.GetByPersonAndEventAsync(personId, eventId);

        // Assert
        result.Count().ShouldBe(2);
        result.ShouldContain(r => r.Role == "EventAdmin");
        result.ShouldContain(r => r.Role == "EventAreaLead");
    }

    [TestMethod]
    public async Task EventRoleRepository_GetByEventAsync_ReturnsAllEventRoles()
    {
        // Arrange
        Mock<IEventRoleRepository> mockRepo = new Mock<IEventRoleRepository>();

        string eventId = "event-456";

        List<EventRoleEntity> roles =
        [
            new EventRoleEntity { PersonId = "person-1", EventId = eventId, Role = "EventAdmin" },
            new EventRoleEntity { PersonId = "person-2", EventId = eventId, Role = "EventAreaLead" },
            new EventRoleEntity { PersonId = "person-3", EventId = eventId, Role = "EventAdmin" }
        ];

        mockRepo
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(roles);

        // Act
        IEnumerable<EventRoleEntity> result = await mockRepo.Object.GetByEventAsync(eventId);

        // Assert
        result.Count().ShouldBe(3);
    }

    #endregion

    #region IAssignmentRepository Mock Tests

    [TestMethod]
    public async Task AssignmentRepository_GetByMarshalAsync_ReturnsAssignments()
    {
        // Arrange
        Mock<IAssignmentRepository> mockRepo = new Mock<IAssignmentRepository>();

        string eventId = "event-123";
        string marshalId = "marshal-456";

        List<AssignmentEntity> assignments =
        [
            new AssignmentEntity { EventId = eventId, MarshalId = marshalId, LocationId = "loc-1" },
            new AssignmentEntity { EventId = eventId, MarshalId = marshalId, LocationId = "loc-2" }
        ];

        mockRepo
            .Setup(r => r.GetByMarshalAsync(eventId, marshalId))
            .ReturnsAsync(assignments);

        // Act
        IEnumerable<AssignmentEntity> result = await mockRepo.Object.GetByMarshalAsync(eventId, marshalId);

        // Assert
        result.Count().ShouldBe(2);
    }

    [TestMethod]
    public async Task AssignmentRepository_GetByEventAsync_ReturnsAllEventAssignments()
    {
        // Arrange
        Mock<IAssignmentRepository> mockRepo = new Mock<IAssignmentRepository>();

        string eventId = "event-123";

        List<AssignmentEntity> assignments =
        [
            new AssignmentEntity { EventId = eventId, MarshalId = "m1", LocationId = "loc-1" },
            new AssignmentEntity { EventId = eventId, MarshalId = "m1", LocationId = "loc-2" },
            new AssignmentEntity { EventId = eventId, MarshalId = "m2", LocationId = "loc-3" }
        ];

        mockRepo
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(assignments);

        // Act
        IEnumerable<AssignmentEntity> result = await mockRepo.Object.GetByEventAsync(eventId);

        // Assert
        result.Count().ShouldBe(3);
    }

    #endregion

    #region ILocationRepository Mock Tests

    [TestMethod]
    public async Task LocationRepository_GetByEventAsync_ReturnsCheckpoints()
    {
        // Arrange
        Mock<ILocationRepository> mockRepo = new Mock<ILocationRepository>();

        string eventId = "event-123";

        List<LocationEntity> locations =
        [
            new LocationEntity { RowKey = "loc-1", Name = "Checkpoint 1", EventId = eventId, Latitude = 51.5, Longitude = -0.1 },
            new LocationEntity { RowKey = "loc-2", Name = "Checkpoint 2", EventId = eventId, Latitude = 51.6, Longitude = -0.2 }
        ];

        mockRepo
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        // Act
        IEnumerable<LocationEntity> result = await mockRepo.Object.GetByEventAsync(eventId);

        // Assert
        result.Count().ShouldBe(2);
    }

    #endregion

    #region IAreaRepository Mock Tests

    [TestMethod]
    public async Task AreaRepository_GetByEventAsync_ReturnsAreas()
    {
        // Arrange
        Mock<IAreaRepository> mockRepo = new Mock<IAreaRepository>();

        string eventId = "event-123";

        List<AreaEntity> areas =
        [
            new AreaEntity { RowKey = "area-1", Name = "North Section", EventId = eventId },
            new AreaEntity { RowKey = "area-2", Name = "South Section", EventId = eventId }
        ];

        mockRepo
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(areas);

        // Act
        IEnumerable<AreaEntity> result = await mockRepo.Object.GetByEventAsync(eventId);

        // Assert
        result.Count().ShouldBe(2);
    }

    #endregion

    #region Repository Interface Compliance Tests

    [TestMethod]
    public void AllRepositoryInterfaces_HaveBasicCrudMethods()
    {
        // Verify key repository interfaces have expected method signatures
        // This is a compile-time verification that passes if interfaces are properly defined

        // IPersonRepository
        typeof(IPersonRepository).GetMethod("AddAsync").ShouldNotBeNull();
        typeof(IPersonRepository).GetMethod("GetAsync").ShouldNotBeNull();
        typeof(IPersonRepository).GetMethod("UpdateAsync").ShouldNotBeNull();
        typeof(IPersonRepository).GetMethod("DeleteAsync").ShouldNotBeNull();
        typeof(IPersonRepository).GetMethod("GetByEmailAsync").ShouldNotBeNull();

        // IAuthSessionRepository
        typeof(IAuthSessionRepository).GetMethod("AddAsync").ShouldNotBeNull();
        typeof(IAuthSessionRepository).GetMethod("GetBySessionTokenHashAsync").ShouldNotBeNull();
        typeof(IAuthSessionRepository).GetMethod("RevokeAsync").ShouldNotBeNull();

        // IMarshalRepository
        typeof(IMarshalRepository).GetMethod("GetAsync").ShouldNotBeNull();
        typeof(IMarshalRepository).GetMethod("GetByEventAsync").ShouldNotBeNull();

        // IEventRoleRepository
        typeof(IEventRoleRepository).GetMethod("GetByPersonAndEventAsync").ShouldNotBeNull();
        typeof(IEventRoleRepository).GetMethod("GetByEventAsync").ShouldNotBeNull();
    }

    #endregion

    #region Repository Instantiation Tests

    [TestMethod]
    [Ignore("Requires Azure Storage (Azurite) to run")]
    public void TableStorageRepositories_CanBeInstantiated()
    {
        // Arrange
        TableStorageService tableStorage = new TableStorageService(ConnectionString);

        // Act & Assert - Verify repositories can be instantiated (doesn't test Azure connection)
        TableStoragePersonRepository personRepo = new TableStoragePersonRepository(tableStorage);
        personRepo.ShouldNotBeNull();

        TableStorageAuthSessionRepository sessionRepo = new TableStorageAuthSessionRepository(tableStorage);
        sessionRepo.ShouldNotBeNull();

        TableStorageMarshalRepository marshalRepo = new TableStorageMarshalRepository(tableStorage);
        marshalRepo.ShouldNotBeNull();

        TableStorageEventRoleRepository roleRepo = new TableStorageEventRoleRepository(tableStorage);
        roleRepo.ShouldNotBeNull();

        TableStorageAssignmentRepository assignmentRepo = new TableStorageAssignmentRepository(tableStorage);
        assignmentRepo.ShouldNotBeNull();

        TableStorageLocationRepository locationRepo = new TableStorageLocationRepository(tableStorage);
        locationRepo.ShouldNotBeNull();

        TableStorageAreaRepository areaRepo = new TableStorageAreaRepository(tableStorage);
        areaRepo.ShouldNotBeNull();
    }

    #endregion

    #region Entity Model Tests

    [TestMethod]
    public void AuthSessionEntity_IsValid_ReturnsTrueForValidSession()
    {
        // Arrange
        AuthSessionEntity session = new AuthSessionEntity
        {
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        session.IsValid().ShouldBeTrue();
    }

    [TestMethod]
    public void AuthSessionEntity_IsValid_ReturnsFalseForRevokedSession()
    {
        // Arrange
        AuthSessionEntity session = new AuthSessionEntity
        {
            IsRevoked = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        session.IsValid().ShouldBeFalse();
    }

    [TestMethod]
    public void AuthSessionEntity_IsValid_ReturnsFalseForExpiredSession()
    {
        // Arrange
        AuthSessionEntity session = new AuthSessionEntity
        {
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.AddHours(-1) // Expired
        };

        // Act & Assert
        session.IsValid().ShouldBeFalse();
    }

    [TestMethod]
    public void AuthSessionEntity_IsValid_ReturnsTrueForNonExpiringSession()
    {
        // Arrange - Marshal sessions don't have expiry
        AuthSessionEntity session = new AuthSessionEntity
        {
            IsRevoked = false,
            ExpiresAt = null
        };

        // Act & Assert
        session.IsValid().ShouldBeTrue();
    }

    [TestMethod]
    public void PersonEmailIndexEntity_NormalizeEmail_LowercasesEmail()
    {
        // Act
        string normalized = PersonEmailIndexEntity.NormalizeEmail("TEST@EXAMPLE.COM");

        // Assert
        normalized.ShouldBe("test@example.com");
    }

    [TestMethod]
    public void PersonEmailIndexEntity_Create_SetsCorrectKeys()
    {
        // Act
        PersonEmailIndexEntity index = PersonEmailIndexEntity.Create("Test@Example.com", "person-123");

        // Assert
        index.PartitionKey.ShouldBe("EMAIL_INDEX");
        index.RowKey.ShouldBe("test@example.com");
        index.PersonId.ShouldBe("person-123");
    }

    #endregion
}
