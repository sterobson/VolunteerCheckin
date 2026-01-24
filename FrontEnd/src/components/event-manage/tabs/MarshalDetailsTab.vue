<template>
  <div class="tab-content">
    <!-- General error message -->
    <div v-if="validationErrors.general" class="error-banner">
      {{ validationErrors.general }}
    </div>

    <div class="form-group" :class="{ 'has-error': validationErrors.name }">
      <label>Name *</label>
      <input
        ref="nameInputRef"
        :value="form.name"
        @input="handleInput('name', $event.target.value)"
        type="text"
        required
        class="form-input"
        :class="{ 'input-error': validationErrors.name }"
      />
      <span v-if="validationErrors.name" class="field-error">{{ validationErrors.name }}</span>
    </div>

    <div class="contact-row">
      <div class="form-group contact-email" :class="{ 'has-error': validationErrors.email }">
        <label>Email</label>
        <input
          ref="emailInputRef"
          :value="form.email"
          @input="handleInput('email', $event.target.value)"
          type="email"
          class="form-input"
          :class="{ 'input-error': validationErrors.email }"
        />
        <span v-if="validationErrors.email" class="field-error">{{ validationErrors.email }}</span>
      </div>

      <div class="form-group contact-phone" :class="{ 'has-error': validationErrors.phoneNumber }">
        <label>Phone number</label>
        <input
          ref="phoneInputRef"
          :value="form.phoneNumber"
          @input="handleInput('phoneNumber', $event.target.value)"
          type="tel"
          class="form-input"
          :class="{ 'input-error': validationErrors.phoneNumber }"
        />
        <span v-if="validationErrors.phoneNumber" class="field-error">{{ validationErrors.phoneNumber }}</span>
      </div>
    </div>

    <div class="form-group" :class="{ 'has-error': validationErrors.notes }">
      <label>Additional details</label>
      <textarea
        ref="notesInputRef"
        :value="form.notes"
        @input="handleInput('notes', $event.target.value)"
        class="form-input"
        :class="{ 'input-error': validationErrors.notes }"
        rows="3"
        placeholder="e.g., Needs to leave by 11am"
      ></textarea>
      <span v-if="validationErrors.notes" class="field-error">{{ validationErrors.notes }}</span>
    </div>

    <!-- Magic Link Section (only for existing marshals) -->
    <div v-if="marshalId" class="magic-link-section">
      <label>Login link</label>
      <p class="help-text">Share this unique link with the marshal so they can access their dashboard.</p>

      <div v-if="loadingMagicLink" class="loading-text">Loading link...</div>

      <div v-else-if="magicLink" class="magic-link-container">
        <input
          type="text"
          :value="magicLink"
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
          <button
            type="button"
            class="btn btn-secondary btn-icon"
            @click="openEmailModal"
            title="Send via email"
          >
            <!-- Email icon -->
            <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <rect x="2" y="4" width="20" height="16" rx="2"></rect>
              <path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"></path>
            </svg>
          </button>
        </div>
        <p v-if="copyError" class="error-text">{{ copyError }}</p>
        <p v-if="emailSentSuccess" class="success-text">Email sent successfully!</p>
        <p v-if="emailError" class="error-text">{{ emailError }}</p>
      </div>

      <div v-else-if="magicLinkError" class="error-text">{{ magicLinkError }}</div>
    </div>

    <!-- QR Code Modal -->
    <QrCodeModal
      :show="showQrCode"
      :url="magicLink"
      title="Scan to log in"
      @close="showQrCode = false"
    />

    <!-- Email Modal -->
    <Teleport to="body">
      <div v-if="showEmailModal" class="modal-overlay" @click.self="closeEmailModal">
        <div class="modal-content email-modal">
          <div class="modal-header">
            <h3>Send login link via email</h3>
            <button type="button" class="close-btn" @click="closeEmailModal">&times;</button>
          </div>
          <div class="modal-body">
            <div class="form-group">
              <label for="emailInput">Email address</label>
              <input
                id="emailInput"
                ref="emailModalInputRef"
                v-model="emailModalAddress"
                type="email"
                class="form-input"
                placeholder="Enter email address"
                @keyup.enter="sendEmailFromModal"
              />
            </div>
            <div class="form-group checkbox-group">
              <label class="checkbox-label">
                <input
                  type="checkbox"
                  v-model="includeDetails"
                />
                <span>Include event and {{ termsLower.checkpoint }} details</span>
              </label>
              <p v-if="includeDetails" class="help-text">
                The email will include the event start time and your assigned {{ termsLower.checkpoints }} with arrival times.
              </p>
            </div>
            <p v-if="emailModalError" class="error-text">{{ emailModalError }}</p>
          </div>
          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" @click="closeEmailModal">Cancel</button>
            <button
              type="button"
              class="btn btn-primary"
              :disabled="!emailModalAddress || sendingEmail"
              @click="sendEmailFromModal"
            >
              {{ sendingEmail ? 'Sending...' : 'Send' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits, defineExpose, nextTick } from 'vue';
import { marshalsApi } from '../../../services/api';
import QrCodeModal from '../../common/QrCodeModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { termsLower } = useTerminology();

// Check if Web Share API is available
const canShare = computed(() => !!navigator.share);

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  eventId: {
    type: String,
    default: null,
  },
  eventName: {
    type: String,
    default: '',
  },
  marshalId: {
    type: String,
    default: null,
  },
  validationErrors: {
    type: Object,
    default: () => ({}),
  },
});

const emit = defineEmits(['update:form', 'input']);

// Input refs for focusing
const nameInputRef = ref(null);
const emailInputRef = ref(null);
const phoneInputRef = ref(null);
const notesInputRef = ref(null);

const magicLink = ref('');
const hasEmail = ref(false);
const loadingMagicLink = ref(false);
const magicLinkError = ref('');
const copySuccess = ref(false);
const sendingEmail = ref(false);
const emailSentSuccess = ref(false);
const emailError = ref('');
const showQrCode = ref(false);

// Email modal state
const showEmailModal = ref(false);
const emailModalAddress = ref('');
const emailModalInputRef = ref(null);
const emailModalError = ref('');
const includeDetails = ref(false);

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

const copyError = ref('');

const copyLink = async () => {
  copyError.value = '';

  try {
    // Try modern clipboard API first
    if (navigator.clipboard && navigator.clipboard.writeText) {
      await navigator.clipboard.writeText(magicLink.value);
      copySuccess.value = true;
      setTimeout(() => {
        copySuccess.value = false;
      }, 2000);
      return;
    }
  } catch (error) {
    console.warn('Clipboard API failed, trying fallback:', error);
  }

  // Fallback for older browsers (including some Samsung Internet versions)
  try {
    const textArea = document.createElement('textarea');
    textArea.value = magicLink.value;
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
  } catch (error) {
    console.error('Fallback copy failed:', error);
    copyError.value = 'Copy not supported. Please select and copy the link manually.';
    setTimeout(() => {
      copyError.value = '';
    }, 4000);
  }
};

const shareLink = async () => {
  if (!navigator.share || !magicLink.value) return;

  const marshalName = props.form.name || 'Hi';
  const eventName = props.eventName || 'the event';

  try {
    await navigator.share({
      title: 'Your OnTheDayApp login link',
      text: `${marshalName}, here's your magic link for ${eventName} in OnTheDayApp`,
      url: magicLink.value,
    });
  } catch (error) {
    // User cancelled or share failed - silently ignore
    if (error.name !== 'AbortError') {
      console.error('Share failed:', error);
    }
  }
};

// Email modal functions
const openEmailModal = () => {
  // Pre-fill with marshal's email if available
  emailModalAddress.value = props.form.email || '';
  emailModalError.value = '';
  includeDetails.value = false;
  showEmailModal.value = true;
  nextTick(() => {
    emailModalInputRef.value?.focus();
  });
};

const closeEmailModal = () => {
  showEmailModal.value = false;
  emailModalAddress.value = '';
  emailModalError.value = '';
  includeDetails.value = false;
};

const sendEmailFromModal = async () => {
  if (!props.eventId || !props.marshalId || !emailModalAddress.value) return;

  sendingEmail.value = true;
  emailSentSuccess.value = false;
  emailError.value = '';
  emailModalError.value = '';

  try {
    await marshalsApi.sendMagicLink(props.eventId, props.marshalId, {
      email: emailModalAddress.value,
      includeDetails: includeDetails.value,
    });
    emailSentSuccess.value = true;
    closeEmailModal();
    setTimeout(() => {
      emailSentSuccess.value = false;
    }, 3000);
  } catch (error) {
    console.error('Failed to send email:', error);
    emailModalError.value = error.response?.data?.message || 'Failed to send email';
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

// Focus on a specific field
const focusField = (fieldName) => {
  nextTick(() => {
    const refMap = {
      name: nameInputRef,
      email: emailInputRef,
      phoneNumber: phoneInputRef,
      notes: notesInputRef,
    };
    const inputRef = refMap[fieldName];
    if (inputRef?.value) {
      inputRef.value.focus();
      inputRef.value.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  });
};

defineExpose({
  focusField,
});
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
  container-type: inline-size;
  display: flex;
  flex-direction: row;
  flex-wrap: wrap;
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

/* Validation error styles */
.error-banner {
  background: var(--danger-bg);
  border: 1px solid var(--danger-border);
  color: var(--danger-text);
  padding: 0.75rem 1rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.form-group.has-error label {
  color: var(--danger);
}

.form-input.input-error {
  border-color: var(--danger);
}

.form-input.input-error:focus {
  border-color: var(--danger);
  box-shadow: 0 0 0 2px var(--danger-bg);
}

.field-error {
  display: block;
  margin-top: 0.25rem;
  color: var(--danger);
  font-size: 0.85rem;
}

/* Contact row - responsive email/phone layout */
.contact-row {
  display: flex;
  flex-direction: column;
}

.contact-row .form-group {
  margin-bottom: 1.5rem;
}

@media (min-width: 390px) {
  .contact-row {
    flex-direction: row;
    gap: 1rem;
  }

  .contact-email {
    flex: 0 0 60%;
    min-width: 0;
  }

  .contact-phone {
    flex: 0 0 calc(40% - 1rem);
    min-width: 0;
  }
}

@media (min-width: 500px) {
  .contact-email {
    flex: 1 1 50%;
  }

  .contact-phone {
    flex: 1 1 50%;
  }
}

/* Email Modal Styles */
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
  z-index: 1100;
  padding: 1rem;
}

.modal-content.email-modal {
  background: var(--bg-primary);
  border-radius: 8px;
  width: 100%;
  max-width: 400px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  border-bottom: 1px solid var(--border-color);
}

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-primary);
}

.close-btn {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.close-btn:hover {
  color: var(--text-primary);
}

.modal-body {
  padding: 1rem;
}

.modal-body .form-group {
  margin-bottom: 1rem;
}

.modal-body .form-group:last-of-type {
  margin-bottom: 0;
}

.checkbox-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checkbox-label {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: normal;
}

.checkbox-label input[type="checkbox"] {
  margin-top: 0.2rem;
  flex-shrink: 0;
}

.checkbox-label span {
  color: var(--text-primary);
}

.modal-body .help-text {
  margin: 0;
  padding-left: 1.5rem;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding: 1rem;
  border-top: 1px solid var(--border-color);
}
</style>
