using System.Text.Json;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Core scope evaluation logic shared between checklists, notes, and other scope-based features.
/// Implements "Most Specific Wins" algorithm for determining visibility and access.
/// </summary>
public static class ScopeEvaluator
{
    /// <summary>
    /// Context information about a marshal for scope-based filtering
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
    public static class SpecificityLevel
    {
        public const int Marshal = 1;      // Most specific - explicit marshal ID
        public const int Checkpoint = 2;   // Checkpoint assignment
        public const int Area = 3;         // Area assignment
        public const int NoMatch = int.MaxValue; // No match found
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
            return new ScopeMatchResult(false, null, SpecificityLevel.NoMatch, string.Empty, string.Empty);
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
                    return (config, SpecificityLevel.Marshal, marshalContext.MarshalId);
                }

                // Most specific - marshal ID explicitly listed
                if (config.Ids.Contains(marshalContext.MarshalId))
                {
                    return (config, SpecificityLevel.Marshal, marshalContext.MarshalId);
                }
                break;

            case "Checkpoint":
                // Check if marshal is directly assigned to any of these checkpoints
                string? matchedCheckpoint = FindMatchingCheckpoint(config.Ids, marshalContext);
                if (matchedCheckpoint != null)
                {
                    // For personal scopes, use marshal ID as context
                    // For shared scopes, use checkpoint ID as context
                    string contextId = IsPersonalScope(config.Scope) ? marshalContext.MarshalId : matchedCheckpoint;
                    return (config, SpecificityLevel.Checkpoint, contextId);
                }

                // For OnePerCheckpoint and OneLeadPerArea scopes, area leads can also see/complete items
                // at checkpoints in their areas (even if not directly assigned)
                if (config.Scope == Constants.ChecklistScopeOnePerCheckpoint ||
                    config.Scope == Constants.ChecklistScopeOneLeadPerArea)
                {
                    string? areaLeadCheckpoint = CheckAreaLeadCheckpointAccess(config.Ids, marshalContext, checkpointLookup);
                    if (areaLeadCheckpoint != null)
                    {
                        return (config, SpecificityLevel.Checkpoint, areaLeadCheckpoint);
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
                            return (config, SpecificityLevel.Checkpoint, marshalContext.AssignedLocationIds.First());
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
                                    return (config, SpecificityLevel.Checkpoint, locationId);
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
                    return (config, SpecificityLevel.Area, leadArea);
                }
                break;

            case Constants.ChecklistScopeEveryAreaLead:
                // ONLY area leads can see/complete (personal per lead)
                string? everyLeadArea = FindMatchingArea(targetAreaIds, marshalContext.AreaLeadForAreaIds);
                if (everyLeadArea != null)
                {
                    return (config, SpecificityLevel.Area, marshalContext.MarshalId); // Personal context
                }
                break;

            default:
                // For other scopes (EveryoneInAreas, OnePerArea), check if marshal is assigned to area
                string? assignedArea = FindMatchingArea(targetAreaIds, marshalContext.AssignedAreaIds);
                if (assignedArea != null)
                {
                    // For personal scopes (EveryoneInAreas), use marshal ID as context
                    // For shared scopes (OnePerArea), use area ID as context
                    string contextId = IsPersonalScope(config.Scope) ? marshalContext.MarshalId : assignedArea;
                    return (config, SpecificityLevel.Area, contextId);
                }

                // For OnePerArea scope, also check if marshal is area lead
                if (config.Scope == Constants.ChecklistScopeOnePerArea)
                {
                    string? onePerAreaLead = FindMatchingArea(targetAreaIds, marshalContext.AreaLeadForAreaIds);
                    if (onePerAreaLead != null)
                    {
                        return (config, SpecificityLevel.Area, onePerAreaLead);
                    }
                }
                break;
        }

        return null;
    }

    /// <summary>
    /// Determines the context type based on scope
    /// </summary>
    public static string GetContextTypeForScope(string scope)
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
    public static bool IsPersonalContextType(string contextType)
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
    /// Gets all checkpoint contexts that match for a OnePerCheckpoint scope
    /// </summary>
    public static List<ScopeMatchResult> GetAllCheckpointContexts(
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
                    SpecificityLevel.Checkpoint,
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
                                SpecificityLevel.Checkpoint,
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
                            SpecificityLevel.Checkpoint,
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
    public static List<ScopeMatchResult> GetAllAreaContexts(
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
                SpecificityLevel.Area,
                Constants.ChecklistContextArea,
                areaId
            ));
        }

        return results;
    }
}
