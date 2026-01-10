<template>
  <BaseModal
    :show="show"
    :title="`Add existing contact to ${areaName}`"
    size="medium"
    @close="handleClose"
  >
    <div class="contact-selector-section">
      <div class="form-group">
        <label>Contact *</label>
        <select v-model="selectedContactId" class="form-input">
          <option value="">Choose a contact...</option>
          <option
            v-for="contact in availableContacts"
            :key="contact.contactId"
            :value="contact.contactId"
          >
            {{ contact.name }}{{ contact.role ? ` (${contact.role})` : '' }}
          </option>
        </select>
      </div>

      <p v-if="availableContacts.length === 0" class="no-contacts-message">
        No available contacts to add. All contacts are already assigned to this {{ termsLower.area }}.
      </p>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button
        @click="handleSelect"
        class="btn btn-primary"
        :disabled="!selectedContactId"
      >
        Add to {{ termsLower.area }}
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  areaName: {
    type: String,
    default: '',
  },
  areaId: {
    type: String,
    default: '',
  },
  contacts: {
    type: Array,
    default: () => [],
  },
  excludeContactIds: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'select']);

const selectedContactId = ref('');

// Filter out contacts already in this area or pending
const availableContacts = computed(() => {
  return props.contacts
    .filter(contact => {
      // Exclude contacts already in exclude list (already scoped + pending)
      if (props.excludeContactIds.includes(contact.contactId)) {
        return false;
      }
      return true;
    })
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
});

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset state when modal closes
    selectedContactId.value = '';
  }
});

const handleSelect = () => {
  if (selectedContactId.value) {
    const contact = props.contacts.find((c) => c.contactId === selectedContactId.value);
    if (contact) {
      emit('select', contact);
    }
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.contact-selector-section {
  margin-bottom: 1rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-dark);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.no-contacts-message {
  color: var(--text-secondary);
  font-size: 0.9rem;
  font-style: italic;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
