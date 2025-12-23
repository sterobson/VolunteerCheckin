<template>
  <div v-if="show" class="modal" @click.self="close">
    <div class="modal-content">
      <button @click="close" class="modal-close-btn">✕</button>
      <h2>Add event administrator</h2>
      <p class="instruction">Enter the email address of the administrator to add</p>

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

        <div class="modal-actions">
          <button type="button" @click="close" class="btn btn-secondary">
            Cancel
          </button>
          <button type="submit" class="btn btn-primary">Add administrator</button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, defineProps, defineEmits, watch } from 'vue';

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

const close = () => {
  if (props.isDirty) {
    if (confirm('You have unsaved changes. Are you sure you want to close?')) {
      emit('close');
    }
  } else {
    emit('close');
  }
};
</script>

<style scoped>
.modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 500px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
  position: relative;
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: #666;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.modal-close-btn:hover {
  background: #f0f0f0;
  color: #333;
}

h2 {
  margin: 0 0 1rem 0;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 1.5rem;
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

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
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
