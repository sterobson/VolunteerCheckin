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

  // Aggregate all marshals from area lead checkpoints (deduplicated, sorted by name)
  const allAreaLeadMarshals = computed(() => {
    // Access the version trigger to force recomputation when data changes
    // eslint-disable-next-line no-unused-vars
    const _version = areaLeadMarshalDataVersion.value;
    const checkpoints = areaLeadRef?.value?.checkpoints || areaLeadCheckpoints?.value || [];
    const marshalMap = new Map();

    for (const checkpoint of checkpoints) {
      for (const marshal of (checkpoint.marshals || [])) {
        if (!marshalMap.has(marshal.marshalId)) {
          marshalMap.set(marshal.marshalId, {
            ...marshal,
            checkpoints: [],
            allTasks: [],
            totalTaskCount: 0,
            completedTaskCount: 0,
          });
        }
        const m = marshalMap.get(marshal.marshalId);
        m.checkpoints.push({
          checkpointId: checkpoint.checkpointId,
          name: checkpoint.name,
          description: checkpoint.description,
        });
        // Add marshal's outstanding tasks
        if (marshal.outstandingTasks) {
          for (const task of marshal.outstandingTasks) {
            m.allTasks.push({ ...task, isCompleted: false, checkpointName: checkpoint.name });
            m.totalTaskCount++;
          }
        }
        // Add marshal's completed tasks if available
        if (marshal.completedTasks) {
          for (const task of marshal.completedTasks) {
            m.allTasks.push({ ...task, isCompleted: true, checkpointName: checkpoint.name });
            m.totalTaskCount++;
            m.completedTaskCount++;
          }
        }
      }
    }

    return Array.from(marshalMap.values()).sort((a, b) =>
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
