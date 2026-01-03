/**
 * IndexedDB service for offline data storage
 * Provides caching for event data and queuing for pending actions
 */

const DB_NAME = 'VolunteerCheckinOffline';
const DB_VERSION = 1;

let db = null;

/**
 * Initialize the IndexedDB database
 */
export async function initDb() {
  if (db) return db;

  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => {
      console.error('Failed to open IndexedDB:', request.error);
      reject(request.error);
    };

    request.onsuccess = () => {
      db = request.result;
      resolve(db);
    };

    request.onupgradeneeded = (event) => {
      const database = event.target.result;

      // Store for cached event data (keyed by eventId)
      if (!database.objectStoreNames.contains('eventData')) {
        database.createObjectStore('eventData', { keyPath: 'eventId' });
      }

      // Store for pending offline actions
      if (!database.objectStoreNames.contains('pendingActions')) {
        const store = database.createObjectStore('pendingActions', { keyPath: 'id', autoIncrement: true });
        store.createIndex('createdAt', 'createdAt', { unique: false });
      }

      // Store for sync metadata
      if (!database.objectStoreNames.contains('syncMeta')) {
        database.createObjectStore('syncMeta', { keyPath: 'key' });
      }
    };
  });
}

/**
 * Cache all event data for offline access
 * @param {string} eventId - The event ID
 * @param {Object} data - Object containing event, locations, assignments, checklist, contacts, notes
 */
export async function cacheEventData(eventId, data) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['eventData', 'syncMeta'], 'readwrite');
    const eventStore = transaction.objectStore('eventData');
    const metaStore = transaction.objectStore('syncMeta');

    const eventData = {
      eventId,
      ...data,
      cachedAt: new Date().toISOString()
    };

    eventStore.put(eventData);
    metaStore.put({ key: `lastSync_${eventId}`, value: new Date().toISOString() });

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

/**
 * Get cached event data
 * @param {string} eventId - The event ID
 * @returns {Object|null} Cached event data or null
 */
export async function getCachedEventData(eventId) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['eventData'], 'readonly');
    const store = transaction.objectStore('eventData');
    const request = store.get(eventId);

    request.onsuccess = () => resolve(request.result || null);
    request.onerror = () => reject(request.error);
  });
}

/**
 * Update a specific field in cached event data
 * @param {string} eventId - The event ID
 * @param {string} field - The field to update (e.g., 'assignments', 'checklist')
 * @param {any} value - The new value
 */
export async function updateCachedField(eventId, field, value) {
  const data = await getCachedEventData(eventId);
  if (data) {
    data[field] = value;
    data.cachedAt = new Date().toISOString();
    await cacheEventData(eventId, data);
  }
}

/**
 * Queue an action for later sync
 * @param {Object} action - The action to queue
 * @param {string} action.type - Action type: 'checkin', 'checklist_complete', 'checklist_uncomplete'
 * @param {Object} action.payload - The action payload
 * @param {string} action.eventId - The event ID
 * @returns {number} The action ID
 */
export async function queueAction(action) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['pendingActions'], 'readwrite');
    const store = transaction.objectStore('pendingActions');

    const actionRecord = {
      ...action,
      createdAt: new Date().toISOString(),
      attempts: 0,
      lastAttempt: null,
      status: 'pending'
    };

    const request = store.add(actionRecord);

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

/**
 * Get all pending actions
 * @returns {Array} Array of pending actions
 */
export async function getPendingActions() {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['pendingActions'], 'readonly');
    const store = transaction.objectStore('pendingActions');
    const index = store.index('createdAt');
    const request = index.getAll();

    request.onsuccess = () => {
      const actions = request.result.filter(a => a.status === 'pending' || a.status === 'retrying');
      resolve(actions);
    };
    request.onerror = () => reject(request.error);
  });
}

/**
 * Get pending actions count
 * @returns {number} Number of pending actions
 */
export async function getPendingActionsCount() {
  const actions = await getPendingActions();
  return actions.length;
}

/**
 * Update a pending action (e.g., increment attempts, mark as failed)
 * @param {number} id - The action ID
 * @param {Object} updates - Fields to update
 */
export async function updatePendingAction(id, updates) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['pendingActions'], 'readwrite');
    const store = transaction.objectStore('pendingActions');
    const getRequest = store.get(id);

    getRequest.onsuccess = () => {
      const action = getRequest.result;
      if (action) {
        Object.assign(action, updates);
        store.put(action);
      }
    };

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

/**
 * Remove a pending action after successful sync
 * @param {number} id - The action ID
 */
export async function removePendingAction(id) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['pendingActions'], 'readwrite');
    const store = transaction.objectStore('pendingActions');
    const request = store.delete(id);

    request.onsuccess = () => resolve();
    request.onerror = () => reject(request.error);
  });
}

/**
 * Get the last sync time for an event
 * @param {string} eventId - The event ID
 * @returns {string|null} ISO date string or null
 */
export async function getLastSyncTime(eventId) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['syncMeta'], 'readonly');
    const store = transaction.objectStore('syncMeta');
    const request = store.get(`lastSync_${eventId}`);

    request.onsuccess = () => resolve(request.result?.value || null);
    request.onerror = () => reject(request.error);
  });
}

/**
 * Clear all cached data (useful for logout)
 */
export async function clearAllCache() {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['eventData', 'pendingActions', 'syncMeta'], 'readwrite');

    transaction.objectStore('eventData').clear();
    transaction.objectStore('pendingActions').clear();
    transaction.objectStore('syncMeta').clear();

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

/**
 * Clear cached data for a specific event
 * @param {string} eventId - The event ID
 */
export async function clearEventCache(eventId) {
  await initDb();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(['eventData', 'syncMeta'], 'readwrite');

    transaction.objectStore('eventData').delete(eventId);
    transaction.objectStore('syncMeta').delete(`lastSync_${eventId}`);

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

export default {
  initDb,
  cacheEventData,
  getCachedEventData,
  updateCachedField,
  queueAction,
  getPendingActions,
  getPendingActionsCount,
  updatePendingAction,
  removePendingAction,
  getLastSyncTime,
  clearAllCache,
  clearEventCache
};
