using System.Text.Json;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for determining checklist item visibility and completion permissions
/// based on scope configurations and marshal context using "Most Specific Wins" logic
/// </summary>
public static class ChecklistScopeHelper
{
    /// <summary>
    /// Context information about a marshal for checklist filtering
    /// </summary>
    public record MarshalContext(
        string MarshalId,
        List<string> AssignedAreaIds,
        List<string> AssignedLocationIds,
        List<string> AreaLeadForAreaIds
    );

    /// <summary>
    /// Result of evaluating scope configurations against a marshal context
    /// </summary>
    public record ScopeMatchResult(
        bool IsRelevant,
        ScopeConfiguration? WinningConfig,
        int Specificity,
        string ContextType,
        string ContextId
    );

    /// <summary>
    /// Evaluates all scope configurations and returns the most specific match
    /// </summary>
    public static ScopeMatchResult EvaluateScopeConfigurations(
        List<ScopeConfiguration> configurations,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<(ScopeConfiguration config, int specificity, string contextId)> matches = [];

        foreach (ScopeConfiguration config in configurations)
        {
            var match = EvaluateSingleConfiguration(config, marshalContext, checkpointLookup);
            if (match.HasValue)
            {
                matches.Add(match.Value);
            }
        }

        if (!matches.Any())
        {
            return new ScopeMatchResult(false, null, int.MaxValue, string.Empty, string.Empty);
        }

        // Pick the most specific match (lowest specificity number wins)
        var winner = matches.OrderBy(m => m.specificity).ThenBy(m => m.contextId).First();
        string contextType = GetContextTypeForScope(winner.config.Scope);

        return new ScopeMatchResult(
            true,
            winner.config,
            winner.specificity,
            contextType,
            winner.contextId
        );
    }

    /// <summary>
    /// Evaluates a single scope configuration against marshal context
    /// Returns (config, specificity, contextId) if match, null otherwise
    /// </summary>
    private static (ScopeConfiguration config, int specificity, string contextId)? EvaluateSingleConfiguration(
        ScopeConfiguration config,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        if (config.ItemType == null || string.IsNullOrEmpty(config.ItemType))
        {
            // Everyone scope - no filters
            return (config, 4, marshalContext.MarshalId);
        }

        switch (config.ItemType)
        {
            case "Marshal":
                // Most specific - marshal ID explicitly listed
                if (config.Ids.Contains(marshalContext.MarshalId))
                {
                    return (config, 1, marshalContext.MarshalId);
                }
                break;

            case "Checkpoint":
                // Check if marshal is assigned to any of these checkpoints
                string? matchedCheckpoint = config.Ids.FirstOrDefault(id =>
                    marshalContext.AssignedLocationIds.Contains(id));

                if (matchedCheckpoint != null)
                {
                    return (config, 2, matchedCheckpoint);
                }

                // Also check if marshal is area lead for checkpoints in their areas
                if (config.Scope == Constants.ChecklistScopeOnePerCheckpoint ||
                    config.Scope == Constants.ChecklistScopeAreaLead)
                {
                    foreach (string checkpointId in config.Ids)
                    {
                        if (checkpointLookup.TryGetValue(checkpointId, out LocationEntity? checkpoint))
                        {
                            List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                            string? matchedArea = checkpointAreas.FirstOrDefault(areaId =>
                                marshalContext.AreaLeadForAreaIds.Contains(areaId));

                            if (matchedArea != null)
                            {
                                return (config, 2, checkpointId);
                            }
                        }
                    }
                }
                break;

            case "Area":
                // For AreaLead scope, ONLY area leads can see/complete items
                if (config.Scope == Constants.ChecklistScopeAreaLead)
                {
                    string? matchedLeadArea = config.Ids.FirstOrDefault(id =>
                        marshalContext.AreaLeadForAreaIds.Contains(id));

                    if (matchedLeadArea != null)
                    {
                        return (config, 3, matchedLeadArea);
                    }
                }
                else
                {
                    // For other scopes (EveryoneInAreas, OnePerArea), check if marshal is assigned to area
                    // (assignedAreaIds is pre-computed from marshal's checkpoint locations)
                    string? matchedAssignedArea = config.Ids.FirstOrDefault(areaId =>
                        marshalContext.AssignedAreaIds.Contains(areaId));

                    if (matchedAssignedArea != null)
                    {
                        return (config, 3, matchedAssignedArea);
                    }

                    // For OnePerArea scope, also check if marshal is area lead
                    if (config.Scope == Constants.ChecklistScopeOnePerArea)
                    {
                        string? matchedLeadArea = config.Ids.FirstOrDefault(id =>
                            marshalContext.AreaLeadForAreaIds.Contains(id));

                        if (matchedLeadArea != null)
                        {
                            return (config, 3, matchedLeadArea);
                        }
                    }
                }
                break;
        }

        return null; // No match
    }

    /// <summary>
    /// Determines if a checklist item is relevant/visible to a specific marshal
    /// </summary>
    public static bool IsItemRelevantToMarshal(
        ChecklistItemEntity item,
        MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson) ?? [];

        ScopeMatchResult result = EvaluateScopeConfigurations(configs, context, checkpointLookup);
        return result.IsRelevant;
    }

    /// <summary>
    /// Determines if a marshal can complete a checklist item
    /// </summary>
    public static bool CanMarshalCompleteItem(
        ChecklistItemEntity item,
        MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        // Same as IsItemRelevantToMarshal - if you can see it, you can complete it
        // (subject to completion context rules)
        return IsItemRelevantToMarshal(item, context, checkpointLookup);
    }

    /// <summary>
    /// Determines the completion context type and ID for a checklist item completion
    /// </summary>
    public static (string ContextType, string ContextId, string MatchedScope) DetermineCompletionContext(
        ChecklistItemEntity item,
        MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson) ?? [];

        ScopeMatchResult result = EvaluateScopeConfigurations(configs, context, checkpointLookup);

        if (!result.IsRelevant || result.WinningConfig == null)
        {
            return (Constants.ChecklistContextPersonal, context.MarshalId, string.Empty);
        }

        // For personal context types, the contextId should always be the marshal ID
        // For shared context types (OnePerCheckpoint, OnePerArea, AreaLead), use the matched item ID
        string contextId = IsPersonalContextType(result.ContextType) ? context.MarshalId : result.ContextId;

        return (result.ContextType, contextId, result.WinningConfig.Scope);
    }

    /// <summary>
    /// Checks if a checklist item has already been completed in the given context
    /// </summary>
    public static bool IsItemCompletedInContext(
        ChecklistItemEntity item,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup,
        List<ChecklistCompletionEntity> allCompletions)
    {
        (string contextType, string contextId, _) = DetermineCompletionContext(item, marshalContext, checkpointLookup);

        // For personal items, check if THIS marshal has completed it
        if (IsPersonalContextType(contextType))
        {
            return allCompletions.Any(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletedByMarshalId == marshalContext.MarshalId &&
                !c.IsDeleted);
        }

        // For shared items (OnePerCheckpoint, OnePerArea), check if ANYONE has completed it in this context
        return allCompletions.Any(c =>
            c.ChecklistItemId == item.ItemId &&
            c.CompletionContextType == contextType &&
            c.CompletionContextId == contextId &&
            !c.IsDeleted);
    }

    /// <summary>
    /// Gets completion details for a checklist item (who completed it and when)
    /// </summary>
    public static (string? CompletedByName, DateTime? CompletedAt) GetCompletionDetails(
        ChecklistItemEntity item,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup,
        List<ChecklistCompletionEntity> allCompletions)
    {
        (string contextType, string contextId, _) = DetermineCompletionContext(item, marshalContext, checkpointLookup);

        ChecklistCompletionEntity? completion;

        if (IsPersonalContextType(contextType))
        {
            completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletedByMarshalId == marshalContext.MarshalId &&
                !c.IsDeleted);
        }
        else
        {
            completion = allCompletions.FirstOrDefault(c =>
                c.ChecklistItemId == item.ItemId &&
                c.CompletionContextType == contextType &&
                c.CompletionContextId == contextId &&
                !c.IsDeleted);
        }

        return (completion?.CompletedByMarshalName, completion?.CompletedAt);
    }

    /// <summary>
    /// Determines the context type based on scope
    /// </summary>
    private static string GetContextTypeForScope(string scope)
    {
        return scope switch
        {
            Constants.ChecklistScopeEveryone or
            Constants.ChecklistScopeEveryoneInAreas or
            Constants.ChecklistScopeEveryoneAtCheckpoints or
            Constants.ChecklistScopeSpecificPeople =>
                Constants.ChecklistContextPersonal,

            Constants.ChecklistScopeOnePerCheckpoint =>
                Constants.ChecklistContextCheckpoint,

            Constants.ChecklistScopeOnePerArea or
            Constants.ChecklistScopeAreaLead =>
                Constants.ChecklistContextArea,

            _ => Constants.ChecklistContextPersonal
        };
    }

    /// <summary>
    /// Determines if a context type is personal (one per marshal) vs shared (one per area/checkpoint)
    /// </summary>
    private static bool IsPersonalContextType(string contextType)
    {
        return contextType == Constants.ChecklistContextPersonal;
    }

    /// <summary>
    /// Determines if a scope is personal (one per marshal) vs shared (one per area/checkpoint)
    /// </summary>
    public static bool IsPersonalScope(string scope)
    {
        return scope is
            Constants.ChecklistScopeEveryone or
            Constants.ChecklistScopeEveryoneInAreas or
            Constants.ChecklistScopeEveryoneAtCheckpoints or
            Constants.ChecklistScopeSpecificPeople;
    }
}
