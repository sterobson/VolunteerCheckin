<template>
  <div class="notes-preview">
    <!-- Header with add button -->
    <div class="preview-actions">
      <p class="preview-header">
        {{ isEditing ? `Add new note for this ${checkpointTermLower}:` : `Notes that will be visible at this ${checkpointTermLower}:` }}
      </p>
      <button
        type="button"
        class="add-item-btn"
        @click="showAddForm = !showAddForm"
      >
        {{ showAddForm ? 'Cancel' : '+ Add note' }}
      </button>
    </div>

    <!-- Add new note form -->
    <div v-if="showAddForm" class="add-item-form">
      <div class="add-form-fields">
        <input
          v-model="newNoteTitle"
          type="text"
          placeholder="Note title..."
          class="add-item-input title-input"
        />
        <textarea
          v-model="newNoteContent"
          placeholder="Note content..."
          class="add-item-input content-input"
          rows="3"
        ></textarea>
      </div>
      <button
        type="button"
        class="save-item-btn"
        :disabled="!newNoteTitle.trim() && !newNoteContent.trim()"
        @click="addNewNote"
      >
        Add
      </button>
    </div>

    <!-- Pending new notes -->
    <div v-if="pendingNewNotes.length > 0" class="pending-items">
      <div
        v-for="(note, index) in pendingNewNotes"
        :key="`pending-${index}`"
        class="note-item pending"
      >
        <div class="note-header">
          <span class="note-title">{{ note.title || 'Untitled' }}</span>
          <div class="note-actions">
            <span class="scope-pill new-pill" title="Will be created when saved">
              New
            </span>
            <button
              type="button"
              class="remove-item-btn"
              @click="removeNewNote(index)"
              title="Remove"
            >
              &times;
            </button>
          </div>
        </div>
        <div v-if="note.content" class="note-content">{{ note.content }}</div>
      </div>
    </div>

    <!-- Empty state (only shown when creating, not editing) -->
    <div v-if="!isEditing && filteredNotes.length === 0 && pendingNewNotes.length === 0" class="empty-state">
      <p>No notes will apply based on the current {{ termsLower.area }} assignments.</p>
      <p v-if="pendingAreaIds.length === 0" class="help-text">
        Assign {{ termsLower.areas }} to see relevant notes.
      </p>
    </div>

    <!-- Existing notes that match (only shown when creating, not editing) -->
    <div v-if="!isEditing && filteredNotes.length > 0" class="notes-list">
      <div
        v-for="note in filteredNotes"
        :key="note.id"
        class="note-item"
      >
        <div class="note-header">
          <span class="note-title">{{ note.title || 'Untitled' }}</span>
          <span class="scope-pill" :title="getScopeTooltip(note)">
            {{ getScopeLabel(note) }}
          </span>
        </div>
        <div v-if="note.content" class="note-content">{{ note.content }}</div>
        <div v-if="getContextInfo(note)" class="note-context">
          {{ getContextInfo(note) }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import { useTerminology } from '../composables/useTerminology';

const { termsLower } = useTerminology();

const props = defineProps({
  allNotes: {
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

const emit = defineEmits(['update:modelValue', 'change']);

// Use prop if provided, otherwise fall back to global term
const checkpointTermLower = computed(() => {
  return props.checkpointTermSingular ? props.checkpointTermSingular.toLowerCase() : termsLower.value.checkpoint;
});

// Local state
const showAddForm = ref(false);
const newNoteTitle = ref('');
const newNoteContent = ref('');
const pendingNewNotes = ref([...props.modelValue]);

// Sync with v-model
watch(() => props.modelValue, (newVal) => {
  pendingNewNotes.value = [...newVal];
}, { deep: true });

const addNewNote = () => {
  if (!newNoteTitle.value.trim() && !newNoteContent.value.trim()) return;

  pendingNewNotes.value.push({
    title: newNoteTitle.value.trim(),
    content: newNoteContent.value.trim(),
    isNew: true,
  });

  newNoteTitle.value = '';
  newNoteContent.value = '';
  showAddForm.value = false;
  emitChanges();
};

const removeNewNote = (index) => {
  pendingNewNotes.value.splice(index, 1);
  emitChanges();
};

const emitChanges = () => {
  emit('update:modelValue', [...pendingNewNotes.value]);
  emit('change');
};

// Check if a scope configuration matches a new checkpoint with the given areas
const configMatchesCheckpoint = (config) => {
  const scope = config.scope;
  const itemType = config.itemType;
  const ids = config.ids || [];

  // EveryoneAtCheckpoints with Checkpoint itemType - ALL_CHECKPOINTS
  if (scope === 'EveryoneAtCheckpoints' && itemType === 'Checkpoint') {
    return ids.includes('ALL_CHECKPOINTS');
  }

  // Area-based scopes
  if (scope === 'EveryoneInAreas' && itemType === 'Area') {
    if (ids.includes('ALL_AREAS')) return true;
    return ids.some(id => props.pendingAreaIds.includes(id));
  }

  return false;
};

// Filter notes that would be visible at this checkpoint
const filteredNotes = computed(() => {
  return props.allNotes.filter(note => {
    // Check scopeConfigurations array
    if (note.scopeConfigurations && note.scopeConfigurations.length > 0) {
      return note.scopeConfigurations.some(config => configMatchesCheckpoint(config));
    }
    return false;
  });
});

const formatScope = (scope) => {
  const scopeMap = {
    'EveryoneAtCheckpoints': `All at ${termsLower.value.checkpoints}`,
    'EveryoneInAreas': `All in ${termsLower.value.areas}`,
  };
  return scopeMap[scope] || scope;
};

const getScopeLabel = (note) => {
  if (!note.scopeConfigurations || note.scopeConfigurations.length === 0) {
    return 'Unknown';
  }

  const matchingConfig = note.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) {
    return formatScope(note.scopeConfigurations[0].scope);
  }

  const ids = matchingConfig.ids || [];
  if (ids.includes('ALL_CHECKPOINTS')) return `All ${termsLower.value.checkpoints}`;
  if (ids.includes('ALL_AREAS')) return `All ${termsLower.value.areas}`;

  return formatScope(matchingConfig.scope);
};

const getScopeTooltip = (note) => {
  if (!note.scopeConfigurations || note.scopeConfigurations.length === 0) {
    return '';
  }

  const matchingConfig = note.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) return '';

  const tooltips = {
    'EveryoneAtCheckpoints': `This note is for everyone at specified ${termsLower.value.checkpoints}`,
    'EveryoneInAreas': `This note is for everyone in specified ${termsLower.value.areas}`,
  };
  return tooltips[matchingConfig.scope] || '';
};

const getContextInfo = (note) => {
  if (!note.scopeConfigurations || note.scopeConfigurations.length === 0) {
    return null;
  }

  const matchingConfig = note.scopeConfigurations.find(config => configMatchesCheckpoint(config));
  if (!matchingConfig) return null;

  const itemType = matchingConfig.itemType;
  const ids = matchingConfig.ids || [];

  if (ids.includes('ALL_CHECKPOINTS') || ids.includes('ALL_AREAS')) {
    return null;
  }

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
.notes-preview {
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
  align-items: flex-start;
}

.add-form-fields {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.add-item-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
}

.add-item-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.content-input {
  resize: vertical;
  min-height: 60px;
}

.save-item-btn {
  padding: 0.5rem 1rem;
  background: var(--success);
  color: var(--card-bg);
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  align-self: flex-start;
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

.notes-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.note-item {
  padding: 0.75rem;
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 6px;
}

.note-item.pending {
  background: var(--brand-primary-light);
  border-color: var(--brand-primary);
}

.note-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.note-title {
  font-weight: 600;
  color: var(--text-dark);
  flex: 1;
}

.note-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
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

.note-content {
  font-size: 0.9rem;
  color: var(--text-darker);
  line-height: 1.5;
  white-space: pre-wrap;
}

.note-context {
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: var(--brand-primary);
  font-weight: 500;
}
</style>
