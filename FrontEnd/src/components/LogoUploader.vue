<template>
  <div class="logo-uploader">
    <label class="uploader-label">Event Logo</label>

    <!-- Current Logo Preview -->
    <div v-if="hasLogo" class="logo-preview">
      <img :src="displayUrl" alt="Event logo" class="logo-image" />
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
        <span class="upload-hint">PNG, JPEG, SVG, GIF, WebP (max 5MB)</span>
      </div>
    </div>

    <!-- Error Message -->
    <div v-if="errorMessage" class="error-message">
      {{ errorMessage }}
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onUnmounted } from 'vue';
import { API_BASE_URL } from '../config';

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
  // When staged=true, don't upload immediately - store file locally for later upload
  staged: {
    type: Boolean,
    default: false,
  },
});

// Get auth headers (same as api.js interceptor)
function getAuthHeaders() {
  const headers = {};
  const adminEmail = localStorage.getItem('adminEmail');
  if (adminEmail) {
    headers['X-Admin-Email'] = adminEmail;
  }
  const sessionToken = localStorage.getItem('sessionToken');
  if (sessionToken) {
    headers['Authorization'] = `Bearer ${sessionToken}`;
  }
  return headers;
}

// Emit staged-change with detailed state object
const emit = defineEmits(['update:modelValue', 'staged-change']);

function emitStagedChange() {
  emit('staged-change', {
    hasPendingChanges: stagedFile.value !== null || pendingDelete.value,
    isPendingDelete: pendingDelete.value,
    displayUrl: stagedPreviewUrl.value || (pendingDelete.value ? '' : props.modelValue) || '',
  });
}

const fileInput = ref(null);
const isUploading = ref(false);
const isDragOver = ref(false);
const errorMessage = ref('');

// Cache-busting key that changes on each upload
const uploadKey = ref(Date.now());

// Staged mode: store the file and its local preview URL
const stagedFile = ref(null);
const stagedPreviewUrl = ref('');
// Track if we need to delete existing logo when saving (staged mode)
const pendingDelete = ref(false);

// Check if we have a logo to display (either staged or server URL)
const hasLogo = computed(() => {
  // In staged mode, check for staged file or valid server URL (not marked for deletion)
  if (props.staged) {
    if (stagedPreviewUrl.value) return true;
    if (pendingDelete.value) return false;
  }
  return !!props.modelValue;
});

// Display URL with cache-busting to prevent showing stale images
const displayUrl = computed(() => {
  // Don't return URL if pending deletion
  if (props.staged && pendingDelete.value) return '';
  // In staged mode, show the staged preview if available
  if (props.staged && stagedPreviewUrl.value) {
    return stagedPreviewUrl.value;
  }
  if (!props.modelValue) return '';
  const separator = props.modelValue.includes('?') ? '&' : '?';
  return `${props.modelValue}${separator}_t=${uploadKey.value}`;
});

// Cleanup blob URLs when component unmounts or when new file is staged
function cleanupStagedPreview() {
  if (stagedPreviewUrl.value) {
    URL.revokeObjectURL(stagedPreviewUrl.value);
    stagedPreviewUrl.value = '';
  }
}

onUnmounted(() => {
  cleanupStagedPreview();
});

const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
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
    errorMessage.value = `File too large. Maximum size is ${MAX_FILE_SIZE / (1024 * 1024)}MB.`;
    return;
  }

  // Staged mode: store file locally, don't upload yet
  if (props.staged) {
    cleanupStagedPreview();
    stagedFile.value = file;
    stagedPreviewUrl.value = URL.createObjectURL(file);
    pendingDelete.value = false;
    // Notify parent that staged state changed
    emitStagedChange();
    return;
  }

  // Immediate mode: upload now
  isUploading.value = true;

  try {
    const response = await fetch(`${API_BASE_URL}/events/${props.eventId}/logo`, {
      method: 'POST',
      headers: {
        'Content-Type': file.type,
        ...getAuthHeaders(),
      },
      body: file,
    });

    if (!response.ok) {
      const errorText = await response.text().catch(() => '');
      let errorMsg = `Upload failed (${response.status})`;
      try {
        const errorJson = JSON.parse(errorText);
        errorMsg = errorJson.message || errorMsg;
      } catch {
        if (errorText) errorMsg = errorText;
      }
      throw new Error(errorMsg);
    }

    const result = await response.json();
    // Update cache-busting key to force image refresh
    uploadKey.value = Date.now();
    emit('update:modelValue', result.logoUrl);
  } catch (error) {
    console.error('Logo upload error:', error);
    // Provide more helpful error message for network errors
    if (error.name === 'TypeError' && error.message === 'Failed to fetch') {
      errorMessage.value = 'Network error. Check your connection or try a smaller file.';
    } else if (error.message?.includes('401') || error.message?.includes('403')) {
      errorMessage.value = 'Not authorized. Please refresh the page and try again.';
    } else {
      errorMessage.value = error.message || 'Failed to upload logo. Please try again.';
    }
  } finally {
    isUploading.value = false;
  }
}

async function removeLogo() {
  errorMessage.value = '';

  // Staged mode: just clear the staged file, mark for deletion if there was a server logo
  if (props.staged) {
    // If we have a staged file, just clear it
    if (stagedFile.value) {
      cleanupStagedPreview();
      stagedFile.value = null;
    } else if (props.modelValue) {
      // Otherwise mark the existing server logo for deletion
      pendingDelete.value = true;
    }
    // Notify parent with detailed state
    emitStagedChange();
    return;
  }

  // Immediate mode: delete from server now
  isUploading.value = true;

  try {
    const response = await fetch(`${API_BASE_URL}/events/${props.eventId}/logo`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
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

// Methods for parent to access staged file and upload it
async function uploadStagedFile() {
  if (!stagedFile.value) {
    // If pending delete, return empty URL; otherwise return current URL
    return { success: true, logoUrl: pendingDelete.value ? '' : props.modelValue };
  }

  isUploading.value = true;
  errorMessage.value = '';

  try {
    const response = await fetch(`${API_BASE_URL}/events/${props.eventId}/logo`, {
      method: 'POST',
      headers: {
        'Content-Type': stagedFile.value.type,
        ...getAuthHeaders(),
      },
      body: stagedFile.value,
    });

    if (!response.ok) {
      const errorText = await response.text().catch(() => '');
      let errorMsg = `Upload failed (${response.status})`;
      try {
        const errorJson = JSON.parse(errorText);
        errorMsg = errorJson.message || errorMsg;
      } catch {
        if (errorText) errorMsg = errorText;
      }
      throw new Error(errorMsg);
    }

    const result = await response.json();
    uploadKey.value = Date.now();
    cleanupStagedPreview();
    stagedFile.value = null;
    pendingDelete.value = false;
    return { success: true, logoUrl: result.logoUrl };
  } catch (error) {
    console.error('Logo upload error:', error);
    errorMessage.value = error.message || 'Failed to upload logo. Please try again.';
    return { success: false, error: error.message };
  } finally {
    isUploading.value = false;
  }
}

async function deleteStagedLogo() {
  if (!pendingDelete.value) {
    return { success: true };
  }

  isUploading.value = true;
  errorMessage.value = '';

  try {
    const response = await fetch(`${API_BASE_URL}/events/${props.eventId}/logo`, {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({}));
      throw new Error(error.message || 'Delete failed');
    }

    pendingDelete.value = false;
    return { success: true };
  } catch (error) {
    console.error('Logo delete error:', error);
    errorMessage.value = error.message || 'Failed to remove logo. Please try again.';
    return { success: false, error: error.message };
  } finally {
    isUploading.value = false;
  }
}

// Check if there are pending changes
function hasPendingChanges() {
  return stagedFile.value !== null || pendingDelete.value;
}

// Get the staged file for external use
function getStagedFile() {
  return stagedFile.value;
}

// Check if pending delete
function isPendingDelete() {
  return pendingDelete.value;
}

// Get the current display URL (staged or server, empty if pending delete)
function getDisplayUrl() {
  if (stagedPreviewUrl.value) return stagedPreviewUrl.value;
  if (pendingDelete.value) return '';
  return props.modelValue || '';
}

// Reset all staged state (for when branding is reset)
function resetStagedState() {
  cleanupStagedPreview();
  stagedFile.value = null;
  pendingDelete.value = false;
}

// Expose methods for parent component
defineExpose({
  uploadStagedFile,
  deleteStagedLogo,
  hasPendingChanges,
  getStagedFile,
  isPendingDelete,
  getDisplayUrl,
  resetStagedState,
});
</script>

<style scoped>
.logo-uploader {
  margin-bottom: 1rem;
}

.uploader-label {
  display: block;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.logo-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.logo-image {
  width: 160px;
  height: 160px;
  object-fit: cover;
  object-position: center;
  border-radius: 8px;
  background: var(--bg-tertiary);
}

.remove-btn {
  padding: 0.4rem 0.75rem;
  font-size: 0.85rem;
  color: var(--danger);
  background: var(--card-bg);
  border: 1px solid var(--danger);
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
}

.remove-btn:hover:not(:disabled) {
  background: var(--danger);
  color: var(--card-bg);
}

.remove-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.upload-area {
  border: 2px dashed var(--border-dark);
  border-radius: 8px;
  padding: 2rem;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--bg-lighter);
}

.upload-area:hover {
  border-color: var(--brand-primary);
  background: var(--brand-primary-light);
}

.upload-area.drag-over {
  border-color: var(--brand-primary);
  background: var(--brand-primary-bg);
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
  background: var(--bg-tertiary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.upload-text {
  font-size: 0.95rem;
  color: var(--text-dark);
}

.upload-hint {
  font-size: 0.8rem;
  color: var(--text-light);
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
  border: 3px solid var(--bg-tertiary);
  border-top-color: var(--brand-primary);
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
  background: var(--danger-bg-lighter);
  border: 1px solid var(--danger-border);
  border-radius: 4px;
  color: var(--danger-text);
  font-size: 0.85rem;
}
</style>
