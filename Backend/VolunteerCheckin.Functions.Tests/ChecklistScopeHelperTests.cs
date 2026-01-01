using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
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
        string? marshalId = null,
        List<string>? assignedAreaIds = null,
        List<string>? assignedLocationIds = null,
        List<string>? areaLeadForAreaIds = null)
    {
        return new ChecklistScopeHelper.MarshalContext(
            marshalId ?? MarshalId,
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [AreaId1] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [AreaId1] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [AreaId1] }]);
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = "other-marshal",
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
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
                ContextOwnerMarshalId = "other-marshal",
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
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        DateTime completedAt = DateTime.UtcNow;
        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                ContextOwnerMarshalName = "John Doe",
                ActorType = Constants.ActorTypeMarshal,
                ActorId = MarshalId,
                ActorName = "John Doe",
                CompletedAt = completedAt,
                IsDeleted = false
            }
        ];

        // Act
        (string? actorName, string? actorType, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        actorName.ShouldBe("John Doe");
        actorType.ShouldBe(Constants.ActorTypeMarshal);
        completedAtResult.ShouldBe(completedAt);
    }

    [TestMethod]
    public void GetCompletionDetails_PersonalScope_NotCompleted_ReturnsNull()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();
        List<ChecklistCompletionEntity> completions = [];

        // Act
        (string? actorName, string? actorType, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        actorName.ShouldBeNull();
        actorType.ShouldBeNull();
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
                ContextOwnerMarshalId = "other-marshal",
                ContextOwnerMarshalName = "Other Marshal",
                ActorType = Constants.ActorTypeMarshal,
                ActorId = "other-marshal",
                ActorName = "Jane Smith",
                CompletedAt = completedAt,
                CompletionContextType = Constants.ChecklistContextCheckpoint,
                CompletionContextId = LocationId1,
                IsDeleted = false
            }
        ];

        // Act
        (string? actorName, string? actorType, DateTime? completedAtResult) =
            ChecklistScopeHelper.GetCompletionDetails(item, context, checkpointLookup, completions);

        // Assert
        actorName.ShouldBe("Jane Smith");
        actorType.ShouldBe(Constants.ActorTypeMarshal);
        completedAtResult.ShouldBe(completedAt);
    }

    #endregion

    #region IsPersonalScope Tests

    [TestMethod]
    public void IsPersonalScope_EveryoneScope_ReturnsTrue()
    {
        // Act
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeSpecificPeople);

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
        bool result = ChecklistScopeHelper.IsPersonalScope(Constants.ChecklistScopeOneLeadPerArea);

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
        // Arrange - Item matches AllMarshals, EveryoneInAreas, AND SpecificPeople
        // SpecificPeople (specificity=1) should win
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] },
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
    public void EvaluateScopeConfigurations_EmptyConfigurations_ReturnsNotRelevant()
    {
        // Arrange - Empty configurations list
        // This tests line 52 in ChecklistScopeHelper.cs
        List<ScopeConfiguration> configs = [];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
        result.WinningConfig.ShouldBeNull();
        result.Specificity.ShouldBe(int.MaxValue);
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
    public void EvaluateScopeConfigurations_CheckpointNotInLookup_HandlesGracefully()
    {
        // Arrange - Config references checkpoint ID that doesn't exist in lookup
        // This tests defensive handling at lines 153, 188 in ChecklistScopeHelper.cs
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = ["nonexistent-checkpoint"] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]); // Area lead trying to access nonexistent checkpoint
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup(); // Doesn't contain "nonexistent-checkpoint"

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse(); // Should return not relevant, not crash
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_CheckpointWithNoAreas_NoAreaLeadMatch()
    {
        // Arrange - Checkpoint exists but has empty AreaIdsJson
        // This tests lines 123, 155, 191 in ChecklistScopeHelper.cs
        string checkpointWithNoAreas = "checkpoint-no-areas";
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [checkpointWithNoAreas] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [], // Not directly assigned
            areaLeadForAreaIds: [AreaId1]); // Area lead, but checkpoint has no areas

        // Create checkpoint lookup with a checkpoint that has no areas
        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [checkpointWithNoAreas] = new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = checkpointWithNoAreas,
                AreaIdsJson = "[]", // Empty areas
                Latitude = 0.0,
                Longitude = 0.0
            }
        };

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse(); // Area lead shouldn't match checkpoint with no areas
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MultipleMatchesSameSpecificity_TieBreaker()
    {
        // Arrange - Item for two different checkpoints with SHARED scope (OnePerCheckpoint),
        // marshal assigned to both. For shared scopes, contextId is the checkpoint ID.
        // Should use ThenBy(contextId) to break tie alphabetically.
        List<ScopeConfiguration> configs =
        [
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId2] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] }
        ];
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(configs, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(2);
        // Should pick LocationId1 ("location1") due to alphabetical tie-breaking
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
        // Arrange - Marshal with no assignments should only see AllMarshals items
        ChecklistItemEntity everyoneItem = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
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

    #region Sentinel Values Tests - ALL_CHECKPOINTS

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_EveryoneAtCheckpoints_MarshalAssigned_ReturnsTrue()
    {
        // Arrange - Item applies to ALL_CHECKPOINTS
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_MarshalNotAssigned_ReturnsFalse()
    {
        // Arrange - Item applies to ALL_CHECKPOINTS, but marshal has no assignments
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: []);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_OnePerCheckpoint_ReturnsTrue()
    {
        // Arrange - OnePerCheckpoint with ALL_CHECKPOINTS
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void DetermineCompletionContext_AllCheckpoints_OnePerCheckpoint_UsesFirstAssignedCheckpoint()
    {
        // Arrange - Should use first assigned checkpoint as context
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        contextId.ShouldBe(LocationId1); // Should use first assigned checkpoint
        matchedScope.ShouldBe(Constants.ChecklistScopeOnePerCheckpoint);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_AreaLeadCanSee_ReturnsTrue()
    {
        // Arrange - Area lead should see items for ALL_CHECKPOINTS (in their areas)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_OneLeadPerArea_AreaLeadCanSee_ReturnsTrue()
    {
        // Arrange - Area lead should see ALL_CHECKPOINTS items with OneLeadPerArea scope
        // This tests the OneLeadPerArea branch (line 115 in ChecklistScopeHelper.cs)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [], // Not directly assigned to any checkpoint
            areaLeadForAreaIds: [AreaId1]); // But is area lead for Area1
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue(); // Area lead should see it
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllCheckpoints_MixedWithSpecific_BothWork()
    {
        // Arrange - Can mix ALL_CHECKPOINTS with specific checkpoint IDs
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints, LocationId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region Sentinel Values Tests - ALL_AREAS

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_EveryoneInAreas_MarshalInArea_ReturnsTrue()
    {
        // Arrange - Item applies to ALL_AREAS
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_MarshalNotInAnyArea_ReturnsFalse()
    {
        // Arrange - Item applies to ALL_AREAS, but marshal has no area assignments
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: []);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_OnePerArea_ReturnsTrue()
    {
        // Arrange - OnePerArea with ALL_AREAS
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [Constants.AllAreas] }
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
    public void DetermineCompletionContext_AllAreas_OnePerArea_UsesFirstAssignedArea()
    {
        // Arrange - Should use first assigned area as context
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1, AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextArea);
        contextId.ShouldBe(AreaId1); // Should use first assigned area
        matchedScope.ShouldBe(Constants.ChecklistScopeOnePerArea);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_AreaLeadScope_OnlyAreaLeadsCanSee()
    {
        // Arrange - AreaLead scope with ALL_AREAS - only area leads should see it
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);

        // Marshal assigned to area but not area lead
        ChecklistScopeHelper.MarshalContext assignedContext = CreateMarshalContext(
            assignedAreaIds: [AreaId1],
            areaLeadForAreaIds: []);

        // Marshal who is area lead
        ChecklistScopeHelper.MarshalContext leadContext = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);

        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool assignedResult = ChecklistScopeHelper.IsItemRelevantToMarshal(item, assignedContext, checkpointLookup);
        bool leadResult = ChecklistScopeHelper.IsItemRelevantToMarshal(item, leadContext, checkpointLookup);

        // Assert
        assignedResult.ShouldBeFalse(); // Regular marshal can't see AreaLead items
        leadResult.ShouldBeTrue(); // Area lead can see it
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_OnePerArea_AreaLeadCanAlsoComplete()
    {
        // Arrange - OnePerArea with ALL_AREAS - area lead can also complete
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [], // Not assigned to any area
            areaLeadForAreaIds: [AreaId1]); // But is area lead
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllAreas_MixedWithSpecific_BothWork()
    {
        // Arrange - Can mix ALL_AREAS with specific area IDs
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [Constants.AllAreas, AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    #endregion

    #region Sentinel Values Tests - ALL_MARSHALS

    [TestMethod]
    public void IsItemRelevantToMarshal_AllMarshals_SpecificPeople_AnyMarshal_ReturnsTrue()
    {
        // Arrange - Item applies to ALL_MARSHALS
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void DetermineCompletionContext_AllMarshals_ReturnsPersonalContext()
    {
        // Arrange - ALL_MARSHALS should give Personal context with marshal ID
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextPersonal);
        contextId.ShouldBe(MarshalId);
        matchedScope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_AllMarshals_WinsOverCheckpoint()
    {
        // Arrange - ALL_MARSHALS (specificity 1) should win over Checkpoint (specificity 2)
        // This forces Personal context even when marshal matches a checkpoint
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert - Should use Marshal config (most specific) giving Personal context
        contextType.ShouldBe(Constants.ChecklistContextPersonal);
        contextId.ShouldBe(MarshalId);
        matchedScope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_AllMarshals_WinsOverArea()
    {
        // Arrange - ALL_MARSHALS (specificity 1) should win over Area (specificity 3)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert - Should use Marshal config (most specific)
        contextType.ShouldBe(Constants.ChecklistContextPersonal);
        contextId.ShouldBe(MarshalId);
        matchedScope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_AllMarshals_MixedWithSpecific_BothWork()
    {
        // Arrange - Can mix ALL_MARSHALS with specific marshal IDs
        string otherMarshalId = "other-marshal";
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals, otherMarshalId] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_AllMarshals_VsEveryone_MarshalWins()
    {
        // Arrange - ALL_MARSHALS (specificity 1) - test that it works correctly
        // Both give Personal context, but specificity matters for the algorithm
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(
            [
                new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
            ],
            context,
            checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(1); // Marshal specificity
        result.WinningConfig!.Scope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
    }

    #endregion

    #region OnePerCheckpoint Filtered By Areas Tests

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_FilteredByArea_MarshalInArea_ReturnsTrue()
    {
        // Arrange - OnePerCheckpoint scope filtered by area
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]); // LocationId1 is in AreaId1
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_FilteredByArea_MarshalNotInArea_ReturnsFalse()
    {
        // Arrange - OnePerCheckpoint scope filtered by area2, but marshal at checkpoint in area1
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]); // LocationId1 is in AreaId1, not AreaId2
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void DetermineCompletionContext_OnePerCheckpoint_FilteredByArea_ReturnsCheckpointContext()
    {
        // Arrange - Should use Checkpoint context, not Area context
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextCheckpoint); // Not Area!
        contextId.ShouldBe(LocationId1); // Should be checkpoint ID, not area ID
        matchedScope.ShouldBe(Constants.ChecklistScopeOnePerCheckpoint);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_FilteredByMultipleAreas_MatchesAny()
    {
        // Arrange - Marshal at checkpoint in area1, item applies to area1 and area2
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1, AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]); // LocationId1 is in AreaId1
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_FilteredByArea_MarshalAtMultipleCheckpoints()
    {
        // Arrange - Marshal at checkpoints in both area1 and area2, item only for area1
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]); // LocationId1 in AreaId1, LocationId2 in AreaId2
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Get the context to verify it matches the correct checkpoint
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
        contextId.ShouldBe(LocationId1); // Should match first checkpoint in area1, not LocationId2
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_OnePerCheckpoint_AllAreas_ReturnsTrue()
    {
        // Arrange - OnePerCheckpoint with ALL_AREAS sentinel
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OnePerCheckpoint_FilteredByArea_CorrectSpecificity()
    {
        // Arrange - OnePerCheckpoint filtered by Area should have checkpoint specificity (2)
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(
            [new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1] }],
            context,
            checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(2); // Checkpoint specificity, not Area specificity (3)
        result.ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
    }

    #endregion

    #region EveryAreaLead Scope Tests

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryAreaLead_AreaLead_ReturnsTrue()
    {
        // Arrange - EveryAreaLead scope, marshal is area lead
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryAreaLead_NotAreaLead_ReturnsFalse()
    {
        // Arrange - EveryAreaLead scope, but marshal is NOT area lead
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1], // Assigned to area but not lead
            areaLeadForAreaIds: []);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeFalse(); // Only area leads should see it
    }

    [TestMethod]
    public void DetermineCompletionContext_EveryAreaLead_ReturnsPersonalContext()
    {
        // Arrange - EveryAreaLead should give Personal context (per lead)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string contextType, string contextId, string matchedScope) = ChecklistScopeHelper.DetermineCompletionContext(item, context, checkpointLookup);

        // Assert
        contextType.ShouldBe(Constants.ChecklistContextPersonal); // Personal, not Area!
        contextId.ShouldBe(MarshalId); // Should be marshal ID, not area ID
        matchedScope.ShouldBe(Constants.ChecklistScopeEveryAreaLead);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryAreaLead_MultipleAreaLeads_EachSeesIt()
    {
        // Arrange - Two leads for the same area should each see it separately
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext lead1Context = CreateMarshalContext(
            marshalId: "lead1",
            areaLeadForAreaIds: [AreaId1]);
        ChecklistScopeHelper.MarshalContext lead2Context = CreateMarshalContext(
            marshalId: "lead2",
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool lead1Result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, lead1Context, checkpointLookup);
        bool lead2Result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, lead2Context, checkpointLookup);

        (string lead1ContextType, string lead1ContextId, _) = ChecklistScopeHelper.DetermineCompletionContext(item, lead1Context, checkpointLookup);
        (string lead2ContextType, string lead2ContextId, _) = ChecklistScopeHelper.DetermineCompletionContext(item, lead2Context, checkpointLookup);

        // Assert
        lead1Result.ShouldBeTrue();
        lead2Result.ShouldBeTrue();
        // Each should have their own personal context
        lead1ContextType.ShouldBe(Constants.ChecklistContextPersonal);
        lead2ContextType.ShouldBe(Constants.ChecklistContextPersonal);
        lead1ContextId.ShouldBe("lead1");
        lead2ContextId.ShouldBe("lead2");
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryAreaLead_AllAreas_ReturnsTrue()
    {
        // Arrange - EveryAreaLead with ALL_AREAS sentinel
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [Constants.AllAreas] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        bool result = ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup);

        // Assert
        result.ShouldBeTrue();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_EveryAreaLead_CorrectSpecificity()
    {
        // Arrange - EveryAreaLead should have area specificity (3)
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        ChecklistScopeHelper.ScopeMatchResult result = ChecklistScopeHelper.EvaluateScopeConfigurations(
            [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }],
            context,
            checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(3); // Area-level specificity
        result.ContextType.ShouldBe(Constants.ChecklistContextPersonal);
    }

    [TestMethod]
    public void IsItemRelevantToMarshal_EveryAreaLead_VsAreaLead_DifferentCompletion()
    {
        // Arrange - Compare EveryAreaLead (personal) vs AreaLead (shared)
        ChecklistItemEntity everyAreaLeadItem = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryAreaLead, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistItemEntity areaLeadItem = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        (string everyContext, string everyContextId, _) = ChecklistScopeHelper.DetermineCompletionContext(everyAreaLeadItem, context, checkpointLookup);
        (string areaContext, string areaContextId, _) = ChecklistScopeHelper.DetermineCompletionContext(areaLeadItem, context, checkpointLookup);

        // Assert - Different completion contexts!
        everyContext.ShouldBe(Constants.ChecklistContextPersonal);
        everyContextId.ShouldBe(MarshalId); // Personal - each lead completes individually

        areaContext.ShouldBe(Constants.ChecklistContextArea);
        areaContextId.ShouldBe(AreaId1); // Shared - one completion per area
    }

    #endregion

    #region GetAllRelevantContexts Tests

    [TestMethod]
    public void GetAllRelevantContexts_PersonalScope_ReturnsSingleContext()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(1);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextPersonal);
        results[0].ContextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OnePerCheckpoint_MarshalAtTwoCheckpoints_ReturnsTwoContexts()
    {
        // Arrange - This is Amy's scenario!
        string checkpoint17 = "checkpoint17";
        string checkpoint18 = "checkpoint18";
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [checkpoint17, checkpoint18] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [checkpoint17, checkpoint18]);
        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [checkpoint17] = new LocationEntity { PartitionKey = EventId, RowKey = checkpoint17, AreaIdsJson = "[]", Latitude = 0.0, Longitude = 0.0 },
            [checkpoint18] = new LocationEntity { PartitionKey = EventId, RowKey = checkpoint18, AreaIdsJson = "[]", Latitude = 0.0, Longitude = 0.0 }
        };

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert - Should see the item TWICE, once for each checkpoint!
        results.Count.ShouldBe(2);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[0].ContextId.ShouldBe(checkpoint17);
        results[1].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[1].ContextId.ShouldBe(checkpoint18);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OnePerCheckpoint_AllCheckpoints_ReturnsTwoContexts()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [Constants.AllCheckpoints] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(2);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[0].ContextId.ShouldBe(LocationId1);
        results[1].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[1].ContextId.ShouldBe(LocationId2);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OnePerArea_MarshalInTwoAreas_ReturnsTwoContexts()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId1, AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1, AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(2);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextArea);
        results[0].ContextId.ShouldBe(AreaId1);
        results[1].ContextType.ShouldBe(Constants.ChecklistContextArea);
        results[1].ContextId.ShouldBe(AreaId2);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OneLeadPerArea_LeadOfTwoAreas_ReturnsTwoContexts()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOneLeadPerArea, ItemType = "Area", Ids = [AreaId1, AreaId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            areaLeadForAreaIds: [AreaId1, AreaId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(2);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextArea);
        results[0].ContextId.ShouldBe(AreaId1);
        results[1].ContextType.ShouldBe(Constants.ChecklistContextArea);
        results[1].ContextId.ShouldBe(AreaId2);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OnePerCheckpoint_AreaLeadAccessMultipleCheckpoints_ReturnsMultipleContexts()
    {
        // Arrange - Area lead can access checkpoints in their area
        string checkpoint1InArea1 = "checkpoint1";
        string checkpoint2InArea1 = "checkpoint2";
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [checkpoint1InArea1, checkpoint2InArea1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [],
            areaLeadForAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [checkpoint1InArea1] = new LocationEntity { PartitionKey = EventId, RowKey = checkpoint1InArea1, AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId1 }), Latitude = 0.0, Longitude = 0.0 },
            [checkpoint2InArea1] = new LocationEntity { PartitionKey = EventId, RowKey = checkpoint2InArea1, AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaId1 }), Latitude = 0.0, Longitude = 0.0 }
        };

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert - Area lead sees all checkpoints in their area
        results.Count.ShouldBe(2);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[0].ContextId.ShouldBe(checkpoint1InArea1);
        results[1].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[1].ContextId.ShouldBe(checkpoint2InArea1);
    }

    [TestMethod]
    public void GetAllRelevantContexts_MarshalNotRelevant_ReturnsEmptyList()
    {
        // Arrange
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = ["other-marshal"] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext();
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(0);
    }

    [TestMethod]
    public void GetAllRelevantContexts_OnePerCheckpoint_FilteredByArea_MultipleCheckpointsInArea()
    {
        // Arrange - OnePerCheckpoint scope filtered by areas
        // LocationId1 is in AreaId1, LocationId2 is in AreaId2 (per CreateCheckpointLookup)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Area", Ids = [AreaId1] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedAreaIds: [AreaId1], // Must be assigned to the area
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert - Should return only LocationId1 (in AreaId1), not LocationId2 (in AreaId2)
        results.Count.ShouldBe(1);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextCheckpoint);
        results[0].ContextId.ShouldBe(LocationId1);
    }

    [TestMethod]
    public void GetAllRelevantContexts_MostSpecificWins_PersonalOverCheckpoint()
    {
        // Arrange - Multiple configs, personal (specificity 1) should win over checkpoint (specificity 2)
        ChecklistItemEntity item = CreateChecklistItem([
            new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [MarshalId] },
            new ScopeConfiguration { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId1, LocationId2] }
        ]);
        ChecklistScopeHelper.MarshalContext context = CreateMarshalContext(
            assignedLocationIds: [LocationId1, LocationId2]);
        Dictionary<string, LocationEntity> checkpointLookup = CreateCheckpointLookup();

        // Act
        List<ChecklistScopeHelper.ScopeMatchResult> results = ChecklistScopeHelper.GetAllRelevantContexts(item, context, checkpointLookup);

        // Assert - Personal wins, so only 1 context (not 2 for checkpoints)
        results.Count.ShouldBe(1);
        results[0].ContextType.ShouldBe(Constants.ChecklistContextPersonal);
        results[0].ContextId.ShouldBe(MarshalId);
    }

    #endregion
}
