import { ref, computed } from 'vue';
import { checklistApi, queueOfflineAction, getOfflineMode } from '../services/api';

/**
 * Denormalizes a normalized checklist response back to a flat array of items.
 * This allows the existing frontend logic to work unchanged while benefiting
 * from the reduced network payload size.
 *
 * Short field names (see response._ for mapping):
 *   Response: s=scopes, at=actorTypes, ct=contextTypes, m=marshals, c=contexts, d=items, n=instances
 *   Marshal/Context: i=id, n=name, t=typeIndex
 *   Item: i=itemId, t=text, sc=scopeConfigurations, o=displayOrder, r=isRequired,
 *         vf=visibleFrom, vu=visibleUntil, mb=mustCompleteBy, l=linksToCheckIn, lc=linkedCheckpointId, ln=linkedCheckpointName
 *   Instance: i=itemIndex, c=isCompleted, m=canBeCompletedByMe, a=actorIndex, at=actorTypeIndex,
 *             ca=completedAt, x=contextIndex, s=scopeIndex, o=ownerIndex
 *
 * @param {Object} response - The normalized checklist response
 * @returns {Array} - Flat array of checklist items with all fields expanded
 */
function denormalizeChecklistResponse(response) {
  // If response is already an array (old format), return as-is
  if (Array.isArray(response)) {
    return response;
  }

  // If response doesn't have the expected normalized structure, return empty
  if (!response || !response.n || !response.d) {
    return [];
  }

  const {
    s: scopes = [],
    at: actorTypes = [],
    ct: contextTypes = [],
    m: marshals = [],
    c: contexts = [],
    d: items = [],
    n: instances = [],
  } = response;

  return instances.map(instance => {
    const itemDef = items[instance.i] || {};
    const context = contexts[instance.x] || {};
    const contextType = contextTypes[context.t] || '';
    const scope = scopes[instance.s] || '';

    // Get actor info if completed
    let completedByActorName = null;
    let completedByActorType = null;
    let completedByActorId = null;
    if (instance.c && instance.a != null && instance.a >= 0) {
      const actor = marshals[instance.a];
      if (actor) {
        completedByActorName = actor.n;
        completedByActorId = actor.i;
      }
      if (instance.at != null && instance.at >= 0) {
        completedByActorType = actorTypes[instance.at];
      }
    }

    // Get owner info
    let contextOwnerName = null;
    let contextOwnerMarshalId = null;
    if (instance.o != null && instance.o >= 0) {
      const owner = marshals[instance.o];
      if (owner) {
        contextOwnerName = owner.n;
        contextOwnerMarshalId = owner.i;
      }
    }

    return {
      itemId: itemDef.i,
      text: itemDef.t,
      scopeConfigurations: itemDef.sc || [],
      displayOrder: itemDef.o,
      isRequired: itemDef.r,
      visibleFrom: itemDef.vf,
      visibleUntil: itemDef.vu,
      mustCompleteBy: itemDef.mb,
      linksToCheckIn: itemDef.l,
      linkedCheckpointId: itemDef.lc,
      linkedCheckpointName: itemDef.ln,
      isCompleted: instance.c,
      canBeCompletedByMe: instance.m,
      completedByActorName,
      completedByActorType,
      completedByActorId,
      completedAt: instance.ca,
      completionContextType: contextType,
      completionContextId: context.i,
      matchedScope: scope,
      contextOwnerName,
      contextOwnerMarshalId,
    };
  });
}

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

  // Sort by displayOrder so all derived computations are in order
  // For shared scopes (OnePerCheckpoint, OnePerArea), completed tasks stay visible
  // so all marshals can see the task is done and who completed it
  const visibleChecklistItems = computed(() => {
    return checklistItems.value
      .slice()
      .sort((a, b) => (a.displayOrder || 0) - (b.displayOrder || 0));
  });

  // Checklist completion count
  const completedChecklistCount = computed(() => {
    return visibleChecklistItems.value.filter(item => item.isCompleted).length;
  });

  // Shared scope types where one person completes for the whole group
  const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];

  // Separate checklist items into "your jobs" vs "your area's jobs"
  // "Your jobs" = tasks assigned directly to you (including shared tasks at your checkpoint)
  // "Your area's jobs" = tasks for other people in your area (area leads only)
  const myChecklistItems = computed(() => {
    const myAssignmentIds = assignments.value.map(a => a.locationId);

    return visibleChecklistItems.value.filter(item => {
      // For shared scopes, only include "summary" items (contextOwnerMarshalId is null)
      // Per-marshal items go to areaChecklistItems for area leads to manage
      if (sharedScopes.includes(item.matchedScope)) {
        if (item.contextOwnerMarshalId != null) {
          return false; // Per-marshal items go to area's jobs only
        }
        // Summary item - include if at my checkpoint/area
        if (item.completionContextType === 'Checkpoint') {
          return myAssignmentIds.includes(item.completionContextId);
        }
        if (item.completionContextType === 'Area') {
          return areaLeadAreaIds.value.includes(item.completionContextId);
        }
        return true;
      }

      if (item.completionContextType === 'Personal') {
        return item.contextOwnerMarshalId === currentMarshalId.value;
      }
      if (item.completionContextType === 'Checkpoint') {
        // For linked tasks, filter by marshal ID (each marshal has their own task)
        if (item.linksToCheckIn) {
          return item.contextOwnerMarshalId === currentMarshalId.value;
        }
        // For non-linked checkpoint tasks, include if at my checkpoint
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

    return visibleChecklistItems.value.filter(item => {
      // For shared scopes, include per-marshal items (those with contextOwnerMarshalId set)
      // These show one row per marshal so area lead can see/manage who completed
      if (sharedScopes.includes(item.matchedScope)) {
        return item.contextOwnerMarshalId != null;
      }

      if (item.completionContextType === 'Personal') {
        return item.contextOwnerMarshalId !== currentMarshalId.value;
      }
      if (item.completionContextType === 'Checkpoint') {
        // For linked tasks, include other marshals' tasks (not mine)
        if (item.linksToCheckIn) {
          return item.contextOwnerMarshalId !== currentMarshalId.value;
        }
        // For non-linked checkpoint tasks, exclude (they go to myChecklistItems)
        return false;
      }
      if (item.completionContextType === 'Area') {
        // Non-shared area tasks - exclude (they go to myChecklistItems for area leads)
        return false;
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
      const marshalResponse = await checklistApi.getMarshalChecklist(evtId, currentMarshalId.value);
      let allItems = denormalizeChecklistResponse(marshalResponse.data);

      // For area leads, also fetch checklist items for their areas
      if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
        const areaPromises = areaLeadAreaIds.value.map(areaId =>
          checklistApi.getAreaChecklist(evtId, areaId).catch(err => {
            console.warn(`Failed to fetch checklist for area ${areaId}:`, err);
            return { data: null };
          })
        );
        const areaResponses = await Promise.all(areaPromises);

        // Merge and deduplicate items
        // Include contextOwnerMarshalId in key when present (for linked tasks and shared scope per-marshal items)
        const itemMap = new Map();
        const buildKey = (item) => {
          const baseKey = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
          // Include marshal ID if set - this covers:
          // - Linked tasks (per-marshal items with Checkpoint context)
          // - Shared scope per-marshal items from area checklist
          if (item.contextOwnerMarshalId) {
            return `${baseKey}_${item.contextOwnerMarshalId}`;
          }
          return baseKey;
        };
        for (const item of allItems) {
          itemMap.set(buildKey(item), item);
        }
        for (const response of areaResponses) {
          const areaItems = denormalizeChecklistResponse(response.data);
          for (const item of areaItems) {
            const key = buildKey(item);
            if (!itemMap.has(key)) {
              itemMap.set(key, item);
            }
          }
        }
        allItems = Array.from(itemMap.values());
      }

      checklistItems.value = allItems;
      if (sectionLastLoadedAt?.value) {
        sectionLastLoadedAt.value.checklist = Date.now();
      }
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
