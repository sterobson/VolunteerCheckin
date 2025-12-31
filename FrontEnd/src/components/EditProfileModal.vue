<template>
  <BaseModal
    :show="show"
    title="Edit Profile"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Name *</label>
        <input
          v-model="form.name"
          type="text"
          required
          class="form-input"
          placeholder="Your name"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Email *</label>
        <input
          v-model="form.email"
          type="email"
          required
          class="form-input"
          placeholder="Your email address"
          @input="handleInput"
        />
      </div>

      <div class="form-group">
        <label>Phone</label>
        <input
          v-model="form.phone"
          type="tel"
          class="form-input"
          placeholder="Your phone number"
          @input="handleInput"
        />
      </div>
    </form>

    <!-- Action buttons -->
    <template #actions>
      <button type="button" @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button type="button" @click="handleSubmit" class="btn btn-primary">
        Save changes
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';
import BaseModal from './BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  profile: {
    type: Object,
    default: null,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const form = ref({
  name: '',
  email: '',
  phone: '',
});

watch(() => props.profile, (newVal) => {
  if (newVal) {
    form.value = {
      name: newVal.name || '',
      email: newVal.email || '',
      phone: newVal.phone || '',
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal && props.profile) {
    form.value = {
      name: props.profile.name || '',
      email: props.profile.email || '',
      phone: props.profile.phone || '',
    };
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  if (!form.value.name.trim()) {
    alert('Name is required');
    return;
  }

  if (!form.value.email.trim()) {
    alert('Email is required');
    return;
  }

  emit('submit', {
    name: form.value.name.trim(),
    email: form.value.email.trim(),
    phone: form.value.phone?.trim() || '',
  });
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
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

.btn-primary:hover {
  background: #0056b3;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
