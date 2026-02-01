<template>
  <BaseModal
    :show="show"
    title="Sample event created"
    size="medium"
    :confirm-on-close="false"
    :is-dirty="false"
    @close="handleClose"
  >
    <div class="success-content">
      <div class="success-icon">✓</div>
      <p class="success-message">
        Your sample event is ready to explore!
      </p>
    </div>

    <div class="link-section">
      <label>Your access link</label>
      <p class="help-text">
        This is your unique link to access the event. Copy it now &mdash; you'll need it to return later.
        <strong>This link expires in {{ lifetimeHours }} hours.</strong>
      </p>

      <div class="magic-link-container">
        <input
          type="text"
          :value="accessLink"
          readonly
          class="form-input magic-link-input"
          @focus="$event.target.select()"
        />
        <div class="magic-link-buttons">
          <button
            type="button"
            class="btn btn-secondary btn-icon"
            @click="copyLink"
            :title="copySuccess ? 'Copied!' : 'Copy link'"
          >
            <!-- Checkmark icon when copied -->
            <svg v-if="copySuccess" xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
            <!-- Copy icon -->
            <svg v-else xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
            </svg>
          </button>
          <button
            v-if="canShare"
            type="button"
            class="btn btn-secondary btn-icon"
            @click="shareLink"
            title="Share link"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <circle cx="18" cy="5" r="3"></circle>
              <circle cx="6" cy="12" r="3"></circle>
              <circle cx="18" cy="19" r="3"></circle>
              <line x1="8.59" y1="13.51" x2="15.42" y2="17.49"></line>
              <line x1="15.41" y1="6.51" x2="8.59" y2="10.49"></line>
            </svg>
          </button>
          <button
            type="button"
            class="btn btn-secondary btn-icon"
            @click="showQrCode = true"
            title="Show QR code"
          >
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="3" y="3" width="7" height="7"></rect>
              <rect x="14" y="3" width="7" height="7"></rect>
              <rect x="3" y="14" width="7" height="7"></rect>
              <rect x="14" y="14" width="3" height="3"></rect>
              <rect x="19" y="14" width="2" height="2"></rect>
              <rect x="14" y="19" width="2" height="2"></rect>
              <rect x="19" y="19" width="2" height="2"></rect>
            </svg>
          </button>
        </div>
        <p v-if="copyError" class="error-text">{{ copyError }}</p>
      </div>
    </div>

    <template #actions>
      <button @click="goToEvent" class="btn btn-primary">Go to event</button>
    </template>
  </BaseModal>

  <!-- QR Code Modal (nested, needs higher z-index) -->
  <QrCodeModal
    :show="showQrCode"
    :url="accessLink"
    title="Scan to access event"
    :z-index="1100"
    @close="showQrCode = false"
  />
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import BaseModal from './BaseModal.vue';
import QrCodeModal from './common/QrCodeModal.vue';

// Check if Web Share API is available
const canShare = computed(() => !!navigator.share);

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  eventId: {
    type: String,
    required: true,
  },
  adminCode: {
    type: String,
    required: true,
  },
  lifetimeHours: {
    type: Number,
    default: 4,
  },
});

const emit = defineEmits(['close', 'navigate']);

const copySuccess = ref(false);
const copyError = ref('');
const showQrCode = ref(false);

// Build the access link
const accessLink = computed(() => {
  const base = window.location.origin;
  const isLocalhost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
  const useHashRouting = !isLocalhost && import.meta.env.VITE_USE_HASH_ROUTING === 'true';

  if (useHashRouting) {
    return `${base}/#/admin/event/${props.eventId}?sample=${props.adminCode}`;
  }
  return `${base}/admin/event/${props.eventId}?sample=${props.adminCode}`;
});

const copyLink = async () => {
  copyError.value = '';

  try {
    // Try modern clipboard API first
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(accessLink.value);
      copySuccess.value = true;
      setTimeout(() => {
        copySuccess.value = false;
      }, 2000);
      return;
    }
  } catch (err) {
    console.warn('Clipboard API failed, trying fallback:', err);
  }

  // Fallback for older browsers
  try {
    const textArea = document.createElement('textarea');
    textArea.value = accessLink.value;
    textArea.style.position = 'fixed';
    textArea.style.left = '-9999px';
    textArea.style.top = '0';
    document.body.appendChild(textArea);
    textArea.focus();
    textArea.select();

    const successful = document.execCommand('copy');
    document.body.removeChild(textArea);

    if (successful) {
      copySuccess.value = true;
      setTimeout(() => {
        copySuccess.value = false;
      }, 2000);
    } else {
      copyError.value = 'Copy failed. Please select and copy the link manually.';
      setTimeout(() => {
        copyError.value = '';
      }, 4000);
    }
  } catch (err) {
    console.error('Fallback copy failed:', err);
    copyError.value = 'Copy not supported. Please select and copy the link manually.';
    setTimeout(() => {
      copyError.value = '';
    }, 4000);
  }
};

const shareLink = async () => {
  if (!navigator.share || !accessLink.value) return;

  try {
    await navigator.share({
      title: 'Sample Event - OnTheDayApp',
      text: "Here's a sample event I'm exploring on OnTheDayApp",
      url: accessLink.value,
    });
  } catch (err) {
    // User cancelled or share failed - silently ignore
    if (err.name !== 'AbortError') {
      console.error('Share failed:', err);
    }
  }
};

const goToEvent = () => {
  emit('navigate');
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.success-content {
  text-align: center;
  margin-bottom: 2rem;
}

.success-icon {
  width: 60px;
  height: 60px;
  background: var(--success);
  color: var(--card-bg);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  margin: 0 auto 1rem;
}

.success-message {
  font-size: 1.1rem;
  color: var(--text-dark);
  margin: 0;
}

.link-section {
  background: var(--bg-secondary);
  padding: 1.5rem;
  border-radius: 8px;
}

.link-section label {
  display: block;
  margin-bottom: 0.25rem;
  font-weight: 600;
  color: var(--text-dark);
}

.help-text {
  margin: 0 0 1rem 0;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.magic-link-container {
  container-type: inline-size;
  display: flex;
  flex-direction: row;
  flex-wrap: wrap;
  gap: 0.5rem;
  align-items: stretch;
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

.magic-link-input {
  flex: 1;
  min-width: 0;
  background-color: var(--bg-tertiary);
  font-family: monospace;
  font-size: 0.85rem;
}

.magic-link-buttons {
  display: flex;
  gap: 0.5rem;
  flex-shrink: 0;
}

.btn-icon {
  padding: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-icon svg {
  display: block;
}

/* When container is narrow, put buttons on their own row */
@container (max-width: 400px) {
  .magic-link-input {
    flex-basis: 100%;
  }

  .magic-link-buttons {
    flex-basis: 100%;
    justify-content: center;
  }
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

.btn-primary:hover:not(:disabled) {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.error-text {
  margin: 0.5rem 0 0 0;
  color: var(--danger);
  font-size: 0.85rem;
}
</style>
