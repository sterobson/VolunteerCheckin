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
      <p>No {{ termsLower.peoplePlural }} assigned to this {{ termsLower.checkpoint }} yet.</p>
      <p class="help-text">Assign {{ termsLower.peoplePlural }} to enable {{ termsLower.checklist }} completion.</p>
    </div>

    <div v-else-if="items.length === 0" class="empty-state">
      <p>No checklist items for this {{ termsLower.checkpoint }}.</p>
    </div>

    <GroupedTasksList
      v-else
      :items="itemsWithLocalState"
      :locations="locations"
      :areas="areas"
      @toggle-complete="handleToggleComplete"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { checklistApi } from '../services/api';
import { useTerminology } from '../composables/useTerminology';
import GroupedTasksList from './event-manage/GroupedTasksList.vue';

const { terms, termsLower } = useTerminology();

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
  color: var(--text-secondary);
}

.error-state {
  color: var(--danger);
}

.error-state p {
  margin-bottom: 1rem;
}

.help-text {
  font-size: 0.8rem;
  color: var(--text-muted);
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
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}
</style>
