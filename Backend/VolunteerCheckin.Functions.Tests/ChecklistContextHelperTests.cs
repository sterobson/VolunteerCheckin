using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for ChecklistContextHelper - builds marshal contexts and checklist status.
/// </summary>
[TestClass]
public class ChecklistContextHelperTests
{
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private ChecklistContextHelper _helper = null!;

    private const string EventId = "event-123";
    private const string MarshalId = "marshal-456";
    private const string PersonId = "person-789";
    private const string CheckpointId1 = "checkpoint-1";
    private const string CheckpointId2 = "checkpoint-2";
    private const string AreaId1 = "area-1";
    private const string AreaId2 = "area-2";

    [TestInitialize]
    public void Setup()
    {
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockEventRoleRepository = new Mock<IEventRoleRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();

        _helper = new ChecklistContextHelper(
            _mockAssignmentRepository.Object,
            _mockLocationRepository.Object,
            _mockAreaRepository.Object,
            _mockEventRoleRepository.Object,
            _mockMarshalRepository.Object
        );
    }

    private static LocationEntity CreateLocation(string locationId, params string[] areaIds)
    {
        return new LocationEntity
        {
            PartitionKey = EventId,
            RowKey = locationId,
            EventId = EventId,
            Name = $"Location {locationId}",
            AreaIdsJson = JsonSerializer.Serialize(areaIds.ToList())
        };
    }

    private static AssignmentEntity CreateAssignment(string marshalId, string locationId)
    {
        return new AssignmentEntity
        {
            PartitionKey = EventId,
            RowKey = $"{marshalId}|{locationId}",
            EventId = EventId,
            MarshalId = marshalId,
            LocationId = locationId
        };
    }

    private static MarshalEntity CreateMarshal(string marshalId, string? personId = null)
    {
        return new MarshalEntity
        {
            PartitionKey = EventId,
            RowKey = marshalId,
            MarshalId = marshalId,
            Name = $"Marshal {marshalId}",
            PersonId = personId ?? string.Empty
        };
    }

    private static AreaEntity CreateArea(string areaId)
    {
        return new AreaEntity
        {
            PartitionKey = EventId,
            RowKey = areaId,
            EventId = EventId,
            Name = $"Area {areaId}"
        };
    }

    #region BuildMarshalContextAsync Tests

    [TestMethod]
    public async Task BuildMarshalContextAsync_MarshalWithAssignments_ReturnsCorrectContext()
    {
        // Arrange
        List<AssignmentEntity> assignments =
        [
            CreateAssignment(MarshalId, CheckpointId1),
            CreateAssignment(MarshalId, CheckpointId2)
        ];

        List<LocationEntity> locations =
        [
            CreateLocation(CheckpointId1, AreaId1),
            CreateLocation(CheckpointId2, AreaId1, AreaId2)
        ];

        MarshalEntity marshal = CreateMarshal(MarshalId, PersonId);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync(assignments);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(locations);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);
        _mockEventRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);

        // Act
        ChecklistScopeHelper.MarshalContext context = await _helper.BuildMarshalContextAsync(EventId, MarshalId);

        // Assert
        context.MarshalId.ShouldBe(MarshalId);
        context.AssignedLocationIds.ShouldContain(CheckpointId1);
        context.AssignedLocationIds.ShouldContain(CheckpointId2);
        context.AssignedAreaIds.ShouldContain(AreaId1);
        context.AssignedAreaIds.ShouldContain(AreaId2);
    }

    [TestMethod]
    public async Task BuildMarshalContextAsync_MarshalWithNoAssignments_ReturnsEmptyLists()
    {
        // Arrange
        MarshalEntity marshal = CreateMarshal(MarshalId, PersonId);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);
        _mockEventRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);

        // Act
        ChecklistScopeHelper.MarshalContext context = await _helper.BuildMarshalContextAsync(EventId, MarshalId);

        // Assert
        context.MarshalId.ShouldBe(MarshalId);
        context.AssignedLocationIds.ShouldBeEmpty();
        context.AssignedAreaIds.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task BuildMarshalContextAsync_AreaLead_ReturnsAreaLeadIds()
    {
        // Arrange
        MarshalEntity marshal = CreateMarshal(MarshalId, PersonId);

        EventRoleEntity areaLeadRole = new EventRoleEntity
        {
            PartitionKey = PersonId,
            RowKey = $"{EventId}|role-1",
            PersonId = PersonId,
            EventId = EventId,
            Role = Constants.RoleEventAreaLead,
            AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId1, AreaId2 })
        };

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);
        _mockEventRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([areaLeadRole]);

        // Act
        ChecklistScopeHelper.MarshalContext context = await _helper.BuildMarshalContextAsync(EventId, MarshalId);

        // Assert
        context.AreaLeadForAreaIds.ShouldContain(AreaId1);
        context.AreaLeadForAreaIds.ShouldContain(AreaId2);
    }

    [TestMethod]
    public async Task BuildMarshalContextAsync_MarshalWithoutPersonId_ReturnsEmptyAreaLeadIds()
    {
        // Arrange
        MarshalEntity marshalWithoutPerson = CreateMarshal(MarshalId, null);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshalWithoutPerson);

        // Act
        ChecklistScopeHelper.MarshalContext context = await _helper.BuildMarshalContextAsync(EventId, MarshalId);

        // Assert
        context.AreaLeadForAreaIds.ShouldBeEmpty();
        _mockEventRoleRepository.Verify(
            r => r.GetByPersonAndEventAsync(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [TestMethod]
    public async Task BuildMarshalContextAsync_MarshalNotFound_ReturnsEmptyAreaLeadIds()
    {
        // Arrange
        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync((MarshalEntity?)null);

        // Act
        ChecklistScopeHelper.MarshalContext context = await _helper.BuildMarshalContextAsync(EventId, MarshalId);

        // Assert
        context.AreaLeadForAreaIds.ShouldBeEmpty();
    }

    #endregion

    #region PreloadEventDataAsync Tests

    [TestMethod]
    public async Task PreloadEventDataAsync_LoadsAllData()
    {
        // Arrange
        List<AssignmentEntity> assignments =
        [
            CreateAssignment(MarshalId, CheckpointId1),
            CreateAssignment("marshal-other", CheckpointId2)
        ];

        List<LocationEntity> locations =
        [
            CreateLocation(CheckpointId1, AreaId1),
            CreateLocation(CheckpointId2, AreaId2)
        ];

        List<AreaEntity> areas =
        [
            CreateArea(AreaId1),
            CreateArea(AreaId2)
        ];

        List<MarshalEntity> marshals =
        [
            CreateMarshal(MarshalId, PersonId)
        ];

        EventRoleEntity areaLeadRole = new EventRoleEntity
        {
            PersonId = PersonId,
            EventId = EventId,
            Role = Constants.RoleEventAreaLead,
            AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId1 })
        };

        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(assignments);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(locations);
        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(areas);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(marshals);
        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([areaLeadRole]);

        // Act
        ChecklistContextHelper.PreloadedEventData data = await _helper.PreloadEventDataAsync(EventId);

        // Assert
        data.AssignmentsByMarshal.ShouldContainKey(MarshalId);
        data.AssignmentsByMarshal[MarshalId].Count.ShouldBe(1);
        data.LocationsById.ShouldContainKey(CheckpointId1);
        data.LocationsById.ShouldContainKey(CheckpointId2);
        data.Areas.Count.ShouldBe(2);
        data.AreaLeadsByMarshalId.ShouldContainKey(MarshalId);
        data.AreaLeadsByMarshalId[MarshalId].ShouldContain(AreaId1);
    }

    [TestMethod]
    public async Task PreloadEventDataAsync_EmptyEvent_ReturnsEmptyCollections()
    {
        // Arrange
        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);
        _mockEventRoleRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        ChecklistContextHelper.PreloadedEventData data = await _helper.PreloadEventDataAsync(EventId);

        // Assert
        data.AssignmentsByMarshal.ShouldBeEmpty();
        data.LocationsById.ShouldBeEmpty();
        data.Areas.ShouldBeEmpty();
        data.AreaLeadsByMarshalId.ShouldBeEmpty();
    }

    #endregion

    #region BuildMarshalContextFromPreloaded Tests

    [TestMethod]
    public void BuildMarshalContextFromPreloaded_WithPreloadedData_ReturnsCorrectContext()
    {
        // Arrange
        Dictionary<string, List<AssignmentEntity>> assignmentsByMarshal = new()
        {
            [MarshalId] = [CreateAssignment(MarshalId, CheckpointId1)]
        };

        Dictionary<string, LocationEntity> locationsById = new()
        {
            [CheckpointId1] = CreateLocation(CheckpointId1, AreaId1, AreaId2)
        };

        List<AreaEntity> areas = [CreateArea(AreaId1), CreateArea(AreaId2)];

        Dictionary<string, List<string>> areaLeadsByMarshalId = new()
        {
            [MarshalId] = [AreaId1]
        };

        ChecklistContextHelper.PreloadedEventData preloaded = new(
            assignmentsByMarshal,
            locationsById,
            areas,
            areaLeadsByMarshalId
        );

        // Act
        ChecklistScopeHelper.MarshalContext context = ChecklistContextHelper.BuildMarshalContextFromPreloaded(
            MarshalId, preloaded);

        // Assert
        context.MarshalId.ShouldBe(MarshalId);
        context.AssignedLocationIds.ShouldContain(CheckpointId1);
        context.AssignedAreaIds.ShouldContain(AreaId1);
        context.AssignedAreaIds.ShouldContain(AreaId2);
        context.AreaLeadForAreaIds.ShouldContain(AreaId1);
    }

    [TestMethod]
    public void BuildMarshalContextFromPreloaded_MarshalNotInData_ReturnsEmptyContext()
    {
        // Arrange
        ChecklistContextHelper.PreloadedEventData preloaded = new(
            new Dictionary<string, List<AssignmentEntity>>(),
            new Dictionary<string, LocationEntity>(),
            [],
            new Dictionary<string, List<string>>()
        );

        // Act
        ChecklistScopeHelper.MarshalContext context = ChecklistContextHelper.BuildMarshalContextFromPreloaded(
            "unknown-marshal", preloaded);

        // Assert
        context.MarshalId.ShouldBe("unknown-marshal");
        context.AssignedLocationIds.ShouldBeEmpty();
        context.AssignedAreaIds.ShouldBeEmpty();
        context.AreaLeadForAreaIds.ShouldBeEmpty();
    }

    #endregion

    #region BuildMarshalContextsFromPreloaded Tests

    [TestMethod]
    public void BuildMarshalContextsFromPreloaded_MultipleMarshalIds_ReturnsAllContexts()
    {
        // Arrange
        string marshalId2 = "marshal-2";

        Dictionary<string, List<AssignmentEntity>> assignmentsByMarshal = new()
        {
            [MarshalId] = [CreateAssignment(MarshalId, CheckpointId1)],
            [marshalId2] = [CreateAssignment(marshalId2, CheckpointId2)]
        };

        Dictionary<string, LocationEntity> locationsById = new()
        {
            [CheckpointId1] = CreateLocation(CheckpointId1, AreaId1),
            [CheckpointId2] = CreateLocation(CheckpointId2, AreaId2)
        };

        ChecklistContextHelper.PreloadedEventData preloaded = new(
            assignmentsByMarshal,
            locationsById,
            [],
            new Dictionary<string, List<string>>()
        );

        // Act
        Dictionary<string, ChecklistScopeHelper.MarshalContext> contexts =
            ChecklistContextHelper.BuildMarshalContextsFromPreloaded(
                [MarshalId, marshalId2], preloaded);

        // Assert
        contexts.Count.ShouldBe(2);
        contexts[MarshalId].AssignedLocationIds.ShouldContain(CheckpointId1);
        contexts[marshalId2].AssignedLocationIds.ShouldContain(CheckpointId2);
    }

    #endregion

    #region IsItemVisible Tests

    [TestMethod]
    public void IsItemVisible_NoTimeConstraints_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = null,
            VisibleUntil = null
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemVisible_FutureVisibleFrom_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = DateTime.UtcNow.AddHours(1),
            VisibleUntil = null
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemVisible_PastVisibleUntil_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = null,
            VisibleUntil = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemVisible_WithinTimeWindow_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = DateTime.UtcNow.AddHours(-1),
            VisibleUntil = DateTime.UtcNow.AddHours(1)
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemVisible_PastVisibleFromNoUntil_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = DateTime.UtcNow.AddHours(-1),
            VisibleUntil = null
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemVisible_NoFromFutureUntil_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            VisibleFrom = null,
            VisibleUntil = DateTime.UtcNow.AddHours(1)
        };

        // Act
        bool isVisible = ChecklistContextHelper.IsItemVisible(item);

        // Assert
        isVisible.ShouldBeTrue();
    }

    #endregion

    #region BuildItemWithStatus Tests

    [TestMethod]
    public void BuildItemWithStatus_NotCompleted_ReturnsCorrectStatus()
    {
        // Arrange
        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = "item-1",
            EventId = EventId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeSpecificPeople,
                    ItemType = "Marshal",
                    Ids = [MarshalId]
                }
            }),
            DisplayOrder = 1,
            IsRequired = true
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId, [], [], []);

        Dictionary<string, LocationEntity> checkpointLookup = [];
        List<ChecklistCompletionEntity> completions = [];

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions);

        // Assert
        result.ItemId.ShouldBe("item-1");
        result.Text.ShouldBe("Test item");
        result.IsCompleted.ShouldBeFalse();
        result.CanBeCompletedByMe.ShouldBeTrue();
        result.CompletedByActorName.ShouldBeNull();
        result.CompletedAt.ShouldBeNull();
    }

    [TestMethod]
    public void BuildItemWithStatus_Completed_ReturnsCompletionInfo()
    {
        // Arrange
        string itemId = "item-1";

        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = itemId,
            EventId = EventId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeSpecificPeople,
                    ItemType = "Marshal",
                    Ids = [MarshalId]
                }
            }),
            DisplayOrder = 1
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId, [], [], []);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        DateTime completedAt = DateTime.UtcNow.AddMinutes(-10);
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = itemId,
                ContextOwnerMarshalId = MarshalId,
                ActorName = "John Smith",
                ActorType = Constants.ActorTypeMarshal,
                ActorId = MarshalId,
                CompletedAt = completedAt,
                IsDeleted = false
            }
        ];

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions);

        // Assert
        result.IsCompleted.ShouldBeTrue();
        result.CompletedByActorName.ShouldBe("John Smith");
        result.CompletedByActorType.ShouldBe(Constants.ActorTypeMarshal);
        result.CompletedByActorId.ShouldBe(MarshalId);
        result.CompletedAt.ShouldBe(completedAt);
    }

    [TestMethod]
    public void BuildItemWithStatus_DeletedCompletion_NotShownAsCompleted()
    {
        // Arrange
        string itemId = "item-1";

        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = itemId,
            EventId = EventId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeSpecificPeople,
                    ItemType = "Marshal",
                    Ids = [MarshalId]
                }
            }),
            DisplayOrder = 1
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId, [], [], []);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = itemId,
                ContextOwnerMarshalId = MarshalId,
                ActorName = "John Smith",
                IsDeleted = true // Deleted
            }
        ];

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions);

        // Assert
        result.IsCompleted.ShouldBeFalse();
    }

    [TestMethod]
    public void BuildItemWithStatus_SharedScope_UsesContextIdForCompletion()
    {
        // Arrange
        string itemId = "item-1";

        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = itemId,
            EventId = EventId,
            Text = "Shared checkpoint item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeOnePerCheckpoint,
                    ItemType = "Checkpoint",
                    Ids = [CheckpointId1]
                }
            }),
            DisplayOrder = 1
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId,
            [AreaId1],
            [CheckpointId1],
            []);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Completion by another marshal for the same checkpoint
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = itemId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = CheckpointId1,
                ActorName = "Other Marshal",
                CompletedAt = DateTime.UtcNow,
                IsDeleted = false
            }
        ];

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions);

        // Assert
        result.IsCompleted.ShouldBeTrue();
        result.CompletionContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        result.CompletionContextId.ShouldBe(CheckpointId1);
    }

    [TestMethod]
    public void BuildItemWithStatus_WithScopeMatchResult_UsesProvidedResult()
    {
        // Arrange
        string itemId = "item-1";

        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = itemId,
            EventId = EventId,
            Text = "Test item",
            ScopeConfigurationsJson = "[]", // Empty - would not match normally
            DisplayOrder = 1
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId, [], [], []);

        Dictionary<string, LocationEntity> checkpointLookup = [];
        List<ChecklistCompletionEntity> completions = [];

        // Provide explicit scope match result
        ChecklistScopeHelper.ScopeMatchResult scopeResult = new ChecklistScopeHelper.ScopeMatchResult(
            true,
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople },
            1,
            Constants.ChecklistContextPersonal,
            MarshalId
        );

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions, scopeResult);

        // Assert
        result.CompletionContextType.ShouldBe(Constants.ChecklistContextPersonal);
        result.CompletionContextId.ShouldBe(MarshalId);
        result.MatchedScope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void BuildMarshalContextFromPreloaded_LocationNotInLookup_SkipsAreaResolution()
    {
        // Arrange
        Dictionary<string, List<AssignmentEntity>> assignmentsByMarshal = new()
        {
            [MarshalId] = [CreateAssignment(MarshalId, "unknown-checkpoint")]
        };

        // Location not in lookup
        Dictionary<string, LocationEntity> locationsById = [];

        ChecklistContextHelper.PreloadedEventData preloaded = new(
            assignmentsByMarshal,
            locationsById,
            [],
            new Dictionary<string, List<string>>()
        );

        // Act
        ChecklistScopeHelper.MarshalContext context = ChecklistContextHelper.BuildMarshalContextFromPreloaded(
            MarshalId, preloaded);

        // Assert
        context.AssignedLocationIds.ShouldContain("unknown-checkpoint");
        context.AssignedAreaIds.ShouldBeEmpty(); // Can't resolve areas without location
    }

    [TestMethod]
    public void BuildItemWithStatus_DifferentMarshalCompletion_NotShownAsCompleted()
    {
        // Arrange - Personal scope, completion by different marshal shouldn't show
        string itemId = "item-1";

        ChecklistItemEntity item = new ChecklistItemEntity
        {
            ItemId = itemId,
            EventId = EventId,
            Text = "Personal item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeEveryoneInAreas,
                    ItemType = "Area",
                    Ids = [AreaId1]
                }
            }),
            DisplayOrder = 1
        };

        ChecklistScopeHelper.MarshalContext context = new ChecklistScopeHelper.MarshalContext(
            MarshalId, [AreaId1], [], []);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Completion by different marshal
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = itemId,
                ContextOwnerMarshalId = "different-marshal",
                ActorName = "Other Person",
                IsDeleted = false
            }
        ];

        // Act
        ChecklistItemWithStatus result = ChecklistContextHelper.BuildItemWithStatus(
            item, context, checkpointLookup, completions);

        // Assert
        result.IsCompleted.ShouldBeFalse(); // Different marshal's completion doesn't count for personal scope
    }

    #endregion
}
