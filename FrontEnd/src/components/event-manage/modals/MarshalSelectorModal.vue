<template>
  <BaseModal
    :show="show"
    :title="`Add contact to ${areaName}`"
    size="medium"
    @close="handleClose"
  >
    <div class="marshal-selector-section">
      <h3>Select marshal</h3>
      <div class="form-group">
        <label>Marshal *</label>
        <select v-model="selectedMarshalId" class="form-input">
          <option value="">Choose a marshal...</option>
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
  color: #333;
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
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.form-help {
  display: block;
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: #666;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #0056b3;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
