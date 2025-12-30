<template>
  <div class="scope-configuration-editor">
    <div class="editor-header">
      <h4>Who can see and complete this item?</h4>
      <p class="help-text">You can add multiple scope configurations for complex targeting scenarios.</p>
    </div>

    <div class="configurations-list">
      <div
        v-for="(config, index) in localConfigs"
        :key="index"
        class="configuration-item"
      >
        <div class="config-header">
          <strong>Scope {{ index + 1 }}</strong>
          <button
            v-if="localConfigs.length > 1"
            @click="removeConfiguration(index)"
            class="btn-remove"
            type="button"
          >
            Remove
          </button>
        </div>

        <div class="config-body">
          <div class="form-group">
            <label>Scope type:</label>
            <select v-model="config.scope" @change="handleScopeChange(index)">
              <option value="Everyone">Everyone at the event</option>
              <option value="EveryoneInAreas">Everyone in specific areas</option>
              <option value="EveryoneAtCheckpoints">Everyone at specific checkpoints</option>
              <option value="SpecificPeople">Specific people</option>
              <option value="OnePerCheckpoint">One person per checkpoint</option>
              <option value="OnePerArea">One person per area</option>
              <option value="AreaLead">Area leads only</option>
            </select>
            <small class="help-text">{{ getScopeHelp(config.scope) }}</small>
          </div>

          <!-- Area selection -->
          <div v-if="needsAreaSelection(config.scope)" class="form-group">
            <label>Select areas:</label>
            <div class="checkbox-group scrollable">
              <label v-for="area in areas" :key="area.id" class="checkbox-label">
                <input
                  type="checkbox"
                  :value="area.id"
                  :checked="config.ids.includes(area.id)"
                  @change="toggleId(index, area.id)"
                />
                <span class="area-color-dot" :style="{ backgroundColor: area.color || '#667eea' }"></span>
                <span>{{ area.name }}</span>
              </label>
            </div>
          </div>

          <!-- Checkpoint selection -->
          <div v-if="needsCheckpointSelection(config.scope)" class="form-group">
            <label>Select checkpoints:</label>
            <input
              v-model="checkpointSearches[index]"
              type="text"
              placeholder="Search checkpoints..."
              class="search-input"
            />
            <div class="checkbox-group scrollable">
              <label
                v-for="location in getFilteredLocations(index)"
                :key="location.id"
                class="checkbox-label"
              >
                <input
                  type="checkbox"
                  :value="location.id"
                  :checked="config.ids.includes(location.id)"
                  @change="toggleId(index, location.id)"
                />
                <div class="checkpoint-info">
                  <span class="checkpoint-name">{{ location.name }}</span>
                  <span v-if="location.description" class="checkpoint-description"> - {{ location.description }}</span>
                </div>
              </label>
            </div>
          </div>

          <!-- Marshal selection -->
          <div v-if="needsMarshalSelection(config.scope)" class="form-group">
            <label>Select people:</label>
            <input
              v-model="marshalSearches[index]"
              type="text"
              placeholder="Search marshals..."
              class="search-input"
            />
            <div class="checkbox-group scrollable">
              <label
                v-for="marshal in getFilteredMarshals(index)"
                :key="marshal.id"
                class="checkbox-label"
              >
                <input
                  type="checkbox"
                  :value="marshal.id"
                  :checked="config.ids.includes(marshal.id)"
                  @change="toggleId(index, marshal.id)"
                />
                <span>{{ marshal.name }}</span>
              </label>
            </div>
          </div>

          <div class="specificity-info">
            <small>Specificity level: {{ getSpecificity(config) }} ({{ getSpecificityLabel(config) }})</small>
          </div>
        </div>
      </div>
    </div>

    <button @click="addConfiguration" class="btn btn-secondary btn-add-scope" type="button">
      + Add another scope configuration
    </button>

    <div v-if="localConfigs.length > 1" class="most-specific-wins-info">
      <strong>Most Specific Wins:</strong> When a marshal matches multiple configurations, the most specific one determines the completion context.
      Marshal (1) > Checkpoint (2) > Area (3) > Everyone (4)
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';

const props = defineProps({
  modelValue: {
    type: Array,
    default: () => [{
      scope: 'Everyone',
      itemType: null,
      ids: []
    }]
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
  }
});

const emit = defineEmits(['update:modelValue']);

const localConfigs = ref([]);
const checkpointSearches = ref({});
const marshalSearches = ref({});

// Initialize local configs from prop
watch(() => props.modelValue, (newVal) => {
  if (newVal && newVal.length > 0) {
    // Deep clone to avoid mutation
    const newConfigs = JSON.parse(JSON.stringify(newVal));
    // Only update if actually different to prevent circular updates
    if (JSON.stringify(localConfigs.value) !== JSON.stringify(newConfigs)) {
      localConfigs.value = newConfigs;
    }
  }
}, { immediate: true });

// Emit changes back to parent
watch(localConfigs, (newVal) => {
  // Only emit if different from prop value to prevent circular updates
  if (JSON.stringify(newVal) !== JSON.stringify(props.modelValue)) {
    emit('update:modelValue', newVal);
  }
}, { deep: true });

const needsAreaSelection = (scope) => {
  return scope === 'EveryoneInAreas' || scope === 'OnePerArea' || scope === 'AreaLead';
};

const needsCheckpointSelection = (scope) => {
  return scope === 'EveryoneAtCheckpoints' || scope === 'OnePerCheckpoint';
};

const needsMarshalSelection = (scope) => {
  return scope === 'SpecificPeople';
};

const getScopeHelp = (scope) => {
  const helpText = {
    'Everyone': 'Every person at the event must complete this (personal completion)',
    'EveryoneInAreas': 'Every person in the selected areas must complete this (personal completion)',
    'EveryoneAtCheckpoints': 'Every person at the selected checkpoints must complete this (personal completion)',
    'SpecificPeople': 'Only the selected people must complete this (personal completion)',
    'OnePerCheckpoint': 'One completion needed per selected checkpoint (shared completion)',
    'OnePerArea': 'One completion needed per selected area (shared completion)',
    'AreaLead': 'Only area leads for the selected areas must complete this (shared per area)',
  };
  return helpText[scope] || '';
};

const handleScopeChange = (index) => {
  const config = localConfigs.value[index];

  // Set appropriate itemType based on scope
  if (config.scope === 'Everyone') {
    config.itemType = null;
  } else if (needsAreaSelection(config.scope)) {
    config.itemType = 'Area';
  } else if (needsCheckpointSelection(config.scope)) {
    config.itemType = 'Checkpoint';
  } else if (needsMarshalSelection(config.scope)) {
    config.itemType = 'Marshal';
  }

  // Clear IDs when scope changes
  config.ids = [];
};

const toggleId = (configIndex, id) => {
  const config = localConfigs.value[configIndex];
  const index = config.ids.indexOf(id);

  if (index >= 0) {
    config.ids.splice(index, 1);
  } else {
    config.ids.push(id);
  }
};

const getFilteredLocations = (index) => {
  const search = checkpointSearches.value[index];
  let filtered = props.locations;

  if (search) {
    const searchLower = search.toLowerCase();
    filtered = filtered.filter(loc =>
      loc.name.toLowerCase().includes(searchLower) ||
      (loc.description && loc.description.toLowerCase().includes(searchLower))
    );
  }

  // Sort using alphanumeric comparison
  return [...filtered].sort((a, b) => alphanumericCompare(a.name, b.name));
};

const getFilteredMarshals = (index) => {
  const search = marshalSearches.value[index];
  if (!search) return props.marshals;

  const searchLower = search.toLowerCase();
  return props.marshals.filter(marshal =>
    marshal.name.toLowerCase().includes(searchLower) ||
    (marshal.email && marshal.email.toLowerCase().includes(searchLower))
  );
};

const addConfiguration = () => {
  localConfigs.value.push({
    scope: 'Everyone',
    itemType: null,
    ids: []
  });
};

const removeConfiguration = (index) => {
  localConfigs.value.splice(index, 1);
};

const getSpecificity = (config) => {
  if (config.itemType === 'Marshal') return 1;
  if (config.itemType === 'Checkpoint') return 2;
  if (config.itemType === 'Area') return 3;
  return 4;
};

const getSpecificityLabel = (config) => {
  const labels = {
    1: 'Most specific - explicit person',
    2: 'High specificity - checkpoint level',
    3: 'Medium specificity - area level',
    4: 'Least specific - everyone'
  };
  return labels[getSpecificity(config)];
};
</script>

<style scoped>
.scope-configuration-editor {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.editor-header h4 {
  margin: 0 0 0.5rem 0;
  color: #333;
  font-size: 1rem;
}

.editor-header .help-text {
  margin: 0;
  font-size: 0.85rem;
  color: #666;
  font-style: italic;
}

.configurations-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.configuration-item {
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  padding: 1rem;
  background: #f9f9f9;
}

.config-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid #ddd;
}

.config-header strong {
  color: #667eea;
  font-size: 0.95rem;
}

.btn-remove {
  padding: 0.3rem 0.75rem;
  background: #dc3545;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  transition: background-color 0.2s;
}

.btn-remove:hover {
  background: #c82333;
}

.config-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.9rem;
}

select,
.search-input {
  padding: 0.6rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  background: white;
}

select {
  cursor: pointer;
}

.help-text {
  font-size: 0.8rem;
  color: #666;
  font-style: italic;
  margin-top: 0.25rem;
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  background: white;
}

.checkbox-group.scrollable {
  max-height: 200px;
  overflow-y: auto;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  padding: 0.25rem;
  font-size: 0.9rem;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
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
  color: #333;
  flex-shrink: 0;
}

.checkpoint-description {
  font-size: 0.85rem;
  color: #666;
  font-style: italic;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.specificity-info {
  padding: 0.5rem;
  background: #e3f2fd;
  border-radius: 4px;
  border: 1px solid #90caf9;
}

.specificity-info small {
  color: #1976d2;
  font-size: 0.8rem;
}

.btn-add-scope {
  padding: 0.75rem 1.25rem;
  background: #6c757d;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
  align-self: flex-start;
}

.btn-add-scope:hover {
  background: #545b62;
}

.most-specific-wins-info {
  padding: 1rem;
  background: #fff3cd;
  border: 1px solid #ffc107;
  border-radius: 4px;
  font-size: 0.85rem;
  color: #856404;
  line-height: 1.6;
}

.most-specific-wins-info strong {
  display: block;
  margin-bottom: 0.25rem;
  color: #856404;
}
</style>
