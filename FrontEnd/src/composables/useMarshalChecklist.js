import { ref, computed } from 'vue';
import { checklistApi, queueOfflineAction, getOfflineMode } from '../services/api';

/**
 * Composable for managing marshal checklist functionality
 */
export function useMarshalChecklist({
  eventId,
  currentMarshalId,
  currentPerson,
  assignments,
  areaLeadAreaIds,
  isAreaLead,
  allLocations,
  areas,
  terms,
  termsLower,
  areaLeadRef,
  areaLeadCheckpoints,
  sectionLastLoadedAt,
  updateCachedField,
  updatePendingCount,
}) {
  // State
  const checklistItems = ref([]);
  const checklistLoading = ref(false);
  const checklistError = ref(null);
  const savingChecklist = ref(false);
  const checklistGroupBy = ref('person');
  const expandedChecklistGroup = ref(null);

  // Filter out completed shared tasks that the marshal doesn't need to see
  const visibleChecklistItems = computed(() => {
    return checklistItems.value.filter(item => {
      if (!item.isCompleted) return true;

      const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];
      if (sharedScopes.includes(item.matchedScope)) {
        return item.completedByActorId === currentMarshalId.value;
      }

      return true;
    });
  });

  // Checklist completion count
  const completedChecklistCount = computed(() => {
    return visibleChecklistItems.value.filter(item => item.isCompleted).length;
  });

  // Separate checklist items into "your jobs" vs "your area's jobs"
  const myChecklistItems = computed(() => {
    const myAssignmentIds = assignments.value.map(a => a.locationId);

    return visibleChecklistItems.value.filter(item => {
      if (item.completionContextType === 'Personal') {
        return item.contextOwnerMarshalId === currentMarshalId.value;
      }
      if (item.completionContextType === 'Checkpoint') {
        return myAssignmentIds.includes(item.completionContextId);
      }
      if (item.completionContextType === 'Area') {
        return areaLeadAreaIds.value.includes(item.completionContextId);
      }
      return true;
    });
  });

  const areaChecklistItems = computed(() => {
    if (!isAreaLead.value) return [];
    const myAssignmentIds = assignments.value.map(a => a.locationId);

    return visibleChecklistItems.value.filter(item => {
      if (item.completionContextType === 'Personal') {
        return item.contextOwnerMarshalId !== currentMarshalId.value;
      }
      if (item.completionContextType === 'Checkpoint') {
        return !myAssignmentIds.includes(item.completionContextId);
      }
      if (item.completionContextType === 'Area') {
        return !areaLeadAreaIds.value.includes(item.completionContextId);
      }
      return false;
    });
  });

  // Items with local state for GroupedTasksList
  const myChecklistItemsWithLocalState = computed(() => {
    return myChecklistItems.value
      .map(item => ({
        ...item,
        localIsCompleted: item.isCompleted,
        isModified: false,
      }))
      .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
  });

  const areaChecklistItemsWithLocalState = computed(() => {
    return areaChecklistItems.value
      .map(item => ({
        ...item,
        localIsCompleted: item.isCompleted,
        isModified: false,
      }))
      .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
  });

  // Collect all marshals from area lead's checkpoints for name lookup
  const areaMarshalsForChecklist = computed(() => {
    const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
    if (checkpoints.length === 0) return [];

    const marshals = [];
    const seenIds = new Set();
    for (const checkpoint of checkpoints) {
      for (const marshal of (checkpoint.marshals || [])) {
        if (!seenIds.has(marshal.marshalId)) {
          seenIds.add(marshal.marshalId);
          marshals.push(marshal);
        }
      }
    }
    return marshals;
  });

  // Effective grouping mode for non-leads
  const effectiveChecklistGroupBy = computed(() => {
    if (isAreaLead.value) return 'none';

    const items = visibleChecklistItems.value;
    const checkpointIds = new Set();
    for (const item of items) {
      if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
        checkpointIds.add(item.completionContextId);
      }
    }

    return checkpointIds.size > 1 ? 'checkpoint' : 'none';
  });

  // Group checklist items by checkpoint
  const groupedChecklistItems = computed(() => {
    const items = visibleChecklistItems.value;
    const groups = {};

    for (const item of items) {
      let key, name;

      if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
        key = `checkpoint_${item.completionContextId}`;
        const location = allLocations.value.find(l => l.id === item.completionContextId);
        name = location?.name || 'Unknown ' + terms.value.checkpoint;
      } else if (item.completionContextType === 'Area' && item.completionContextId) {
        key = `area_${item.completionContextId}`;
        const area = areas.value.find(a => a.id === item.completionContextId);
        name = area?.name || 'Unknown ' + terms.value.area;
      } else {
        key = 'personal';
        name = 'Personal';
      }

      if (!groups[key]) {
        groups[key] = { key, name, items: [], completedCount: 0 };
      }
      groups[key].items.push(item);
      if (item.isCompleted) {
        groups[key].completedCount++;
      }
    }

    // Sort items within each group by displayOrder
    for (const group of Object.values(groups)) {
      group.items.sort((a, b) => {
        const orderA = a.displayOrder || 0;
        const orderB = b.displayOrder || 0;
        return orderA - orderB;
      });
    }

    return Object.values(groups).sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })
    );
  });

  // Toggle checklist group expansion
  const toggleChecklistGroup = (key) => {
    expandedChecklistGroup.value = expandedChecklistGroup.value === key ? null : key;
  };

  // Get context name for display
  const getContextName = (item) => {
    if (!item.completionContextType || !item.completionContextId) {
      return null;
    }

    if (item.completionContextType === 'Checkpoint') {
      const location = allLocations.value.find(l => l.id === item.completionContextId);
      if (!location) return null;
      const desc = location.description ? ` - ${location.description}` : '';
      return `At ${termsLower.value.checkpoint} ${location.name}${desc}`;
    }

    if (item.completionContextType === 'Area') {
      const area = areas.value.find(a => a.id === item.completionContextId);
      if (!area) return null;
      return `At ${termsLower.value.area} ${area.name}`;
    }

    if (item.completionContextType === 'Personal') {
      return 'Personal item';
    }

    return null;
  };

  // Load checklist data
  const loadChecklist = async (silent = false) => {
    if (!currentMarshalId.value) {
      console.warn('No marshal ID, skipping checklist load');
      return;
    }

    if (!silent) {
      checklistLoading.value = true;
    }
    checklistError.value = null;

    try {
      const evtId = eventId.value;
      console.log('Fetching checklist for marshal:', currentMarshalId.value);

      const marshalResponse = await checklistApi.getMarshalChecklist(evtId, currentMarshalId.value);
      let allItems = marshalResponse.data || [];

      // For area leads, also fetch checklist items for their areas
      if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
        console.log('Fetching area checklists for areas:', areaLeadAreaIds.value);
        const areaPromises = areaLeadAreaIds.value.map(areaId =>
          checklistApi.getAreaChecklist(evtId, areaId).catch(err => {
            console.warn(`Failed to fetch checklist for area ${areaId}:`, err);
            return { data: [] };
          })
        );
        const areaResponses = await Promise.all(areaPromises);

        // Merge and deduplicate items
        const itemMap = new Map();
        for (const item of allItems) {
          const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
          itemMap.set(key, item);
        }
        for (const response of areaResponses) {
          for (const item of (response.data || [])) {
            const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
            if (!itemMap.has(key)) {
              itemMap.set(key, item);
            }
          }
        }
        allItems = Array.from(itemMap.values());
        console.log('Merged checklist items:', allItems.length);
      }

      checklistItems.value = allItems;
      if (sectionLastLoadedAt?.value) {
        sectionLastLoadedAt.value.checklist = Date.now();
      }
      console.log('Checklist loaded:', checklistItems.value.length, 'items');
    } catch (error) {
      console.error('Failed to load checklist:', error.response?.status, error.response?.data);
      checklistError.value = error.response?.data?.message || 'Failed to load checklist';
    } finally {
      if (!silent) {
        checklistLoading.value = false;
      }
    }
  };

  // Handle toggling a checklist item
  const handleToggleChecklist = async (item) => {
    if (savingChecklist.value) return;

    savingChecklist.value = true;
    const evtId = eventId.value;

    const actionData = {
      marshalId: item.contextOwnerMarshalId || currentMarshalId.value,
      contextType: item.completionContextType,
      contextId: item.completionContextId,
      actorMarshalId: currentMarshalId.value,
    };

    try {
      if (item.isCompleted) {
        if (getOfflineMode()) {
          await queueOfflineAction('checklist_uncomplete', {
            eventId: evtId,
            itemId: item.itemId,
            data: actionData
          });
          item.isCompleted = false;
          item.completedAt = null;
          item.completedByActorName = null;
          if (updatePendingCount) await updatePendingCount();
        } else {
          await checklistApi.uncomplete(evtId, item.itemId, actionData);

          // Handle linked check-out - update assignment state if this was a linked task
          if (item.linksToCheckIn && item.linkedCheckpointId) {
            const assignment = assignments.value.find(a => a.locationId === item.linkedCheckpointId);
            if (assignment) {
              assignment.isCheckedIn = false;
              assignment.checkedInAt = null;
              console.log(`Linked check-out: Checked out from ${item.linkedCheckpointName || 'checkpoint'}`);
            }
          }

          await loadChecklist(true);
        }
      } else {
        if (getOfflineMode()) {
          await queueOfflineAction('checklist_complete', {
            eventId: evtId,
            itemId: item.itemId,
            data: actionData
          });
          item.isCompleted = true;
          item.completedAt = new Date().toISOString();
          item.completedByActorName = currentPerson.value?.name || 'You';
          if (updatePendingCount) await updatePendingCount();
        } else {
          const response = await checklistApi.complete(evtId, item.itemId, actionData);

          // Handle linked check-in - update assignment state if check-in occurred
          // Use item.linkedCheckpointId as primary source (consistent with uncomplete logic)
          // Fall back to response.data.linkedCheckIn if available
          const checkpointId = item.linkedCheckpointId || response.data?.linkedCheckIn?.locationId;
          if (item.linksToCheckIn && checkpointId) {
            const assignment = assignments.value.find(a => a.locationId === checkpointId);
            if (assignment) {
              const checkedInAt = response.data?.linkedCheckIn?.checkedInAt || new Date().toISOString();
              assignment.isCheckedIn = true;
              assignment.checkedInAt = checkedInAt;
              console.log(`Linked check-in: Checked in at ${item.linkedCheckpointName || checkpointId}`);
            } else {
              console.warn('Could not find assignment for linked check-in:', { checkpointId, linkedCheckpointId: item.linkedCheckpointId });
            }
          }

          await loadChecklist(true);
        }
      }

      if (updateCachedField) {
        await updateCachedField(evtId, 'checklist', JSON.parse(JSON.stringify(checklistItems.value)));
      }
    } catch (error) {
      console.error('Failed to toggle checklist item:', error);

      if (getOfflineMode() || !error.response) {
        const actionType = item.isCompleted ? 'checklist_uncomplete' : 'checklist_complete';
        await queueOfflineAction(actionType, {
          eventId: evtId,
          itemId: item.itemId,
          data: actionData
        });

        if (item.isCompleted) {
          item.isCompleted = false;
          item.completedAt = null;
          item.completedByActorName = null;
        } else {
          item.isCompleted = true;
          item.completedAt = new Date().toISOString();
          item.completedByActorName = currentPerson.value?.name || 'You';
        }

        if (updatePendingCount) await updatePendingCount();
        if (updateCachedField) {
          await updateCachedField(evtId, 'checklist', JSON.parse(JSON.stringify(checklistItems.value)));
        }
      }
    } finally {
      savingChecklist.value = false;
    }
  };

  return {
    // State
    checklistItems,
    checklistLoading,
    checklistError,
    savingChecklist,
    checklistGroupBy,
    expandedChecklistGroup,

    // Computed
    visibleChecklistItems,
    completedChecklistCount,
    myChecklistItems,
    areaChecklistItems,
    myChecklistItemsWithLocalState,
    areaChecklistItemsWithLocalState,
    areaMarshalsForChecklist,
    effectiveChecklistGroupBy,
    groupedChecklistItems,

    // Functions
    loadChecklist,
    handleToggleChecklist,
    toggleChecklistGroup,
    getContextName,
  };
}
