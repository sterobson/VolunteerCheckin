<template>
  <div class="admin-login">
    <div class="login-card">
      <h2>Admin Login</h2>

      <div v-if="!tokenSent && !validatingToken">
        <p class="instruction">Enter your admin email to receive a secure login link</p>

        <form @submit.prevent="handleRequestLink">
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
            {{ loading ? 'Sending...' : 'Send Login Link' }}
          </button>
        </form>

        <div v-if="error" class="error">{{ error }}</div>
      </div>

      <div v-else-if="tokenSent && !validatingToken" class="success">
        <p>Check your email for the login link!</p>
        <p class="small">The link will expire in 15 minutes.</p>
      </div>

      <div v-else-if="validatingToken" class="validating">
        <p>Validating your login link...</p>
      </div>

      <router-link to="/" class="back-link">← Back to Home</router-link>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import { useAuthStore } from '../stores/auth';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

const email = ref('');
const loading = ref(false);
const error = ref(null);
const tokenSent = ref(false);
const validatingToken = ref(false);

const handleRequestLink = async () => {
  loading.value = true;
  error.value = null;

  try {
    const response = await authStore.requestMagicLink(email.value);
    if (response.success) {
      tokenSent.value = true;
    } else {
      error.value = response.message;
    }
  } catch (err) {
    error.value = 'Failed to send login link. Please try again.';
  } finally {
    loading.value = false;
  }
};

const validateToken = async (token) => {
  validatingToken.value = true;

  try {
    const response = await authStore.validateToken(token);
    if (response.isValid) {
      router.push({ name: 'AdminDashboard' });
    } else {
      error.value = 'Invalid or expired login link.';
      validatingToken.value = false;
    }
  } catch (err) {
    error.value = 'Failed to validate login link.';
    validatingToken.value = false;
  }
};

onMounted(() => {
  const token = route.query.token;
  if (token) {
    validateToken(token);
  }
});
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

.success {
  text-align: center;
  color: #333;
}

.success p {
  margin-bottom: 0.5rem;
}

.success .small {
  font-size: 0.875rem;
  color: #666;
}

.validating {
  text-align: center;
  color: #333;
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
</style>
