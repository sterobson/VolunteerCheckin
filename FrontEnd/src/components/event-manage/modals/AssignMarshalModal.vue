<template>
  <BaseModal
    :show="show"
    :title="showCreateMode ? 'Create new marshal' : `Assign marshal to ${locationName}`"
    size="medium"
    @close="handleClose"
  >
    <!-- Select existing marshal - only show if not creating new -->
    <div v-if="!showCreateMode" class="assign-marshal-section">
      <h3>Select existing marshal</h3>
      <select v-model="selectedMarshalId" class="form-input">
        <option value="">Choose a marshal...</option>
        <option
          v-for="marshal in marshals"
          :key="marshal.id"
          :value="marshal.id"
        >
          {{ marshal.name }}
          <template v-if="marshal.assignedLocationIds && marshal.assignedLocationIds.length > 0">
            (assigned to {{ marshal.assignedLocationIds.length }} checkpoint{{ marshal.assignedLocationIds.length > 1 ? 's' : '' }})
          </template>
        </option>
      </select>
      <button
        @click="handleAssignExisting"
        class="btn btn-primary btn-full"
        style="margin-top: 1rem;"
        :disabled="!selectedMarshalId"
      >
        Assign selected marshal
      </button>

      <div class="divider">OR</div>

      <button @click="toggleCreateMode" class="btn btn-secondary btn-full">
        Create new marshal
      </button>
    </div>

    <!-- Create new marshal form - only show when creating -->
    <div v-else class="assign-marshal-section create-mode">
      <h3>Create new marshal</h3>
      <div class="form-group">
        <label>Name *</label>
        <input
          v-model="newMarshalForm.name"
          type="text"
          required
          class="form-input"
        />
      </div>
      <div class="form-group">
        <label>Email</label>
        <input
          v-model="newMarshalForm.email"
          type="email"
          class="form-input"
        />
      </div>
      <div class="form-group">
        <label>Phone</label>
        <input
          v-model="newMarshalForm.phoneNumber"
          type="tel"
          class="form-input"
        />
      </div>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <template v-if="showCreateMode">
        <button @click="handleCancelCreate" class="btn btn-secondary">
          Cancel
        </button>
        <button @click="handleCreateAndAssign" class="btn btn-primary">
          Create & assign
        </button>
      </template>
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
  locationName: {
    type: String,
    default: '',
  },
  marshals: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'assign-existing', 'create-and-assign']);

const showCreateMode = ref(false);
const selectedMarshalId = ref('');
const newMarshalForm = ref({
  name: '',
  email: '',
  phoneNumber: '',
});

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset state when modal closes
    showCreateMode.value = false;
    selectedMarshalId.value = '';
    newMarshalForm.value = {
      name: '',
      email: '',
      phoneNumber: '',
    };
  }
});

const toggleCreateMode = () => {
  showCreateMode.value = true;
};

const handleAssignExisting = () => {
  if (selectedMarshalId.value) {
    emit('assign-existing', selectedMarshalId.value);
  }
};

const handleCreateAndAssign = () => {
  if (newMarshalForm.value.name) {
    emit('create-and-assign', { ...newMarshalForm.value });
  }
};

const handleCancelCreate = () => {
  showCreateMode.value = false;
  newMarshalForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.assign-marshal-section {
  margin-bottom: 1rem;
}

.assign-marshal-section h3 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1rem;
  font-weight: 600;
}

.create-mode {
  background: #f5f7fa;
  padding: 1.5rem;
  border-radius: 8px;
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

.divider {
  text-align: center;
  margin: 1.5rem 0;
  color: #666;
  font-weight: 600;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-full {
  width: 100%;
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
