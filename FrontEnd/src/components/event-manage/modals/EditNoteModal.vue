<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit note' : 'Create note'"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tab Headers -->
    <template #tab-header>
      <TabHeader
        v-model="activeTab"
        :tabs="tabs"
      />
    </template>

    <form @submit.prevent="handleSave" class="note-form">
      <!-- Details Tab -->
      <div v-show="activeTab === 'details'" class="tab-content">
        <div class="form-group">
          <label for="title">Title: *</label>
          <input
            id="title"
            v-model="form.title"
            type="text"
            @input="handleInput"
            required
            placeholder="Enter note title..."
          />
        </div>

        <div class="form-group">
          <label for="content">Content:</label>
          <textarea
            id="content"
            v-model="form.content"
            @input="handleInput"
            rows="6"
            placeholder="Enter note content (supports markdown)..."
          />
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="priority">Priority:</label>
            <select id="priority" v-model="form.priority" @change="handleInput">
              <option value="Emergency">Emergency</option>
              <option value="Urgent">Urgent</option>
              <option value="High">High</option>
              <option value="Normal">Normal</option>
              <option value="Low">Low</option>
            </select>
            <label class="checkbox-label-inline show-emergency-checkbox">
              <input
                type="checkbox"
                v-model="form.showInEmergencyInfo"
                @change="handleInput"
              />
              <span>Show in emergency info</span>
            </label>
          </div>

          <div class="form-group">
            <label for="category">Category:</label>
            <input
              id="category"
              v-model="form.category"
              type="text"
              @input="handleInput"
              placeholder="e.g., Safety, Logistics..."
            />
          </div>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="displayOrder">Display order:</label>
            <input
              id="displayOrder"
              v-model.number="form.displayOrder"
              type="number"
              @input="handleInput"
              min="0"
            />
            <span class="help-text">Lower numbers appear first</span>
          </div>

          <div class="form-group checkbox-group-inline">
            <label class="checkbox-label-inline">
              <input
                type="checkbox"
                v-model="form.isPinned"
                @change="handleInput"
              />
              <span>Pin to top</span>
            </label>
            <span class="help-text">Pinned notes always appear at the top</span>
          </div>
        </div>

      </div>

      <!-- Visibility Tab -->
      <div v-show="activeTab === 'visibility'" class="tab-content">
        <ScopeConfigurationEditor
          v-model="form.scopeConfigurations"
          :areas="areas"
          :locations="locations"
          :marshals="marshals"
          :is-editing="isEditing"
          :exclude-scopes="['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea']"
          header-text="Who can see this note?"
          @user-changed="handleInput"
        />
      </div>
    </form>

    <template #footer>
      <div class="custom-footer">
        <div class="footer-left">
          <button
            v-if="isEditing"
            type="button"
            @click="handleDelete"
            class="btn btn-danger"
          >
            Delete
          </button>
          <button
            v-if="!isEditing && !isLastTab"
            type="button"
            @click="goToNextTab"
            class="btn btn-secondary mobile-only"
          >
            {{ nextTabLabel }}...
          </button>
        </div>
        <button type="button" @click="handleSave" class="btn btn-success">
          Save
        </button>
      </div>
    </template>
  </BaseModal>

  <InfoModal
    :show="showErrorModal"
    title="Validation error"
    :message="errorMessage"
    @close="showErrorModal = false"
  />
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';
import InfoModal from '../../InfoModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  note: {
    type: Object,
    default: null,
  },
  initialTab: {
    type: String,
    default: 'details',
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'update:isDirty',
]);

const showErrorModal = ref(false);
const errorMessage = ref('');
const activeTab = ref('details');

const form = ref({
  title: '',
  content: '',
  scopeConfigurations: [{
    scope: 'SpecificPeople',
    itemType: 'Marshal',
    ids: ['ALL_MARSHALS']
  }],
  displayOrder: 0,
  priority: 'Normal',
  category: '',
  isPinned: false,
  showInEmergencyInfo: false,
});

// Helper to check if a priority should default to showing in emergency info
const isEmergencyPriority = (priority) => {
  return priority === 'Urgent' || priority === 'Emergency';
};

const isEditing = computed(() => !!props.note);

const tabs = [
  { value: 'details', label: 'Details' },
  { value: 'visibility', label: 'Visibility' },
];

const currentTabIndex = computed(() => tabs.findIndex(t => t.value === activeTab.value));
const isLastTab = computed(() => currentTabIndex.value === tabs.length - 1);
const nextTabLabel = computed(() => {
  if (isLastTab.value) return '';
  return tabs[currentTabIndex.value + 1]?.label || '';
});

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = props.initialTab;

    if (props.note) {
      form.value = {
        title: props.note.title || '',
        content: props.note.content || '',
        scopeConfigurations: props.note.scopeConfigurations
          ? JSON.parse(JSON.stringify(props.note.scopeConfigurations))
          : [{
              scope: 'SpecificPeople',
              itemType: 'Marshal',
              ids: ['ALL_MARSHALS']
            }],
        displayOrder: props.note.displayOrder || 0,
        priority: props.note.priority || 'Normal',
        category: props.note.category || '',
        isPinned: props.note.isPinned || false,
        showInEmergencyInfo: props.note.showInEmergencyInfo ?? isEmergencyPriority(props.note.priority),
      };
    } else {
      form.value = {
        title: '',
        content: '',
        scopeConfigurations: [{
          scope: 'SpecificPeople',
          itemType: 'Marshal',
          ids: ['ALL_MARSHALS']
        }],
        displayOrder: 0,
        priority: 'Normal',
        category: '',
        isPinned: false,
        showInEmergencyInfo: false,
      };
    }

    emit('update:isDirty', false);
  }
});

// Auto-update showInEmergencyInfo when priority changes to Urgent/Emergency (only when creating new)
watch(() => form.value.priority, (newPriority, oldPriority) => {
  // Only auto-update when creating new note
  if (!isEditing.value && newPriority !== oldPriority) {
    if (isEmergencyPriority(newPriority)) {
      form.value.showInEmergencyInfo = true;
    } else if (isEmergencyPriority(oldPriority)) {
      // If switching away from emergency priority, turn it off
      form.value.showInEmergencyInfo = false;
    }
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleClose = () => {
  emit('close');
};

const goToNextTab = () => {
  if (!isLastTab.value) {
    activeTab.value = tabs[currentTabIndex.value + 1].value;
  }
};

const showError = (message) => {
  errorMessage.value = message;
  showErrorModal.value = true;
};

const handleSave = () => {
  if (!form.value.title || !form.value.title.trim()) {
    showError('Please enter a note title');
    return;
  }

  if (!form.value.scopeConfigurations || form.value.scopeConfigurations.length === 0) {
    showError('Please select at least one visibility scope');
    return;
  }

  const hasValidScope = form.value.scopeConfigurations.some(config => {
    if (config.itemType !== null && (!config.ids || config.ids.length === 0)) {
      return false;
    }
    return true;
  });

  if (!hasValidScope) {
    showError('Please configure at least one scope with valid selections');
    return;
  }

  const data = {
    title: form.value.title.trim(),
    content: form.value.content || '',
    scopeConfigurations: form.value.scopeConfigurations,
    displayOrder: form.value.displayOrder || 0,
    priority: form.value.priority || 'Normal',
    category: form.value.category?.trim() || null,
    isPinned: form.value.isPinned || false,
    showInEmergencyInfo: form.value.showInEmergencyInfo,
  };

  emit('save', data);
};

const handleDelete = () => {
  emit('delete', props.note.noteId);
};
</script>

<style scoped>
.tab-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.note-form {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1rem 0;
  min-width: 450px;
}

@media (max-width: 520px) {
  .note-form {
    min-width: 0;
  }
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

label {
  font-weight: 500;
  color: var(--text-dark);
  font-size: 0.9rem;
}

input[type="text"],
input[type="number"],
textarea,
select {
  padding: 0.6rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
  background: var(--input-bg);
  color: var(--text-primary);
}

textarea {
  resize: vertical;
  min-height: 120px;
}

select {
  background: var(--input-bg);
  cursor: pointer;
}

.help-text {
  font-size: 0.8rem;
  color: var(--text-secondary);
  font-style: italic;
}

.checkbox-group-inline {
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.checkbox-label-inline {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: normal;
}

.checkbox-label-inline input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
}

.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.footer-left {
  display: flex;
  gap: 0.5rem;
}

.mobile-only {
  display: none;
}

@media (max-width: 768px) {
  .mobile-only {
    display: inline-block;
  }
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

.btn-success {
  background: var(--success);
  color: white;
}

.btn-success:hover {
  background: var(--success-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

.show-emergency-checkbox {
  margin-top: 0.5rem;
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
