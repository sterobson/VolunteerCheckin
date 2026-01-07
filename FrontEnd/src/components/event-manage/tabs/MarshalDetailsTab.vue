<template>
  <div class="tab-content">
    <div class="form-group">
      <label>Name *</label>
      <input
        :value="form.name"
        @input="handleInput('name', $event.target.value)"
        type="text"
        required
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Email</label>
      <input
        :value="form.email"
        @input="handleInput('email', $event.target.value)"
        type="email"
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Phone number</label>
      <input
        :value="form.phoneNumber"
        @input="handleInput('phoneNumber', $event.target.value)"
        type="tel"
        class="form-input"
      />
    </div>

    <div class="form-group">
      <label>Notes</label>
      <textarea
        :value="form.notes"
        @input="handleInput('notes', $event.target.value)"
        class="form-input"
        rows="3"
        placeholder="e.g., Needs to leave by 11am"
      ></textarea>
    </div>

    <!-- Magic Link Section (only for existing marshals) -->
    <div v-if="marshalId" class="magic-link-section">
      <label>Login link</label>
      <p class="help-text">Share this unique link with the marshal so they can access their dashboard.</p>

      <div v-if="loadingMagicLink" class="loading-text">Loading link...</div>

      <div v-else-if="magicLink" class="magic-link-container">
        <div class="magic-link-row">
          <input
            type="text"
            :value="magicLink"
            readonly
            class="form-input magic-link-input"
            @focus="$event.target.select()"
          />
          <button
            type="button"
            class="btn btn-secondary"
            @click="copyLink"
          >
            {{ copySuccess ? 'Copied!' : 'Copy link' }}
          </button>
        </div>
        <div v-if="hasEmail" class="magic-link-actions">
          <button
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

      <div v-else-if="magicLinkError" class="error-text">{{ magicLinkError }}</div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, defineProps, defineEmits } from 'vue';
import { marshalsApi } from '../../../services/api';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  eventId: {
    type: String,
    default: null,
  },
  marshalId: {
    type: String,
    default: null,
  },
});

const emit = defineEmits(['update:form', 'input']);

const magicLink = ref('');
const hasEmail = ref(false);
const loadingMagicLink = ref(false);
const magicLinkError = ref('');
const copySuccess = ref(false);
const sendingEmail = ref(false);
const emailSentSuccess = ref(false);
const emailError = ref('');

const handleInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};

const fetchMagicLink = async () => {
  if (!props.eventId || !props.marshalId) return;

  loadingMagicLink.value = true;
  magicLinkError.value = '';

  try {
    const response = await marshalsApi.getMagicLink(props.eventId, props.marshalId);
    magicLink.value = response.data.magicLink;
    hasEmail.value = response.data.hasEmail;
  } catch (error) {
    console.error('Failed to fetch magic link:', error);
    magicLinkError.value = 'Failed to load login link';
  } finally {
    loadingMagicLink.value = false;
  }
};

const copyLink = async () => {
  try {
    await navigator.clipboard.writeText(magicLink.value);
    copySuccess.value = true;
    setTimeout(() => {
      copySuccess.value = false;
    }, 2000);
  } catch (error) {
    console.error('Failed to copy:', error);
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
  } catch (error) {
    console.error('Failed to send email:', error);
    emailError.value = error.response?.data?.message || 'Failed to send email';
  } finally {
    sendingEmail.value = false;
  }
};

// Fetch magic link when marshalId changes or on mount
watch(() => props.marshalId, (newVal) => {
  if (newVal) {
    fetchMagicLink();
  } else {
    magicLink.value = '';
    hasEmail.value = false;
  }
}, { immediate: true });
</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-primary);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  font-family: inherit;
  background: var(--input-bg);
  color: var(--text-primary);
}

textarea.form-input {
  resize: vertical;
}

/* Magic Link Section */
.magic-link-section {
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid var(--border-color);
}

.magic-link-section label {
  display: block;
  margin-bottom: 0.25rem;
  font-weight: 500;
  color: var(--text-primary);
}

.help-text {
  margin: 0 0 1rem 0;
  font-size: 0.85rem;
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

.magic-link-row {
  display: flex;
  gap: 0.5rem;
  align-items: stretch;
}

.magic-link-input {
  flex: 1;
  min-width: 0;
  background-color: var(--bg-tertiary);
  font-family: monospace;
  font-size: 0.85rem;
}

.magic-link-row .btn {
  flex-shrink: 0;
  white-space: nowrap;
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
  background: var(--accent-primary);
  color: var(--btn-primary-text);
}

.btn-primary:hover:not(:disabled) {
  background: var(--accent-primary-hover);
}

.btn-primary:disabled {
  background: var(--btn-secondary-bg);
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
