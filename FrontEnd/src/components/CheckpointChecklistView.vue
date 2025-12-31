<template>
  <div class="checkpoint-checklist-view">
    <div v-if="loading" class="loading-state">
      Loading checklist...
    </div>

    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
      <button @click="loadChecklist" class="btn btn-secondary btn-small">
        Retry
      </button>
    </div>

    <div v-else-if="noMarshalsAssigned" class="empty-state">
      <p>No marshals assigned to this checkpoint yet.</p>
      <p class="help-text">Assign marshals to enable checklist completion.</p>
    </div>

    <div v-else-if="items.length === 0" class="empty-state">
      <p>No checklist items for this checkpoint.</p>
    </div>

    <div v-else class="checklist-items">
      <div
        v-for="item in itemsWithLocalState"
        :key="`${item.itemId}-${item.completionContextId}`"
        class="checklist-item"
        :class="{
          'item-completed': item.localIsCompleted,
          'item-modified': item.isModified
        }"
      >
        <div class="item-checkbox">
          <input
            type="checkbox"
            :checked="item.localIsCompleted"
            :disabled="!item.canBeCompletedByMe && !item.localIsCompleted"
            @change="handleToggleComplete(item)"
          />
        </div>

        <div class="item-content">
          <div class="item-header">
            <div class="item-text-wrapper">
              <div class="item-text">{{ item.text }}</div>
              <div v-if="getContextName(item)" class="item-context">
                {{ getContextName(item) }}
              </div>
            </div>
            <div class="item-scope">
              <span class="scope-pill" :title="getScopeTooltip(item)">
                {{ getScopeLabel(item) }}
              </span>
            </div>
          </div>

          <div v-if="item.localIsCompleted" class="completion-info">
            <span v-if="item.isModified && !item.isCompleted" class="pending-text">
              Will be marked as completed (pending save)
            </span>
            <template v-else>
              <span class="completion-text">
                Completed by {{ item.completedByActorName || 'Unknown' }}
              </span>
              <span class="completion-time">
                {{ formatDateTime(item.completedAt) }}
              </span>
            </template>
          </div>

          <div v-if="item.isModified && item.isCompleted && !item.localIsCompleted" class="uncomplete-info">
            <span class="pending-text">Will be marked as incomplete (pending save)</span>
          </div>

          <div v-if="!item.canBeCompletedByMe && !item.localIsCompleted" class="disabled-reason">
            Already completed by someone else
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { checklistApi } from '../services/api';

const props = defineProps({
  eventId: {
    type: String,
    required: true,
  },
  locationId: {
    type: String,
    default: null,
  },
  areaId: {
    type: String,
    default: null,
  },
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  modelValue: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['update:modelValue', 'change']);

const items = ref([]);
const loading = ref(false);
const error = ref(null);
const localCompletions = ref(new Map()); // itemId -> { complete: boolean, contextType, contextId }

const noMarshalsAssigned = computed(() => {
  return props.assignments.length === 0;
});

const itemsWithLocalState = computed(() => {
  return items.value.map(item => {
    const localState = localCompletions.value.get(`${item.itemId}|||${item.completionContextId}`);
    const localIsCompleted = localState !== undefined ? localState.complete : item.isCompleted;
    const isModified = localState !== undefined && localState.complete !== item.isCompleted;

    return {
      ...item,
      localIsCompleted,
      isModified,
    };
  });
});

const loadChecklist = async () => {
  if (!props.eventId || (!props.locationId && !props.areaId)) return;

  loading.value = true;
  error.value = null;

  try {
    const response = props.locationId
      ? await checklistApi.getCheckpointChecklist(props.eventId, props.locationId)
      : await checklistApi.getAreaChecklist(props.eventId, props.areaId);
    items.value = response.data || [];

    // Initialize local completions from modelValue if provided
    if (props.modelValue && props.modelValue.length > 0) {
      const newMap = new Map();
      props.modelValue.forEach(completion => {
        newMap.set(`${completion.itemId}|||${completion.contextId}`, {
          complete: completion.complete,
          contextType: completion.contextType,
          contextId: completion.contextId,
        });
      });
      localCompletions.value = newMap;
    }
  } catch (err) {
    console.error('Failed to load checklist:', err);
    error.value = err.response?.data?.message || 'Failed to load checklist';
  } finally {
    loading.value = false;
  }
};

const handleToggleComplete = (item) => {
  // For personal items, use the owner's marshal ID
  // For shared items, use the first assigned marshal
  const marshalId = item.contextOwnerMarshalId || (props.assignments.length > 0 ? props.assignments[0].marshalId : null);
  if (!marshalId) return;

  const key = `${item.itemId}|||${item.completionContextId}`;
  const currentLocal = localCompletions.value.get(key);
  const currentState = currentLocal !== undefined ? currentLocal.complete : item.isCompleted;

  // Toggle the local state
  localCompletions.value.set(key, {
    complete: !currentState,
    contextType: item.completionContextType,
    contextId: item.completionContextId,
    marshalId: marshalId,
  });

  // Emit the changes
  emitChanges();
};

const emitChanges = () => {
  const changes = [];

  for (const [key, localState] of localCompletions.value.entries()) {
    const [itemId, contextId] = key.split('|||');
    const item = items.value.find(i => i.itemId === itemId && i.completionContextId === contextId);
    if (item && localState.complete !== item.isCompleted) {
      changes.push({
        itemId,
        complete: localState.complete,
        contextType: localState.contextType,
        contextId: localState.contextId,
        marshalId: localState.marshalId,
      });
    }
  }

  emit('update:modelValue', changes);
  emit('change', changes);
};

// Reset local state when modal opens/closes
const resetLocalState = () => {
  localCompletions.value = new Map();
  if (props.modelValue && props.modelValue.length > 0) {
    const newMap = new Map();
    props.modelValue.forEach(completion => {
      newMap.set(`${completion.itemId}|||${completion.contextId}`, {
        complete: completion.complete,
        contextType: completion.contextType,
        contextId: completion.contextId,
        marshalId: completion.marshalId,
      });
    });
    localCompletions.value = newMap;
  }
};

defineExpose({
  resetLocalState,
  loadChecklist,
});

const getScopeLabel = (item) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'SpecificPeople': 'Assigned to you',
    'EveryoneInAreas': 'Your areas',
    'EveryoneAtCheckpoints': 'Your checkpoints',
    'OnePerCheckpoint': 'One per checkpoint',
    'OnePerArea': 'One per area',
    'EveryAreaLead': 'Area lead',
    'OneLeadPerArea': 'One lead per area',
  };

  return scopeMap[item.matchedScope] || item.matchedScope;
};

const getScopeTooltip = (item) => {
  const tooltips = {
    'Everyone': 'This item is for everyone at the event',
    'SpecificPeople': 'This item is specifically assigned to certain people',
    'EveryoneInAreas': 'This item is for everyone in certain areas',
    'EveryoneAtCheckpoints': 'This item is for everyone at certain checkpoints',
    'OnePerCheckpoint': 'One person at the checkpoint needs to complete this',
    'OnePerArea': 'One person in the area needs to complete this',
    'EveryAreaLead': 'This item is for area leads only',
    'OneLeadPerArea': 'One area lead needs to complete this',
  };

  return tooltips[item.matchedScope] || '';
};

const getContextName = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return null;
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = props.locations.find(l => l.id === item.completionContextId);
    if (!location) return null;

    const checkpointLabel = location.description
      ? `${location.name} - ${location.description}`
      : location.name;
    return `at checkpoint ${checkpointLabel}`;
  }

  if (item.completionContextType === 'Area') {
    const area = props.areas.find(a => a.id === item.completionContextId);
    return area ? `in area ${area.name}` : null;
  }

  if (item.completionContextType === 'Personal') {
    if (item.contextOwnerName) {
      return `for ${item.contextOwnerName}`;
    }
    return 'assigned individually';
  }

  return null;
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  return date.toLocaleString();
};

// Watch for changes and reload
watch(() => [props.eventId, props.locationId, props.areaId], () => {
  loadChecklist();
}, { immediate: true });
</script>

<style scoped>
.checkpoint-checklist-view {
  padding: 1rem 0;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: #666;
}

.error-state {
  color: #dc3545;
}

.error-state p {
  margin-bottom: 1rem;
}

.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: white;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item:hover {
  border-color: #ddd;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.05);
}

.checklist-item.item-completed {
  background: #f8f9fa;
  opacity: 0.8;
}

.checklist-item.item-modified {
  border-color: #ffc107;
  background: #fffbf0;
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.item-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
}

.item-checkbox input[type="checkbox"]:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  flex-wrap: wrap;
}

.item-text-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 200px;
}

.item-text {
  font-size: 0.95rem;
  color: #333;
  word-wrap: break-word;
}

.item-context {
  font-size: 0.85rem;
  color: #667eea;
  font-weight: 500;
}

.item-scope {
  flex-shrink: 0;
  min-width: 150px;
}

.scope-pill {
  display: inline-block;
  padding: 0.25rem 0.65rem;
  background: #e3f2fd;
  color: #1976d2;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
}

.completion-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: #666;
}

.completion-text {
  font-weight: 500;
}

.completion-time {
  font-size: 0.8rem;
  color: #999;
}

.pending-info {
  font-size: 0.85rem;
  color: #999;
  font-style: italic;
}

.pending-text {
  font-size: 0.85rem;
  color: #ff9800;
  font-weight: 500;
  font-style: italic;
}

.uncomplete-info {
  font-size: 0.85rem;
  color: #dc3545;
}

.disabled-reason {
  font-size: 0.85rem;
  color: #dc3545;
  font-style: italic;
}

.help-text {
  font-size: 0.8rem;
  color: #999;
  margin-top: 0.5rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}
</style>
