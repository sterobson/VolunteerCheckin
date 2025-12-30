<template>
  <BaseModal
    :show="show"
    :title="isEditing ? `Edit checklist item` : 'Create checklist item'"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <form @submit.prevent="handleSave" class="checklist-form">
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

      <ScopeConfigurationEditor
        v-model="form.scopeConfigurations"
        :areas="areas"
        :locations="locations"
        :marshals="marshals"
      />
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
        <button type="button" @click="handleSave" class="btn btn-primary">
          {{ isEditing ? 'Save changes' : 'Create' }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  checklistItem: {
    type: Object,
    default: null,
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

// Flag to prevent marking form as dirty during initialization
let isInitializing = false;
let initialFormState = null;

watch(() => props.show, (newVal) => {
  if (newVal) {
    // Set initializing flag to prevent dirty state on form setup
    isInitializing = true;

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

    // Reset isDirty to false for clean state
    emit('update:isDirty', false);

    // Clear initializing flag after allowing child components to fully initialize
    setTimeout(() => {
      isInitializing = false;
      // Store the initial form state after initialization completes
      initialFormState = JSON.stringify(form.value);
    }, 100);
  }
});

// Watch for changes to scopeConfigurations to mark form as dirty
// Only after modal is open (skip initial setup)
watch(() => form.value.scopeConfigurations, () => {
  if (!isInitializing && props.show) {
    handleInput();
  }
}, { deep: true });

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
  // Only mark as dirty if form has actually changed from initial state
  if (initialFormState && JSON.stringify(form.value) !== initialFormState) {
    emit('update:isDirty', true);
  } else if (initialFormState) {
    emit('update:isDirty', false);
  }
};

const handleClose = () => {
  emit('close');
};

const handleSave = () => {
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
  color: #333;
  font-size: 0.9rem;
}

input[type="text"],
input[type="number"],
input[type="datetime-local"],
textarea,
select,
.search-input {
  padding: 0.6rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
}

textarea {
  resize: vertical;
  min-height: 80px;
}

select {
  background: white;
  cursor: pointer;
}

.help-text {
  font-size: 0.8rem;
  color: #666;
  font-style: italic;
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  background: #f9f9f9;
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
  background: #f0f7ff;
  border-radius: 4px;
  border: 1px solid #b3d9ff;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
  }
}
</style>
