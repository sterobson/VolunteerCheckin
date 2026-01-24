<template>
  <div class="admin-verify">
    <div class="verify-card">
      <div v-if="loading" class="status">
        <div class="spinner"></div>
        <h2>Verifying your login...</h2>
        <p>Please wait while we confirm your identity.</p>
      </div>

      <div v-else-if="error" class="status error">
        <h2>Login failed</h2>
        <p>{{ error }}</p>
        <router-link :to="{ name: 'Home', query: { login: 'true' } }" class="btn btn-primary">
          Try again
        </router-link>
      </div>

      <div v-else class="status success">
        <h2>Login successful!</h2>
        <p>Redirecting to your events...</p>
      </div>
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

const loading = ref(true);
const error = ref(null);

onMounted(async () => {
  const token = route.query.token;

  if (!token) {
    error.value = 'No login token provided. Please request a new login link.';
    loading.value = false;
    return;
  }

  try {
    const response = await authStore.verifyToken(token);
    if (response.success) {
      // Small delay so user sees success message
      setTimeout(() => {
        router.push({ name: 'MyEvents' });
      }, 1000);
    } else {
      error.value = response.message || 'Invalid or expired login link. Please request a new one.';
    }
  } catch (err) {
    console.error('Verification error:', err);
    error.value = 'Failed to verify login. Please try again.';
  } finally {
    loading.value = false;
  }
});
</script>

<style scoped>
.admin-verify {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--brand-gradient);
  padding: 2rem;
}

.verify-card {
  background: var(--card-bg);
  padding: 3rem;
  border-radius: 12px;
  box-shadow: var(--shadow-xl);
  max-width: 400px;
  width: 100%;
  text-align: center;
}

.status h2 {
  margin-bottom: 1rem;
  color: var(--text-dark);
}

.status p {
  color: var(--text-secondary);
  margin-bottom: 1.5rem;
}

.status.error h2 {
  color: var(--danger);
}

.status.success h2 {
  color: var(--success);
}

.spinner {
  width: 50px;
  height: 50px;
  border: 4px solid var(--border-light);
  border-top-color: var(--brand-primary);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin: 0 auto 1.5rem;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.btn {
  display: inline-block;
  padding: 0.875rem 1.5rem;
  font-size: 1rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  text-decoration: none;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--brand-primary-hover);
}
</style>
