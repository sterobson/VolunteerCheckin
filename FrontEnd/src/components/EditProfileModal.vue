<template>
  <BaseModal
    :show="show"
    title="Edit profile"
    size="medium"
    :z-index="zIndex"
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
      <div class="actions-left">
        <button v-if="showLogout" type="button" @click="handleLogout" class="btn btn-danger">
          Log out
        </button>
      </div>
      <button type="button" @click="handleSubmit" class="btn btn-success">
        Save
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, watch } from 'vue';
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
  zIndex: {
    type: Number,
    default: 1000,
  },
  showLogout: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty', 'logout']);

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

const handleLogout = () => {
  emit('logout');
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

.btn-danger {
  background: var(--danger, #dc3545);
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

.actions-left {
  margin-right: auto;
}
</style>
