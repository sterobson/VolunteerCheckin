using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for NoteFunctions CRUD operations and marshal note queries.
/// </summary>
[TestClass]
public class NoteFunctionsTests
{
    private Mock<ILogger<NoteFunctions>> _mockLogger = null!;
    private Mock<INoteRepository> _mockNoteRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private NoteFunctions _functions = null!;

    private const string EventId = "event123";
    private const string NoteId = "note456";
    private const string MarshalId = "marshal789";
    private const string PersonId = "person123";
    private const string PersonName = "Test User";
    private const string AdminEmail = "admin@test.com";
    private const string AreaId = "area1";
    private const string LocationId = "location1";
    private const string SessionToken = "valid-session-token";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<NoteFunctions>>();
        _mockNoteRepository = new Mock<INoteRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();

        // Create mock for ClaimsService - needs constructor args but we'll use CallBase = false
        _mockClaimsService = new Mock<ClaimsService>(
            Mock.Of<IAuthSessionRepository>(),
            Mock.Of<IPersonRepository>(),
            Mock.Of<IEventRoleRepository>(),
            Mock.Of<IMarshalRepository>(),
            Mock.Of<ISampleEventService>(),
            Mock.Of<IEventDeletionRepository>()
        );

        _functions = new NoteFunctions(
            _mockLogger.Object,
            _mockNoteRepository.Object,
            _mockLocationRepository.Object,
            _mockMarshalRepository.Object,
            _mockAssignmentRepository.Object,
            _mockAreaRepository.Object,
            _mockClaimsService.Object
        );
    }

    private UserClaims CreateAdminClaims()
    {
        return new UserClaims(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: AdminEmail,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null,
            EventRoles: [new EventRoleInfo(Constants.RoleEventAdmin, [])]
        );
    }

    private UserClaims CreateAreaLeadClaims()
    {
        return new UserClaims(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: AdminEmail,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: MarshalId,
            EventRoles: [new EventRoleInfo(Constants.RoleEventAreaLead, [AreaId])]
        );
    }

    private UserClaims CreateMarshalClaims()
    {
        return new UserClaims(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: "marshal@test.com",
            EventId: EventId,
            AuthMethod: Constants.AuthMethodMarshalMagicCode,
            MarshalId: MarshalId,
            EventRoles: []
        );
    }

    private void SetupClaimsService(UserClaims? claims)
    {
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(claims);
    }

    #region CreateNote Tests

    [TestMethod]
    public async Task CreateNote_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        CreateNoteRequest request = new(
            Title: "Important Notice",
            Content: "This is the content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }],
            DisplayOrder: 1,
            Priority: Constants.NotePriorityHigh,
            Category: "Safety",
            IsPinned: true
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        NoteEntity? capturedEntity = null;
        _mockNoteRepository
            .Setup(r => r.AddAsync(It.IsAny<NoteEntity>()))
            .Callback<NoteEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        NoteResponse response = (NoteResponse)okResult.Value!;

        response.EventId.ShouldBe(EventId);
        response.Title.ShouldBe("Important Notice");
        response.Content.ShouldBe("This is the content");
        response.Priority.ShouldBe(Constants.NotePriorityHigh);
        response.Category.ShouldBe("Safety");
        response.IsPinned.ShouldBeTrue();
        response.ScopeConfigurations.Count.ShouldBe(1);
        response.ScopeConfigurations[0].Scope.ShouldBe(Constants.ChecklistScopeEveryoneInAreas);

        capturedEntity.ShouldNotBeNull();
        capturedEntity.CreatedByPersonId.ShouldBe(PersonId);
        capturedEntity.CreatedByName.ShouldBe(PersonName);
    }

    [TestMethod]
    public async Task CreateNote_AreaLeadCanCreate_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAreaLeadClaims());

        CreateNoteRequest request = new(
            Title: "Area Lead Note",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }]
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        _mockNoteRepository
            .Setup(r => r.AddAsync(It.IsAny<NoteEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [TestMethod]
    public async Task CreateNote_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(null);

        CreateNoteRequest request = new(
            Title: "Test",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }]
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task CreateNote_MarshalCannotCreate_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(CreateMarshalClaims());

        CreateNoteRequest request = new(
            Title: "Test",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }]
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task CreateNote_MissingTitle_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        CreateNoteRequest request = new(
            Title: "",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }]
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task CreateNote_MissingScopeConfigurations_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        CreateNoteRequest request = new(
            Title: "Test",
            Content: "Content",
            ScopeConfigurations: []
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task CreateNote_OnePerScopeNotAllowed_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        CreateNoteRequest request = new(
            Title: "Test",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId] }]
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateNote(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        BadRequestObjectResult badRequest = (BadRequestObjectResult)result;
        string json = JsonSerializer.Serialize(badRequest.Value);
        json.ShouldContain("One per");
    }

    #endregion

    #region GetNotes Tests

    [TestMethod]
    public async Task GetNotes_AdminCanSeeAll_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        List<NoteEntity> notes =
        [
            CreateNoteEntity("note1", "Note 1"),
            CreateNoteEntity("note2", "Note 2")
        ];

        _mockNoteRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(notes);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<NoteResponse> response = (List<NoteResponse>)okResult.Value!;

        response.Count.ShouldBe(2);
        response[0].NoteId.ShouldBe("note1");
        response[1].NoteId.ShouldBe("note2");
    }

    [TestMethod]
    public async Task GetNotes_NonAdminCannotSeeAll_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(CreateAreaLeadClaims());

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region GetNote Tests

    [TestMethod]
    public async Task GetNote_NoteExists_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        NoteEntity note = CreateNoteEntity(NoteId, "Test Note");

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync(note);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        NoteResponse response = (NoteResponse)okResult.Value!;

        response.NoteId.ShouldBe(NoteId);
        response.Title.ShouldBe("Test Note");
    }

    [TestMethod]
    public async Task GetNote_NoteNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync((NoteEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task GetNote_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(CreateMarshalClaims());

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region UpdateNote Tests

    [TestMethod]
    public async Task UpdateNote_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        NoteEntity existingNote = CreateNoteEntity(NoteId, "Old Title");

        UpdateNoteRequest request = new(
            Title: "Updated Title",
            Content: "Updated content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }],
            DisplayOrder: 5,
            Priority: Constants.NotePriorityUrgent,
            Category: "Updated Category",
            IsPinned: true,
            ShowInEmergencyInfo: true
        );

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync(existingNote);

        NoteEntity? capturedEntity = null;
        _mockNoteRepository
            .Setup(r => r.UpdateAsync(It.IsAny<NoteEntity>()))
            .Callback<NoteEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        NoteResponse response = (NoteResponse)okResult.Value!;

        response.Title.ShouldBe("Updated Title");
        response.Content.ShouldBe("Updated content");
        response.Priority.ShouldBe(Constants.NotePriorityUrgent);
        response.DisplayOrder.ShouldBe(5);
        response.IsPinned.ShouldBeTrue();

        capturedEntity.ShouldNotBeNull();
        capturedEntity.UpdatedByPersonId.ShouldBe(PersonId);
        capturedEntity.UpdatedByName.ShouldBe(PersonName);
        capturedEntity.UpdatedAt.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task UpdateNote_NoteNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        UpdateNoteRequest request = new(
            Title: "Updated Title",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }],
            DisplayOrder: 1,
            Priority: Constants.NotePriorityNormal,
            Category: null,
            IsPinned: false,
            ShowInEmergencyInfo: false
        );

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync((NoteEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UpdateNote_MissingTitle_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        NoteEntity existingNote = CreateNoteEntity(NoteId, "Old Title");

        UpdateNoteRequest request = new(
            Title: "",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }],
            DisplayOrder: 1,
            Priority: Constants.NotePriorityNormal,
            Category: null,
            IsPinned: false,
            ShowInEmergencyInfo: false
        );

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync(existingNote);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UpdateNote_OnePerScopeNotAllowed_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        NoteEntity existingNote = CreateNoteEntity(NoteId, "Old Title");

        UpdateNoteRequest request = new(
            Title: "Title",
            Content: "Content",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId] }],
            DisplayOrder: 1,
            Priority: Constants.NotePriorityNormal,
            Category: null,
            IsPinned: false,
            ShowInEmergencyInfo: false
        );

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync(existingNote);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region DeleteNote Tests

    [TestMethod]
    public async Task DeleteNote_NoteExists_ReturnsNoContent()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        NoteEntity note = CreateNoteEntity(NoteId, "Test Note");

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync(note);

        _mockNoteRepository
            .Setup(r => r.DeleteAsync(EventId, NoteId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockNoteRepository.Verify(r => r.DeleteAsync(EventId, NoteId), Times.Once);
    }

    [TestMethod]
    public async Task DeleteNote_NoteNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        _mockNoteRepository
            .Setup(r => r.GetAsync(EventId, NoteId))
            .ReturnsAsync((NoteEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task DeleteNote_AreaLeadCannotDelete_ReturnsUnauthorized()
    {
        // Arrange - Area leads can create/edit but not delete
        SetupClaimsService(CreateAreaLeadClaims());

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteNote(httpRequest, EventId, NoteId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region GetNotesForMarshal Tests

    [TestMethod]
    public async Task GetNotesForMarshal_ReturnsRelevantNotes()
    {
        // Arrange
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal",
            PersonId = PersonId
        };

        List<AssignmentEntity> assignments =
        [
            new AssignmentEntity
            {
                PartitionKey = EventId,
                RowKey = "assignment1",
                MarshalId = MarshalId,
                LocationId = LocationId
            }
        ];

        List<LocationEntity> locations =
        [
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = LocationId,
                Name = "Checkpoint 1",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId })
            }
        ];

        List<AreaEntity> areas =
        [
            new AreaEntity
            {
                PartitionKey = EventId,
                RowKey = AreaId,
                Name = "Area 1",
                ContactsJson = "[]"
            }
        ];

        // Note that should be visible (matches area)
        NoteEntity visibleNote = CreateNoteEntity("note1", "Visible Note");
        visibleNote.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }
        });

        // Note that should NOT be visible (different area)
        NoteEntity hiddenNote = CreateNoteEntity("note2", "Hidden Note");
        hiddenNote.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = ["other-area"] }
        });

        _mockMarshalRepository.Setup(r => r.GetAsync(EventId, MarshalId)).ReturnsAsync(marshal);
        _mockMarshalRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([marshal]);
        _mockAssignmentRepository.Setup(r => r.GetByMarshalAsync(EventId, MarshalId)).ReturnsAsync(assignments);
        _mockLocationRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(locations);
        _mockAreaRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(areas);
        _mockNoteRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([visibleNote, hiddenNote]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.GetNotesForMarshal(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<NoteForMarshalResponse> response = (List<NoteForMarshalResponse>)okResult.Value!;

        response.Count.ShouldBe(1);
        response[0].NoteId.ShouldBe("note1");
        response[0].Title.ShouldBe("Visible Note");
    }

    [TestMethod]
    public async Task GetNotesForMarshal_MarshalNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockMarshalRepository.Setup(r => r.GetAsync(EventId, MarshalId)).ReturnsAsync((MarshalEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.GetNotesForMarshal(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task GetNotesForMarshal_SortsByPriorityAndPinned()
    {
        // Arrange
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal",
            PersonId = PersonId
        };

        List<AssignmentEntity> assignments =
        [
            new AssignmentEntity
            {
                PartitionKey = EventId,
                RowKey = "assignment1",
                MarshalId = MarshalId,
                LocationId = LocationId
            }
        ];

        List<LocationEntity> locations =
        [
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = LocationId,
                Name = "Checkpoint 1",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId })
            }
        ];

        List<AreaEntity> areas = [];

        // Create notes with different priorities
        NoteEntity normalNote = CreateNoteEntity("note1", "Normal Note");
        normalNote.Priority = Constants.NotePriorityNormal;
        normalNote.IsPinned = false;
        normalNote.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId] }
        });

        NoteEntity urgentNote = CreateNoteEntity("note2", "Urgent Note");
        urgentNote.Priority = Constants.NotePriorityUrgent;
        urgentNote.IsPinned = false;
        urgentNote.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId] }
        });

        NoteEntity pinnedNote = CreateNoteEntity("note3", "Pinned Note");
        pinnedNote.Priority = Constants.NotePriorityLow;
        pinnedNote.IsPinned = true;
        pinnedNote.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId] }
        });

        _mockMarshalRepository.Setup(r => r.GetAsync(EventId, MarshalId)).ReturnsAsync(marshal);
        _mockMarshalRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([marshal]);
        _mockAssignmentRepository.Setup(r => r.GetByMarshalAsync(EventId, MarshalId)).ReturnsAsync(assignments);
        _mockLocationRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(locations);
        _mockAreaRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(areas);
        _mockNoteRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([normalNote, urgentNote, pinnedNote]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.GetNotesForMarshal(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<NoteForMarshalResponse> response = (List<NoteForMarshalResponse>)okResult.Value!;

        response.Count.ShouldBe(3);
        // Pinned first, then by priority (Urgent > Normal > Low)
        response[0].NoteId.ShouldBe("note3"); // Pinned
        response[1].NoteId.ShouldBe("note2"); // Urgent
        response[2].NoteId.ShouldBe("note1"); // Normal
    }

    #endregion

    #region GetMyNotes Tests

    [TestMethod]
    public async Task GetMyNotes_MarshalSession_ReturnsRelevantNotes()
    {
        // Arrange
        UserClaims marshalClaims = CreateMarshalClaims();
        SetupClaimsService(marshalClaims);

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal",
            PersonId = PersonId
        };

        List<AssignmentEntity> assignments =
        [
            new AssignmentEntity
            {
                PartitionKey = EventId,
                RowKey = "assignment1",
                MarshalId = MarshalId,
                LocationId = LocationId
            }
        ];

        List<LocationEntity> locations =
        [
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = LocationId,
                Name = "Checkpoint 1",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId })
            }
        ];

        List<AreaEntity> areas = [];

        NoteEntity note = CreateNoteEntity("note1", "Marshal Note");
        note.ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
        {
            new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId] }
        });

        _mockMarshalRepository.Setup(r => r.GetAsync(EventId, MarshalId)).ReturnsAsync(marshal);
        _mockMarshalRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([marshal]);
        _mockAssignmentRepository.Setup(r => r.GetByMarshalAsync(EventId, MarshalId)).ReturnsAsync(assignments);
        _mockLocationRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(locations);
        _mockAreaRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(areas);
        _mockNoteRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([note]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetMyNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<NoteForMarshalResponse> response = (List<NoteForMarshalResponse>)okResult.Value!;

        response.Count.ShouldBe(1);
        response[0].Title.ShouldBe("Marshal Note");
    }

    [TestMethod]
    public async Task GetMyNotes_AdminWithoutMarshalAssignment_ReturnsAllNotes()
    {
        // Arrange
        UserClaims adminClaims = new(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: AdminEmail,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null, // Admin without marshal assignment
            EventRoles: [new EventRoleInfo(Constants.RoleEventAdmin, [])]
        );
        SetupClaimsService(adminClaims);

        List<NoteEntity> notes =
        [
            CreateNoteEntity("note1", "Note 1"),
            CreateNoteEntity("note2", "Note 2")
        ];

        _mockNoteRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync(notes);
        _mockMarshalRepository.Setup(r => r.GetByEventAsync(EventId)).ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetMyNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<NoteForMarshalResponse> response = (List<NoteForMarshalResponse>)okResult.Value!;

        response.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task GetMyNotes_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetMyNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task GetMyNotes_NoMarshalAssignmentNonAdmin_ReturnsBadRequest()
    {
        // Arrange
        UserClaims claims = new(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: "user@test.com",
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null, // No marshal ID
            EventRoles: [] // Not an admin
        );
        SetupClaimsService(claims);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetMyNotes(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region Helper Methods

    private NoteEntity CreateNoteEntity(string noteId, string title)
    {
        return new NoteEntity
        {
            PartitionKey = EventId,
            RowKey = noteId,
            EventId = EventId,
            NoteId = noteId,
            Title = title,
            Content = "Test content",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }
            }),
            DisplayOrder = 0,
            Priority = Constants.NotePriorityNormal,
            Category = string.Empty,
            IsPinned = false,
            CreatedByPersonId = PersonId,
            CreatedByName = PersonName,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }

    #endregion
}
