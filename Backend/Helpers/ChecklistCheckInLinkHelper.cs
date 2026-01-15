using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for managing the linkage between checklist items and check-in status.
/// When a task has LinksToCheckIn=true, completing the task checks in the marshal,
/// and checking in completes the task.
/// </summary>
public class ChecklistCheckInLinkHelper
{
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;
    private readonly IAssignmentRepository _assignmentRepository;

    public ChecklistCheckInLinkHelper(
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository,
        IAssignmentRepository assignmentRepository)
    {
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
        _assignmentRepository = assignmentRepository;
    }

    /// <summary>
    /// Validates that the scope configurations are compatible with LinksToCheckIn.
    /// Only EveryoneAtCheckpoints and SpecificPeople scopes can be linked to check-in.
    /// </summary>
    public static bool CanLinkToCheckIn(List<ScopeConfiguration> scopeConfigurations)
    {
        if (scopeConfigurations == null || scopeConfigurations.Count == 0)
        {
            return false;
        }

        // All active scopes must be either EveryoneAtCheckpoints or SpecificPeople
        return scopeConfigurations.All(config =>
            config.Scope == Constants.ChecklistScopeEveryoneAtCheckpoints ||
            config.Scope == Constants.ChecklistScopeSpecificPeople);
    }

    /// <summary>
    /// Gets the validation error message if LinksToCheckIn is set but scopes are invalid.
    /// Returns null if validation passes.
    /// </summary>
    public static string? GetLinkToCheckInValidationError(List<ScopeConfiguration> scopeConfigurations, bool linksToCheckIn)
    {
        if (!linksToCheckIn)
        {
            return null; // No validation needed if not linking
        }

        if (!CanLinkToCheckIn(scopeConfigurations))
        {
            return "LinksToCheckIn can only be enabled for tasks with EveryoneAtCheckpoints or SpecificPeople scopes. " +
                   "Shared scopes (OnePerCheckpoint) and area-based scopes are not supported.";
        }

        return null;
    }

    /// <summary>
    /// Finds the assignment for a marshal at a specific checkpoint.
    /// </summary>
    public async Task<AssignmentEntity?> FindAssignmentAsync(string eventId, string marshalId, string checkpointId)
    {
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
        return assignments.FirstOrDefault(a => a.LocationId == checkpointId);
    }

    /// <summary>
    /// Finds all checklist items with LinksToCheckIn=true that apply to a marshal at a specific checkpoint.
    /// </summary>
    public async Task<List<ChecklistItemEntity>> FindLinkedItemsForCheckpointAsync(
        string eventId,
        string checkpointId,
        string marshalId,
        ChecklistScopeHelper.MarshalContext context,
        Dictionary<string, LocationEntity> checkpointLookup)
    {
        IEnumerable<ChecklistItemEntity> allItems = await _checklistItemRepository.GetByEventAsync(eventId);

        List<ChecklistItemEntity> linkedItems = [];

        foreach (ChecklistItemEntity item in allItems)
        {
            if (!item.LinksToCheckIn)
            {
                continue;
            }

            // Check if this item is relevant to the marshal
            if (!ChecklistScopeHelper.IsItemRelevantToMarshal(item, context, checkpointLookup))
            {
                continue;
            }

            // Check if this item applies to this specific checkpoint
            // For linked items, we need to verify the checkpoint is in the scope
            if (IsItemLinkedToCheckpoint(item, checkpointId, context))
            {
                linkedItems.Add(item);
            }
        }

        return linkedItems;
    }

    /// <summary>
    /// Checks if a linked checklist item applies to a specific checkpoint for a given marshal context.
    /// </summary>
    private static bool IsItemLinkedToCheckpoint(
        ChecklistItemEntity item,
        string checkpointId,
        ChecklistScopeHelper.MarshalContext context)
    {
        List<ScopeConfiguration> scopeConfigs = System.Text.Json.JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        foreach (ScopeConfiguration config in scopeConfigs)
        {
            if (config.Scope == Constants.ChecklistScopeEveryoneAtCheckpoints)
            {
                // Check if this checkpoint is in the scope
                if (config.Ids.Contains(Constants.AllCheckpoints) || config.Ids.Contains(checkpointId))
                {
                    return true;
                }
            }
            else if (config.Scope == Constants.ChecklistScopeSpecificPeople &&
                     (config.Ids.Contains(Constants.AllMarshals) || config.Ids.Contains(context.MarshalId)) &&
                     context.AssignedLocationIds.Contains(checkpointId))
            {
                // For SpecificPeople, check if the marshal is in the scope AND has assignment at this checkpoint
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a checklist item is already completed for a marshal at a specific checkpoint context.
    /// </summary>
    public async Task<bool> IsItemCompletedForContextAsync(
        string eventId,
        string itemId,
        string marshalId,
        string checkpointId)
    {
        IEnumerable<ChecklistCompletionEntity> completions = await _checklistCompletionRepository.GetByItemAsync(eventId, itemId);

        // For linked items, completion context is Checkpoint, not Personal
        return completions.Any(c =>
            !c.IsDeleted &&
            c.ContextOwnerMarshalId == marshalId &&
            c.CompletionContextType == Constants.ChecklistContextCheckpoint &&
            c.CompletionContextId == checkpointId);
    }

    /// <summary>
    /// Finds the completion record for a linked task.
    /// </summary>
    public async Task<ChecklistCompletionEntity?> FindCompletionAsync(
        string eventId,
        string itemId,
        string marshalId,
        string checkpointId)
    {
        IEnumerable<ChecklistCompletionEntity> completions = await _checklistCompletionRepository.GetByItemAsync(eventId, itemId);

        return completions.FirstOrDefault(c =>
            !c.IsDeleted &&
            c.ContextOwnerMarshalId == marshalId &&
            c.CompletionContextType == Constants.ChecklistContextCheckpoint &&
            c.CompletionContextId == checkpointId);
    }

    /// <summary>
    /// Creates a completion record for a linked checklist item.
    /// </summary>
    public async Task<ChecklistCompletionEntity> CreateCompletionAsync(
        string eventId,
        string itemId,
        string marshalId,
        string marshalName,
        string checkpointId,
        string actorType,
        string actorId,
        string actorName)
    {
        string completionId = Guid.NewGuid().ToString();
        string partitionKey = ChecklistCompletionEntity.CreatePartitionKey(eventId);
        string rowKey = ChecklistCompletionEntity.CreateRowKey(itemId, completionId);

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = partitionKey,
            RowKey = rowKey,
            EventId = eventId,
            ChecklistItemId = itemId,
            CompletionContextType = Constants.ChecklistContextCheckpoint,
            CompletionContextId = checkpointId,
            ContextOwnerMarshalId = marshalId,
            ContextOwnerMarshalName = marshalName,
            ActorType = actorType,
            ActorId = actorId,
            ActorName = actorName,
            CompletedAt = DateTime.UtcNow
        };

        await _checklistCompletionRepository.AddAsync(completion);
        return completion;
    }

    /// <summary>
    /// Soft-deletes a completion record (uncompletes the task).
    /// </summary>
    public async Task UncompleteAsync(
        ChecklistCompletionEntity completion,
        string actorType,
        string actorId,
        string actorName)
    {
        completion.IsDeleted = true;
        completion.UncompletedAt = DateTime.UtcNow;
        completion.UncompletedByActorType = actorType;
        completion.UncompletedByActorId = actorId;
        completion.UncompletedByActorName = actorName;

        await _checklistCompletionRepository.UpdateAsync(completion);
    }

    /// <summary>
    /// Gets the checkpoint ID from a task's scope for a specific marshal.
    /// For linked tasks with multiple checkpoints, returns the checkpoint where the marshal is assigned.
    /// </summary>
    public static string? DetermineCheckpointForLinkedTask(
        ChecklistItemEntity item,
        ChecklistScopeHelper.MarshalContext context,
        string? requestedCheckpointId = null)
    {
        // If a specific checkpoint was requested, use it
        if (!string.IsNullOrEmpty(requestedCheckpointId))
        {
            return requestedCheckpointId;
        }

        List<ScopeConfiguration> scopeConfigs = System.Text.Json.JsonSerializer.Deserialize<List<ScopeConfiguration>>(
            item.ScopeConfigurationsJson, FunctionHelpers.JsonOptions) ?? [];

        foreach (ScopeConfiguration config in scopeConfigs)
        {
            if (config.Scope == Constants.ChecklistScopeEveryoneAtCheckpoints)
            {
                // If ALL_CHECKPOINTS, use the first checkpoint the marshal is assigned to
                if (config.Ids.Contains(Constants.AllCheckpoints))
                {
                    return context.AssignedLocationIds.FirstOrDefault();
                }

                // Otherwise, find the first checkpoint in scope that the marshal is assigned to
                foreach (string checkpointId in config.Ids)
                {
                    if (context.AssignedLocationIds.Contains(checkpointId))
                    {
                        return checkpointId;
                    }
                }
            }
            else if (config.Scope == Constants.ChecklistScopeSpecificPeople &&
                     (config.Ids.Contains(Constants.AllMarshals) || config.Ids.Contains(context.MarshalId)))
            {
                // For SpecificPeople, return the first checkpoint the marshal is assigned to
                return context.AssignedLocationIds.FirstOrDefault();
            }
        }

        return null;
    }
}
