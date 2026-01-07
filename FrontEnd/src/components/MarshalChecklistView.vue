<template>
  <div class="marshal-checklist-view">
    <div v-if="loading" class="loading-state">
      Loading checklist...
    </div>

    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
      <button @click="loadChecklist" class="btn btn-secondary btn-small">
        Retry
      </button>
    </div>

    <div v-else-if="items.length === 0" class="empty-state">
      <p>No checklist items for this person.</p>
    </div>

    <div v-else class="checklist-items">
      <div
        v-for="item in itemsWithLocalState"
        :key="`${item.itemId}_${item.completionContextType}_${item.completionContextId}`"
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
            :disabled="!item.canBeCompletedByMe"
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
            {{ getDisabledReason(item) }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { checklistApi } from '../services/api';
import { useTerminology } from '../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  eventId: {
    type: String,
    required: true,
  },
  marshalId: {
    type: String,
    required: true,
  },
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
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
const localCompletions = ref(new Map()); // itemId -> { complete: boolean }

const itemsWithLocalState = computed(() => {
  return items.value.map(item => {
    const localState = localCompletions.value.get(item.itemId);
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
  if (!props.eventId || !props.marshalId) return;

  loading.value = true;
  error.value = null;

  try {
    const response = await checklistApi.getMarshalChecklist(props.eventId, props.marshalId);
    items.value = response.data || [];

    // Initialize local completions from modelValue if provided
    if (props.modelValue && props.modelValue.length > 0) {
      const newMap = new Map();
      props.modelValue.forEach(completion => {
        newMap.set(completion.itemId, {
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
  const currentLocal = localCompletions.value.get(item.itemId);
  const currentState = currentLocal !== undefined ? currentLocal.complete : item.isCompleted;

  // Toggle the local state
  localCompletions.value.set(item.itemId, {
    complete: !currentState,
    contextType: item.completionContextType,
    contextId: item.completionContextId,
  });

  // Emit the changes
  emitChanges();
};

const emitChanges = () => {
  const changes = [];

  for (const [itemId, localState] of localCompletions.value.entries()) {
    const item = items.value.find(i => i.itemId === itemId);
    if (item && localState.complete !== item.isCompleted) {
      changes.push({
        itemId,
        complete: localState.complete,
        contextType: localState.contextType,
        contextId: localState.contextId,
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
      newMap.set(completion.itemId, {
        complete: completion.complete,
        contextType: completion.contextType,
        contextId: completion.contextId,
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
    'EveryoneInAreas': `Your ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `Your ${termsLower.value.checkpoints}`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'EveryAreaLead': `${terms.value.area} lead`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
  };

  return scopeMap[item.matchedScope] || item.matchedScope;
};

const getScopeTooltip = (item) => {
  const tooltips = {
    'Everyone': 'This item is for everyone at the event',
    'SpecificPeople': 'This item is specifically assigned to this person',
    'EveryoneInAreas': `This item is for everyone in certain ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `This item is for everyone at certain ${termsLower.value.checkpoints}`,
    'OnePerCheckpoint': `One person at the ${termsLower.value.checkpoint} needs to complete this`,
    'OnePerArea': `One person in the ${termsLower.value.area} needs to complete this`,
    'EveryAreaLead': `This item is for ${termsLower.value.area} leads only`,
    'OneLeadPerArea': `One ${termsLower.value.area} lead needs to complete this`,
  };

  return tooltips[item.matchedScope] || '';
};

const getDisabledReason = (item) => {
  if (item.completionContextType === 'Checkpoint' || item.completionContextType === 'Area') {
    return 'Already completed by someone else';
  }
  return 'Cannot be completed';
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
    return `at ${termsLower.value.checkpoint} ${checkpointLabel}`;
  }

  if (item.completionContextType === 'Area') {
    const area = props.areas.find(a => a.id === item.completionContextId);
    return area ? `in ${termsLower.value.area} ${area.name}` : null;
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
watch(() => [props.eventId, props.marshalId], () => {
  loadChecklist();
}, { immediate: true });
</script>

<style scoped>
.marshal-checklist-view {
  padding: 1rem 0;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-secondary);
}

.error-state {
  color: var(--accent-danger);
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
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  transition: all 0.2s;
}

.checklist-item:hover {
  border-color: var(--border-color);
  box-shadow: var(--shadow-sm);
}

.checklist-item.item-completed {
  background: var(--bg-tertiary);
  opacity: 0.8;
}

.checklist-item.item-modified {
  border-color: var(--accent-warning);
  background: var(--status-warning-bg);
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
  color: var(--text-primary);
  word-wrap: break-word;
}

.item-context {
  font-size: 0.85rem;
  color: var(--accent-primary);
  font-weight: 500;
}

.item-scope {
  flex-shrink: 0;
  min-width: 150px;
}

.scope-pill {
  display: inline-block;
  padding: 0.25rem 0.65rem;
  background: var(--bg-active);
  color: var(--accent-primary);
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
  color: var(--text-secondary);
}

.completion-text {
  font-weight: 500;
}

.completion-time {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.disabled-reason {
  font-size: 0.85rem;
  color: var(--accent-danger);
  font-style: italic;
}

.pending-text {
  font-size: 0.85rem;
  color: var(--accent-warning);
  font-weight: 500;
  font-style: italic;
}

.uncomplete-info {
  font-size: 0.85rem;
  color: var(--accent-danger);
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
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}
</style>
