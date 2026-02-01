<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit contact' : 'Create contact'"
    size="medium"
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

    <form @submit.prevent="handleSave" class="contact-form">
      <!-- Details Tab -->
      <div v-show="activeTab === 'details'" class="tab-content">
        <!-- Show marshal link dropdown only when creating new and not pre-linked -->
        <div v-if="!isCreatingFromMarshal && !isEditingLinkedContact" class="form-group">
          <label for="marshalId">Link to {{ terms.person.toLowerCase() }}:</label>
          <select id="marshalId" v-model="form.marshalId" @change="handleMarshalSelect">
            <option :value="null">-- Not linked --</option>
            <option v-for="marshal in sortedMarshals" :key="marshal.id" :value="marshal.id">
              {{ marshal.name }}
            </option>
          </select>
          <span class="help-text">Optionally link to an existing {{ terms.person.toLowerCase() }} in the system</span>
        </div>

        <!-- Show linked marshal info when editing a linked contact -->
        <div v-if="isEditingLinkedContact" class="form-group">
          <label>Linked {{ terms.person.toLowerCase() }}:</label>
          <div class="linked-marshal-info">
            <span class="linked-marshal-name">{{ linkedMarshalName }}</span>
            <span class="linked-marshal-hint">Contact details are synced with this {{ terms.person.toLowerCase() }}</span>
          </div>
        </div>

        <div v-if="!isLinkedToMarshal" class="form-group">
          <label for="name">Name: *</label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            @input="handleInput"
            placeholder="Contact name..."
          />
        </div>

        <div class="form-group">
          <label>Roles: *</label>
          <!-- Selected roles as removable pills -->
          <div v-if="form.roles.length > 0" class="selected-roles">
            <span
              v-for="roleId in form.roles"
              :key="roleId"
              class="role-pill"
            >
              {{ getRoleName(roleId) }}
              <button
                type="button"
                class="remove-role-btn"
                @click="removeRole(roleId)"
                title="Remove role"
              >×</button>
            </span>
          </div>
          <!-- Role selector -->
          <div class="role-input-group">
            <select
              id="role"
              v-model="selectedRoleToAdd"
              @change="addSelectedRole"
            >
              <option value="" disabled>Add a role...</option>
              <option v-for="role in availableRoles" :key="role.roleId" :value="role.roleId">
                {{ role.name }}
              </option>
            </select>
          </div>
          <label class="checkbox-label-inline show-emergency-checkbox">
            <input
              type="checkbox"
              v-model="form.showInEmergencyInfo"
              @change="handleInput"
            />
            <span>Show in emergency info</span>
          </label>
        </div>

        <div class="form-row">
          <div class="form-group">
            <label for="phone">Phone:</label>
            <input
              id="phone"
              v-model="form.phone"
              type="tel"
              @input="handleInput"
              placeholder="Phone number..."
            />
          </div>

          <div class="form-group">
            <label for="email">Email:</label>
            <input
              id="email"
              v-model="form.email"
              type="email"
              @input="handleInput"
              placeholder="Email address..."
            />
          </div>
        </div>

        <div class="form-group">
          <label for="notes">Notes / Instructions:</label>
          <textarea
            id="notes"
            v-model="form.notes"
            @input="handleInput"
            rows="3"
            placeholder="Additional notes or instructions for when to contact..."
          />
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
          <span class="help-text">Pinned contacts always appear at the top</span>
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
          header-text="Who can see this contact?"
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
            v-if="!isEditing && activeTab === 'details'"
            type="button"
            @click="goToVisibilityTab"
            class="btn btn-secondary mobile-only"
          >
            Visibility...
          </button>
        </div>
        <button
          type="button"
          @click="handleSave"
          class="btn btn-success"
          :disabled="isEditing && !isDirty"
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
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import ScopeConfigurationEditor from '../ScopeConfigurationEditor.vue';
import InfoModal from '../../InfoModal.vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  contact: {
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
  availableRoles: {
    type: Object,
    default: () => ({ builtInRoles: [], customRoles: [] }),
  },
  roleDefinitions: {
    type: Array,
    default: () => [],
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
  preselectedArea: {
    type: Object,
    default: null,
  },
  prefilledName: {
    type: String,
    default: '',
  },
  prefilledMarshalId: {
    type: String,
    default: null,
  },
  createFromAreaContext: {
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
const selectedRoleToAdd = ref('');

const form = ref({
  roles: [],
  name: '',
  phone: '',
  email: '',
  notes: '',
  marshalId: null,
  showInEmergencyInfo: false,
  displayOrder: 0,
  isPinned: false,
  scopeConfigurations: [{
    scope: 'SpecificPeople',
    itemType: 'Marshal',
    ids: ['ALL_MARSHALS']
  }],
});

// Sort roles: built-in first, then by displayOrder, then alphabetically
const sortedRoleDefinitions = computed(() => {
  return [...props.roleDefinitions].sort((a, b) => {
    // Built-in first
    if (a.isBuiltIn !== b.isBuiltIn) {
      return a.isBuiltIn ? -1 : 1;
    }
    // Then by display order
    if (a.displayOrder !== b.displayOrder) {
      return (a.displayOrder || 0) - (b.displayOrder || 0);
    }
    // Then alphabetically
    return (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

// Filter out already selected roles from available options
const availableRoles = computed(() => {
  const selectedIds = form.value.roles || [];
  return sortedRoleDefinitions.value.filter(r => !selectedIds.includes(r.roleId));
});

// Get role name from roleId
const getRoleName = (roleId) => {
  const role = props.roleDefinitions.find(r => r.roleId === roleId);
  return role?.name || roleId;
};

// Helper to check if a roleId refers to the EmergencyContact role
const isEmergencyRole = (roleId) => {
  const role = props.roleDefinitions.find(r => r.roleId === roleId);
  return role?.name === 'EmergencyContact';
};

// Role management functions
const addSelectedRole = () => {
  if (selectedRoleToAdd.value && !form.value.roles.includes(selectedRoleToAdd.value)) {
    form.value.roles.push(selectedRoleToAdd.value);
    // Auto-enable emergency info if adding EmergencyContact role
    if (isEmergencyRole(selectedRoleToAdd.value)) {
      form.value.showInEmergencyInfo = true;
    }
    handleInput();
  }
  selectedRoleToAdd.value = '';
};

const removeRole = (roleId) => {
  const index = form.value.roles.indexOf(roleId);
  if (index > -1) {
    form.value.roles.splice(index, 1);
    // If removing the last EmergencyContact role, disable emergency info
    if (isEmergencyRole(roleId) && !form.value.roles.some(r => isEmergencyRole(r))) {
      form.value.showInEmergencyInfo = false;
    }
    handleInput();
  }
};

const isEditing = computed(() => !!props.contact);

// Hide visibility tab when creating from area context (scope set when area is saved)
const tabs = computed(() => {
  if (props.createFromAreaContext) {
    return [{ value: 'details', label: 'Details' }];
  }
  return [
    { value: 'details', label: 'Details' },
    { value: 'visibility', label: 'Visibility' },
  ];
});

const isLinkedToMarshal = computed(() => !!form.value.marshalId);

// Check if this is a "create from marshal" flow (marshal was pre-selected from previous modal)
const isCreatingFromMarshal = computed(() => !isEditing.value && !!props.prefilledMarshalId);

// Check if editing an existing contact that is linked to a marshal
const isEditingLinkedContact = computed(() => isEditing.value && !!props.contact?.marshalId);

// Get the name of the linked marshal for display
const linkedMarshalName = computed(() => {
  if (!props.contact?.marshalId) return '';
  const marshal = props.marshals.find(m => m.id === props.contact.marshalId);
  return marshal?.name || 'Unknown';
});

const sortedMarshals = computed(() => {
  return [...props.marshals].sort((a, b) => alphanumericCompare(a.name, b.name));
});

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = props.initialTab;
    selectedRoleToAdd.value = '';

    if (props.contact) {
      // Get roles from contact - should be array of roleIds now
      let contactRoles = [];
      if (props.contact.roles && Array.isArray(props.contact.roles)) {
        contactRoles = [...props.contact.roles];
      }

      form.value = {
        roles: contactRoles,
        name: props.contact.name || '',
        phone: props.contact.phone || '',
        email: props.contact.email || '',
        notes: props.contact.notes || '',
        marshalId: props.contact.marshalId || null,
        showInEmergencyInfo: props.contact.showInEmergencyInfo ?? contactRoles.some(r => isEmergencyRole(r)),
        displayOrder: props.contact.displayOrder || 0,
        isPinned: props.contact.isPinned || false,
        scopeConfigurations: props.contact.scopeConfigurations
          ? JSON.parse(JSON.stringify(props.contact.scopeConfigurations))
          : [{
              scope: 'SpecificPeople',
              itemType: 'Marshal',
              ids: ['ALL_MARSHALS']
            }],
      };
    } else {
      // Determine default scope configuration:
      // - If creating from area context, use empty scope (area scope added when area is saved)
      // - If preselected area, pre-configure for that area
      // - Otherwise, default to all marshals
      let defaultScopeConfig;
      if (props.createFromAreaContext) {
        // Empty scope - will be set when area is saved
        defaultScopeConfig = [];
      } else if (props.preselectedArea) {
        defaultScopeConfig = [{
          scope: 'EveryoneInAreas',
          itemType: 'Area',
          ids: [props.preselectedArea.id]
        }];
      } else {
        defaultScopeConfig = [{
          scope: 'SpecificPeople',
          itemType: 'Marshal',
          ids: ['ALL_MARSHALS']
        }];
      }

      // Check if we have a prefilled marshal - get their details including roles
      let prefilledDetails = { name: props.prefilledName || '', phone: '', email: '', roles: [] };
      if (props.prefilledMarshalId) {
        const marshal = props.marshals.find(m => m.id === props.prefilledMarshalId);
        if (marshal) {
          prefilledDetails = {
            name: marshal.name || '',
            phone: marshal.phoneNumber || '',
            email: marshal.email || '',
            roles: marshal.roles || [],
          };
        }
      }

      form.value = {
        roles: [...prefilledDetails.roles],
        name: prefilledDetails.name,
        phone: prefilledDetails.phone,
        email: prefilledDetails.email,
        notes: '',
        marshalId: props.prefilledMarshalId || null,
        showInEmergencyInfo: prefilledDetails.roles.some(r => isEmergencyRole(r)),
        displayOrder: 0,
        isPinned: false,
        scopeConfigurations: defaultScopeConfig,
      };
    }

    emit('update:isDirty', false);
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleMarshalSelect = () => {
  // When a marshal is selected, auto-fill contact details and sync roles
  if (form.value.marshalId) {
    const marshal = props.marshals.find(m => m.id === form.value.marshalId);
    if (marshal) {
      // Always set name from linked marshal
      form.value.name = marshal.name;
      // Only auto-fill phone/email if fields are empty
      if (!form.value.phone && marshal.phoneNumber) form.value.phone = marshal.phoneNumber;
      if (!form.value.email && marshal.email) form.value.email = marshal.email;
      // Sync roles from linked marshal
      form.value.roles = marshal.roles ? [...marshal.roles] : [];
      // Update emergency info flag based on roles
      form.value.showInEmergencyInfo = form.value.roles.some(r => isEmergencyRole(r));
    }
  } else {
    // When unlinked, clear the name (user must provide it)
    form.value.name = '';
  }
  handleInput();
};

const handleClose = () => {
  emit('close');
};

const goToVisibilityTab = () => {
  // Validate details first
  if (!form.value.roles || form.value.roles.length === 0) {
    showError('Please select or enter at least one role');
    return;
  }
  if (!form.value.name || !form.value.name.trim()) {
    showError('Please enter a contact name');
    return;
  }

  activeTab.value = 'visibility';
};

const showError = (message) => {
  errorMessage.value = message;
  showErrorModal.value = true;
};

const handleSave = () => {
  if (!form.value.roles || form.value.roles.length === 0) {
    showError('Please select or enter at least one role');
    return;
  }

  if (!form.value.name || !form.value.name.trim()) {
    showError('Please enter a contact name');
    return;
  }

  // Skip scope validation when creating from area context (scope will be set when area is saved)
  if (!props.createFromAreaContext) {
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
  }

  const data = {
    roles: form.value.roles,
    name: form.value.name.trim(),
    phone: form.value.phone?.trim() || null,
    email: form.value.email?.trim() || null,
    notes: form.value.notes?.trim() || null,
    marshalId: form.value.marshalId || null,
    showInEmergencyInfo: form.value.showInEmergencyInfo,
    displayOrder: form.value.displayOrder || 0,
    isPinned: form.value.isPinned || false,
    scopeConfigurations: form.value.scopeConfigurations,
  };

  emit('save', data);
};

const handleDelete = () => {
  emit('delete', props.contact.contactId);
};
</script>

<style scoped>
.tab-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.contact-form {
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
input[type="tel"],
input[type="email"],
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

input.input-disabled,
input:disabled {
  background-color: var(--input-disabled-bg);
  color: var(--text-secondary);
  cursor: not-allowed;
}

textarea {
  resize: vertical;
  min-height: 80px;
}

select {
  background: var(--input-bg);
  cursor: pointer;
}

.selected-roles {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.role-pill {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.35rem 0.5rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 16px;
  font-size: 0.85rem;
  font-weight: 500;
}

.remove-role-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 18px;
  height: 18px;
  padding: 0;
  margin-left: 0.25rem;
  background: rgba(255, 255, 255, 0.2);
  border: none;
  border-radius: 50%;
  color: white;
  font-size: 14px;
  line-height: 1;
  cursor: pointer;
  transition: background 0.2s;
}

.remove-role-btn:hover {
  background: rgba(255, 255, 255, 0.4);
}

.role-input-group {
  display: flex;
  gap: 0.5rem;
}

.role-input-group select {
  flex: 1;
}

.help-text {
  font-size: 0.8rem;
  color: var(--text-secondary);
  font-style: italic;
}

.linked-marshal-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.75rem;
  background: var(--bg-tertiary);
  border-radius: 6px;
  border: 1px solid var(--border-color);
}

.linked-marshal-name {
  font-weight: 600;
  color: var(--text-primary);
}

.linked-marshal-hint {
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
  width: 100%;
  gap: 1rem;
}

.footer-left {
  display: flex;
  gap: 0.5rem;
}

.mobile-only {
  display: none;
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

.btn-success {
  background: var(--success);
  color: white;
}

.btn-success:hover:not(:disabled) {
  background: var(--success-hover);
}

.btn-success:disabled {
  opacity: 0.6;
  cursor: not-allowed;
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

  .role-input-group {
    flex-direction: column;
  }

  .mobile-only {
    display: inline-block;
  }
}
</style>
