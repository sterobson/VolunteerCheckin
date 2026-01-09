<template>
  <div class="checklist-preview">
    <!-- Header with add button -->
    <div class="preview-actions">
      <p class="preview-header">
        {{ isEditing ? `Add new ${termsLower.checklist} for this ${checkpointTermLower}:` : `${terms.checklists} that will apply at this ${checkpointTermLower}:` }}
      </p>
      <button
        type="button"
        class="add-item-btn"
        @click="showAddForm = !showAddForm"
      >
        {{ showAddForm ? 'Cancel' : `Add ${termsLower.checklist}` }}
      </button>
    </div>

    <!-- Add new item form -->
    <div v-if="showAddForm" class="add-item-form">
      <input
        v-model="newItemText"
        type="text"
        :placeholder="`Enter ${termsLower.checklist} text...`"
        class="add-item-input"
        @keyup.enter="addNewItem"
      />
      <button
        type="button"
        class="save-item-btn"
        :disabled="!newItemText.trim()"
        @click="addNewItem"
      >
        Add
      </button>
    </div>

    <!-- Pending new items -->
    <div v-if="pendingNewItems.length > 0" class="pending-items">
      <div
        v-for="(item, index) in pendingNewItems"
        :key="`pending-${index}`"
        class="checklist-item pending"
      >
        <div class="item-checkbox">
          <input
            type="checkbox"
            :checked="false"
            disabled
          />
        </div>
        <div class="item-content">
          <div class="item-header">
            <div class="item-text">{{ item.text }}</div>
            <div class="item-scope">
              <span class="scope-pill new-pill" title="Will be created when saved">
                New
              </span>
              <button
                type="button"
                class="remove-item-btn"
                @click="removeNewItem(index)"
                title="Remove"
              >
                &times;
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty state (only shown when creating, not editing) -->
    <div v-if="!isEditing && filteredItems.length === 0 && pendingNewItems.length === 0" class="empty-state">
      <p>No {{ termsLower.checklists }} will apply based on the current {{ termsLower.area }} assignments.</p>
      <p v-if="pendingAreaIds.length === 0" class="help-text">
        Assign {{ termsLower.areas }} to see relevant {{ termsLower.checklists }}.
      </p>
    </div>

    <!-- Existing items that match (only shown when creating, not editing) -->
    <div v-if="!isEditing && filteredItems.length > 0" class="checklist-items">
      <div
        v-for="item in filteredItems"
        :key="item.id"
        class="checklist-item"
      >
        <div class="item-checkbox">
          <input
            type="checkbox"
            :checked="false"
            disabled
          />
        </div>

        <div class="item-content">
          <div class="item-header">
            <div class="item-text">{{ item.text }}</div>
            <div class="item-scope">
              <span class="scope-pill" :title="getScopeTooltip(item)">
                {{ getScopeLabel(item) }}
              </span>
            </div>
          </div>
          <div v-if="getContextInfo(item)" class="item-context">
            {{ getContextInfo(item) }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import { useTerminology } from '../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  allChecklistItems: {
    type: Array,
    default: () => [],
  },
  pendingAreaIds: {
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
  isEditing: {
    type: Boolean,
    default: false,
  },
  checkpointTermSingular: {
    type: String,
    default: '',
  },
});

// Use prop if provided, otherwise fall back to global term
const checkpointTermLower = computed(() => {
  return props.checkpointTermSingular ? props.checkpointTermSingular.toLowerCase() : termsLower.value.checkpoint;
});

const emit = defineEmits(['update:modelValue', 'change']);

// Local state
const showAddForm = ref(false);
const newItemText = ref('');
const pendingNewItems = ref([...props.modelValue]);

// Sync with v-model
watch(() => props.modelValue, (newVal) => {
  pendingNewItems.value = [...newVal];
}, { deep: true });

const addNewItem = () => {
  if (!newItemText.value.trim()) return;

  pendingNewItems.value.push({
    text: newItemText.value.trim(),
    isNew: true,
  });

  newItemText.value = '';
  showAddForm.value = false;
  emitChanges();
};

const removeNewItem = (index) => {
  pendingNewItems.value.splice(index, 1);
  emitChanges();
};

const emitChanges = () => {
  emit('update:modelValue', [...pendingNewItems.value]);
  emit('change');
};

// Check if a scope configuration matches a new checkpoint with the given areas
const configMatchesCheckpoint = (config) => {
  const scope = config.scope;
  const itemType = config.itemType;
  const ids = config.ids || [];

  // EveryoneAtCheckpoints or OnePerCheckpoint with Checkpoint itemType - ALL_CHECKPOINTS
  if ((scope === 'EveryoneAtCheckpoints' || scope === 'OnePerCheckpoint') && itemType === 'Checkpoint') {
    return ids.includes('ALL_CHECKPOINTS');
  }

  // OnePerCheckpoint filtered by areas
  if (scope === 'OnePerCheckpoint' && itemType === 'Area') {
    if (ids.includes('ALL_AREAS')) return true;
    return ids.some(id => props.pendingAreaIds.includes(id));
  }

  // Area-based scopes
  if ((scope === 'EveryoneInAreas' || scope === 'OnePerArea' ||
       scope === 'EveryAreaLead' || scope === 'OneLeadPerArea') && itemType === 'Area') {
    if (ids.includes('ALL_AREAS')) return true;
    return ids.some(id => props.pendingAreaIds.includes(id));
  }

  return false;
};

// Filter checklist items that would apply to this checkpoint
const filteredItems = computed(() => {
  return props.allChecklistItems.filter(item => {
    // Check scopeConfigurations array
    if (item.scopeConfigurations && item.scopeConfigurations.length > 0) {
      return item.scopeConfigurations.some(config => configMatchesCheckpoint(config));
    }
    return false;
  });
});

const formatScope = (scope) => {
  const scopeMap = {
    'EveryoneAtCheckpoints': `All at ${termsLower.value.checkpoints}`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'EveryoneInAreas': `All in ${termsLower.value.areas}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'EveryAreaLead': `${termsLower.value.area} leads`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
  };
  return scopeMap[scope] || scope;
};

const getScopeLabel = (item) => {
  if (!item.scopeConfigurations || item.scopeConfigurations.length === 0) {
    return 'Unknown';
  }

  // Find the matching configuration
  const matchingConfig = item.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) {
    // Just show the first scope
    return formatScope(item.scopeConfigurations[0].scope);
  }

  const ids = matchingConfig.ids || [];

  // Check for ALL_ variants
  if (ids.includes('ALL_CHECKPOINTS')) return `All ${termsLower.value.checkpoints}`;
  if (ids.includes('ALL_AREAS')) return `All ${termsLower.value.areas}`;

  return formatScope(matchingConfig.scope);
};

const getScopeTooltip = (item) => {
  if (!item.scopeConfigurations || item.scopeConfigurations.length === 0) {
    return '';
  }

  const matchingConfig = item.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) return '';

  const tooltips = {
    'EveryoneAtCheckpoints': `This item is for everyone at specified ${termsLower.value.checkpoints}`,
    'OnePerCheckpoint': `One person at each ${termsLower.value.checkpoint} needs to complete this`,
    'EveryoneInAreas': `This item is for everyone in specified ${termsLower.value.areas}`,
    'OnePerArea': `One person in each ${termsLower.value.area} needs to complete this`,
    'EveryAreaLead': `This item is for ${termsLower.value.area} leads only`,
    'OneLeadPerArea': `One ${termsLower.value.area} lead needs to complete this`,
  };
  return tooltips[matchingConfig.scope] || '';
};

const getContextInfo = (item) => {
  if (!item.scopeConfigurations || item.scopeConfigurations.length === 0) {
    return null;
  }

  const matchingConfig = item.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) return null;

  const itemType = matchingConfig.itemType;
  const ids = matchingConfig.ids || [];

  // Skip if ALL_ variant
  if (ids.includes('ALL_CHECKPOINTS') || ids.includes('ALL_AREAS')) {
    return null;
  }

  // Show which areas this applies to
  if (itemType === 'Area') {
    const matchingAreas = props.areas
      .filter(a => ids.includes(a.id) && props.pendingAreaIds.includes(a.id))
      .map(a => a.name);
    if (matchingAreas.length > 0) {
      return `In: ${matchingAreas.join(', ')}`;
    }
  }

  return null;
};
</script>

<style scoped>
.checklist-preview {
  padding: 1rem 0;
}

.preview-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.preview-header {
  margin: 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.add-item-btn {
  padding: 0.4rem 0.75rem;
  background: var(--brand-primary);
  color: var(--card-bg);
  border: none;
  border-radius: 4px;
  font-size: 0.85rem;
  cursor: pointer;
  transition: background-color 0.2s;
  white-space: nowrap;
  flex-shrink: 0;
}

.add-item-btn:hover {
  background: var(--brand-primary-hover);
}

.add-item-form {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.75rem;
  background: var(--brand-primary-light);
  border-radius: 6px;
}

.add-item-input {
  flex: 1;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
}

.add-item-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.save-item-btn {
  padding: 0.5rem 1rem;
  background: var(--success);
  color: var(--card-bg);
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
}

.save-item-btn:disabled {
  background: var(--border-dark);
  cursor: not-allowed;
}

.save-item-btn:not(:disabled):hover {
  background: var(--success-hover);
}

.pending-items {
  margin-bottom: 1rem;
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--text-secondary);
}

.empty-state p {
  margin: 0 0 0.5rem 0;
}

.help-text {
  font-size: 0.85rem;
  font-style: italic;
  color: var(--text-muted);
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
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 6px;
}

.checklist-item.pending {
  background: var(--brand-primary-light);
  border-color: var(--brand-primary);
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.25rem;
}

.item-checkbox input[type="checkbox"] {
  width: 1.2rem;
  height: 1.2rem;
  flex-shrink: 0;
  opacity: 0.5;
  cursor: not-allowed;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
}

.item-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  flex-wrap: wrap;
}

.item-text {
  font-size: 0.95rem;
  color: var(--text-dark);
  word-wrap: break-word;
  flex: 1;
  min-width: 150px;
}

.item-scope {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.scope-pill {
  display: inline-block;
  padding: 0.2rem 0.5rem;
  background: var(--info-bg);
  color: var(--info-blue);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
}

.scope-pill.new-pill {
  background: var(--success-bg-light);
  color: var(--success-dark);
}

.remove-item-btn {
  background: none;
  border: none;
  color: var(--danger);
  font-size: 1.2rem;
  line-height: 1;
  cursor: pointer;
  padding: 0 0.25rem;
}

.remove-item-btn:hover {
  color: var(--danger-hover);
}

.item-context {
  font-size: 0.85rem;
  color: var(--brand-primary);
  font-weight: 500;
}
</style>
