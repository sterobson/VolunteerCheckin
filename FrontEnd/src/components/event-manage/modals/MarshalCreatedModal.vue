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
      <label>Login Link</label>
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
    await navigator.clipboard.writeText(magicLink.value);
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
  background: #28a745;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  margin: 0 auto 1rem;
}

.success-message {
  font-size: 1.1rem;
  color: #333;
  margin: 0;
}

.link-section {
  background: #f8f9fa;
  padding: 1.5rem;
  border-radius: 8px;
}

.link-section label {
  display: block;
  margin-bottom: 0.25rem;
  font-weight: 600;
  color: #333;
}

.help-text {
  margin: 0 0 1rem 0;
  font-size: 0.9rem;
  color: #666;
}

.loading-text {
  color: #666;
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
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.magic-link-input {
  background-color: white;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #0056b3;
}

.btn-primary:disabled {
  background: #6c9bd1;
  cursor: not-allowed;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}

.success-text {
  margin: 0.5rem 0 0 0;
  color: #28a745;
  font-size: 0.85rem;
}

.error-text {
  margin: 0.5rem 0 0 0;
  color: #dc3545;
  font-size: 0.85rem;
}
</style>
