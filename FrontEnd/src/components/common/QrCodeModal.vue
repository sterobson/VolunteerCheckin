<template>
  <BaseModal
    :show="show"
    :title="title"
    size="small"
    :z-index="zIndex"
    :confirm-on-close="false"
    @close="$emit('close')"
  >
    <div class="qr-container">
      <canvas ref="canvasRef" class="qr-canvas"></canvas>
      <p v-if="error" class="error-text">{{ error }}</p>
    </div>

    <template #actions>
      <button @click="$emit('close')" class="btn btn-primary">Done</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import QRCode from 'qrcode';
import BaseModal from '../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  url: {
    type: String,
    required: true,
  },
  title: {
    type: String,
    default: 'Scan QR code',
  },
  zIndex: {
    type: Number,
    default: 1000,
  },
});

defineEmits(['close']);

const canvasRef = ref(null);
const error = ref('');

const generateQrCode = async () => {
  if (!canvasRef.value || !props.url) return;

  error.value = '';

  try {
    await QRCode.toCanvas(canvasRef.value, props.url, {
      width: 280,
      margin: 2,
      color: {
        dark: '#000000',
        light: '#ffffff',
      },
    });
  } catch (err) {
    console.error('Failed to generate QR code:', err);
    error.value = 'Failed to generate QR code';
  }
};

// Generate QR code when modal is shown or URL changes
watch(
  () => [props.show, props.url],
  ([newShow]) => {
    if (newShow && props.url) {
      // Wait for canvas to be rendered
      setTimeout(generateQrCode, 50);
    }
  },
  { immediate: true }
);
</script>

<style scoped>
.qr-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 1rem 0;
}

.qr-canvas {
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.error-text {
  margin-top: 1rem;
  color: var(--danger);
  font-size: 0.9rem;
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
</style>
