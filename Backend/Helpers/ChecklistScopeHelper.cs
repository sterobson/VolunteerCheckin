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
    /// Specificity levels for "Most Specific Wins" algorithm.
    /// Lower number = higher priority (more specific).
    /// </summary>
    private static class Specificity
    {
        public const int Marshal = 1;      // Most specific - explicit marshal ID
        public const int Checkpoint = 2;   // Checkpoint assignment
        public const int Area = 3;         // Area assignment
        public const int NoMatch = int.MaxValue; // No match found
    }

    /// <summary>
    /// Finds a matching checkpoint ID from the target list that the marshal is assigned to.
    /// Supports ALL_CHECKPOINTS sentinel value.
    /// </summary>
    private static string? FindMatchingCheckpoint(List<string> targetCheckpointIds, MarshalContext marshalContext)
    {
        if (targetCheckpointIds.Contains(Constants.AllCheckpoints))
        {
            return marshalContext.AssignedLocationIds.FirstOrDefault();
        }

        return targetCheckpointIds.FirstOrDefault(id => marshalContext.AssignedLocationIds.Contains(id));
    }

    /// <summary>
    /// Checks if an area lead can access checkpoint items based on checkpoint locations in their areas.
    /// Returns the first matching checkpoint ID, or null if no match.
    /// </summary>
    private static string? CheckAreaLeadCheckpointAccess(
        List<string> targetCheckpointIds,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        if (!marshalContext.AreaLeadForAreaIds.Any())
        {
            return null;
        }

        // Handle ALL_CHECKPOINTS sentinel
        if (targetCheckpointIds.Contains(Constants.AllCheckpoints))
        {
            return checkpointLookup.Values
                .Where(checkpoint =>
                {
                    List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                    return checkpointAreas.Any(areaId => marshalContext.AreaLeadForAreaIds.Contains(areaId));
                })
                .Select(checkpoint => checkpoint.RowKey)
                .FirstOrDefault();
        }

        // Check specific checkpoint IDs
        foreach (string checkpointId in targetCheckpointIds)
        {
            if (checkpointLookup.TryGetValue(checkpointId, out LocationEntity? checkpoint))
            {
                List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                if (checkpointAreas.Any(areaId => marshalContext.AreaLeadForAreaIds.Contains(areaId)))
                {
                    return checkpointId;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a matching area ID from the target list.
    /// Supports ALL_AREAS sentinel value.
    /// </summary>
    private static string? FindMatchingArea(List<string> targetAreaIds, List<string> marshalAreaIds)
    {
        if (targetAreaIds.Contains(Constants.AllAreas))
        {
            return marshalAreaIds.FirstOrDefault();
        }

        return targetAreaIds.FirstOrDefault(id => marshalAreaIds.Contains(id));
    }

    /// <summary>
    /// Evaluates area scope configuration and returns match result based on scope type.
    /// Handles OneLeadPerArea, EveryAreaLead, and standard area scopes.
    /// </summary>
    private static (ScopeConfiguration config, int specificity, string contextId)? EvaluateAreaScopeMatch(
        ScopeConfiguration config,
        List<string> targetAreaIds,
        MarshalContext marshalContext)
    {
        switch (config.Scope)
        {
            case Constants.ChecklistScopeOneLeadPerArea:
                // ONLY area leads can see/complete (shared per area)
                string? leadArea = FindMatchingArea(targetAreaIds, marshalContext.AreaLeadForAreaIds);
                if (leadArea != null)
                {
                    return (config, Specificity.Area, leadArea);
                }
                break;

            case Constants.ChecklistScopeEveryAreaLead:
                // ONLY area leads can see/complete (personal per lead)
                string? everyLeadArea = FindMatchingArea(targetAreaIds, marshalContext.AreaLeadForAreaIds);
                if (everyLeadArea != null)
                {
                    return (config, Specificity.Area, marshalContext.MarshalId); // Personal context
                }
                break;

            default:
                // For other scopes (EveryoneInAreas, OnePerArea), check if marshal is assigned to area
                string? assignedArea = FindMatchingArea(targetAreaIds, marshalContext.AssignedAreaIds);
                if (assignedArea != null)
                {
                    return (config, Specificity.Area, assignedArea);
                }

                // For OnePerArea scope, also check if marshal is area lead
                if (config.Scope == Constants.ChecklistScopeOnePerArea)
                {
                    string? onePerAreaLead = FindMatchingArea(targetAreaIds, marshalContext.AreaLeadForAreaIds);
                    if (onePerAreaLead != null)
                    {
                        return (config, Specificity.Area, onePerAreaLead);
                    }
                }
                break;
        }

        return null;
    }

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
            (ScopeConfiguration config, int specificity, string contextId)? match = EvaluateSingleConfiguration(config, marshalContext, checkpointLookup);
            if (match.HasValue)
            {
                matches.Add(match.Value);
            }
        }

        if (!matches.Any())
        {
            return new ScopeMatchResult(false, null, Specificity.NoMatch, string.Empty, string.Empty);
        }

        // Pick the most specific match (lowest specificity number wins)
        (ScopeConfiguration config, int specificity, string contextId) winner = matches.OrderBy(m => m.specificity).ThenBy(m => m.contextId).First();
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
        switch (config.ItemType)
        {
            case "Marshal":
                // Check for ALL_MARSHALS sentinel value
                if (config.Ids.Contains(Constants.AllMarshals))
                {
                    // Matches any marshal
                    return (config, Specificity.Marshal, marshalContext.MarshalId);
                }

                // Most specific - marshal ID explicitly listed
                if (config.Ids.Contains(marshalContext.MarshalId))
                {
                    return (config, Specificity.Marshal, marshalContext.MarshalId);
                }
                break;

            case "Checkpoint":
                // Check if marshal is directly assigned to any of these checkpoints
                string? matchedCheckpoint = FindMatchingCheckpoint(config.Ids, marshalContext);
                if (matchedCheckpoint != null)
                {
                    return (config, Specificity.Checkpoint, matchedCheckpoint);
                }

                // For OnePerCheckpoint and OneLeadPerArea scopes, area leads can also see/complete items
                // at checkpoints in their areas (even if not directly assigned)
                if (config.Scope == Constants.ChecklistScopeOnePerCheckpoint ||
                    config.Scope == Constants.ChecklistScopeOneLeadPerArea)
                {
                    string? areaLeadCheckpoint = CheckAreaLeadCheckpointAccess(config.Ids, marshalContext, checkpointLookup);
                    if (areaLeadCheckpoint != null)
                    {
                        return (config, Specificity.Checkpoint, areaLeadCheckpoint);
                    }
                }
                break;

            case "Area":
                // Special case: OnePerCheckpoint scope filtered by areas
                // This means "one completion per checkpoint, but only for checkpoints in these areas"
                if (config.Scope == Constants.ChecklistScopeOnePerCheckpoint)
                {
                    // Check for ALL_AREAS sentinel
                    if (config.Ids.Contains(Constants.AllAreas))
                    {
                        // Any checkpoint in any area
                        if (marshalContext.AssignedLocationIds.Any())
                        {
                            return (config, Specificity.Checkpoint, marshalContext.AssignedLocationIds.First());
                        }
                    }
                    else
                    {
                        // Find first checkpoint assigned to marshal that is in one of the specified areas
                        foreach (string locationId in marshalContext.AssignedLocationIds)
                        {
                            if (checkpointLookup.TryGetValue(locationId, out LocationEntity? checkpoint))
                            {
                                List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                                string? matchedArea = config.Ids.FirstOrDefault(areaId =>
                                    checkpointAreas.Contains(areaId));

                                if (matchedArea != null)
                                {
                                    // Return checkpoint ID as context (checkpoint-level specificity)
                                    return (config, Specificity.Checkpoint, locationId);
                                }
                            }
                        }
                    }

                    // No match for OnePerCheckpoint filtered by areas
                    break;
                }

                // Standard area scope matching (handles ALL_AREAS and specific area IDs)
                return EvaluateAreaScopeMatch(config, config.Ids, marshalContext);
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
                c.ContextOwnerMarshalId == marshalContext.MarshalId &&
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
    public static (string? ActorName, string? ActorType, DateTime? CompletedAt) GetCompletionDetails(
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
                c.ContextOwnerMarshalId == marshalContext.MarshalId &&
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

        return (completion?.ActorName, completion?.ActorType, completion?.CompletedAt);
    }

    /// <summary>
    /// Determines the context type based on scope
    /// </summary>
    private static string GetContextTypeForScope(string scope)
    {
        return scope switch
        {
            Constants.ChecklistScopeEveryoneInAreas or
            Constants.ChecklistScopeEveryoneAtCheckpoints or
            Constants.ChecklistScopeSpecificPeople or
            Constants.ChecklistScopeEveryAreaLead =>
                Constants.ChecklistContextPersonal,

            Constants.ChecklistScopeOnePerCheckpoint =>
                Constants.ChecklistContextCheckpoint,

            Constants.ChecklistScopeOnePerArea or
            Constants.ChecklistScopeOneLeadPerArea =>
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
            Constants.ChecklistScopeEveryoneInAreas or
            Constants.ChecklistScopeEveryoneAtCheckpoints or
            Constants.ChecklistScopeSpecificPeople or
            Constants.ChecklistScopeEveryAreaLead;
    }

    /// <summary>
    /// Gets all relevant contexts for a checklist item for a specific marshal.
    /// For personal scopes, returns one context (the marshal).
    /// For shared scopes (OnePerCheckpoint, OnePerArea), returns multiple contexts.
    /// </summary>
    public static List<ScopeMatchResult> GetAllRelevantContexts(
        ChecklistItemEntity item,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson) ?? [];

        // First, find the most specific scope that matches (using existing logic)
        ScopeMatchResult bestMatch = EvaluateScopeConfigurations(configs, marshalContext, checkpointLookup);

        if (!bestMatch.IsRelevant || bestMatch.WinningConfig == null)
        {
            return [];
        }

        // For personal scopes, return single context
        if (IsPersonalScope(bestMatch.WinningConfig.Scope))
        {
            return [bestMatch];
        }

        // For shared scopes, find ALL matching contexts
        return bestMatch.WinningConfig.Scope switch
        {
            Constants.ChecklistScopeOnePerCheckpoint => GetAllCheckpointContexts(bestMatch.WinningConfig, marshalContext, checkpointLookup),
            Constants.ChecklistScopeOnePerArea => GetAllAreaContexts(bestMatch.WinningConfig, marshalContext, checkpointLookup, false),
            Constants.ChecklistScopeOneLeadPerArea => GetAllAreaContexts(bestMatch.WinningConfig, marshalContext, checkpointLookup, true),
            _ => [bestMatch] // Fallback to single context
        };
    }

    /// <summary>
    /// Gets all checkpoint contexts that match for a OnePerCheckpoint scope
    /// </summary>
    private static List<ScopeMatchResult> GetAllCheckpointContexts(
        ScopeConfiguration config,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<ScopeMatchResult> results = [];

        if (config.ItemType == "Checkpoint")
        {
            // Find all checkpoints the marshal is assigned to that match this config
            List<string> matchingCheckpoints;

            if (config.Ids.Contains(Constants.AllCheckpoints))
            {
                matchingCheckpoints = marshalContext.AssignedLocationIds;
            }
            else
            {
                matchingCheckpoints = config.Ids
                    .Where(checkpointId => marshalContext.AssignedLocationIds.Contains(checkpointId))
                    .ToList();
            }

            foreach (string checkpointId in matchingCheckpoints)
            {
                results.Add(new ScopeMatchResult(
                    true,
                    config,
                    Specificity.Checkpoint,
                    Constants.ChecklistContextCheckpoint,
                    checkpointId
                ));
            }

            // Also check area leads who can access checkpoints in their areas
            if (marshalContext.AreaLeadForAreaIds.Any())
            {
                List<string> checkpointsToCheck = config.Ids.Contains(Constants.AllCheckpoints)
                    ? checkpointLookup.Keys.ToList()
                    : config.Ids;

                foreach (string checkpointId in checkpointsToCheck)
                {
                    // Skip if already added as direct assignment
                    if (marshalContext.AssignedLocationIds.Contains(checkpointId))
                    {
                        continue;
                    }

                    if (checkpointLookup.TryGetValue(checkpointId, out LocationEntity? checkpoint))
                    {
                        List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                        if (checkpointAreas.Any(areaId => marshalContext.AreaLeadForAreaIds.Contains(areaId)))
                        {
                            results.Add(new ScopeMatchResult(
                                true,
                                config,
                                Specificity.Checkpoint,
                                Constants.ChecklistContextCheckpoint,
                                checkpointId
                            ));
                        }
                    }
                }
            }
        }
        else if (config.ItemType == "Area")
        {
            // OnePerCheckpoint filtered by areas - find all checkpoints in matching areas
            List<string> matchingAreas = config.Ids.Contains(Constants.AllAreas)
                ? marshalContext.AssignedAreaIds
                : config.Ids.Where(areaId => marshalContext.AssignedAreaIds.Contains(areaId)).ToList();

            foreach (string locationId in marshalContext.AssignedLocationIds)
            {
                if (checkpointLookup.TryGetValue(locationId, out LocationEntity? checkpoint))
                {
                    List<string> checkpointAreas = JsonSerializer.Deserialize<List<string>>(checkpoint.AreaIdsJson) ?? [];
                    if (checkpointAreas.Any(areaId => matchingAreas.Contains(areaId)))
                    {
                        results.Add(new ScopeMatchResult(
                            true,
                            config,
                            Specificity.Checkpoint,
                            Constants.ChecklistContextCheckpoint,
                            locationId
                        ));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets all area contexts that match for OnePerArea or OneLeadPerArea scopes
    /// </summary>
    private static List<ScopeMatchResult> GetAllAreaContexts(
        ScopeConfiguration config,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup,
        bool leadOnly)
    {
        List<ScopeMatchResult> results = [];
        List<string> matchingAreas;

        if (leadOnly)
        {
            // OneLeadPerArea - only area leads can see/complete
            matchingAreas = config.Ids.Contains(Constants.AllAreas)
                ? marshalContext.AreaLeadForAreaIds
                : config.Ids.Where(areaId => marshalContext.AreaLeadForAreaIds.Contains(areaId)).ToList();
        }
        else
        {
            // OnePerArea - anyone in the area can see/complete
            matchingAreas = config.Ids.Contains(Constants.AllAreas)
                ? marshalContext.AssignedAreaIds
                : config.Ids.Where(areaId => marshalContext.AssignedAreaIds.Contains(areaId)).ToList();

            // Also check area leads
            List<string> leadAreas = config.Ids.Contains(Constants.AllAreas)
                ? marshalContext.AreaLeadForAreaIds
                : config.Ids.Where(areaId => marshalContext.AreaLeadForAreaIds.Contains(areaId)).ToList();

            matchingAreas = matchingAreas.Union(leadAreas).ToList();
        }

        foreach (string areaId in matchingAreas)
        {
            results.Add(new ScopeMatchResult(
                true,
                config,
                Specificity.Area,
                Constants.ChecklistContextArea,
                areaId
            ));
        }

        return results;
    }
}
