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
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 2rem;
}

.login-card {
  background: white;
  padding: 3rem;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  max-width: 400px;
  width: 100%;
}

h2 {
  margin-bottom: 1rem;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 2rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-input {
  width: 100%;
  padding: 0.875rem;
  font-size: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
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
  background: #667eea;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #5568d3;
  transform: translateY(-2px);
  box-shadow: 0 10px 20px rgba(102, 126, 234, 0.3);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  margin-top: 1rem;
  padding: 0.75rem;
  background: #fee;
  color: #c33;
  border-radius: 6px;
  font-size: 0.875rem;
}

.back-link {
  display: block;
  margin-top: 2rem;
  text-align: center;
  color: #667eea;
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
  color: #28a745;
  margin-bottom: 1rem;
}

.email-sent p {
  color: #666;
  margin-bottom: 0.5rem;
}

.email-sent .hint {
  font-size: 0.875rem;
  color: #999;
  margin-bottom: 1.5rem;
}

.check-icon {
  width: 60px;
  height: 60px;
  background: #d4edda;
  color: #28a745;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 2rem;
  margin: 0 auto 1.5rem;
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover {
  background: #d0d0d0;
}
</style>
