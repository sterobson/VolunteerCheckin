<template>
  <div class="logo-uploader">
    <label class="uploader-label">Event Logo</label>

    <!-- Current Logo Preview -->
    <div v-if="modelValue" class="logo-preview">
      <img :src="modelValue" alt="Event logo" class="logo-image" />
      <button type="button" class="remove-btn" @click="removeLogo" :disabled="isUploading">
        Remove
      </button>
    </div>

    <!-- Upload Area -->
    <div
      v-else
      class="upload-area"
      :class="{ 'drag-over': isDragOver, uploading: isUploading }"
      @dragover.prevent="isDragOver = true"
      @dragleave.prevent="isDragOver = false"
      @drop.prevent="handleDrop"
      @click="triggerFileInput"
    >
      <input
        ref="fileInput"
        type="file"
        accept="image/png,image/jpeg,image/svg+xml,image/gif,image/webp"
        class="file-input"
        @change="handleFileSelect"
      />

      <div v-if="isUploading" class="upload-progress">
        <div class="spinner"></div>
        <span>Uploading...</span>
      </div>

      <div v-else class="upload-prompt">
        <div class="upload-icon">+</div>
        <span class="upload-text">Click or drag to upload logo</span>
        <span class="upload-hint">PNG, JPEG, SVG, GIF, WebP (max 500KB)</span>
      </div>
    </div>

    <!-- Error Message -->
    <div v-if="errorMessage" class="error-message">
      {{ errorMessage }}
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';

const props = defineProps({
  modelValue: {
    type: String,
    default: '',
  },
  eventId: {
    type: String,
    required: true,
  },
  adminEmail: {
    type: String,
    required: true,
  },
});

const emit = defineEmits(['update:modelValue']);

const fileInput = ref(null);
const isUploading = ref(false);
const isDragOver = ref(false);
const errorMessage = ref('');

const MAX_FILE_SIZE = 500 * 1024; // 500KB
const ALLOWED_TYPES = ['image/png', 'image/jpeg', 'image/svg+xml', 'image/gif', 'image/webp'];

function triggerFileInput() {
  if (!isUploading.value) {
    fileInput.value?.click();
  }
}

function handleFileSelect(event) {
  const file = event.target.files?.[0];
  if (file) {
    uploadFile(file);
  }
  // Reset input so the same file can be selected again
  if (fileInput.value) {
    fileInput.value.value = '';
  }
}

function handleDrop(event) {
  isDragOver.value = false;
  const file = event.dataTransfer?.files?.[0];
  if (file) {
    uploadFile(file);
  }
}

async function uploadFile(file) {
  errorMessage.value = '';

  // Validate file type
  if (!ALLOWED_TYPES.includes(file.type)) {
    errorMessage.value = 'Invalid file type. Please use PNG, JPEG, SVG, GIF, or WebP.';
    return;
  }

  // Validate file size
  if (file.size > MAX_FILE_SIZE) {
    errorMessage.value = `File too large. Maximum size is ${MAX_FILE_SIZE / 1024}KB.`;
    return;
  }

  isUploading.value = true;

  try {
    const response = await fetch(`/api/events/${props.eventId}/logo`, {
      method: 'POST',
      headers: {
        'Content-Type': file.type,
        'X-Admin-Email': props.adminEmail,
      },
      body: file,
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.message || 'Upload failed');
    }

    const result = await response.json();
    emit('update:modelValue', result.logoUrl);
  } catch (error) {
    console.error('Logo upload error:', error);
    errorMessage.value = error.message || 'Failed to upload logo. Please try again.';
  } finally {
    isUploading.value = false;
  }
}

async function removeLogo() {
  errorMessage.value = '';
  isUploading.value = true;

  try {
    const response = await fetch(`/api/events/${props.eventId}/logo`, {
      method: 'DELETE',
      headers: {
        'X-Admin-Email': props.adminEmail,
      },
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.message || 'Delete failed');
    }

    emit('update:modelValue', '');
  } catch (error) {
    console.error('Logo delete error:', error);
    errorMessage.value = error.message || 'Failed to remove logo. Please try again.';
  } finally {
    isUploading.value = false;
  }
}
</script>

<style scoped>
.logo-uploader {
  margin-bottom: 1rem;
}

.uploader-label {
  display: block;
  font-size: 0.85rem;
  color: #666;
  margin-bottom: 0.5rem;
}

.logo-preview {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #e9ecef;
}

.logo-image {
  max-width: 120px;
  max-height: 60px;
  object-fit: contain;
  border-radius: 4px;
}

.remove-btn {
  padding: 0.4rem 0.75rem;
  font-size: 0.85rem;
  color: #dc3545;
  background: white;
  border: 1px solid #dc3545;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.remove-btn:hover:not(:disabled) {
  background: #dc3545;
  color: white;
}

.remove-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.upload-area {
  border: 2px dashed #ccc;
  border-radius: 8px;
  padding: 2rem;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  background: #fafafa;
}

.upload-area:hover {
  border-color: #667eea;
  background: #f0f4ff;
}

.upload-area.drag-over {
  border-color: #667eea;
  background: #e8edff;
  border-style: solid;
}

.upload-area.uploading {
  cursor: wait;
  opacity: 0.7;
}

.file-input {
  display: none;
}

.upload-prompt {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
}

.upload-icon {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  background: #e9ecef;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  color: #666;
  margin-bottom: 0.5rem;
}

.upload-text {
  font-size: 0.95rem;
  color: #333;
}

.upload-hint {
  font-size: 0.8rem;
  color: #888;
}

.upload-progress {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
}

.spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e9ecef;
  border-top-color: #667eea;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.error-message {
  margin-top: 0.5rem;
  padding: 0.5rem 0.75rem;
  background: #fff5f5;
  border: 1px solid #feb2b2;
  border-radius: 4px;
  color: #c53030;
  font-size: 0.85rem;
}
</style>
