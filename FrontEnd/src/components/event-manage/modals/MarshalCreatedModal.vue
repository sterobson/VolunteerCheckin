<template>
  <BaseModal
    :show="show"
    :title="`${terms.person} created`"
    size="medium"
    :confirm-on-close="false"
    :is-dirty="false"
    @close="handleClose"
  >
    <div class="success-content">
      <div class="success-icon">✓</div>
      <p class="success-message">
        <strong>{{ marshalName }}</strong> has been added successfully.
      </p>
    </div>

    <div class="link-section">
      <label>Login link</label>
      <p class="help-text">
        Share this unique link with {{ marshalName }} so they can access their dashboard and check in.
      </p>

      <div v-if="loading" class="loading-text">Loading link...</div>

      <div v-else-if="magicLink" class="magic-link-container">
        <input
          type="text"
          :value="magicLink"
          readonly
          class="form-input magic-link-input"
          @focus="$event.target.select()"
        />
        <div class="magic-link-actions">
          <button
            type="button"
            class="btn btn-secondary"
            @click="copyLink"
          >
            {{ copySuccess ? 'Copied!' : 'Copy link' }}
          </button>
          <button
            v-if="hasEmail"
            type="button"
            class="btn btn-primary"
            :disabled="sendingEmail"
            @click="sendEmail"
          >
            {{ sendingEmail ? 'Sending...' : 'Send via email' }}
          </button>
        </div>
        <p v-if="emailSentSuccess" class="success-text">Email sent successfully!</p>
        <p v-if="emailError" class="error-text">{{ emailError }}</p>
      </div>

      <div v-else-if="error" class="error-text">{{ error }}</div>
    </div>

    <template #actions>
      <button @click="handleClose" class="btn btn-primary">Done</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, watch, defineProps, defineEmits } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { marshalsApi } from '../../../services/api';
import { useTerminology } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  eventId: {
    type: String,
    required: true,
  },
  marshalId: {
    type: String,
    default: null,
  },
  marshalName: {
    type: String,
    default: '',
  },
});

const emit = defineEmits(['close']);

const magicLink = ref('');
const hasEmail = ref(false);
const loading = ref(false);
const error = ref('');
const copySuccess = ref(false);
const sendingEmail = ref(false);
const emailSentSuccess = ref(false);
const emailError = ref('');

const fetchMagicLink = async () => {
  if (!props.eventId || !props.marshalId) return;

  loading.value = true;
  error.value = '';

  try {
    const response = await marshalsApi.getMagicLink(props.eventId, props.marshalId);
    magicLink.value = response.data.magicLink;
    hasEmail.value = response.data.hasEmail;
  } catch (err) {
    console.error('Failed to fetch magic link:', err);
    error.value = 'Failed to load login link';
  } finally {
    loading.value = false;
  }
};

const copyLink = async () => {
  try {
    // Try modern clipboard API first
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(magicLink.value);
    } else {
      // Fallback for non-secure contexts (e.g., localhost without HTTPS)
      const textArea = document.createElement('textarea');
      textArea.value = magicLink.value;
      textArea.style.position = 'fixed';
      textArea.style.left = '-9999px';
      document.body.appendChild(textArea);
      textArea.select();
      document.execCommand('copy');
      document.body.removeChild(textArea);
    }
    copySuccess.value = true;
    setTimeout(() => {
      copySuccess.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy:', err);
  }
};

const sendEmail = async () => {
  if (!props.eventId || !props.marshalId) return;

  sendingEmail.value = true;
  emailSentSuccess.value = false;
  emailError.value = '';

  try {
    await marshalsApi.sendMagicLink(props.eventId, props.marshalId);
    emailSentSuccess.value = true;
    setTimeout(() => {
      emailSentSuccess.value = false;
    }, 3000);
  } catch (err) {
    console.error('Failed to send email:', err);
    emailError.value = err.response?.data?.message || 'Failed to send email';
  } finally {
    sendingEmail.value = false;
  }
};

const handleClose = () => {
  emit('close');
};

// Fetch magic link when modal is shown
watch(() => props.show, (newVal) => {
  if (newVal && props.marshalId) {
    fetchMagicLink();
  } else {
    // Reset state when closed
    magicLink.value = '';
    hasEmail.value = false;
    error.value = '';
    copySuccess.value = false;
    emailSentSuccess.value = false;
    emailError.value = '';
  }
}, { immediate: true });
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

.loading-text {
  color: var(--text-secondary);
  font-style: italic;
}

.magic-link-container {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.magic-link-input {
  background-color: var(--card-bg);
  font-family: monospace;
  font-size: 0.85rem;
}

.magic-link-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
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

.btn-primary:disabled {
  background: var(--btn-primary-disabled);
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.success-text {
  margin: 0.5rem 0 0 0;
  color: var(--success);
  font-size: 0.85rem;
}

.error-text {
  margin: 0.5rem 0 0 0;
  color: var(--danger);
  font-size: 0.85rem;
}
</style>
