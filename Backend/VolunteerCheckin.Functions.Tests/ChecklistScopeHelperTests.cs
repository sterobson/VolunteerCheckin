using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class ChecklistScopeHelperTests
{
    private const string EventId = "event123";
    private const string MarshalId = "marshal456";
    private const string AreaId1 = "area1";
    private const string AreaId2 = "area2";
    private const string LocationId1 = "location1";
    private const string LocationId2 = "location2";
    private const string ItemId = "item789";

    private ChecklistScopeHelper.MarshalContext CreateMarshalContext(
        List<string>? assignedAreaIds = null,
        List<string>? assignedLocationIds = null,
        List<string>? areaLeadForAreaIds = null)
    {
        return new ChecklistScopeHelper.MarshalContext(
            MarshalId,
            assignedAreaIds ?? [],
            assignedLocationIds ?? [],
            areaLeadForAreaIds ?? []
        );
    }

    private ChecklistItemEntity CreateChecklistItem(List<ScopeConfiguration> scopeConfigurations)
    {
        return new ChecklistItemEntity
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(scopeConfigurations),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = "admin@test.com",
            CreatedDate = DateTime.UtcNow
        };
    }

    private Dictionary<string, LocationEntity> CreateCheckpointLookup()
    {
        return new Dictionary<string, LocationEntity>
        {
            [LocationId1] = new LocationEntity
            {
                RowKey = LocationId1,
                EventId = EventId,
                Name = "Checkpoint 1",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId1 })
            },
            [LocationId2] = new LocationEntity
            {
                RowKey = LocationId2,
                EventId = EventId,
                Name = "Checkpoint 2",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId2 })
            }
        };
    }

    #region IsItemRelevantToMarshal Tests

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryoneScope_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryoneInAreas_MarshalInArea_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1, AreaId2] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryoneInAreas_MarshalNotInArea_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryoneAtCheckpoints_MarshalAtCheckpoint_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId1, LocationId2] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryoneAtCheckpoints_MarshalNotAtCheckpoint_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_SpecificPeople_MarshalInList_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId, "other-marshal"] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_SpecificPeople_MarshalNotInList_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = ["other-marshal"] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerArea_MarshalInArea_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerArea_MarshalIsAreaLead_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_MarshalAtCheckpoint_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_MarshalIsAreaLead_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AreaLead_MarshalIsAreaLead_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeAreaLead, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AreaLead_MarshalNotAreaLead_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeAreaLead, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region CanMarshalCompleteItem Tests

    [TestMethod]
    public void CanMarshalCompleteItem_EveryoneScope_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_EveryoneInAreas_MarshalInArea_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_EveryoneInAreas_MarshalNotInArea_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_SpecificPeople_MarshalInList_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_SpecificPeople_MarshalNotInList_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = ["other-marshal"] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_OnePerArea_MarshalInArea_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_OnePerArea_MarshalIsAreaLead_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void CanMarshalCompleteItem_OnePerCheckpoint_AreaLeadCanComplete_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.CanMarshalCompleteItem(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region DetermineCompletionContext Tests

    [TestMethod]
    public void DetermineCompletionContext_PersonalScope_ReturnsMarshalContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextPersonal);
        contextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void DetermineCompletionContext_EveryoneInAreas_ReturnsMarshalContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextPersonal);
        contextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void DetermineCompletionContext_OnePerCheckpoint_ReturnsCheckpointContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1, LocationId2] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        contextId.ShouldBe(LocationId1);
    }

    [TestMethod]
    public void DetermineCompletionContext_OnePerArea_ReturnsAreaContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1, AreaId2] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextArea);
        contextId.ShouldBe(AreaId1);
    }

    [TestMethod]
    public void DetermineCompletionContext_AreaLead_ReturnsAreaContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeAreaLead, ItemType = "Area", Ids = [AreaId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextArea);
        contextId.ShouldBe(AreaId1);
    }

    #endregion

    #region IsItemCompletedInContext Tests

    [TestMethod]
    public void IsItemCompletedInContext_PersonalScope_MarshalCompleted_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalId = MarshalId,
                IsDeleted = false
            }
        ];

        // Act
        bool result = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, completions);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemCompletedInContext_PersonalScope_OtherMarshalCompleted_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalId = "other-marshal",
                IsDeleted = false
            }
        ];

        // Act
        bool result = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, completions);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemCompletedInContext_PersonalScope_CompletionDeleted_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalId = MarshalId,
                IsDeleted = true
            }
        ];

        // Act
        bool result = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, completions);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemCompletedInContext_SharedScope_AnyoneCompleted_ReturnsTrue()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalId = "other-marshal",
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = LocationId1,
                IsDeleted = false
            }
        ];

        // Act
        bool result = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, completions);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemCompletedInContext_SharedScope_DifferentContext_ReturnsFalse()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = LocationId2, // Different checkpoint
                IsDeleted = false
            }
        ];

        // Act
        bool result = ChecklistScopeHelper.IsItemCompletedInContext(item, context, checkpointLookup, completions);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region GetCompletionDetails Tests

    [TestMethod]
    public void GetCompletionDetails_PersonalScope_Completed_ReturnsDetails()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        DateTime completedAt = DateTime.UtcNow;
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalId = MarshalId,
                CompletedByMarshalName = "John Doe",
                CompletedAt = completedAt,
                IsDeleted = false
            }
        ];

        // Act
        (string? completedByName, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        completedByName.ShouldBe("John Doe");
        completedAtResult.ShouldBe(completedAt);
    }

    [TestMethod]
    public void GetCompletionDetails_PersonalScope_NotCompleted_ReturnsNull()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions = [];

        // Act
        (string? completedByName, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        completedByName.ShouldBeNull();
        completedAtResult.ShouldBeNull();
    }

    [TestMethod]
    public void GetCompletionDetails_SharedScope_Completed_ReturnsDetails()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        DateTime completedAt = DateTime.UtcNow;
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                CompletedByMarshalName = "Jane Smith",
                CompletedAt = completedAt,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = LocationId1,
                IsDeleted = false
            }
        ];

        // Act
        (string? completedByName, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        completedByName.ShouldBe("Jane Smith");
        completedAtResult.ShouldBe(completedAt);
    }

    #endregion

    #region IsPersonalScope Tests

    [TestMethod]
    public void IsPersonalScope_EveryoneScope_ReturnsTrue()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeEveryone);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalScope_EveryoneInAreasScope_ReturnsTrue()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeEveryoneInAreas);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalScope_EveryoneAtCheckpointsScope_ReturnsTrue()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeEveryoneAtCheckpoints);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalScope_SpecificPeopleScope_ReturnsTrue()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeSpecificPeople);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalScope_OnePerCheckpointScope_ReturnsFalse()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeOnePerCheckpoint);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPersonalScope_OnePerAreaScope_ReturnsFalse()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeOnePerArea);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsPersonalScope_AreaLeadScope_ReturnsFalse()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeAreaLead);

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region Most Specific Wins Tests

    [TestMethod]
    public void EvaluateScopeConfigurations_MultipleMatches_ChoosesMostSpecific_MarshalWins()
    {
        // Arrange - Item visible to everyone in area1 AND specific marshal
        // Marshal ID (specificity=1) should win over Area (specificity=3)
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.WinningConfig.ShouldNotBeNull();
        result.WinningConfig.Scope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
        result.Specificity.ShouldBe(1); // Marshal ID is most specific
        result.ContextType.ShouldBe(Constants.ChecklistContextPersonal);
        result.ContextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MultipleMatches_ChoosesMostSpecific_CheckpointWinsOverArea()
    {
        // Arrange - Item visible to everyone at checkpoint1 AND everyone in area1
        // Checkpoint (specificity=2) should win over Area (specificity=3)
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId1] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1],
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.WinningConfig.ShouldNotBeNull();
        result.WinningConfig.Scope.ShouldBe(Constants.ChecklistScopeEveryoneAtCheckpoints);
        result.Specificity.ShouldBe(2); // Checkpoint is more specific than Area
        result.ContextType.ShouldBe(Constants.ChecklistContextPersonal);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MultipleMatches_SharedVsPersonal_MostSpecificDeterminesContext()
    {
        // Arrange - Item with OnePerCheckpoint AND EveryoneInAreas
        // Both match, but OnePerCheckpoint (specificity=2) wins over EveryoneInAreas (specificity=3)
        // This should result in Checkpoint context, not Personal
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1],
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(
            CreateChecklistItem(configs), context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextCheckpoint); // Shared context wins
        contextId.ShouldBe(LocationId1); // Should be checkpoint ID, not marshal ID
        matchedScope.ShouldBe(Constants.ChecklistScopeOnePerCheckpoint);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_ThreeMatches_ChoosesMostSpecific()
    {
        // Arrange - Item matches Everyone, EveryoneInAreas, AND SpecificPeople
        // SpecificPeople (specificity=1) should win
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(1); // Most specific wins
        result.WinningConfig!.Scope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_NoMatches_ReturnsNotRelevant()
    {
        // Arrange - Item only for area2, but marshal is in area1
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId2] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
        result.WinningConfig.ShouldBeNull();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MultipleMatchesSameSpecificity_TieBreaker()
    {
        // Arrange - Item for two different checkpoints, marshal assigned to both
        // Should use ThenBy(contextId) to break tie
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId2] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [LocationId1] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(2);
        // Should pick LocationId1 due to alphabetical tie-breaking
        result.ContextId.ShouldBe(LocationId1);
    }

    #endregion

    #region Complex Marshal Context Tests

    [TestMethod]
    public void IsItemRelevantToMarshal_MarshalIsAreaLeadAndAssigned_BothMatch()
    {
        // Arrange - Marshal is both area lead AND has checkpoint assignment in that area
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1],
            assignedLocationIds: [LocationId1],
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_MarshalInMultipleAreas_MatchesAnyArea()
    {
        // Arrange - Marshal assigned to checkpoints in both area1 and area2
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1, AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_MarshalNoAssignments_OnlyEveryoneScope()
    {
        // Arrange - Marshal with no assignments should only see Everyone items
        ChecklistItemEntity everyoneItem = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
        ]);
        ChecklistItemEntity areaItem = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [],
            assignedLocationIds: []);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool everyoneResult = ChecklistScopeHelper.IsItemRelevantToMarshal(everyoneItem, context, checkpointLookup);
        bool areaResult = ChecklistScopeHelper.IsItemRelevantToMarshal(areaItem, context, checkpointLookup);

        // Assert
        everyoneResult.ShouldBeTrue();
        areaResult.ShouldBeFalse();
    }

    #endregion
}
