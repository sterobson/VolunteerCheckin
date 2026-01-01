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
              <option value="Urgent">Urgent</option>
              <option value="High">High</option>
              <option value="Normal">Normal</option>
              <option value="Low">Low</option>
            </select>
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
    title="Validation Error"
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
});

const isEditing = computed(() => !!props.note);

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
      };
    }

    emit('update:isDirty', false);
  }
});

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
  };

  emit('save', data);
};

const handleDelete = () => {
  emit('delete', props.note.noteId);
};
</script>

<style scoped>
.tab-headers {
  display: flex;
  gap: 0;
  border-bottom: 2px solid #e0e0e0;
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
  color: #666;
  transition: all 0.2s;
  margin-bottom: -2px;
}

.tab-header:hover {
  color: #333;
  background: #f8f9fa;
}

.tab-header.active {
  color: #007bff;
  border-bottom-color: #007bff;
  background: transparent;
}

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
textarea,
select {
  padding: 0.6rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
}

textarea {
  resize: vertical;
  min-height: 120px;
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
