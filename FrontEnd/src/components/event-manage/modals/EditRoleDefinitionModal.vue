<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit role' : 'Create role'"
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

    <form @submit.prevent="handleSave" class="role-form">
      <!-- Details Tab -->
      <div v-show="activeTab === 'details'" class="tab-content">
        <div class="form-group">
          <label for="name">Name: *</label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            @input="handleInput"
            required
            placeholder="Enter role name..."
          />
        </div>

        <div class="form-group">
          <label for="notes">Notes:</label>
          <textarea
            id="notes"
            v-model="form.notes"
            @input="handleInput"
            rows="6"
            placeholder="Enter role description or notes..."
          />
          <span class="help-text">Describe what this role is responsible for</span>
        </div>

        <div class="form-group checkbox-group">
          <label class="checkbox-label">
            <input
              type="checkbox"
              v-model="form.canManageAreaCheckpoints"
              @change="handleInput"
            />
            <span class="checkbox-text">Is {{ terms.area.toLowerCase() }} lead</span>
          </label>
          <span class="help-text">When enabled, {{ terms.people.toLowerCase() }} with this role can view all other {{ terms.checkpoints.toLowerCase() }} and {{ terms.people.toLowerCase() }} in their {{ terms.area.toLowerCase() }}.</span>
        </div>

      </div>

      <!-- Assignments Tab -->
      <div v-show="activeTab === 'assignments'" class="tab-content">
        <div class="assignments-header">
          <h4>Who has this role?</h4>
          <p class="help-text">Select the people who should have this role assigned.</p>
        </div>

        <div v-if="loadingPeople" class="loading-state">
          Loading people...
        </div>

        <div v-else-if="people.length === 0" class="empty-state">
          <p>No marshals or contacts to assign.</p>
        </div>

        <div v-else class="people-list">
          <div class="search-row">
            <input
              v-model="peopleSearchQuery"
              type="text"
              class="search-input"
              placeholder="Search by name..."
            />
            <button
              type="button"
              class="btn btn-secondary btn-sm"
              @click="toggleSelectAll"
            >
              {{ allSelected ? 'Deselect all' : 'Select all' }}
            </button>
          </div>

          <div class="people-items">
            <label
              v-for="person in filteredPeople"
              :key="person.id"
              class="person-item"
              :class="{ checked: person.hasRole }"
            >
              <input
                type="checkbox"
                :checked="person.hasRole"
                @change="togglePersonRole(person)"
              />
              <span class="person-name">{{ person.name }}</span>
              <template v-if="person.personType === 'Linked'">
                <span class="person-type-badge marshal">{{ terms.person }}</span>
                <span class="person-type-badge contact">Contact</span>
              </template>
              <span v-else class="person-type-badge" :class="person.personType.toLowerCase()">
                {{ formatPersonType(person.personType) }}
              </span>
            </label>
          </div>

          <div v-if="filteredPeople.length === 0" class="empty-search">
            No people match your search.
          </div>
        </div>
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
            :disabled="role?.usageCount > 0"
            :title="role?.usageCount > 0 ? 'Cannot delete role that is in use' : 'Delete this role'"
          >
            Delete
          </button>
        </div>
        <button
          type="button"
          @click="handleSave"
          class="btn btn-success"
        >
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
import { ref, computed, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import InfoModal from '../../InfoModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  role: {
    type: Object,
    default: null,
  },
  initialTab: {
    type: String,
    default: 'details',
  },
  people: {
    type: Array,
    default: () => [],
  },
  loadingPeople: {
    type: Boolean,
    default: false,
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
  'load-people',
  'toggle-person-role',
]);

const showErrorModal = ref(false);
const errorMessage = ref('');
const peopleSearchQuery = ref('');

// Track initial hasRole state for stable sorting (only update on reload)
const initialHasRoleMap = ref(new Map());

// Tab configuration
const tabs = [
  { value: 'details', label: 'Details' },
  { value: 'assignments', label: 'Assignments' },
];

const activeTab = ref(props.initialTab);

// Form data
const form = ref({
  name: '',
  notes: '',
  canManageAreaCheckpoints: false,
});

// Is editing vs creating
const isEditing = computed(() => !!props.role?.roleId);

// Reset form when modal opens or role changes
watch(() => [props.show, props.role], ([show, role]) => {
  if (show) {
    activeTab.value = props.initialTab;
    peopleSearchQuery.value = '';
    initialHasRoleMap.value = new Map(); // Reset so it captures fresh state when people are loaded

    if (role) {
      form.value = {
        name: role.name || '',
        notes: role.notes || '',
        canManageAreaCheckpoints: role.canManageAreaCheckpoints || false,
      };
    } else {
      form.value = {
        name: '',
        notes: '',
        canManageAreaCheckpoints: false,
      };
    }
    emit('update:isDirty', false);
  }
}, { immediate: true });

// Load people when switching to assignments tab for existing role
watch(activeTab, (newTab) => {
  if (newTab === 'assignments' && isEditing.value && props.people.length === 0) {
    emit('load-people', props.role.roleId);
  }
});

// Capture initial hasRole state when people are loaded (for stable sorting)
watch(() => props.people, (newPeople) => {
  if (newPeople.length > 0 && initialHasRoleMap.value.size === 0) {
    // Only set initial state when first loaded
    const map = new Map();
    for (const person of newPeople) {
      map.set(person.id, person.hasRole);
    }
    initialHasRoleMap.value = map;
  }
}, { immediate: true });

// Mark form as dirty on any input
const handleInput = () => {
  emit('update:isDirty', true);
};

// Format person type for display
const formatPersonType = (type) => {
  if (type === 'Linked') return 'Linked';
  if (type === 'Marshal') return terms.value.person;
  if (type === 'Contact') return 'Contact';
  return type;
};

// Filter and sort people - initially checked first (based on load state), then by name
const filteredPeople = computed(() => {
  let result = [...props.people];

  // Filter by search query if present
  if (peopleSearchQuery.value.trim()) {
    const query = peopleSearchQuery.value.toLowerCase();
    result = result.filter(p => p.name.toLowerCase().includes(query));
  }

  // Sort: use initial hasRole state for stable sorting (doesn't jump around while editing)
  return result.sort((a, b) => {
    const aInitiallyChecked = initialHasRoleMap.value.get(a.id) ?? false;
    const bInitiallyChecked = initialHasRoleMap.value.get(b.id) ?? false;
    if (aInitiallyChecked !== bInitiallyChecked) {
      return aInitiallyChecked ? -1 : 1;
    }
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });
});

// Check if all people are selected
const allSelected = computed(() => {
  return props.people.length > 0 && props.people.every(p => p.hasRole);
});

// Toggle select all / deselect all
const toggleSelectAll = () => {
  const shouldAdd = !allSelected.value;
  for (const person of props.people) {
    if (person.hasRole !== shouldAdd) {
      emit('toggle-person-role', {
        person,
        addRole: shouldAdd,
      });
    }
  }
};

// Toggle role assignment for a person
const togglePersonRole = (person) => {
  emit('toggle-person-role', {
    person,
    addRole: !person.hasRole,
  });
};

// Handle save
const handleSave = () => {
  // Validate
  if (!form.value.name.trim()) {
    errorMessage.value = 'Role name is required.';
    showErrorModal.value = true;
    return;
  }

  emit('save', {
    name: form.value.name.trim(),
    notes: form.value.notes.trim(),
    canManageAreaCheckpoints: form.value.canManageAreaCheckpoints,
  });
};

// Handle delete
const handleDelete = () => {
  if (props.role?.usageCount > 0) {
    errorMessage.value = 'Cannot delete a role that is still assigned to people. Remove the role from all people first.';
    showErrorModal.value = true;
    return;
  }
  emit('delete', props.role.roleId);
};

// Handle close
const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.role-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: min(500px, 90vw);
}

@media (min-width: 640px) {
  .role-form {
    min-width: 500px;
  }
}

.tab-content {
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
  color: var(--text-primary);
}

.form-group input,
.form-group textarea {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-group input:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.form-group textarea {
  resize: vertical;
  min-height: 100px;
}

.help-text {
  font-size: 0.8rem;
  color: var(--text-muted);
}

.checkbox-group {
  padding: 0.75rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.checkbox-label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: normal;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  margin-top: 2px;
  cursor: pointer;
  flex-shrink: 0;
}

.checkbox-text {
  color: var(--text-primary);
  font-weight: 500;
}

/* Assignments Tab */
.assignments-header {
  margin-bottom: 0.5rem;
}

.assignments-header h4 {
  margin: 0;
  color: var(--text-primary);
}

.loading-state,
.empty-state {
  text-align: center;
  padding: 2rem;
  color: var(--text-muted);
}

.people-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.search-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.search-input {
  flex: 1;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.btn-sm {
  padding: 0.4rem 0.75rem;
  font-size: 0.85rem;
  white-space: nowrap;
}

.people-items {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  max-height: 400px;
  overflow-y: auto;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 0.5rem;
}

.person-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.75rem;
  border-radius: 6px;
  cursor: pointer;
  transition: background-color 0.15s;
}

.person-item:hover {
  background: var(--bg-tertiary);
}

.person-item.checked {
  background: var(--success-bg);
}

.person-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.person-name {
  flex: 1;
  color: var(--text-primary);
}

.person-type-badge {
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.person-type-badge.marshal {
  background: var(--accent-primary-bg, rgba(59, 130, 246, 0.15));
  color: var(--accent-primary);
}

.person-type-badge.contact {
  background: var(--warning-bg);
  color: var(--warning-color);
}

.empty-search {
  text-align: center;
  padding: 1rem;
  color: var(--text-muted);
  font-style: italic;
}

/* Action buttons at bottom of form */
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding-top: 1.5rem;
  margin-top: 1rem;
  border-top: 1px solid var(--border-color);
}

/* Footer */
.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.footer-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.delete-warning {
  font-size: 0.8rem;
  color: var(--text-muted);
  font-style: italic;
}

</style>
