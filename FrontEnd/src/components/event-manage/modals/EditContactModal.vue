<template>
  <BaseModal
    :show="show"
    :title="isEditing ? 'Edit contact' : 'Create contact'"
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

    <form @submit.prevent="handleSave" class="contact-form">
      <!-- Details Tab -->
      <div v-show="activeTab === 'details'" class="tab-content">
        <div class="form-group">
          <label for="marshalId">Link to {{ terms.person.toLowerCase() }}:</label>
          <select id="marshalId" v-model="form.marshalId" @change="handleMarshalSelect">
            <option :value="null">-- Not linked --</option>
            <option v-for="marshal in sortedMarshals" :key="marshal.id" :value="marshal.id">
              {{ marshal.name }}
            </option>
          </select>
          <span class="help-text">Optionally link to an existing {{ terms.person.toLowerCase() }} in the system</span>
        </div>

        <div class="form-group">
          <label for="name">Name: *</label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            @input="handleInput"
            :disabled="isLinkedToMarshal"
            :class="{ 'input-disabled': isLinkedToMarshal }"
            placeholder="Contact name..."
          />
        </div>

        <div class="form-group">
          <label for="role">Role: *</label>
          <div class="role-input-group">
            <select
              v-if="!useCustomRole"
              id="role"
              v-model="form.role"
              @change="handleInput"
            >
              <option value="" disabled>Select a role...</option>
              <optgroup v-if="availableRoles.builtInRoles?.length > 0" label="Built-in Roles">
                <option v-for="role in availableRoles.builtInRoles" :key="role" :value="role">
                  {{ formatRoleName(role) }}
                </option>
              </optgroup>
              <optgroup v-if="availableRoles.customRoles?.length > 0" label="Custom Roles">
                <option v-for="role in availableRoles.customRoles" :key="role" :value="role">
                  {{ formatRoleName(role) }}
                </option>
              </optgroup>
            </select>
            <input
              v-else
              id="customRole"
              v-model="form.role"
              type="text"
              @input="handleInput"
              placeholder="Enter custom role name..."
            />
            <button
              type="button"
              class="toggle-custom-btn"
              @click="useCustomRole = !useCustomRole"
            >
              {{ useCustomRole ? 'Use existing' : 'Custom' }}
            </button>
          </div>
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
  isDirty: {
    type: Boolean,
    default: false,
  },
  preselectedArea: {
    type: Object,
    default: null,
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
const useCustomRole = ref(false);

const form = ref({
  role: '',
  name: '',
  phone: '',
  email: '',
  notes: '',
  marshalId: null,
  scopeConfigurations: [{
    scope: 'SpecificPeople',
    itemType: 'Marshal',
    ids: ['ALL_MARSHALS']
  }],
});

const isEditing = computed(() => !!props.contact);

const tabs = [
  { value: 'details', label: 'Details' },
  { value: 'visibility', label: 'Visibility' },
];

const isLinkedToMarshal = computed(() => !!form.value.marshalId);

const sortedMarshals = computed(() => {
  return [...props.marshals].sort((a, b) => alphanumericCompare(a.name, b.name));
});

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = props.initialTab;

    if (props.contact) {
      // Check if role is custom (not in built-in or custom roles list)
      const allKnownRoles = [
        ...(props.availableRoles.builtInRoles || []),
        ...(props.availableRoles.customRoles || [])
      ];
      useCustomRole.value = props.contact.role && !allKnownRoles.includes(props.contact.role);

      form.value = {
        role: props.contact.role || '',
        name: props.contact.name || '',
        phone: props.contact.phone || '',
        email: props.contact.email || '',
        notes: props.contact.notes || '',
        marshalId: props.contact.marshalId || null,
        scopeConfigurations: props.contact.scopeConfigurations
          ? JSON.parse(JSON.stringify(props.contact.scopeConfigurations))
          : [{
              scope: 'SpecificPeople',
              itemType: 'Marshal',
              ids: ['ALL_MARSHALS']
            }],
      };
    } else {
      useCustomRole.value = false;
      // Check if there's a preselected area - pre-configure scope for that area
      const defaultScopeConfig = props.preselectedArea
        ? [{
            scope: 'EveryoneInAreas',
            itemType: 'Area',
            ids: [props.preselectedArea.id]
          }]
        : [{
            scope: 'SpecificPeople',
            itemType: 'Marshal',
            ids: ['ALL_MARSHALS']
          }];

      form.value = {
        role: '',
        name: '',
        phone: '',
        email: '',
        notes: '',
        marshalId: null,
        scopeConfigurations: defaultScopeConfig,
      };
    }

    emit('update:isDirty', false);
  }
});

const formatRoleName = (role) => {
  if (!role) return '';
  // Special handling for AreaLead - use terminology
  if (role === 'AreaLead') {
    return `${terms.value.area} Lead`;
  }
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleMarshalSelect = () => {
  // When a marshal is selected, auto-fill contact details
  if (form.value.marshalId) {
    const marshal = props.marshals.find(m => m.id === form.value.marshalId);
    if (marshal) {
      // Always set name from linked marshal
      form.value.name = marshal.name;
      // Only auto-fill phone/email if fields are empty
      if (!form.value.phone && marshal.phoneNumber) form.value.phone = marshal.phoneNumber;
      if (!form.value.email && marshal.email) form.value.email = marshal.email;
    }
  }
  handleInput();
};

const handleClose = () => {
  emit('close');
};

const goToVisibilityTab = () => {
  // Validate details first
  if (!form.value.role || !form.value.role.trim()) {
    showError('Please select or enter a role');
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
  if (!form.value.role || !form.value.role.trim()) {
    showError('Please select or enter a role');
    return;
  }

  if (!form.value.name || !form.value.name.trim()) {
    showError('Please enter a contact name');
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
    role: form.value.role.trim(),
    name: form.value.name.trim(),
    phone: form.value.phone?.trim() || null,
    email: form.value.email?.trim() || null,
    notes: form.value.notes?.trim() || null,
    marshalId: form.value.marshalId || null,
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

.role-input-group {
  display: flex;
  gap: 0.5rem;
}

.role-input-group select,
.role-input-group input {
  flex: 1;
}

.toggle-custom-btn {
  padding: 0.5rem 0.75rem;
  background: var(--bg-tertiary);
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  color: var(--text-secondary);
  white-space: nowrap;
  transition: all 0.2s;
}

.toggle-custom-btn:hover {
  background: var(--bg-hover);
  color: var(--text-dark);
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
