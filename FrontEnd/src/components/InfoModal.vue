<template>
  <div v-if="show" class="modal-overlay" @click.self="handleClose">
    <div class="info-modal">
      <h3>{{ title }}</h3>
      <div class="info-content" v-html="message"></div>
      <div class="info-actions">
        <button @click="handleClose" class="btn btn-primary">{{ okText }}</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  title: {
    type: String,
    default: 'Information',
  },
  message: {
    type: String,
    required: true,
  },
  okText: {
    type: String,
    default: 'OK',
  },
});

const emit = defineEmits(['close']);

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2000;
}

.info-modal {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 500px;
  width: 90%;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

.info-modal h3 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1.25rem;
}

.info-content {
  margin: 0 0 1.5rem 0;
  color: #666;
  line-height: 1.5;
}

.info-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
}

.btn {
  padding: 0.5rem 1.5rem;
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
</style>
