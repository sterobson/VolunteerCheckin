import { ref, computed } from 'vue';
import { checklistApi } from '../services/api';

/**
 * Composable for managing area lead marshal aggregation and tasks
 */
export function useAreaLeadMarshals({
  eventId,
  currentMarshalId,
  areaLeadRef,
  areaLeadCheckpoints,
  areaChecklistItems,
  myChecklistItems,
  areaLeadAreaIds,
  loadChecklist,
}) {
  // State
  const expandedAreaLeadMarshal = ref(null);
  const savingAreaLeadMarshalTask = ref(false);
  const areaLeadMarshalDataVersion = ref(0);

  // Toggle marshal expansion
  const toggleAreaLeadMarshalExpansion = (marshalId) => {
    expandedAreaLeadMarshal.value = expandedAreaLeadMarshal.value === marshalId ? null : marshalId;
  };

  // Aggregate all marshals from area lead checkpoints, with tasks derived from checklist items
  const allAreaLeadMarshals = computed(() => {
    // Access the version trigger to force recomputation when data changes
    // eslint-disable-next-line no-unused-vars
    const _version = areaLeadMarshalDataVersion.value;

    // Get checkpoints from either source - prefer the one with data
    // (empty array is truthy, so we check length explicitly)
    const checkpointsFromRef = areaLeadRef?.value?.checkpoints;
    const checkpointsFromProp = areaLeadCheckpoints?.value;
    const checkpoints = (checkpointsFromRef?.length > 0)
      ? checkpointsFromRef
      : (checkpointsFromProp || []);

    // Get checklist items - combine area checklist items with the area lead's own items
    // (areaChecklistItems excludes the area lead's own tasks, so we need to add them back)
    const areaItems = areaChecklistItems?.value || [];
    const myItems = myChecklistItems?.value || [];
    const checklistItems = [...areaItems, ...myItems];

    // Build marshal map with checkpoint assignments
    const marshalMap = new Map();

    for (const checkpoint of checkpoints) {
      const marshals = checkpoint.marshals || checkpoint.Marshals || [];
      const checkpointId = checkpoint.checkpointId || checkpoint.CheckpointId;
      const checkpointName = checkpoint.name || checkpoint.Name;
      const checkpointDescription = checkpoint.description || checkpoint.Description;
      const checkpointAreaIds = checkpoint.areaIds || checkpoint.AreaIds || [];

      for (const marshal of marshals) {
        const marshalId = marshal.marshalId || marshal.MarshalId;
        if (!marshalId) continue;

        if (!marshalMap.has(marshalId)) {
          marshalMap.set(marshalId, {
            ...marshal,
            marshalId,
            checkpoints: [],
            checkpointIds: new Set(),
            areaIds: new Set(),
            allTasks: [],
            totalTaskCount: 0,
            completedTaskCount: 0,
          });
        }
        const m = marshalMap.get(marshalId);
        m.checkpoints.push({
          checkpointId,
          name: checkpointName,
          description: checkpointDescription,
        });
        m.checkpointIds.add(checkpointId);
        // Track which areas this marshal is in (via checkpoint assignments)
        for (const areaId of checkpointAreaIds) {
          m.areaIds.add(areaId);
        }
      }
    }

    // Now assign tasks from checklist items to each marshal
    for (const item of checklistItems) {
      const contextType = item.completionContextType;
      const contextId = item.completionContextId;

      // Determine which marshals this task applies to
      let applicableMarshals = [];

      if (contextType === 'Personal') {
        // Personal task - only applies to the specific marshal
        const targetMarshalId = item.contextOwnerMarshalId;
        if (targetMarshalId && marshalMap.has(targetMarshalId)) {
          applicableMarshals = [marshalMap.get(targetMarshalId)];
        }
      } else if (contextType === 'Checkpoint') {
        // Checkpoint task - applies to all marshals assigned to this checkpoint
        for (const m of marshalMap.values()) {
          if (m.checkpointIds.has(contextId)) {
            applicableMarshals.push(m);
          }
        }
      } else if (contextType === 'Area') {
        // Area task - applies to all marshals in this area
        for (const m of marshalMap.values()) {
          if (m.areaIds.has(contextId)) {
            applicableMarshals.push(m);
          }
        }
      } else {
        // Event-wide or unknown context - applies to all marshals
        applicableMarshals = Array.from(marshalMap.values());
      }

      // Add task to each applicable marshal
      for (const m of applicableMarshals) {
        m.allTasks.push({
          ...item,
          isCompleted: item.isCompleted || item.localIsCompleted,
        });
        m.totalTaskCount++;
        if (item.isCompleted || item.localIsCompleted) {
          m.completedTaskCount++;
        }
      }
    }

    // Convert Sets to arrays for serialization and clean up
    const result = Array.from(marshalMap.values()).map(m => ({
      ...m,
      checkpointIds: Array.from(m.checkpointIds),
      areaIds: Array.from(m.areaIds),
    }));

    return result.sort((a, b) =>
      (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
    );
  });

  // Toggle task completion from the area lead marshals section
  const toggleAreaLeadMarshalTask = async (task, marshal) => {
    if (savingAreaLeadMarshalTask.value) return;
    savingAreaLeadMarshalTask.value = true;

    const actionData = {
      marshalId: marshal.marshalId,
      contextType: task.contextType,
      contextId: task.contextId,
      actorMarshalId: currentMarshalId.value,
    };

    try {
      if (task.isCompleted) {
        // Uncomplete
        await checklistApi.uncomplete(eventId.value, task.itemId, actionData);
      } else {
        // Complete
        await checklistApi.complete(eventId.value, task.itemId, actionData);
      }
      // Reload the area lead dashboard data
      if (areaLeadRef?.value?.loadDashboard) {
        await areaLeadRef.value.loadDashboard();
      }
      // Force recomputation of allAreaLeadMarshals
      areaLeadMarshalDataVersion.value++;
      // Also reload the main checklist
      if (loadChecklist) {
        await loadChecklist(true);
      }
    } catch (err) {
      console.error('Failed to toggle area lead marshal task:', err);
    } finally {
      savingAreaLeadMarshalTask.value = false;
    }
  };

  return {
    // State
    expandedAreaLeadMarshal,
    savingAreaLeadMarshalTask,
    areaLeadMarshalDataVersion,

    // Computed
    allAreaLeadMarshals,

    // Functions
    toggleAreaLeadMarshalExpansion,
    toggleAreaLeadMarshalTask,
  };
}
