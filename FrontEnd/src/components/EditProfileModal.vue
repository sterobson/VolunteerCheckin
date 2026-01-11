<template>
  <BaseModal
    :show="show"
    title="Edit profile"
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
      <button type="button" @click="handleSubmit" class="btn btn-success">
        Save
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
</style>
