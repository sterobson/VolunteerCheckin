<template>
  <BaseModal
    :show="show"
    title="Add event administrator"
    size="medium"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Instruction text -->
    <p class="instruction">Enter the email address of the administrator to add</p>

    <!-- Form -->
    <form @submit.prevent="handleSubmit">
      <div class="form-group">
        <label>Email address</label>
        <input
          v-model="email"
          type="email"
          required
          class="form-input"
          placeholder="admin@example.com"
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
        Add administrator
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
  isDirty: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['close', 'submit', 'update:isDirty']);

const email = ref('');

watch(() => props.show, (newVal) => {
  if (!newVal) {
    email.value = '';
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleSubmit = () => {
  emit('submit', email.value);
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.instruction {
  color: var(--text-secondary);
  margin-bottom: 1.5rem;
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
