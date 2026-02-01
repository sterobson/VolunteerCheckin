<template>
  <div class="demo-banner" v-if="visible">
    <div class="demo-banner-content">
      <span class="demo-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <polyline points="12 6 12 12 16 14"></polyline>
        </svg>
      </span>
      <span class="demo-text">
        Demo mode - expires in {{ timeRemaining }}
      </span>
      <button class="demo-cta" @click="handleSignUp">
        Sign up to keep your event
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRouter } from 'vue-router';

const props = defineProps({
  expiresAt: {
    type: [String, Date],
    required: true
  }
});

const router = useRouter();
const now = ref(new Date());
let intervalId = null;

const visible = computed(() => {
  if (!props.expiresAt) return false;
  const expires = new Date(props.expiresAt);
  return expires > now.value;
});

const timeRemaining = computed(() => {
  if (!props.expiresAt) return '';

  const expires = new Date(props.expiresAt);
  const diff = expires - now.value;

  if (diff <= 0) return 'expired';

  const hours = Math.floor(diff / (1000 * 60 * 60));
  const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));

  if (hours > 0) {
    return `${hours}h ${minutes}m`;
  }
  return `${minutes}m`;
});

function handleSignUp() {
  router.push('/?login=true');
}

onMounted(() => {
  // Update the time every minute
  intervalId = setInterval(() => {
    now.value = new Date();
  }, 60000);
});

onUnmounted(() => {
  if (intervalId) {
    clearInterval(intervalId);
  }
});
</script>

<style scoped>
.demo-banner {
  position: sticky;
  top: 0;
  left: 0;
  right: 0;
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  color: white;
  padding: 0.5rem 1rem;
  z-index: 1000;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.demo-banner-content {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  max-width: 1200px;
  margin: 0 auto;
}

.demo-icon {
  display: flex;
  align-items: center;
}

.demo-text {
  font-weight: 500;
  font-size: 0.9rem;
}

.demo-cta {
  background: white;
  color: #d97706;
  border: none;
  padding: 0.35rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.demo-cta:hover {
  background: #fef3c7;
  transform: translateY(-1px);
}

@media (max-width: 480px) {
  .demo-banner-content {
    flex-direction: column;
    gap: 0.5rem;
  }

  .demo-text {
    font-size: 0.85rem;
  }
}
</style>
