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
      <p>No checklist items for this {{ termsLower.person }}.</p>
    </div>

    <GroupedTasksList
      v-else
      :items="itemsWithLocalState"
      :locations="locations"
      :areas="areas"
      :allow-reorder="allowReorder"
      @toggle-complete="handleToggleComplete"
      @reorder="handleReorder"
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
  allowReorder: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:modelValue', 'change', 'reorder']);

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

const handleReorder = (changes) => {
  emit('reorder', changes);
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
