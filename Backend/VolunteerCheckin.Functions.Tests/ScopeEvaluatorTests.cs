using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for ScopeEvaluator - the core "Most Specific Wins" algorithm for scope-based access control.
/// </summary>
[TestClass]
public class ScopeEvaluatorTests
{
    private const string MarshalId = "marshal-123";
    private const string MarshalId2 = "marshal-456";
    private const string CheckpointId1 = "checkpoint-1";
    private const string CheckpointId2 = "checkpoint-2";
    private const string CheckpointId3 = "checkpoint-3";
    private const string AreaId1 = "area-1";
    private const string AreaId2 = "area-2";
    private const string AreaId3 = "area-3";

    private static LocationEntity CreateCheckpoint(string checkpointId, params string[] areaIds)
    {
        return new LocationEntity
        {
            RowKey = checkpointId,
            PartitionKey = "event-123",
            Name = $"Checkpoint {checkpointId}",
            AreaIdsJson = System.Text.Json.JsonSerializer.Serialize(areaIds.ToList())
        };
    }

    private static ScopeEvaluator.MarshalContext CreateMarshalContext(
        string marshalId,
        List<string>? assignedAreaIds = null,
        List<string>? assignedLocationIds = null,
        List<string>? areaLeadForAreaIds = null)
    {
        return new ScopeEvaluator.MarshalContext(
            marshalId,
            assignedAreaIds ?? [],
            assignedLocationIds ?? [],
            areaLeadForAreaIds ?? []
        );
    }

    #region EvaluateScopeConfigurations Tests

    [TestMethod]
    public void EvaluateScopeConfigurations_NoConfigurations_ReturnsNotRelevant()
    {
        // Arrange
        List<ScopeConfiguration> configurations = [];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
        result.WinningConfig.ShouldBeNull();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.NoMatch);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MarshalScope_SpecificMarshal_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [MarshalId]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Marshal);
        result.ContextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MarshalScope_AllMarshals_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [Constants.AllMarshals]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Marshal);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MarshalScope_DifferentMarshal_DoesNotMatch()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [MarshalId2]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_CheckpointScope_AssignedCheckpoint_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Checkpoint);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_CheckpointScope_AllCheckpoints_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = [Constants.AllCheckpoints]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Checkpoint);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_CheckpointScope_NotAssigned_DoesNotMatch()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId2]); // Different checkpoint
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_AreaScope_AssignedArea_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneInAreas,
                ItemType = "Area",
                Ids = [AreaId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Area);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_AreaScope_AllAreas_Matches()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneInAreas,
                ItemType = "Area",
                Ids = [Constants.AllAreas]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Area);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MostSpecificWins_MarshalOverCheckpoint()
    {
        // Arrange - Both marshal and checkpoint scopes match, marshal should win
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            },
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [MarshalId]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Marshal);
        result.WinningConfig!.ItemType.ShouldBe("Marshal");
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_MostSpecificWins_CheckpointOverArea()
    {
        // Arrange - Both checkpoint and area scopes match, checkpoint should win
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneInAreas,
                ItemType = "Area",
                Ids = [AreaId1]
            },
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1],
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.Specificity.ShouldBe(ScopeEvaluator.SpecificityLevel.Checkpoint);
        result.WinningConfig!.ItemType.ShouldBe("Checkpoint");
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OnePerCheckpoint_AreaLead_CanAccessCheckpointsInArea()
    {
        // Arrange - Area lead should be able to access checkpoints in their area
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOnePerCheckpoint,
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            areaLeadForAreaIds: [AreaId1]); // Area lead but not directly assigned to checkpoint

        LocationEntity checkpoint = CreateCheckpoint(CheckpointId1, AreaId1);
        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [CheckpointId1] = checkpoint
        };

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(CheckpointId1);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OneLeadPerArea_OnlyAreaLeadsMatch()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOneLeadPerArea,
                ItemType = "Area",
                Ids = [AreaId1]
            }
        ];

        // Regular marshal in area (not area lead)
        ScopeEvaluator.MarshalContext regularMarshal = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1]);

        // Area lead
        ScopeEvaluator.MarshalContext areaLead = CreateMarshalContext(
            MarshalId2,
            areaLeadForAreaIds: [AreaId1]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult regularResult = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, regularMarshal, checkpointLookup);
        ScopeEvaluator.ScopeMatchResult leadResult = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, areaLead, checkpointLookup);

        // Assert
        regularResult.IsRelevant.ShouldBeFalse(); // Regular marshal doesn't match
        leadResult.IsRelevant.ShouldBeTrue(); // Area lead matches
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_EveryAreaLead_PersonalContext()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryAreaLead,
                ItemType = "Area",
                Ids = [AreaId1]
            }
        ];

        ScopeEvaluator.MarshalContext areaLead = CreateMarshalContext(
            MarshalId,
            areaLeadForAreaIds: [AreaId1]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, areaLead, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(MarshalId); // Personal context uses marshal ID
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OnePerArea_AreaLeadCanComplete()
    {
        // Arrange - OnePerArea allows both assigned marshals AND area leads
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOnePerArea,
                ItemType = "Area",
                Ids = [AreaId1]
            }
        ];

        ScopeEvaluator.MarshalContext areaLead = CreateMarshalContext(
            MarshalId,
            areaLeadForAreaIds: [AreaId1]); // Only area lead, not assigned to area

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, areaLead, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(AreaId1); // Shared context uses area ID
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OnePerCheckpoint_FilteredByAreas()
    {
        // Arrange - OnePerCheckpoint scoped to specific areas
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOnePerCheckpoint,
                ItemType = "Area",
                Ids = [AreaId1] // Filter to checkpoints in Area 1
            }
        ];

        LocationEntity checkpoint1 = CreateCheckpoint(CheckpointId1, AreaId1);
        LocationEntity checkpoint2 = CreateCheckpoint(CheckpointId2, AreaId2);

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1],
            assignedLocationIds: [CheckpointId1]);

        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [CheckpointId1] = checkpoint1,
            [CheckpointId2] = checkpoint2
        };

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(CheckpointId1);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_OnePerCheckpoint_FilteredByAllAreas()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOnePerCheckpoint,
                ItemType = "Area",
                Ids = [Constants.AllAreas]
            }
        ];

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
    }

    #endregion

    #region GetContextTypeForScope Tests

    [TestMethod]
    public void GetContextTypeForScope_PersonalScopes_ReturnPersonal()
    {
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeEveryoneInAreas)
            .ShouldBe(Constants.ChecklistContextPersonal);
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeEveryoneAtCheckpoints)
            .ShouldBe(Constants.ChecklistContextPersonal);
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeSpecificPeople)
            .ShouldBe(Constants.ChecklistContextPersonal);
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeEveryAreaLead)
            .ShouldBe(Constants.ChecklistContextPersonal);
    }

    [TestMethod]
    public void GetContextTypeForScope_CheckpointScope_ReturnCheckpoint()
    {
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeOnePerCheckpoint)
            .ShouldBe(Constants.ChecklistContextCheckpoint);
    }

    [TestMethod]
    public void GetContextTypeForScope_AreaScopes_ReturnArea()
    {
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeOnePerArea)
            .ShouldBe(Constants.ChecklistContextArea);
        ScopeEvaluator.GetContextTypeForScope(Constants.ChecklistScopeOneLeadPerArea)
            .ShouldBe(Constants.ChecklistContextArea);
    }

    [TestMethod]
    public void GetContextTypeForScope_UnknownScope_ReturnsPersonal()
    {
        ScopeEvaluator.GetContextTypeForScope("UnknownScope")
            .ShouldBe(Constants.ChecklistContextPersonal);
    }

    #endregion

    #region IsPersonalContextType Tests

    [TestMethod]
    public void IsPersonalContextType_Personal_ReturnsTrue()
    {
        ScopeEvaluator.IsPersonalContextType(Constants.ChecklistContextPersonal).ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalContextType_Checkpoint_ReturnsFalse()
    {
        ScopeEvaluator.IsPersonalContextType(Constants.ChecklistContextCheckpoint).ShouldBeFalse();
    }

    [TestMethod]
    public void IsPersonalContextType_Area_ReturnsFalse()
    {
        ScopeEvaluator.IsPersonalContextType(Constants.ChecklistContextArea).ShouldBeFalse();
    }

    #endregion

    #region IsPersonalScope Tests

    [TestMethod]
    public void IsPersonalScope_PersonalScopes_ReturnsTrue()
    {
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeEveryoneInAreas).ShouldBeTrue();
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeEveryoneAtCheckpoints).ShouldBeTrue();
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeSpecificPeople).ShouldBeTrue();
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeEveryAreaLead).ShouldBeTrue();
    }

    [TestMethod]
    public void IsPersonalScope_SharedScopes_ReturnsFalse()
    {
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeOnePerCheckpoint).ShouldBeFalse();
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeOnePerArea).ShouldBeFalse();
        ScopeEvaluator.IsPersonalScope(Constants.ChecklistScopeOneLeadPerArea).ShouldBeFalse();
    }

    #endregion

    #region GetAllCheckpointContexts Tests

    [TestMethod]
    public void GetAllCheckpointContexts_DirectAssignment_ReturnsAllMatches()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerCheckpoint,
            ItemType = "Checkpoint",
            Ids = [CheckpointId1, CheckpointId2, CheckpointId3]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1, CheckpointId2]); // Assigned to 2 of 3

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllCheckpointContexts(
            config, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(r => r.ContextId == CheckpointId1);
        results.ShouldContain(r => r.ContextId == CheckpointId2);
    }

    [TestMethod]
    public void GetAllCheckpointContexts_AllCheckpoints_ReturnsAllAssigned()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerCheckpoint,
            ItemType = "Checkpoint",
            Ids = [Constants.AllCheckpoints]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1, CheckpointId2, CheckpointId3]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllCheckpointContexts(
            config, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(3);
    }

    [TestMethod]
    public void GetAllCheckpointContexts_AreaLeadAccess_IncludesCheckpointsInArea()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerCheckpoint,
            ItemType = "Checkpoint",
            Ids = [CheckpointId1, CheckpointId2]
        };

        LocationEntity checkpoint1 = CreateCheckpoint(CheckpointId1, AreaId1);
        LocationEntity checkpoint2 = CreateCheckpoint(CheckpointId2, AreaId2);

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            areaLeadForAreaIds: [AreaId1]); // Area lead for Area 1 only

        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [CheckpointId1] = checkpoint1,
            [CheckpointId2] = checkpoint2
        };

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllCheckpointContexts(
            config, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(1);
        results[0].ContextId.ShouldBe(CheckpointId1);
    }

    [TestMethod]
    public void GetAllCheckpointContexts_AreaFiltered_ReturnsCheckpointsInMatchingAreas()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerCheckpoint,
            ItemType = "Area",
            Ids = [AreaId1]
        };

        LocationEntity checkpoint1 = CreateCheckpoint(CheckpointId1, AreaId1);
        LocationEntity checkpoint2 = CreateCheckpoint(CheckpointId2, AreaId2);

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1, AreaId2],
            assignedLocationIds: [CheckpointId1, CheckpointId2]);

        Dictionary<string, LocationEntity> checkpointLookup = new()
        {
            [CheckpointId1] = checkpoint1,
            [CheckpointId2] = checkpoint2
        };

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllCheckpointContexts(
            config, context, checkpointLookup);

        // Assert
        results.Count.ShouldBe(1);
        results[0].ContextId.ShouldBe(CheckpointId1);
    }

    #endregion

    #region GetAllAreaContexts Tests

    [TestMethod]
    public void GetAllAreaContexts_OnePerArea_ReturnsAllAssignedAreas()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerArea,
            ItemType = "Area",
            Ids = [AreaId1, AreaId2, AreaId3]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1, AreaId2]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllAreaContexts(
            config, context, checkpointLookup, leadOnly: false);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(r => r.ContextId == AreaId1);
        results.ShouldContain(r => r.ContextId == AreaId2);
    }

    [TestMethod]
    public void GetAllAreaContexts_OnePerArea_IncludesAreaLeadAreas()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerArea,
            ItemType = "Area",
            Ids = [AreaId1, AreaId2]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1],
            areaLeadForAreaIds: [AreaId2]); // Lead for area 2 but not assigned

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllAreaContexts(
            config, context, checkpointLookup, leadOnly: false);

        // Assert
        results.Count.ShouldBe(2); // Both assigned area and lead area
    }

    [TestMethod]
    public void GetAllAreaContexts_OneLeadPerArea_OnlyReturnsLeadAreas()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOneLeadPerArea,
            ItemType = "Area",
            Ids = [AreaId1, AreaId2]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1, AreaId2],
            areaLeadForAreaIds: [AreaId1]); // Only lead for area 1

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllAreaContexts(
            config, context, checkpointLookup, leadOnly: true);

        // Assert
        results.Count.ShouldBe(1);
        results[0].ContextId.ShouldBe(AreaId1);
    }

    [TestMethod]
    public void GetAllAreaContexts_AllAreas_ReturnsAllMatchingAreas()
    {
        // Arrange
        ScopeConfiguration config = new ScopeConfiguration
        {
            Scope = Constants.ChecklistScopeOnePerArea,
            ItemType = "Area",
            Ids = [Constants.AllAreas]
        };

        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedAreaIds: [AreaId1, AreaId2, AreaId3]);

        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        List<ScopeEvaluator.ScopeMatchResult> results = ScopeEvaluator.GetAllAreaContexts(
            config, context, checkpointLookup, leadOnly: false);

        // Assert
        results.Count.ShouldBe(3);
    }

    #endregion

    #region SpecificityLevel Tests

    [TestMethod]
    public void SpecificityLevel_MarshalIsHighestPriority()
    {
        ScopeEvaluator.SpecificityLevel.Marshal.ShouldBeLessThan(ScopeEvaluator.SpecificityLevel.Checkpoint);
        ScopeEvaluator.SpecificityLevel.Marshal.ShouldBeLessThan(ScopeEvaluator.SpecificityLevel.Area);
    }

    [TestMethod]
    public void SpecificityLevel_CheckpointHigherThanArea()
    {
        ScopeEvaluator.SpecificityLevel.Checkpoint.ShouldBeLessThan(ScopeEvaluator.SpecificityLevel.Area);
    }

    [TestMethod]
    public void SpecificityLevel_NoMatchIsLowest()
    {
        ScopeEvaluator.SpecificityLevel.NoMatch.ShouldBeGreaterThan(ScopeEvaluator.SpecificityLevel.Marshal);
        ScopeEvaluator.SpecificityLevel.NoMatch.ShouldBeGreaterThan(ScopeEvaluator.SpecificityLevel.Checkpoint);
        ScopeEvaluator.SpecificityLevel.NoMatch.ShouldBeGreaterThan(ScopeEvaluator.SpecificityLevel.Area);
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void EvaluateScopeConfigurations_EmptyIds_DoesNotMatch()
    {
        // Arrange
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [] // Empty list
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeFalse();
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_TieBreaker_UsesContextIdForConsistency()
    {
        // Arrange - Two marshal configs with same specificity
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeSpecificPeople,
                ItemType = "Marshal",
                Ids = [MarshalId, MarshalId2]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(MarshalId);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(MarshalId);
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_PersonalScope_CheckpointType_UseMarshalIdAsContext()
    {
        // Arrange - Personal scope (EveryoneAtCheckpoints) should use marshal ID as context
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints, // Personal scope
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(MarshalId); // Personal context
    }

    [TestMethod]
    public void EvaluateScopeConfigurations_SharedScope_CheckpointType_UseCheckpointIdAsContext()
    {
        // Arrange - Shared scope (OnePerCheckpoint) should use checkpoint ID as context
        List<ScopeConfiguration> configurations =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeOnePerCheckpoint, // Shared scope
                ItemType = "Checkpoint",
                Ids = [CheckpointId1]
            }
        ];
        ScopeEvaluator.MarshalContext context = CreateMarshalContext(
            MarshalId,
            assignedLocationIds: [CheckpointId1]);
        Dictionary<string, LocationEntity> checkpointLookup = [];

        // Act
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations, context, checkpointLookup);

        // Assert
        result.IsRelevant.ShouldBeTrue();
        result.ContextId.ShouldBe(CheckpointId1); // Shared context
    }

    #endregion
}
