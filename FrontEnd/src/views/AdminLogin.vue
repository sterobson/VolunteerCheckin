<template>
  <div class="admin-login">
    <div class="login-card">
      <h2>Admin login</h2>

      <!-- Email sent confirmation -->
      <div v-if="emailSent" class="email-sent">
        <div class="check-icon">&#10003;</div>
        <h3>Check your email!</h3>
        <p>We've sent a login link to <strong>{{ email }}</strong></p>
        <p class="hint">The link will expire in 15 minutes.</p>
        <button @click="resetForm" class="btn btn-secondary">
          Use a different email
        </button>
      </div>

      <!-- Login form -->
      <template v-else>
        <p class="instruction">Enter your email to receive a login link</p>

        <form @submit.prevent="handleLogin">
          <div class="form-group">
            <input
              v-model="email"
              type="email"
              placeholder="admin@example.com"
              required
              class="form-input"
            />
          </div>

          <button type="submit" class="btn btn-primary" :disabled="loading">
            {{ loading ? 'Sending...' : 'Send login link' }}
          </button>
        </form>

        <div v-if="error" class="error">{{ error }}</div>
      </template>

      <router-link to="/" class="back-link">← Back to Home</router-link>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue';
import { useAuthStore } from '../stores/auth';

const authStore = useAuthStore();

const email = ref('');
const loading = ref(false);
const error = ref(null);
const emailSent = ref(false);

const handleLogin = async () => {
  loading.value = true;
  error.value = null;

  try {
    const response = await authStore.requestLogin(email.value);
    if (response.success) {
      emailSent.value = true;
    } else {
      error.value = response.message || 'Failed to send login link. Please try again.';
    }
  } catch (err) {
    error.value = 'Failed to send login link. Please try again.';
  } finally {
    loading.value = false;
  }
};

const resetForm = () => {
  email.value = '';
  emailSent.value = false;
  error.value = null;
};
</script>

<style scoped>
.admin-login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--brand-gradient);
  padding: 2rem;
}

.login-card {
  background: var(--card-bg);
  padding: 3rem;
  border-radius: 12px;
  box-shadow: var(--shadow-xl);
  max-width: 400px;
  width: 100%;
}

h2 {
  margin-bottom: 1rem;
  color: var(--text-dark);
}

.instruction {
  color: var(--text-secondary);
  margin-bottom: 2rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-input {
  width: 100%;
  padding: 0.875rem;
  font-size: 1rem;
  border: 2px solid var(--border-light);
  border-radius: 8px;
  transition: border-color 0.3s;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.btn {
  width: 100%;
  padding: 1rem;
  font-size: 1rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;
  font-weight: 600;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--brand-primary-hover);
  transform: translateY(-2px);
  box-shadow: 0 10px 20px var(--brand-shadow-md);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  margin-top: 1rem;
  padding: 0.75rem;
  background: var(--danger-bg-lighter);
  color: var(--danger);
  border-radius: 6px;
  font-size: 0.875rem;
}

.back-link {
  display: block;
  margin-top: 2rem;
  text-align: center;
  color: var(--brand-primary);
  text-decoration: none;
  font-weight: 500;
}

.back-link:hover {
  text-decoration: underline;
}

.email-sent {
  text-align: center;
}

.email-sent h3 {
  color: var(--success);
  margin-bottom: 1rem;
}

.email-sent p {
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.email-sent .hint {
  font-size: 0.875rem;
  color: var(--text-muted);
  margin-bottom: 1.5rem;
}

.check-icon {
  width: 60px;
  height: 60px;
  background: var(--success-bg);
  color: var(--success);
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  margin: 0 auto 1.5rem;
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover {
  background: var(--btn-cancel-hover);
}
</style>
