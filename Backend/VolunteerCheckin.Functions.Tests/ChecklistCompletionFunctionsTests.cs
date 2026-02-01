using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class ChecklistCompletionFunctionsTests
{
    private Mock<ILogger<ChecklistCompletionFunctions>> _mockLogger = null!;
    private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
    private Mock<IChecklistCompletionRepository> _mockChecklistCompletionRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private ChecklistCompletionFunctions _functions = null!;

    private const string EventId = "event123";
    private const string ItemId = "item456";
    private const string MarshalId = "marshal789";
    private const string AdminEmail = "admin@test.com";
    private const string AreaId = "area1";
    private const string LocationId = "location1";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ChecklistCompletionFunctions>>();
        _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
        _mockChecklistCompletionRepository = new Mock<IChecklistCompletionRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockEventRoleRepository = new Mock<IEventRoleRepository>();
        _mockClaimsService = new Mock<ClaimsService>(null!, null!, null!, null!, null!, null!);

        SetupClaimsService();

        _functions = new ChecklistCompletionFunctions(
            _mockLogger.Object,
            _mockChecklistItemRepository.Object,
            _mockChecklistCompletionRepository.Object,
            _mockMarshalRepository.Object,
            _mockLocationRepository.Object,
            _mockAssignmentRepository.Object,
            _mockAreaRepository.Object,
            _mockEventRoleRepository.Object,
            _mockClaimsService.Object
        );
    }

    private void SetupClaimsService()
    {
        UserClaims adminClaims = new(
            PersonId: "person123",
            PersonName: "Admin User",
            PersonEmail: AdminEmail,
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: "SecureEmailLink",
            MarshalId: null,
            EventRoles: []
        );

        _mockClaimsService
            .Setup(c => c.GetClaimsWithSampleSupportAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(adminClaims);
    }

    private void SetupMarshalContext()
    {
        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([new AssignmentEntity { LocationId = LocationId }]);

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventRoleEntity>());
    }

    #region CompleteChecklistItem Tests

    [TestMethod]
    public async Task CompleteChecklistItem_ValidRequest_ReturnsOk()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        ChecklistCompletionEntity? capturedCompletion = null;
        _mockChecklistCompletionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Callback<ChecklistCompletionEntity>(c => capturedCompletion = c)
            .ReturnsAsync((ChecklistCompletionEntity c) => c);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        capturedCompletion.ShouldNotBeNull();
        capturedCompletion.ChecklistItemId.ShouldBe(ItemId);
        capturedCompletion.ContextOwnerMarshalId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public async Task CompleteChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_MarshalNotFound_ReturnsNotFound()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync((MarshalEntity?)null);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_AlreadyCompleted_ReturnsBadRequest()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        // Setup marshal lookup (needed because it now happens before completion check)
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        // Already has a completion
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                IsDeleted = false
            }]);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_MarshalLacksPermission_ReturnsForbidden()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Item scoped to different marshal
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = ["other-marshal"] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<ForbidResult>();
    }

    #endregion

    #region UncompleteChecklistItem Tests

    [TestMethod]
    public async Task UncompleteChecklistItem_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = ChecklistCompletionEntity.CreatePartitionKey(EventId),
            RowKey = ChecklistCompletionEntity.CreateRowKey(ItemId, "completion1"),
            ChecklistItemId = ItemId,
            ContextOwnerMarshalId = MarshalId,
            IsDeleted = false
        };
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        _mockChecklistCompletionRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_CompletionNotFound_ReturnsNotFound()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        // No completions exist
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_WithActorMarshalId_NoAdminEmail_ReturnsNoContent()
    {
        // Arrange - Area lead (Ruth) uncompleting marshal's (Andrew's) task
        string actorMarshalId = "area-lead-ruth";
        CompleteChecklistItemRequest request = new(MarshalId, null, null, actorMarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request); // No admin email header

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        // Setup actor marshal (area lead Ruth)
        MarshalEntity actorMarshal = new()
        {
            MarshalId = actorMarshalId,
            EventId = EventId,
            Name = "Ruth"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, actorMarshalId))
            .ReturnsAsync(actorMarshal);

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = ChecklistCompletionEntity.CreatePartitionKey(EventId),
            RowKey = ChecklistCompletionEntity.CreateRowKey(ItemId, "completion1"),
            ChecklistItemId = ItemId,
            ContextOwnerMarshalId = MarshalId,
            IsDeleted = false
        };
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        _mockChecklistCompletionRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();

        // Verify the completion was updated with correct actor info
        _mockChecklistCompletionRepository.Verify(
            r => r.UpdateAsync(It.Is<ChecklistCompletionEntity>(c =>
                c.IsDeleted == true &&
                c.UncompletedByActorType == Constants.ActorTypeMarshal &&
                c.UncompletedByActorId == actorMarshalId &&
                c.UncompletedByActorName == "Ruth"
            )),
            Times.Once
        );
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_MarshalUncompletingOwnTask_NoAdminEmail_ReturnsNoContent()
    {
        // Arrange - Marshal uncompleting their own task (no actorMarshalId provided)
        CompleteChecklistItemRequest request = new(MarshalId, null, null, null);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request); // No admin email header

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        SetupMarshalContext();

        // Setup the marshal lookup for the actor (marshal uncompleting their own task)
        MarshalEntity marshal = new()
        {
            MarshalId = MarshalId,
            EventId = EventId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = ChecklistCompletionEntity.CreatePartitionKey(EventId),
            RowKey = ChecklistCompletionEntity.CreateRowKey(ItemId, "completion1"),
            ChecklistItemId = ItemId,
            ContextOwnerMarshalId = MarshalId,
            IsDeleted = false
        };
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        _mockChecklistCompletionRepository
            .Setup(r => r.UpdateAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();

        // Verify the completion was updated with the marshal as the actor
        _mockChecklistCompletionRepository.Verify(
            r => r.UpdateAsync(It.Is<ChecklistCompletionEntity>(c =>
                c.IsDeleted == true &&
                c.UncompletedByActorType == Constants.ActorTypeMarshal &&
                c.UncompletedByActorId == MarshalId &&
                c.UncompletedByActorName == "Test Marshal"
            )),
            Times.Once
        );
    }

    #endregion

    #region Multi-Checkpoint Completion Tests

    [TestMethod]
    public async Task CompleteChecklistItem_PersonalScopeAtMultipleCheckpoints_CanCompleteAtSecondCheckpoint()
    {
        // Arrange - Marshal is assigned to two checkpoints and has a personal scope task for both
        // They have already completed at checkpoint 1, now trying to complete at checkpoint 2
        string checkpoint1 = "checkpoint-1";
        string checkpoint2 = "checkpoint-2";

        CompleteChecklistItemRequest request = new(MarshalId, Constants.ChecklistContextCheckpoint, checkpoint2);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Check in at your checkpoint",
            LinksToCheckIn = true,
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [checkpoint1, checkpoint2] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        // Marshal is assigned to both checkpoints
        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([
                new AssignmentEntity { LocationId = checkpoint1 },
                new AssignmentEntity { LocationId = checkpoint2 }
            ]);

        LocationEntity location1 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint1,
            Name = "Checkpoint 1",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        LocationEntity location2 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint2,
            Name = "Checkpoint 2",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location1, location2]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventRoleEntity>());

        // Already has a completion at checkpoint 1 - but NOT at checkpoint 2
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = checkpoint1, // Completed at checkpoint 1 only
                IsDeleted = false
            }]);

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        ChecklistCompletionEntity? capturedCompletion = null;
        _mockChecklistCompletionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Callback<ChecklistCompletionEntity>(c => capturedCompletion = c)
            .ReturnsAsync((ChecklistCompletionEntity c) => c);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert - Should succeed because checkpoint 2 hasn't been completed yet
        result.ShouldBeOfType<OkObjectResult>();
        capturedCompletion.ShouldNotBeNull();
        capturedCompletion.ChecklistItemId.ShouldBe(ItemId);
        capturedCompletion.ContextOwnerMarshalId.ShouldBe(MarshalId);
        capturedCompletion.CompletionContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        capturedCompletion.CompletionContextId.ShouldBe(checkpoint2);
    }

    [TestMethod]
    public async Task CompleteChecklistItem_LinkedTaskAtSameCheckpoint_ReturnsBadRequest()
    {
        // Arrange - Marshal tries to complete at checkpoint 1 when already completed there
        string checkpoint1 = "checkpoint-1";

        CompleteChecklistItemRequest request = new(MarshalId, Constants.ChecklistContextCheckpoint, checkpoint1);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Check in at your checkpoint",
            LinksToCheckIn = true,
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [checkpoint1] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([new AssignmentEntity { LocationId = checkpoint1 }]);

        LocationEntity location1 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint1,
            Name = "Checkpoint 1",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location1]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventRoleEntity>());

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        // Already completed at checkpoint 1
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = checkpoint1,
                IsDeleted = false
            }]);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert - Should fail because already completed at this checkpoint
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_CheckpointContextTask_CompletionOrderDoesNotMatter()
    {
        // Arrange - Marshal completes checkpoint 2 first, then checkpoint 1
        // This tests that the fix allows completion in any order
        string checkpoint1 = "checkpoint-1";
        string checkpoint2 = "checkpoint-2";

        // Completing checkpoint 1 after checkpoint 2 was already completed
        CompleteChecklistItemRequest request = new(MarshalId, Constants.ChecklistContextCheckpoint, checkpoint1);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Check in at your checkpoint",
            LinksToCheckIn = true,
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [checkpoint1, checkpoint2] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([
                new AssignmentEntity { LocationId = checkpoint1 },
                new AssignmentEntity { LocationId = checkpoint2 }
            ]);

        LocationEntity location1 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint1,
            Name = "Checkpoint 1",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        LocationEntity location2 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint2,
            Name = "Checkpoint 2",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location1, location2]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventRoleEntity>());

        // Checkpoint 2 was completed first
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = checkpoint2, // Checkpoint 2 done first
                IsDeleted = false
            }]);

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        ChecklistCompletionEntity? capturedCompletion = null;
        _mockChecklistCompletionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Callback<ChecklistCompletionEntity>(c => capturedCompletion = c)
            .ReturnsAsync((ChecklistCompletionEntity c) => c);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert - Should succeed regardless of completion order
        result.ShouldBeOfType<OkObjectResult>();
        capturedCompletion.ShouldNotBeNull();
        capturedCompletion.CompletionContextId.ShouldBe(checkpoint1);
    }

    [TestMethod]
    public async Task CompleteChecklistItem_NonLinkedCheckpointTask_UsesContextFromRequest()
    {
        // Arrange - Non-linked task with checkpoint context
        string checkpoint1 = "checkpoint-1";
        string checkpoint2 = "checkpoint-2";

        CompleteChecklistItemRequest request = new(MarshalId, Constants.ChecklistContextCheckpoint, checkpoint2);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Equipment check",
            LinksToCheckIn = false, // Not a linked task
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [checkpoint1, checkpoint2] }
            })
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([
                new AssignmentEntity { LocationId = checkpoint1 },
                new AssignmentEntity { LocationId = checkpoint2 }
            ]);

        LocationEntity location1 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint1,
            Name = "Checkpoint 1",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        LocationEntity location2 = new()
        {
            PartitionKey = EventId,
            RowKey = checkpoint2,
            Name = "Checkpoint 2",
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location1, location2]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventRoleEntity>());

        // Already completed at checkpoint 1
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = checkpoint1,
                IsDeleted = false
            }]);

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        ChecklistCompletionEntity? capturedCompletion = null;
        _mockChecklistCompletionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Callback<ChecklistCompletionEntity>(c => capturedCompletion = c)
            .ReturnsAsync((ChecklistCompletionEntity c) => c);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert - Should succeed for checkpoint 2 even though checkpoint 1 is done
        result.ShouldBeOfType<OkObjectResult>();
        capturedCompletion.ShouldNotBeNull();
        capturedCompletion.CompletionContextId.ShouldBe(checkpoint2);
    }

    #endregion

    #region GetChecklistCompletionReport Tests

    [TestMethod]
    public async Task GetChecklistCompletionReport_ReturnsReport()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = "[]",
            IsRequired = true
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([item]);

        ChecklistCompletionEntity completion = new()
        {
            ChecklistItemId = ItemId,
            ContextOwnerMarshalId = MarshalId
        };
        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([completion]);

        MarshalEntity marshal = new()
        {
            MarshalId = MarshalId,
            Name = "Test Marshal"
        };
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([marshal]);

        // Act
        IActionResult result = await _functions.GetChecklistCompletionReport(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion
}
