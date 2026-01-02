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
  color: #333;
  font-size: 0.9rem;
}

input[type="text"],
input[type="tel"],
input[type="email"],
input[type="number"],
textarea,
select {
  padding: 0.6rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  font-family: inherit;
}

input.input-disabled,
input:disabled {
  background-color: #f5f5f5;
  color: #666;
  cursor: not-allowed;
}

textarea {
  resize: vertical;
  min-height: 80px;
}

select {
  background: white;
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
  background: #f0f0f0;
  border: 1px solid #ddd;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.8rem;
  color: #666;
  white-space: nowrap;
  transition: all 0.2s;
}

.toggle-custom-btn:hover {
  background: #e0e0e0;
  color: #333;
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

  .role-input-group {
    flex-direction: column;
  }
}
</style>
