<template>
  <div class="payment-success-page">
    <div class="payment-card">
      <template v-if="status === 'processing'">
        <div class="spinner"></div>
        <h2>Processing your payment...</h2>
        <p>Please wait while we set up your event.</p>
      </template>

      <template v-else-if="status === 'success'">
        <div class="success-icon">&#10003;</div>
        <h2>Payment successful!</h2>
        <p v-if="isUpgrade">Your marshal tier has been upgraded.</p>
        <p v-else>Your event is being created. Redirecting...</p>
      </template>

      <template v-else-if="status === 'timeout'">
        <div class="info-icon">&#8505;</div>
        <h2>Payment received</h2>
        <p>Your payment was successful. Your event is being set up and will appear in My Events shortly.</p>
        <router-link to="/myevents" class="btn btn-primary mt-3">Go to My Events</router-link>
      </template>

      <template v-else-if="status === 'error'">
        <div class="error-icon">&#10007;</div>
        <h2>Something went wrong</h2>
        <p>{{ errorMessage }}</p>
        <router-link to="/myevents" class="btn btn-primary mt-3">Go to My Events</router-link>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { paymentsApi } from '../services/api'

const route = useRoute()
const router = useRouter()

const status = ref('processing')
const errorMessage = ref('')
const isUpgrade = ref(false)
let pollInterval = null

onMounted(async () => {
  const sessionId = route.query.session_id
  isUpgrade.value = route.query.upgrade === 'true'
  const upgradeEventId = route.query.eventId

  if (!sessionId) {
    status.value = 'error'
    errorMessage.value = 'No payment session found.'
    return
  }

  // Poll for payment verification
  let attempts = 0
  const maxAttempts = 15 // 30 seconds (2s intervals)

  pollInterval = setInterval(async () => {
    attempts++
    try {
      const response = await paymentsApi.verifySession(sessionId)
      if (response.data.isComplete) {
        clearInterval(pollInterval)
        status.value = 'success'

        // Redirect after a short delay
        setTimeout(() => {
          if (isUpgrade.value && upgradeEventId) {
            router.push(`/admin/event/${upgradeEventId}`)
          } else if (response.data.eventId) {
            router.push(`/admin/event/${response.data.eventId}`)
          } else {
            router.push('/myevents')
          }
        }, 1500)
      } else if (attempts >= maxAttempts) {
        clearInterval(pollInterval)
        status.value = 'timeout'
      }
    } catch (err) {
      if (attempts >= maxAttempts) {
        clearInterval(pollInterval)
        status.value = 'timeout'
      }
    }
  }, 2000)
})

onUnmounted(() => {
  if (pollInterval) {
    clearInterval(pollInterval)
  }
})
</script>

<style scoped>
.payment-success-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 80vh;
  padding: 2rem;
}

.payment-card {
  background: white;
  border-radius: 12px;
  padding: 3rem;
  text-align: center;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.1);
}

.payment-card h2 {
  margin: 1rem 0 0.5rem;
  font-size: 1.5rem;
  color: #1a1a2e;
}

.payment-card p {
  color: #666;
  margin-bottom: 0;
}

.spinner {
  width: 48px;
  height: 48px;
  border: 4px solid #e0e0e0;
  border-top-color: #667eea;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.success-icon {
  width: 48px;
  height: 48px;
  background: #10b981;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  margin: 0 auto;
}

.info-icon {
  width: 48px;
  height: 48px;
  background: #3b82f6;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  margin: 0 auto;
}

.error-icon {
  width: 48px;
  height: 48px;
  background: #ef4444;
  color: white;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  margin: 0 auto;
}

.btn {
  display: inline-block;
  padding: 0.75rem 1.5rem;
  border-radius: 8px;
  text-decoration: none;
  font-weight: 500;
  cursor: pointer;
  border: none;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover {
  background: #5a6fd6;
}

.mt-3 {
  margin-top: 1.5rem;
}
</style>
