<template>
  <Teleport to="body">
    <Transition name="modal">
      <div v-if="show" class="modal-overlay" :style="{ zIndex }" @click.self="handleClose">
        <div class="modal-card">
          <button class="modal-close" @click="handleClose">&times;</button>

          <!-- Email sent - show code input -->
          <div v-if="emailSent" class="code-entry">
            <div class="check-icon">&#10003;</div>
            <h3>Check your email!</h3>
            <p>We've sent a 6-digit code to <strong>{{ email }}</strong></p>

            <form @submit.prevent="handleVerifyCode" class="code-form">
              <div class="code-input-wrapper">
                <input
                  ref="codeInput"
                  v-model="loginCode"
                  type="text"
                  inputmode="numeric"
                  pattern="[0-9]*"
                  maxlength="6"
                  placeholder="000000"
                  class="code-input"
                  :disabled="verifying"
                />
              </div>
              <button type="submit" class="btn btn-primary btn-full" :disabled="verifying || loginCode.length !== 6">
                {{ verifying ? 'Verifying...' : 'Login' }}
              </button>
            </form>

            <div v-if="codeError" class="error">{{ codeError }}</div>

            <p class="hint">The code expires in 15 minutes.</p>
            <button @click="resetForm" class="btn btn-secondary btn-small">
              Use a different email
            </button>
          </div>

          <!-- Login form -->
          <div v-else>
            <h2>Join us or login</h2>
            <p class="instruction">Enter your email to receive a login link</p>

            <form @submit.prevent="handleLogin">
              <div class="form-group">
                <input
                  v-model="email"
                  type="email"
                  placeholder="you@example.com"
                  required
                  class="form-input"
                />
              </div>

              <button type="submit" class="btn btn-primary btn-full" :disabled="loading">
                {{ loading ? 'Sending...' : 'Send login link' }}
              </button>
            </form>

            <div v-if="error" class="error">{{ error }}</div>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<script setup>
import { ref, watch, nextTick } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  zIndex: {
    type: Number,
    default: 1000,
  },
});

const emit = defineEmits(['close', 'success']);

const router = useRouter();
const authStore = useAuthStore();

const email = ref('');
const loading = ref(false);
const error = ref(null);
const emailSent = ref(false);
const loginCode = ref('');
const verifying = ref(false);
const codeError = ref(null);
const codeInput = ref(null);

async function handleLogin() {
  loading.value = true;
  error.value = null;

  try {
    const response = await authStore.requestLogin(email.value);
    if (response.success) {
      emailSent.value = true;
      emit('success', email.value);
      // Focus the code input after it renders
      nextTick(() => {
        codeInput.value?.focus();
      });
    } else {
      error.value = response.message || 'Failed to send login link. Please try again.';
    }
  } catch (err) {
    error.value = 'Failed to send login link. Please try again.';
  } finally {
    loading.value = false;
  }
}

async function handleVerifyCode() {
  if (loginCode.value.length !== 6) return;

  verifying.value = true;
  codeError.value = null;

  try {
    const response = await authStore.verifyCode(email.value, loginCode.value);
    if (response.success) {
      emit('close');
      router.push('/myevents');
    } else {
      codeError.value = response.message || 'Invalid code. Please try again.';
    }
  } catch (err) {
    console.error('Verify code error:', err);
    codeError.value = err.response?.data?.message || 'Failed to verify code. Please try again.';
  } finally {
    verifying.value = false;
  }
}

function resetForm() {
  email.value = '';
  emailSent.value = false;
  error.value = null;
  loginCode.value = '';
  codeError.value = null;
}

function handleClose() {
  emit('close');
}

// Reset form when modal closes
watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Delay reset to allow close animation
    setTimeout(() => {
      resetForm();
    }, 200);
  }
});
</script>

<style scoped>
/* Modal transitions */
.modal-enter-active,
.modal-leave-active {
  transition: opacity 0.2s ease;
}

.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}

.modal-enter-active .modal-card,
.modal-leave-active .modal-card {
  transition: transform 0.2s ease;
}

.modal-enter-from .modal-card,
.modal-leave-to .modal-card {
  transform: scale(0.95);
}

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
}

.modal-card {
  background: #1e1e2e;
  border: 1px solid rgba(255, 255, 255, 0.1);
  padding: 2rem;
  border-radius: 16px;
  max-width: 380px;
  width: 100%;
  position: relative;
}

.modal-close {
  position: absolute;
  top: 0.75rem;
  right: 0.75rem;
  background: none;
  border: none;
  color: #888;
  font-size: 1.5rem;
  cursor: pointer;
  line-height: 1;
  padding: 0.25rem;
}

.modal-close:hover {
  color: white;
}

.modal-card h2 {
  margin: 0 0 0.5rem;
  font-size: 1.5rem;
  color: white;
}

.modal-card .instruction {
  color: #aaa;
  margin-bottom: 1.5rem;
  font-size: 0.9rem;
}

.modal-card .form-group {
  margin-bottom: 1rem;
}

.modal-card .form-input {
  width: 100%;
  padding: 0.75rem 1rem;
  font-size: 1rem;
  border: 1px solid rgba(255, 255, 255, 0.2);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.05);
  color: white;
  box-sizing: border-box;
}

.modal-card .form-input:focus {
  outline: none;
  border-color: #667eea;
}

.modal-card .form-input::placeholder {
  color: #666;
}

.btn {
  display: inline-block;
  padding: 0.75rem 2rem;
  font-size: 1rem;
  text-decoration: none;
  border-radius: 50px;
  transition: all 0.3s;
  border: none;
  cursor: pointer;
}

.btn-full {
  width: 100%;
}

.btn-primary {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  font-weight: 600;
  box-shadow: 0 4px 15px rgba(102, 126, 234, 0.4);
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(102, 126, 234, 0.5);
}

.btn-primary:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
  border: 1px solid rgba(255, 255, 255, 0.2);
}

.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.15);
}

.error {
  margin-top: 1rem;
  padding: 0.75rem;
  background: rgba(220, 53, 69, 0.2);
  color: #ff6b6b;
  border-radius: 6px;
  font-size: 0.85rem;
}

.code-entry {
  text-align: center;
}

.code-entry h3 {
  color: #4ade80;
  margin-bottom: 0.75rem;
  font-size: 1.25rem;
}

.code-entry p {
  color: #ccc;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.code-entry .hint {
  font-size: 0.8rem;
  color: #888;
  margin-bottom: 1rem;
  margin-top: 1rem;
}

.code-form {
  margin: 1.5rem 0;
}

.code-input-wrapper {
  margin-bottom: 1rem;
}

.code-input {
  width: 100%;
  padding: 1rem;
  font-size: 2rem;
  font-weight: 700;
  letter-spacing: 0.5rem;
  text-align: center;
  border: 2px solid rgba(102, 126, 234, 0.3);
  border-radius: 12px;
  background: rgba(102, 126, 234, 0.1);
  color: #667eea;
  box-sizing: border-box;
  font-family: monospace;
}

.code-input:focus {
  outline: none;
  border-color: #667eea;
  background: rgba(102, 126, 234, 0.15);
}

.code-input::placeholder {
  color: rgba(102, 126, 234, 0.3);
}

.btn-small {
  padding: 0.5rem 1rem;
  font-size: 0.85rem;
}

.check-icon {
  width: 50px;
  height: 50px;
  background: rgba(74, 222, 128, 0.2);
  color: #4ade80;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  margin: 0 auto 1rem;
}

@media (max-width: 480px) {
  .modal-card {
    padding: 1.5rem;
  }
}
</style>
