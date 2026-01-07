<template>
  <BaseModal
    :show="show"
    :title="`Add contact to ${areaName}`"
    size="medium"
    @close="handleClose"
  >
    <div class="marshal-selector-section">
      <h3>Select {{ termsLower.person }}</h3>
      <div class="form-group">
        <label>{{ terms.person }} *</label>
        <select v-model="selectedMarshalId" class="form-input">
          <option value="">Choose a {{ termsLower.person }}...</option>
          <option
            v-for="marshal in marshals"
            :key="marshal.id"
            :value="marshal.id"
          >
            {{ marshal.name }}
          </option>
        </select>
      </div>

      <div class="form-group">
        <label>Role *</label>
        <select v-model="selectedRole" class="form-input">
          <option value="">Choose a role...</option>
          <option value="Lead">Lead</option>
          <option value="Deputy">Deputy</option>
          <option value="Support">Support</option>
        </select>
        <small class="form-help">
          Lead: Primary contact for this area<br>
          Deputy: Secondary contact<br>
          Support: Additional support contact
        </small>
      </div>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button
        @click="handleAssign"
        class="btn btn-primary"
        :disabled="!selectedMarshalId || !selectedRole"
      >
        Add contact
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
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
  marshals: {
    type: Array,
    default: () => [],
  },
  existingContacts: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'assign']);

const selectedMarshalId = ref('');
const selectedRole = ref('');

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset state when modal closes
    selectedMarshalId.value = '';
    selectedRole.value = '';
  }
});

const handleAssign = () => {
  if (selectedMarshalId.value && selectedRole.value) {
    const marshal = props.marshals.find((m) => m.id === selectedMarshalId.value);
    if (marshal) {
      emit('assign', {
        marshalId: marshal.id,
        marshalName: marshal.name,
        role: selectedRole.value,
      });
    }
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.marshal-selector-section {
  margin-bottom: 1rem;
}

.marshal-selector-section h3 {
  margin: 0 0 1rem 0;
  color: var(--text-dark);
  font-size: 1rem;
  font-weight: 600;
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

.form-help {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.4;
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
