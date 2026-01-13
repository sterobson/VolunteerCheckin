<template>
  <div class="scope-configuration-editor">
    <div class="editor-header">
      <h4>{{ effectiveHeaderText }}</h4>
    </div>

    <div class="scope-types-list">
      <div
        v-for="scopeType in displayedScopeTypes"
        :key="scopeType.scope"
        class="scope-type-row"
        :class="{ active: isScopeActive(scopeType.scope) }"
        @click="openScopeConfig(scopeType.scope)"
      >
        <div class="scope-type-label">{{ scopeType.label }}</div>
        <div class="scope-type-pill">{{ getScopePill(scopeType.scope) }}</div>
      </div>

      <div v-if="hasHiddenScopes" class="expand-row" @click="showAllScopes = !showAllScopes">
        <div class="expand-label">
          {{ showAllScopes ? 'Show less' : `Show ${hiddenScopeCount} more option${hiddenScopeCount !== 1 ? 's' : ''}...` }}
        </div>
        <div class="expand-icon">{{ showAllScopes ? '▲' : '▼' }}</div>
      </div>
    </div>

    <!-- Modal for configuring a specific scope -->
    <div v-if="editingScope" class="scope-config-modal" @click.self="closeConfigModal">
      <div class="modal-content">
        <div class="modal-header">
          <h5>{{ getScopeTypeLabel(editingScope) }}</h5>
          <button @click="closeConfigModal" class="btn-close" type="button">&times;</button>
        </div>

        <div class="modal-body">
          <p class="scope-help">{{ getScopeHelp(editingScope) }}</p>

          <!-- Marshal selection -->
          <div v-if="needsMarshalSelection(editingScope)" class="form-group">
            <label class="checkbox-label special-option">
              <input
                type="checkbox"
                :checked="isAllMarshalsSelected()"
                @change="toggleAllMarshals"
              />
              <span><strong>Everyone at the event</strong> (all current and future {{ termsLower.people }})</span>
            </label>

            <div v-if="!isAllMarshalsSelected()" class="specific-selection">
              <input
                v-model="marshalSearch"
                type="text"
                :placeholder="`Search ${termsLower.people}...`"
                class="search-input"
              />
              <div class="checkbox-group scrollable">
                <label
                  v-for="marshal in filteredMarshals"
                  :key="marshal.id"
                  class="checkbox-label"
                >
                  <input
                    type="checkbox"
                    :checked="isMarshalSelected(marshal.id)"
                    @change="toggleMarshal(marshal.id)"
                  />
                  <span>{{ marshal.name }}</span>
                </label>
              </div>
            </div>
          </div>

          <!-- Area selection scopes -->
          <div v-if="needsAreaSelection(editingScope)" class="form-group">
            <label class="checkbox-label special-option">
              <input
                type="checkbox"
                :checked="isAllAreasSelected()"
                @change="toggleAllAreas"
              />
              <span><strong>All {{ termsLower.areas }}</strong> (includes future {{ termsLower.areas }})</span>
            </label>

            <div v-if="!isAllAreasSelected()" class="specific-selection">
              <div class="checkbox-group scrollable">
                <label v-for="area in areas" :key="area.id" class="checkbox-label">
                  <input
                    type="checkbox"
                    :checked="isAreaSelected(area.id)"
                    @change="toggleArea(area.id)"
                  />
                  <span class="area-color-dot" :style="{ backgroundColor: area.color || '#667eea' }"></span>
                  <span>{{ area.name }}</span>
                </label>
              </div>
            </div>
          </div>

          <!-- Checkpoint selection scopes -->
          <div v-if="needsCheckpointSelection(editingScope)" class="form-group">
            <label class="checkbox-label special-option">
              <input
                type="checkbox"
                :checked="isAllCheckpointsSelected()"
                @change="toggleAllCheckpoints"
              />
              <span><strong>All {{ termsLower.checkpoints }}</strong> (includes future {{ termsLower.checkpoints }})</span>
            </label>

            <div v-if="!isAllCheckpointsSelected()" class="specific-selection">
              <input
                v-model="checkpointSearch"
                type="text"
                :placeholder="`Search ${termsLower.checkpoints}...`"
                class="search-input"
              />
              <div class="checkbox-group scrollable">
                <!-- This checkpoint option when creating a new checkpoint -->
                <label
                  v-if="showThisCheckpointOption"
                  class="checkbox-label this-checkpoint-option"
                >
                  <input
                    type="checkbox"
                    :checked="isCheckpointSelected('THIS_CHECKPOINT')"
                    @change="toggleCheckpoint('THIS_CHECKPOINT')"
                  />
                  <div class="checkpoint-info">
                    <span class="checkpoint-name"><strong>This {{ termsLower.checkpoint }}</strong></span>
                  </div>
                </label>
                <label
                  v-for="location in filteredCheckpoints"
                  :key="location.id"
                  class="checkbox-label"
                >
                  <input
                    type="checkbox"
                    :checked="isCheckpointSelected(location.id)"
                    @change="toggleCheckpoint(location.id)"
                  />
                  <div class="checkpoint-info">
                    <span class="checkpoint-name">{{ location.name }}</span>
                    <span v-if="location.description" class="checkpoint-description"> - {{ location.description }}</span>
                  </div>
                </label>
              </div>
            </div>
          </div>

        </div>

        <div class="modal-footer">
          <button @click="clearScope" class="btn btn-secondary" type="button">Deselect all</button>
          <button @click="closeConfigModal" class="btn btn-primary" type="button">Done</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => []
  },
  areas: {
    type: Array,
    default: () => []
  },
  locations: {
    type: Array,
    default: () => []
  },
  marshals: {
    type: Array,
    default: () => []
  },
  isEditing: {
    type: Boolean,
    default: false
  },
  excludeScopes: {
    type: Array,
    default: () => []
  },
  headerText: {
    type: String,
    default: ''
  },
  intent: {
    type: String,
    default: 'completion', // 'completion' or 'location-update'
    validator: (value) => ['completion', 'location-update'].includes(value)
  },
  currentCheckpointId: {
    type: String,
    default: null
  }
});

const emit = defineEmits(['update:modelValue', 'user-changed']);

// Scope types with labels (in display order) - computed to use terminology
const scopeTypes = computed(() => [
  { scope: 'SpecificPeople', label: `Specific ${termsLower.value.people}` },
  { scope: 'EveryoneAtCheckpoints', label: `Every ${termsLower.value.person} at specified ${termsLower.value.checkpoints}` },
  { scope: 'OnePerCheckpoint', label: `One ${termsLower.value.person} at each specified ${termsLower.value.checkpoint}` },
  { scope: 'OnePerArea', label: `One ${termsLower.value.person} in specified ${termsLower.value.areas}` },
  { scope: 'EveryoneInAreas', label: `Everyone in specified ${termsLower.value.areas}` },
  { scope: 'EveryAreaLead', label: `Every ${termsLower.value.area} lead at specified ${termsLower.value.areas}` },
  { scope: 'OneLeadPerArea', label: `One lead per specified ${termsLower.value.area}` },
]);

// Local state for each scope type
const scopeStates = ref({
  SpecificPeople: { enabled: true, ids: ['ALL_MARSHALS'] },
  EveryoneInAreas: { enabled: false, ids: [] },
  EveryoneAtCheckpoints: { enabled: false, ids: [] },
  OnePerCheckpoint: { enabled: false, ids: [] },
  OnePerArea: { enabled: false, ids: [] },
  EveryAreaLead: { enabled: false, ids: [] },
  OneLeadPerArea: { enabled: false, ids: [] },
});

const editingScope = ref(null);
const checkpointSearch = ref('');
const marshalSearch = ref('');
// When creating, show all scopes by default. When editing, collapse to only show configured scopes.
const showAllScopes = ref(!props.isEditing);

let isInitializing = true;
let isSyncingFromParent = false;

// Filter out excluded scopes
const availableScopeTypes = computed(() => {
  return scopeTypes.value.filter(st => !props.excludeScopes.includes(st.scope));
});

// Computed properties for collapsible scope list
const displayedScopeTypes = computed(() => {
  if (showAllScopes.value) {
    return availableScopeTypes.value;
  }
  // Only show scopes that are active (have values)
  return availableScopeTypes.value.filter(st => isScopeActive(st.scope));
});

const hasHiddenScopes = computed(() => {
  const activeCount = availableScopeTypes.value.filter(st => isScopeActive(st.scope)).length;
  return activeCount < availableScopeTypes.value.length;
});

const hiddenScopeCount = computed(() => {
  const activeCount = availableScopeTypes.value.filter(st => isScopeActive(st.scope)).length;
  return availableScopeTypes.value.length - activeCount;
});

// Show "This checkpoint" option when creating a new checkpoint (no currentCheckpointId)
const showThisCheckpointOption = computed(() => {
  return !props.currentCheckpointId && props.intent === 'location-update';
});

// Initialize from modelValue
watch(() => props.modelValue, (newVal) => {
  if (!newVal) return;

  // Don't sync if we just emitted this change
  if (isSyncingFromParent) {
    isSyncingFromParent = false;
    return;
  }

  isInitializing = true;

  // Reset all states
  Object.keys(scopeStates.value).forEach(scope => {
    scopeStates.value[scope] = { enabled: false, ids: [] };
  });

  // Parse incoming configurations
  newVal.forEach(config => {
    // Handle legacy "Everyone" scope by converting to SpecificPeople with ALL_MARSHALS
    if (config.scope === 'Everyone') {
      scopeStates.value.SpecificPeople = {
        enabled: true,
        ids: ['ALL_MARSHALS']
      };
    }
    // Handle legacy "AreaLead" scope by converting to EveryAreaLead
    else if (config.scope === 'AreaLead') {
      scopeStates.value.EveryAreaLead = {
        enabled: true,
        ids: [...config.ids]
      };
    }
    else if (scopeStates.value[config.scope]) {
      scopeStates.value[config.scope] = {
        enabled: true,
        ids: [...config.ids]
      };
    }
  });

  isInitializing = false;
}, { immediate: true });

// Convert local state back to scopeConfigurations array
const buildScopeConfigurations = () => {
  const configs = [];

  Object.keys(scopeStates.value).forEach(scope => {
    const state = scopeStates.value[scope];
    if (state.enabled) {
      const config = {
        scope,
        itemType: getItemType(scope),
        ids: [...state.ids]
      };
      configs.push(config);
    }
  });

  return configs;
};

// Watch scope states and emit changes
watch(scopeStates, () => {
  if (isInitializing) return;

  // Auto-remove ALL_MARSHALS from SpecificPeople if any other scope is enabled
  const otherScopesEnabled = Object.keys(scopeStates.value).some(scope =>
    scope !== 'SpecificPeople' && scopeStates.value[scope].enabled
  );

  if (otherScopesEnabled && scopeStates.value.SpecificPeople?.ids.includes('ALL_MARSHALS')) {
    const allMarshalsIndex = scopeStates.value.SpecificPeople.ids.indexOf('ALL_MARSHALS');
    scopeStates.value.SpecificPeople.ids.splice(allMarshalsIndex, 1);

    // If no specific people left, disable the scope
    if (scopeStates.value.SpecificPeople.ids.length === 0) {
      scopeStates.value.SpecificPeople.enabled = false;
    }
  }

  isSyncingFromParent = true;
  const newConfigs = buildScopeConfigurations();
  emit('update:modelValue', newConfigs);
  emit('user-changed');
}, { deep: true });

// Helper functions
const getItemType = (scope) => {
  if (needsAreaSelection(scope)) return 'Area';
  if (needsCheckpointSelection(scope)) return 'Checkpoint';
  if (needsMarshalSelection(scope)) return 'Marshal';
  return null;
};

const needsAreaSelection = (scope) => {
  return scope === 'EveryoneInAreas' || scope === 'OnePerArea' || scope === 'EveryAreaLead' || scope === 'OneLeadPerArea';
};

const needsCheckpointSelection = (scope) => {
  return scope === 'EveryoneAtCheckpoints' || scope === 'OnePerCheckpoint';
};

const needsMarshalSelection = (scope) => {
  return scope === 'SpecificPeople';
};

const getScopeTypeLabel = (scope) => {
  const type = scopeTypes.value.find(t => t.scope === scope);
  return type ? type.label : scope;
};

// Computed header text based on intent
const effectiveHeaderText = computed(() => {
  if (props.headerText) return props.headerText;
  if (props.intent === 'location-update') {
    return 'Who can update this location?';
  }
  return `Who can see and complete this ${termsLower.value.checklist}?`;
});

const getScopeHelp = (scope) => {
  if (props.intent === 'location-update') {
    const locationUpdateHelp = {
      'SpecificPeople': `Select "Everyone" or choose specific ${termsLower.value.people} who can update the location`,
      'EveryoneAtCheckpoints': `Every ${termsLower.value.person} at the selected ${termsLower.value.checkpoints} can update the location`,
      'OnePerCheckpoint': `Any ${termsLower.value.person} at each selected ${termsLower.value.checkpoint} can update the location`,
      'OnePerArea': `Any ${termsLower.value.person} in each selected ${termsLower.value.area} can update the location`,
      'EveryoneInAreas': `Every ${termsLower.value.person} in the selected ${termsLower.value.areas} can update the location`,
      'EveryAreaLead': `Every ${termsLower.value.area} lead at the selected ${termsLower.value.areas} can update the location`,
      'OneLeadPerArea': `Any ${termsLower.value.area} lead at the selected ${termsLower.value.areas} can update the location`,
    };
    return locationUpdateHelp[scope] || '';
  }

  const helpText = {
    'SpecificPeople': `Select "Everyone at the event" or choose specific ${termsLower.value.people} who must complete this (personal completion)`,
    'EveryoneAtCheckpoints': `Every ${termsLower.value.person} at the selected ${termsLower.value.checkpoints} must complete this (personal completion)`,
    'OnePerCheckpoint': `One completion needed at each selected ${termsLower.value.checkpoint} (shared completion)`,
    'OnePerArea': `One completion needed in each selected ${termsLower.value.area} (shared completion)`,
    'EveryoneInAreas': `Every ${termsLower.value.person} in the selected ${termsLower.value.areas} must complete this (personal completion)`,
    'EveryAreaLead': `Every ${termsLower.value.area} lead at the selected ${termsLower.value.areas} must complete this individually (personal completion)`,
    'OneLeadPerArea': `One completion per ${termsLower.value.area}, only ${termsLower.value.area} leads can complete (shared among leads)`,
  };
  return helpText[scope] || '';
};

const isScopeActive = (scope) => {
  return scopeStates.value[scope]?.enabled || false;
};

const getScopePill = (scope) => {
  const state = scopeStates.value[scope];

  if (!state || !state.enabled) {
    return 'No';
  }

  const ids = state.ids || [];

  if (needsAreaSelection(scope)) {
    if (ids.includes('ALL_AREAS')) {
      return `All ${termsLower.value.areas}`;
    }
    const count = ids.length;
    return count > 0 ? `${count} ${count !== 1 ? termsLower.value.areas : termsLower.value.area}` : `Select ${termsLower.value.areas}...`;
  }

  if (needsCheckpointSelection(scope)) {
    if (ids.includes('ALL_CHECKPOINTS')) {
      return `All ${termsLower.value.checkpoints}`;
    }
    // Check if only "This checkpoint" is selected (for new checkpoints)
    if (ids.length === 1 && ids[0] === 'THIS_CHECKPOINT') {
      return `This ${termsLower.value.checkpoint}`;
    }
    const count = ids.length;
    return count > 0 ? `${count} ${count !== 1 ? termsLower.value.checkpoints : termsLower.value.checkpoint}` : `Select ${termsLower.value.checkpoints}...`;
  }

  if (needsMarshalSelection(scope)) {
    if (ids.includes('ALL_MARSHALS')) {
      return 'Everyone';
    }
    const count = ids.length;
    return count > 0 ? `${count} ${count !== 1 ? termsLower.value.people : termsLower.value.person}` : `Select ${termsLower.value.people}...`;
  }

  return 'Configured';
};

// Modal functions
const openScopeConfig = (scope) => {
  editingScope.value = scope;
  checkpointSearch.value = '';
  marshalSearch.value = '';
};

const closeConfigModal = () => {
  editingScope.value = null;
};

const clearScope = () => {
  if (editingScope.value) {
    scopeStates.value[editingScope.value] = { enabled: false, ids: [] };
  }
  closeConfigModal();
};

// Marshal functions
const isAllMarshalsSelected = () => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes('ALL_MARSHALS');
};

const toggleAllMarshals = () => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  if (isAllMarshalsSelected()) {
    state.ids = [];
    state.enabled = false;
  } else {
    state.ids = ['ALL_MARSHALS'];
    state.enabled = true;
  }
};

const isMarshalSelected = (marshalId) => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes(marshalId);
};

const toggleMarshal = (marshalId) => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  const index = state.ids.indexOf(marshalId);

  if (index >= 0) {
    state.ids.splice(index, 1);
    if (state.ids.length === 0) {
      state.enabled = false;
    }
  } else {
    state.ids.push(marshalId);
    state.enabled = true;
  }
};

const filteredMarshals = computed(() => {
  if (!marshalSearch.value) return props.marshals;

  const searchLower = marshalSearch.value.toLowerCase();
  return props.marshals.filter(marshal =>
    marshal.name.toLowerCase().includes(searchLower) ||
    (marshal.email && marshal.email.toLowerCase().includes(searchLower))
  );
});

// Area functions
const isAllAreasSelected = () => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes('ALL_AREAS');
};

const toggleAllAreas = () => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  if (isAllAreasSelected()) {
    state.ids = [];
    state.enabled = false;
  } else {
    state.ids = ['ALL_AREAS'];
    state.enabled = true;
  }
};

const isAreaSelected = (areaId) => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes(areaId);
};

const toggleArea = (areaId) => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  const index = state.ids.indexOf(areaId);

  if (index >= 0) {
    state.ids.splice(index, 1);
    if (state.ids.length === 0) {
      state.enabled = false;
    }
  } else {
    state.ids.push(areaId);
    state.enabled = true;
  }
};

// Checkpoint functions
const isAllCheckpointsSelected = () => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes('ALL_CHECKPOINTS');
};

const toggleAllCheckpoints = () => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  if (isAllCheckpointsSelected()) {
    state.ids = [];
    state.enabled = false;
  } else {
    state.ids = ['ALL_CHECKPOINTS'];
    state.enabled = true;
  }
};

const isCheckpointSelected = (checkpointId) => {
  return editingScope.value && scopeStates.value[editingScope.value]?.ids.includes(checkpointId);
};

const toggleCheckpoint = (checkpointId) => {
  if (!editingScope.value) return;

  const state = scopeStates.value[editingScope.value];
  const index = state.ids.indexOf(checkpointId);

  if (index >= 0) {
    state.ids.splice(index, 1);
    if (state.ids.length === 0) {
      state.enabled = false;
    }
  } else {
    state.ids.push(checkpointId);
    state.enabled = true;
  }
};

const filteredCheckpoints = computed(() => {
  let filtered = props.locations;

  if (checkpointSearch.value) {
    const searchLower = checkpointSearch.value.toLowerCase();
    filtered = filtered.filter(loc =>
      loc.name.toLowerCase().includes(searchLower) ||
      (loc.description && loc.description.toLowerCase().includes(searchLower))
    );
  }

  return [...filtered].sort((a, b) => alphanumericCompare(a.name, b.name));
});
</script>

<style scoped>
.scope-configuration-editor {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.editor-header h4 {
  margin: 0 0 0.5rem 0;
  color: var(--text-dark);
  font-size: 1rem;
}

.editor-header .help-text {
  margin: 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-style: italic;
}

.scope-types-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  overflow: hidden;
}

.scope-type-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  cursor: pointer;
  transition: background-color 0.15s;
  border-bottom: 1px solid var(--bg-hover);
}

.scope-type-row:last-child {
  border-bottom: none;
}

.scope-type-row:hover {
  background: var(--bg-secondary);
}

.scope-type-row.active {
  background: var(--info-bg);
}

.scope-type-row.active:hover {
  background: var(--accent-primary-light);
}

.scope-type-label {
  font-size: 0.9rem;
  color: var(--text-dark);
  font-weight: 500;
}

.scope-type-pill {
  padding: 0.25rem 0.75rem;
  background: var(--border-light);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.8rem;
  font-weight: 500;
  white-space: nowrap;
}

.scope-type-row.active .scope-type-pill {
  background: var(--brand-primary);
  color: var(--btn-primary-text);
}

.expand-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  background: var(--bg-secondary);
  cursor: pointer;
  transition: background-color 0.15s;
}

.expand-row:hover {
  background: var(--bg-tertiary);
}

.expand-label {
  font-size: 0.85rem;
  color: var(--brand-primary);
  font-weight: 500;
}

.expand-icon {
  font-size: 0.7rem;
  color: var(--brand-primary);
}

/* Modal styles */
.scope-config-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-content {
  background: var(--card-bg);
  border-radius: 8px;
  max-width: 600px;
  width: 100%;
  max-height: 80vh;
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-lg);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem;
  border-bottom: 1px solid var(--border-light);
}

.modal-header h5 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-dark);
}

.btn-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  color: var(--text-muted);
  cursor: pointer;
  padding: 0;
  width: 2rem;
  height: 2rem;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 0.15s;
}

.btn-close:hover {
  background: var(--bg-hover);
  color: var(--text-dark);
}

.modal-body {
  padding: 1.25rem;
  overflow-y: auto;
  flex: 1;
}

.scope-help {
  margin: 0 0 1rem 0;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-left: 3px solid var(--brand-primary);
  font-size: 0.85rem;
  color: var(--text-secondary);
  border-radius: 4px;
}

.scope-enabled-toggle {
  padding: 1rem;
  background: var(--bg-secondary);
  border-radius: 6px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  padding: 0.5rem;
  font-size: 0.9rem;
  border-radius: 4px;
  transition: background-color 0.15s;
}

.checkbox-label:hover {
  background: var(--bg-secondary);
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
}

.checkbox-label.special-option {
  background: var(--bg-secondary);
  border: 2px solid var(--brand-primary);
  margin-bottom: 1rem;
}

.checkbox-label.special-option:hover {
  background: var(--info-bg);
}

.specific-selection {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.search-input {
  padding: 0.6rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  background: var(--card-bg);
}

.checkbox-group.scrollable {
  max-height: 250px;
  overflow-y: auto;
}

.area-color-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.checkpoint-info {
  display: flex;
  flex-direction: row;
  align-items: baseline;
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.checkpoint-name {
  font-weight: 500;
  color: var(--text-dark);
  flex-shrink: 0;
}

.checkpoint-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-style: italic;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.checkbox-label.this-checkpoint-option {
  background: var(--warning-bg-light);
  border: 1px dashed var(--warning);
  border-radius: 4px;
  margin-bottom: 0.5rem;
}

.modal-footer {
  display: flex;
  justify-content: space-between;
  padding: 1rem 1.25rem;
  border-top: 1px solid var(--border-light);
  gap: 1rem;
}

.btn {
  padding: 0.6rem 1.5rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
