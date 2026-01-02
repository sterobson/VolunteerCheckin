<template>
  <div class="offline-indicator-container">
    <!-- Offline Banner -->
    <transition name="slide">
      <div v-if="!isFullyOnline" class="offline-banner">
        <span class="offline-icon">
          <svg viewBox="0 0 24 24" fill="currentColor">
            <path d="M23.64 7c-.45-.34-4.93-4-11.64-4-1.5 0-2.89.19-4.15.48L18.18 13.8 23.64 7zm-6.6 8.22L3.27 1.44 2 2.72l2.05 2.06C1.91 5.76.59 6.82.36 7l11.63 14.49.01.01.01-.01 3.9-4.86 3.32 3.32 1.27-1.27-3.46-3.46z"/>
          </svg>
        </span>
        <span class="offline-text">
          {{ isOnline ? 'Server unavailable' : 'You\'re offline' }}
        </span>
        <span v-if="lastSyncTime" class="last-sync">
          Last synced: {{ formatLastSync }}
        </span>
      </div>
    </transition>

    <!-- Pending Actions Badge (shows when there are pending actions, online or offline) -->
    <transition name="fade">
      <div v-if="pendingActionsCount > 0" class="pending-banner" :class="{ 'offline-pending': !isFullyOnline }">
        <span class="pending-icon">
          <svg viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/>
          </svg>
        </span>
        <span class="pending-text">
          {{ pendingActionsCount }} pending action{{ pendingActionsCount !== 1 ? 's' : '' }}
          <span v-if="!isFullyOnline" class="pending-subtext">(will sync when online)</span>
        </span>
        <button
          v-if="isFullyOnline && syncStatus !== 'syncing'"
          @click="handleSync"
          class="sync-button"
        >
          Sync Now
        </button>
        <span v-else-if="syncStatus === 'syncing'" class="syncing-text">Syncing...</span>
      </div>
    </transition>

    <!-- Sync Success/Error Toast -->
    <transition name="fade">
      <div v-if="showToast" class="sync-toast" :class="toastClass">
        {{ toastMessage }}
      </div>
    </transition>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useOffline } from '../composables/useOffline';

const {
  isOnline,
  isFullyOnline,
  pendingActionsCount,
  lastSyncTime,
  syncStatus,
  syncError,
  forcSync
} = useOffline();

const showToast = ref(false);
const toastMessage = ref('');
const toastClass = ref('');

const formatLastSync = computed(() => {
  if (!lastSyncTime.value) return '';
  const date = new Date(lastSyncTime.value);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  return date.toLocaleDateString();
});

const handleSync = async () => {
  await forcSync();
};

// Show toast on sync status change
watch(syncStatus, (status) => {
  if (status === 'success' && pendingActionsCount.value === 0) {
    toastMessage.value = 'All actions synced successfully!';
    toastClass.value = 'success';
    showToast.value = true;
    setTimeout(() => { showToast.value = false; }, 3000);
  } else if (status === 'error') {
    toastMessage.value = syncError.value || 'Sync failed';
    toastClass.value = 'error';
    showToast.value = true;
    setTimeout(() => { showToast.value = false; }, 5000);
  }
});
</script>

<style scoped>
.offline-indicator-container {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 9999;
  pointer-events: none;
}

.offline-banner,
.pending-banner {
  pointer-events: auto;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 0.5rem 1rem;
  font-size: 0.85rem;
  font-weight: 500;
}

.offline-banner {
  background: linear-gradient(135deg, #ff6b6b 0%, #ee5a5a 100%);
  color: white;
}

.pending-banner {
  background: linear-gradient(135deg, #ffc107 0%, #ffb300 100%);
  color: #333;
}

.pending-banner.offline-pending {
  background: linear-gradient(135deg, #ff9800 0%, #f57c00 100%);
  color: white;
}

.pending-subtext {
  font-size: 0.75rem;
  opacity: 0.9;
}

.offline-icon,
.pending-icon {
  display: flex;
  align-items: center;
}

.offline-icon svg,
.pending-icon svg {
  width: 18px;
  height: 18px;
}

.last-sync {
  opacity: 0.85;
  font-size: 0.8rem;
  margin-left: 0.5rem;
}

.sync-button {
  background: rgba(0, 0, 0, 0.15);
  border: none;
  padding: 0.25rem 0.75rem;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.8rem;
  color: inherit;
  transition: background 0.2s;
  margin-left: 0.5rem;
}

.sync-button:hover {
  background: rgba(0, 0, 0, 0.25);
}

.syncing-text {
  font-size: 0.8rem;
  opacity: 0.8;
  margin-left: 0.5rem;
}

.sync-toast {
  pointer-events: auto;
  position: fixed;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  padding: 0.75rem 1.5rem;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 500;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.sync-toast.success {
  background: #4caf50;
  color: white;
}

.sync-toast.error {
  background: #ff6b6b;
  color: white;
}

/* Transitions */
.slide-enter-active,
.slide-leave-active {
  transition: all 0.3s ease;
}

.slide-enter-from,
.slide-leave-to {
  transform: translateY(-100%);
  opacity: 0;
}

.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
