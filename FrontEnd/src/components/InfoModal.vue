<template>
  <BaseModal
    :show="show"
    :title="title"
    size="small"
    :z-index="2000"
    @close="handleClose"
  >
    <!-- Message content with HTML support -->
    <div class="info-content" v-html="message"></div>

    <!-- OK button -->
    <template #actions>
      <button @click="handleClose" class="btn btn-primary">{{ okText }}</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import BaseModal from './BaseModal.vue';

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
.info-content {
  color: #666;
  line-height: 1.5;
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
