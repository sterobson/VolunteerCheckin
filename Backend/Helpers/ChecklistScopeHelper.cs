using System.Text.Json;
using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for determining checklist item visibility and completion permissions
/// based on scope configurations and marshal context using "Most Specific Wins" logic.
/// Delegates core scope evaluation to ScopeEvaluator for reuse with Notes and other features.
/// </summary>
public static class ChecklistScopeHelper
{
    // Type aliases for backward compatibility
    public record MarshalContext(
        string MarshalId,
        List<string> AssignedAreaIds,
        List<string> AssignedLocationIds,
        List<string> AreaLeadForAreaIds
    )
    {
        /// <summary>
        /// Converts to ScopeEvaluator.MarshalContext
        /// </summary>
        public ScopeEvaluator.MarshalContext ToScopeContext()
        {
            return new ScopeEvaluator.MarshalContext(MarshalId, AssignedAreaIds, AssignedLocationIds, AreaLeadForAreaIds);
        }
    }

    public record ScopeMatchResult(
        bool IsRelevant,
        ScopeConfiguration? WinningConfig,
        int Specificity,
        string ContextType,
        string ContextId
    )
    {
        /// <summary>
        /// Creates from ScopeEvaluator.ScopeMatchResult
        /// </summary>
        public static ScopeMatchResult FromScopeResult(ScopeEvaluator.ScopeMatchResult result)
        {
            return new ScopeMatchResult(result.IsRelevant, result.WinningConfig, result.Specificity, result.ContextType, result.ContextId);
        }
    }

    /// <summary>
    /// Evaluates all scope configurations and returns the most specific match
    /// </summary>
    public static ScopeMatchResult EvaluateScopeConfigurations(
        List<ScopeConfiguration> configurations,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        ScopeEvaluator.ScopeMatchResult result = ScopeEvaluator.EvaluateScopeConfigurations(
            configurations,
            marshalContext.ToScopeContext(),
            checkpointLookup
        );
        return ScopeMatchResult.FromScopeResult(result);
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
            item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

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
            item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        ScopeMatchResult result = EvaluateScopeConfigurations(configs, context, checkpointLookup);

        if (!result.IsRelevant || result.WinningConfig == null)
        {
            return (Constants.ChecklistContextPersonal, context.MarshalId, string.Empty);
        }

        // For personal context types, the contextId should always be the marshal ID
        // For shared context types (OnePerCheckpoint, OnePerArea, AreaLead), use the matched item ID
        string contextId = ScopeEvaluator.IsPersonalContextType(result.ContextType) ? context.MarshalId : result.ContextId;

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
        if (ScopeEvaluator.IsPersonalContextType(contextType))
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

        if (ScopeEvaluator.IsPersonalContextType(contextType))
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
    /// Determines if a scope is personal (one per marshal) vs shared (one per area/checkpoint)
    /// </summary>
    public static bool IsPersonalScope(string scope)
    {
        return ScopeEvaluator.IsPersonalScope(scope);
    }

    /// <summary>
    /// Gets all relevant contexts for a checklist item for a specific marshal.
    /// For personal scopes, returns one context (the marshal).
    /// For shared scopes (OnePerCheckpoint, OnePerArea), returns multiple contexts.
    /// For linked tasks with EveryoneAtCheckpoints/SpecificPeople, returns one context per checkpoint.
    /// </summary>
    public static List<ScopeMatchResult> GetAllRelevantContexts(
        ChecklistItemEntity item,
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        List<ScopeConfiguration> configs = JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        // First, find the most specific scope that matches (using existing logic)
        ScopeMatchResult bestMatch = EvaluateScopeConfigurations(configs, marshalContext, checkpointLookup);

        if (!bestMatch.IsRelevant || bestMatch.WinningConfig == null)
        {
            return [];
        }

        // For linked tasks with checkpoint-based personal scopes, return one context per checkpoint
        if (item.LinksToCheckIn && IsPersonalScope(bestMatch.WinningConfig.Scope))
        {
            return GetLinkedTaskContexts(marshalContext, checkpointLookup, bestMatch);
        }

        // For regular personal scopes, return single context
        if (IsPersonalScope(bestMatch.WinningConfig.Scope))
        {
            return [bestMatch];
        }

        // For shared scopes, find ALL matching contexts
        ScopeEvaluator.MarshalContext scopeContext = marshalContext.ToScopeContext();
        return bestMatch.WinningConfig.Scope switch
        {
            Constants.ChecklistScopeOnePerCheckpoint => ConvertResults(ScopeEvaluator.GetAllCheckpointContexts(bestMatch.WinningConfig, scopeContext, checkpointLookup)),
            Constants.ChecklistScopeOnePerArea => ConvertResults(ScopeEvaluator.GetAllAreaContexts(bestMatch.WinningConfig, scopeContext, checkpointLookup, false)),
            Constants.ChecklistScopeOneLeadPerArea => ConvertResults(ScopeEvaluator.GetAllAreaContexts(bestMatch.WinningConfig, scopeContext, checkpointLookup, true)),
            _ => [bestMatch] // Fallback to single context
        };
    }

    /// <summary>
    /// Gets contexts for linked tasks. For EveryoneAtCheckpoints/SpecificPeople scopes,
    /// returns one Checkpoint context per checkpoint where the marshal is assigned and in scope.
    /// </summary>
    private static List<ScopeMatchResult> GetLinkedTaskContexts(
        MarshalContext marshalContext,
        Dictionary<string, LocationEntity> checkpointLookup,
        ScopeMatchResult bestMatch)
    {
        List<ScopeMatchResult> results = [];
        ScopeConfiguration config = bestMatch.WinningConfig!;

        if (config.Scope == Constants.ChecklistScopeEveryoneAtCheckpoints)
        {
            // Get all checkpoints in scope that the marshal is assigned to
            HashSet<string> checkpointsInScope = config.Ids.Contains(Constants.AllCheckpoints)
                ? new HashSet<string>(checkpointLookup.Keys)
                : new HashSet<string>(config.Ids);

            foreach (string checkpointId in marshalContext.AssignedLocationIds)
            {
                if (checkpointsInScope.Contains(checkpointId))
                {
                    results.Add(new ScopeMatchResult(
                        true,
                        config,
                        bestMatch.Specificity,
                        Constants.ChecklistContextCheckpoint,  // Use Checkpoint context for linked tasks
                        checkpointId
                    ));
                }
            }
        }
        else if (config.Scope == Constants.ChecklistScopeSpecificPeople)
        {
            // For SpecificPeople, check if this marshal is in scope
            if (config.Ids.Contains(Constants.AllMarshals) || config.Ids.Contains(marshalContext.MarshalId))
            {
                // Return one context per checkpoint the marshal is assigned to
                foreach (string checkpointId in marshalContext.AssignedLocationIds)
                {
                    results.Add(new ScopeMatchResult(
                        true,
                        config,
                        bestMatch.Specificity,
                        Constants.ChecklistContextCheckpoint,  // Use Checkpoint context for linked tasks
                        checkpointId
                    ));
                }
            }
        }
        else
        {
            // For other personal scopes (EveryoneInAreas, EveryAreaLead), fall back to single context
            // (these shouldn't be used with LinksToCheckIn but handle gracefully)
            results.Add(bestMatch);
        }

        return results;
    }

    /// <summary>
    /// Converts ScopeEvaluator results to ChecklistScopeHelper results
    /// </summary>
    private static List<ScopeMatchResult> ConvertResults(List<ScopeEvaluator.ScopeMatchResult> results)
    {
        return results.Select(ScopeMatchResult.FromScopeResult).ToList();
    }
}
