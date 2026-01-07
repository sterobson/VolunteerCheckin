<template>
  <BaseModal
    :show="show"
    :title="isEditing ? `Edit checklist item` : 'Create checklist item'"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tab Headers -->
    <div class="tab-headers">
      <button
        type="button"
        class="tab-header"
        :class="{ active: activeTab === 'details' }"
        @click="activeTab = 'details'"
      >
        Details
      </button>
      <button
        type="button"
        class="tab-header"
        :class="{ active: activeTab === 'visibility' }"
        @click="activeTab = 'visibility'"
      >
        Visibility
      </button>
    </div>

    <form @submit.prevent="handleSave" class="checklist-form">
      <!-- Details Tab -->
      <div v-show="activeTab === 'details'" class="tab-content">
        <div class="form-group">
          <label for="text">Checklist text: *</label>
          <textarea
            id="text"
            v-model="form.text"
            @input="handleInput"
            required
            rows="3"
            :placeholder="form.createSeparateItems ? 'Enter checklist items, one per line...' : 'Enter the checklist item text...'"
          />
          <label v-if="!isEditing" class="checkbox-label-inline create-separate-items">
            <input
              type="checkbox"
              v-model="form.createSeparateItems"
              @change="handleInput"
            />
            <span>Each line creates a new item</span>
          </label>
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
          @user-changed="handleInput"
        />
      </div>
    </form>

    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isEditing"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete
        </button>
        <button
          v-if="!isEditing && activeTab === 'details'"
          type="button"
          @click="goToVisibilityTab"
          class="btn btn-primary"
        >
          Next
        </button>
        <button
          v-if="isEditing || activeTab === 'visibility'"
          type="button"
          @click="handleSave"
          class="btn btn-primary"
        >
          {{ isEditing ? 'Save changes' : 'Create' }}
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
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';
import InfoModal from '../../InfoModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  checklistItem: {
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
  text: '',
  scopeConfigurations: [{
    scope: 'Everyone',
    itemType: null,
    ids: []
  }],
  displayOrder: 0,
  isRequired: true,
  visibleFrom: null,
  visibleUntil: null,
  mustCompleteBy: null,
  createSeparateItems: true,
});

const isEditing = computed(() => !!props.checklistItem);

watch(() => props.show, (newVal) => {
  if (newVal) {
    // Set tab based on initialTab prop when opening modal
    activeTab.value = props.initialTab;

    if (props.checklistItem) {
      form.value = {
        text: props.checklistItem.text || '',
        scopeConfigurations: props.checklistItem.scopeConfigurations
          ? JSON.parse(JSON.stringify(props.checklistItem.scopeConfigurations))
          : [{
              scope: 'Everyone',
              itemType: null,
              ids: []
            }],
        displayOrder: props.checklistItem.displayOrder || 0,
        isRequired: props.checklistItem.isRequired !== false,
        visibleFrom: props.checklistItem.visibleFrom ? formatDateTimeForInput(props.checklistItem.visibleFrom) : null,
        visibleUntil: props.checklistItem.visibleUntil ? formatDateTimeForInput(props.checklistItem.visibleUntil) : null,
        mustCompleteBy: props.checklistItem.mustCompleteBy ? formatDateTimeForInput(props.checklistItem.mustCompleteBy) : null,
        createSeparateItems: false, // Not used when editing
      };
    } else {
      form.value = {
        text: '',
        scopeConfigurations: [{
          scope: 'Everyone',
          itemType: null,
          ids: []
        }],
        displayOrder: 0,
        isRequired: true,
        visibleFrom: null,
        visibleUntil: null,
        mustCompleteBy: null,
        createSeparateItems: true, // Default to true for new items
      };
    }

    // Reset isDirty to false
    emit('update:isDirty', false);
  }
});

const formatDateTimeForInput = (dateString) => {
  if (!dateString) return null;
  const date = new Date(dateString);
  const offset = date.getTimezoneOffset();
  const localDate = new Date(date.getTime() - offset * 60 * 1000);
  return localDate.toISOString().slice(0, 16);
};

const parseDateTime = (inputValue) => {
  if (!inputValue) return null;
  return new Date(inputValue).toISOString();
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleClose = () => {
  emit('close');
};

const goToVisibilityTab = () => {
  activeTab.value = 'visibility';
};

const showError = (message) => {
  errorMessage.value = message;
  showErrorModal.value = true;
};

const handleSave = () => {
  // Validate text
  if (!form.value.text || !form.value.text.trim()) {
    showError('Please enter checklist item text');
    return;
  }

  // Validate scope configurations
  if (!form.value.scopeConfigurations || form.value.scopeConfigurations.length === 0) {
    showError('Please select at least one scope');
    return;
  }

  // Validate that at least one scope has valid configuration
  const hasValidScope = form.value.scopeConfigurations.some(config => {
    // SpecificPeople, EveryoneInAreas, etc. must have IDs
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
    text: form.value.text,
    scopeConfigurations: form.value.scopeConfigurations,
    displayOrder: form.value.displayOrder || 0,
    isRequired: form.value.isRequired,
    visibleFrom: parseDateTime(form.value.visibleFrom),
    visibleUntil: parseDateTime(form.value.visibleUntil),
    mustCompleteBy: parseDateTime(form.value.mustCompleteBy),
  };

  // Only include createSeparateItems when creating new items
  if (!isEditing.value) {
    data.createSeparateItems = form.value.createSeparateItems;
  }

  emit('save', data);
};

const handleDelete = () => {
  emit('delete', props.checklistItem.itemId);
};
</script>

<style scoped>
.tab-headers {
  display: flex;
  gap: 0;
  border-bottom: 2px solid var(--border-light);
  margin-bottom: 1.5rem;
}

.tab-header {
  padding: 0.75rem 1.5rem;
  background: transparent;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 500;
  color: var(--text-secondary);
  transition: all 0.2s;
  margin-bottom: -2px;
}

.tab-header:hover {
  color: var(--text-dark);
  background: var(--bg-secondary);
}

.tab-header.active {
  color: var(--accent-primary);
  border-bottom-color: var(--accent-primary);
  background: transparent;
}

.tab-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.checklist-form {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1rem 0;
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
input[type="datetime-local"],
textarea,
select,
.search-input {
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
  min-height: 80px;
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

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  background: var(--bg-muted);
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
}

.checkbox-label-inline {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"],
.checkbox-label-inline input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
}

.create-separate-items {
  margin-top: 0.5rem;
  padding: 0.5rem;
  background: var(--info-bg);
  border-radius: 4px;
  border: 1px solid var(--accent-primary-light);
}

.search-input {
  width: 100%;
  margin-bottom: 0.5rem;
}

.custom-footer {
  display: flex;
  justify-content: space-between;
  width: 100%;
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

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
