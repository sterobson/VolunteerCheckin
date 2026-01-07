import { ref, computed, onMounted, onUnmounted } from 'vue';
import {
  initDb,
  getPendingActions,
  getPendingActionsCount,
  removePendingAction,
  updatePendingAction,
  getLastSyncTime,
  getCachedEventData,
  cacheEventData
} from '../services/offlineDb';
import { checkInApi, checklistApi, healthApi } from '../services/api';

// Singleton state - shared across all components using this composable
const isOnline = ref(navigator.onLine);
const isServerHealthy = ref(true);
const pendingActionsCount = ref(0);
const lastSyncTime = ref(null);
const syncStatus = ref('idle'); // 'idle' | 'syncing' | 'error' | 'success'
const syncError = ref(null);

let healthCheckInterval = null;
let syncInProgress = false;
let autoSyncTimeout = null;

/**
 * Get a random delay between min and max milliseconds
 */
function getRandomDelay(minMs = 1000, maxMs = 10000) {
  return Math.floor(Math.random() * (maxMs - minMs + 1)) + minMs;
}

/**
 * Schedule an auto-sync with random delay
 */
function scheduleAutoSync() {
  // Clear any existing scheduled sync
  if (autoSyncTimeout) {
    clearTimeout(autoSyncTimeout);
    autoSyncTimeout = null;
  }

  const delay = getRandomDelay(1000, 10000);
  console.log(`Scheduling auto-sync in ${delay}ms`);

  autoSyncTimeout = setTimeout(async () => {
    autoSyncTimeout = null;

    if (!navigator.onLine) {
      console.log('Auto-sync cancelled: offline');
      return;
    }

    const count = await getPendingActionsCount();
    if (count === 0) {
      console.log('Auto-sync cancelled: no pending actions');
      return;
    }

    console.log('Starting auto-sync...');
    await syncPendingActions();

    // If there are still pending actions (some failed), schedule another retry
    const remainingCount = await getPendingActionsCount();
    if (remainingCount > 0 && navigator.onLine) {
      console.log(`${remainingCount} actions remaining, scheduling retry...`);
      scheduleAutoSync();
    }
  }, delay);
}

/**
 * Check if the server is responding
 */
async function checkServerHealth() {
  if (!navigator.onLine) {
    isServerHealthy.value = false;
    return false;
  }

  try {
    // Use a lightweight endpoint to check server health
    await healthApi.check();
    isServerHealthy.value = true;
    return true;
  } catch (error) {
    // If health endpoint doesn't exist (404), assume server is healthy
    if (error.response?.status === 404) {
      isServerHealthy.value = true;
      return true;
    }
    if (error.code === 'ECONNABORTED') {
      console.warn('Server health check timed out');
    }
    isServerHealthy.value = false;
    return false;
  }
}

/**
 * Sync a single pending action
 */
async function syncAction(action) {
  try {
    switch (action.type) {
      case 'checkin':
        await checkInApi.checkIn(action.payload);
        break;

      case 'checkin_admin':
        await checkInApi.adminCheckIn(action.payload.eventId, action.payload.assignmentId);
        break;

      case 'checklist_complete':
        await checklistApi.complete(
          action.payload.eventId,
          action.payload.itemId,
          action.payload.data
        );
        break;

      case 'checklist_uncomplete':
        await checklistApi.uncomplete(
          action.payload.eventId,
          action.payload.itemId,
          action.payload.data
        );
        break;

      default:
        console.warn('Unknown action type:', action.type);
        return false;
    }

    return true;
  } catch (error) {
    console.error('Failed to sync action:', action, error);

    // Check if it's a permanent failure (4xx) or temporary (5xx/network)
    if (error.response && error.response.status >= 400 && error.response.status < 500) {
      // Permanent failure - don't retry
      return 'permanent_failure';
    }

    return false;
  }
}

/**
 * Sync all pending actions
 */
async function syncPendingActions() {
  if (syncInProgress) return;
  if (!navigator.onLine) return;

  syncInProgress = true;
  syncStatus.value = 'syncing';
  syncError.value = null;

  try {
    const actions = await getPendingActions();

    if (actions.length === 0) {
      syncStatus.value = 'success';
      pendingActionsCount.value = 0;
      return;
    }

    let successCount = 0;
    let failCount = 0;

    for (const action of actions) {
      const result = await syncAction(action);

      if (result === true) {
        // Success - remove from queue
        await removePendingAction(action.id);
        successCount++;
      } else if (result === 'permanent_failure') {
        // Permanent failure - mark as failed and remove
        await removePendingAction(action.id);
        failCount++;
      } else {
        // Temporary failure - update attempt count
        const newAttempts = (action.attempts || 0) + 1;

        if (newAttempts >= 5) {
          // Max retries reached - mark as failed
          await updatePendingAction(action.id, {
            status: 'failed',
            attempts: newAttempts,
            lastAttempt: new Date().toISOString()
          });
          failCount++;
        } else {
          await updatePendingAction(action.id, {
            status: 'retrying',
            attempts: newAttempts,
            lastAttempt: new Date().toISOString()
          });
        }
      }
    }

    // Update pending count
    pendingActionsCount.value = await getPendingActionsCount();

    if (failCount > 0) {
      syncStatus.value = 'error';
      syncError.value = `${failCount} action(s) failed to sync`;
    } else {
      syncStatus.value = 'success';
    }
  } catch (error) {
    console.error('Sync error:', error);
    syncStatus.value = 'error';
    syncError.value = 'Failed to sync pending actions';
  } finally {
    syncInProgress = false;
  }
}

/**
 * Start periodic health checks
 */
function startHealthCheck() {
  if (healthCheckInterval) return;

  // Check every 30 seconds
  healthCheckInterval = setInterval(async () => {
    if (navigator.onLine) {
      await checkServerHealth();

      // If server is healthy and we have pending actions, try to sync
      if (isServerHealthy.value && pendingActionsCount.value > 0) {
        await syncPendingActions();
      }
    }
  }, 30000);
}

/**
 * Stop periodic health checks
 */
function stopHealthCheck() {
  if (healthCheckInterval) {
    clearInterval(healthCheckInterval);
    healthCheckInterval = null;
  }
}

/**
 * Composable for offline state management
 */
export function useOffline() {
  const isFullyOnline = computed(() => isOnline.value && isServerHealthy.value);

  // Update pending actions count
  async function updatePendingCount() {
    pendingActionsCount.value = await getPendingActionsCount();
  }

  // Handle online/offline events
  async function handleOnline() {
    isOnline.value = true;
    const healthy = await checkServerHealth();

    // If we're back online and have pending actions, schedule auto-sync
    if (healthy && pendingActionsCount.value > 0) {
      scheduleAutoSync();
    }
  }

  function handleOffline() {
    isOnline.value = false;
    isServerHealthy.value = false;

    // Cancel any scheduled auto-sync
    if (autoSyncTimeout) {
      clearTimeout(autoSyncTimeout);
      autoSyncTimeout = null;
    }
  }

  onMounted(async () => {
    // Initialize database
    await initDb();

    // Update pending actions count
    await updatePendingCount();

    // Set up event listeners
    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    // Check server health on mount
    if (navigator.onLine) {
      const healthy = await checkServerHealth();

      // If we're online with pending actions, schedule auto-sync
      if (healthy && pendingActionsCount.value > 0) {
        scheduleAutoSync();
      }
    }

    // Start health checks
    startHealthCheck();
  });

  onUnmounted(() => {
    window.removeEventListener('online', handleOnline);
    window.removeEventListener('offline', handleOffline);

    // Clear auto-sync timeout
    if (autoSyncTimeout) {
      clearTimeout(autoSyncTimeout);
      autoSyncTimeout = null;
    }
  });

  /**
   * Refresh cached data for an event
   */
  async function refreshCache(eventId, dataFetcher) {
    if (!isFullyOnline.value) {
      console.warn('Cannot refresh cache while offline');
      return false;
    }

    try {
      const data = await dataFetcher();
      await cacheEventData(eventId, data);
      lastSyncTime.value = new Date().toISOString();
      return true;
    } catch (error) {
      console.error('Failed to refresh cache:', error);
      return false;
    }
  }

  /**
   * Get cached data for an event
   */
  async function getCachedData(eventId) {
    const data = await getCachedEventData(eventId);
    if (data) {
      lastSyncTime.value = data.cachedAt;
    }
    return data;
  }

  /**
   * Load last sync time for an event
   */
  async function loadLastSyncTime(eventId) {
    lastSyncTime.value = await getLastSyncTime(eventId);
  }

  /**
   * Force sync now
   */
  async function forcSync() {
    if (!navigator.onLine) {
      syncError.value = 'Cannot sync while offline';
      return false;
    }

    const healthy = await checkServerHealth();
    if (!healthy) {
      syncError.value = 'Server is not responding';
      return false;
    }

    await syncPendingActions();
    return syncStatus.value === 'success';
  }

  return {
    // State
    isOnline,
    isServerHealthy,
    isFullyOnline,
    pendingActionsCount,
    lastSyncTime,
    syncStatus,
    syncError,

    // Actions
    checkServerHealth,
    syncPendingActions,
    updatePendingCount,
    refreshCache,
    getCachedData,
    loadLastSyncTime,
    forcSync
  };
}

export default useOffline;
