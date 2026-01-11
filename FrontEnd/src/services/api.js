import axios from 'axios';
import { API_BASE_URL } from '../config';
import {
  getCachedEventData,
  cacheEventData,
  queueAction,
  getPendingActionsCount
} from './offlineDb';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 15000, // 15 second timeout
});

// Offline state tracking
let isOfflineMode = false;
let pendingActionsCallback = null;

/**
 * Set callback for when pending actions count changes
 */
export function onPendingActionsChange(callback) {
  pendingActionsCallback = callback;
}

/**
 * Notify pending actions changed
 */
async function notifyPendingActionsChange() {
  if (pendingActionsCallback) {
    const count = await getPendingActionsCount();
    pendingActionsCallback(count);
  }
}

/**
 * Extract event ID from URL path
 */
function extractEventIdFromUrl(url) {
  // Match patterns like /events/{eventId}, /events/{eventId}/*, etc.
  const patterns = [
    /\/events\/([^/]+)/,
    /\/assignments\/([^/]+)/,
    /\/locations\/([^/]+)/,
    /\/checklist-items\/([^/]+)/,
    /\/areas\/([^/]+)/,
    /\/marshals\/([^/]+)/,
  ];

  for (const pattern of patterns) {
    const match = url.match(pattern);
    if (match) {
      return match[1];
    }
  }
  return null;
}

/**
 * Check if the error is a server error or network issue
 */
function isServerOrNetworkError(error) {
  // Network error (no response)
  if (!error.response && error.request) {
    return true;
  }

  // Server errors (5xx)
  if (error.response && error.response.status >= 500) {
    return true;
  }

  // Timeout
  if (error.code === 'ECONNABORTED') {
    return true;
  }

  return false;
}

// Add interceptor to include auth headers
api.interceptors.request.use((config) => {
  const adminEmail = localStorage.getItem('adminEmail');
  if (adminEmail) {
    config.headers['X-Admin-Email'] = adminEmail;
  }
  const sessionToken = localStorage.getItem('sessionToken');
  if (sessionToken) {
    config.headers['Authorization'] = `Bearer ${sessionToken}`;
  }
  return config;
});

// Track consecutive auth errors to detect session expiration
let authErrorCount = 0;
const AUTH_ERROR_THRESHOLD = 2; // Redirect after this many consecutive auth errors

/**
 * Handle authentication errors by redirecting to login
 */
function handleAuthError() {
  authErrorCount++;

  if (authErrorCount >= AUTH_ERROR_THRESHOLD) {
    // Clear auth data
    localStorage.removeItem('adminEmail');
    localStorage.removeItem('sessionToken');

    // Redirect to home with login modal (avoid redirect loop if already on home)
    // Use hash check since app uses hash-based routing
    const currentHash = window.location.hash || '';
    if (!currentHash.includes('login') && !currentHash.includes('/verify')) {
      window.location.href = `${import.meta.env.BASE_URL}#/?login=true`;
    }
  }
}

/**
 * Reset auth error count on successful request
 */
function resetAuthErrorCount() {
  authErrorCount = 0;
}

// Response interceptor to cache successful responses
api.interceptors.response.use(
  (response) => {
    // Mark as online when we get successful responses
    isOfflineMode = false;

    // Reset auth error count on successful response
    resetAuthErrorCount();

    // Cache GET responses for specific endpoints
    const url = response.config.url;
    const eventId = extractEventIdFromUrl(url);

    if (response.config.method === 'get' && eventId) {
      // We cache at a higher level in MarshalView, but we could add caching here too
      // For now, just ensure we're tracking online status
    }

    return response;
  },
  async (error) => {
    const config = error.config;

    // Handle authentication errors (401 Unauthorized, 403 Forbidden)
    if (error.response && (error.response.status === 401 || error.response.status === 403)) {
      handleAuthError();
      return Promise.reject(error);
    }

    // Check if it's a server or network error
    if (isServerOrNetworkError(error)) {
      isOfflineMode = true;

      // For GET requests, try to return cached data
      if (config.method === 'get') {
        const eventId = extractEventIdFromUrl(config.url);
        if (eventId) {
          const cachedData = await getCachedEventData(eventId);
          if (cachedData) {
            console.log('Returning cached data for:', config.url);
            // Return a mock response with cached data
            // The caller will need to extract the right data based on the endpoint
            return Promise.resolve({
              data: cachedData,
              status: 200,
              statusText: 'OK (cached)',
              headers: {},
              config: config,
              cached: true
            });
          }
        }
      }
    }

    return Promise.reject(error);
  }
);

/**
 * Queue an action for offline sync and return an optimistic response
 */
export async function queueOfflineAction(actionType, payload, optimisticResponse) {
  await queueAction({
    type: actionType,
    payload: payload,
    eventId: payload.eventId
  });

  await notifyPendingActionsChange();

  // Return optimistic response
  return {
    data: optimisticResponse,
    status: 200,
    statusText: 'OK (queued)',
    queued: true
  };
}

/**
 * Check if we're in offline mode
 */
export function getOfflineMode() {
  return isOfflineMode || !navigator.onLine;
}

// Health API
export const healthApi = {
  check: () => api.head('/health'),
};

// Get the frontend base URL (everything before the #)
const getFrontendUrl = () => {
  const url = window.location.href.split('#')[0];
  return url.endsWith('/') ? url.slice(0, -1) : url;
};

// Auth API
export const authApi = {
  requestLogin: (email) => api.post('/auth/request-login', { email, frontendUrl: getFrontendUrl() }),
  verifyToken: (token) => api.post('/auth/verify-token', { token }),
  instantLogin: (email) => api.post('/auth/instant-login', { email }),
  createAdmin: (email) => api.post('/auth/create-admin', { email }),
  marshalLogin: (eventId, magicCode) => api.post('/auth/marshal-login', { eventId, magicCode }),
  getMe: (eventId) => api.get('/auth/me', { params: { eventId } }),
  logout: () => api.post('/auth/logout'),
};

// Profile API
export const profileApi = {
  getProfile: () => api.get('/auth/profile'),
  updateProfile: (data) => api.put('/auth/profile', data),
  getPerson: (personId, eventId) => api.get(`/people/${personId}`, { params: { eventId } }),
  updatePerson: (personId, eventId, data) => api.put(`/people/${personId}`, data, { params: { eventId } }),
};

// Events API
export const eventsApi = {
  create: (data) => api.post('/events', data),
  getAll: () => api.get('/events'),
  getById: (id) => api.get(`/events/${id}`),
  update: (id, data) => api.put(`/events/${id}`, data),
  delete: (id) => api.delete(`/events/${id}`),
  uploadGpx: (eventId, file) => {
    const formData = new FormData();
    formData.append('gpx', file);
    return api.post(`/events/${eventId}/upload-gpx`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Locations API
export const locationsApi = {
  create: (data) => api.post('/locations', data),
  getById: (eventId, locationId) => api.get(`/locations/${eventId}/${locationId}`),
  getByEvent: (eventId) => api.get(`/events/${eventId}/locations`),
  update: (eventId, locationId, data) => api.put(`/locations/${eventId}/${locationId}`, data),
  delete: (eventId, locationId) => api.delete(`/locations/${eventId}/${locationId}`),
  importCsv: (eventId, file, deleteExisting) => {
    const formData = new FormData();
    formData.append('csv', file);
    return api.post(`/locations/import/${eventId}?deleteExisting=${deleteExisting}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
  bulkUpdateTimes: (eventId, timeDelta) => api.post(`/locations/bulk-update-times/${eventId}`, { timeDelta }),
  // Dynamic checkpoint location updates
  updatePosition: (eventId, locationId, data) => api.post(`/locations/${eventId}/${locationId}/update-position`, data),
  getDynamicCheckpoints: (eventId) => api.get(`/events/${eventId}/dynamic-checkpoints`),
};

// Assignments API
export const assignmentsApi = {
  create: (data) => api.post('/assignments', data),
  getById: (eventId, assignmentId) => api.get(`/assignments/${eventId}/${assignmentId}`),
  getByEvent: (eventId) => api.get(`/assignments/event/${eventId}`),
  delete: (eventId, assignmentId) => api.delete(`/assignments/${eventId}/${assignmentId}`),
  getEventStatus: (eventId) => api.get(`/events/${eventId}/status`),
};

// Check-in API
export const checkInApi = {
  // Legacy GPS check-in (marshal self check-in only, no toggle)
  checkIn: (data) => api.post('/checkin', data),
  // Toggle check-in/out with optional GPS and action
  // action: 'check-in', 'check-out', or null for toggle
  toggleCheckIn: (eventId, assignmentId, { latitude, longitude, action } = {}) =>
    api.post(`/checkin/${eventId}/${assignmentId}/toggle`, {
      latitude: latitude ?? null,
      longitude: longitude ?? null,
      action: action ?? null,
    }),
  // Admin-only check-in toggle
  adminCheckIn: (eventId, assignmentId) => api.post(`/checkin/admin/${eventId}/${assignmentId}`),
};

// Event Admins API
export const eventAdminsApi = {
  getAdmins: (eventId) => api.get(`/events/${eventId}/admins`),
  addAdmin: (eventId, userEmail) => api.post(`/events/${eventId}/admins`, { userEmail }),
  removeAdmin: (eventId, userEmail) => api.delete(`/events/${eventId}/admins/${userEmail}`),
};

// Marshals API
export const marshalsApi = {
  create: (data) => api.post('/marshals', data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/marshals`),
  getById: (eventId, marshalId) => api.get(`/marshals/${eventId}/${marshalId}`),
  update: (eventId, marshalId, data) => api.put(`/marshals/${eventId}/${marshalId}`, data),
  delete: (eventId, marshalId) => api.delete(`/marshals/${eventId}/${marshalId}`),
  getMagicLink: (eventId, marshalId) => api.get(`/marshals/${eventId}/${marshalId}/magic-link`, { params: { frontendUrl: getFrontendUrl() } }),
  sendMagicLink: (eventId, marshalId) => api.post(`/marshals/${eventId}/${marshalId}/send-magic-link`, { frontendUrl: getFrontendUrl() }),
  importCsv: (eventId, file) => {
    const formData = new FormData();
    formData.append('csv', file);
    return api.post(`/marshals/import/${eventId}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Areas API
export const areasApi = {
  create: (data) => api.post('/areas', data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/areas`),
  getById: (eventId, areaId) => api.get(`/areas/${eventId}/${areaId}`),
  update: (eventId, areaId, data) => api.put(`/areas/${eventId}/${areaId}`, data),
  delete: (eventId, areaId) => api.delete(`/areas/${eventId}/${areaId}`),
  recalculate: (eventId) => api.post(`/areas/recalculate/${eventId}`),
  getCheckpoints: (eventId, areaId) => api.get(`/areas/${eventId}/${areaId}/locations`),
  getAreaLeadDashboard: (eventId) => api.get(`/events/${eventId}/area-lead-dashboard`),
};

// Checklist API
export const checklistApi = {
  create: (eventId, data) => api.post(`/events/${eventId}/checklist-items`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/checklist-items`),
  getById: (eventId, itemId) => api.get(`/checklist-items/${eventId}/${itemId}`),
  update: (eventId, itemId, data) => api.put(`/checklist-items/${eventId}/${itemId}`, data),
  delete: (eventId, itemId) => api.delete(`/checklist-items/${eventId}/${itemId}`),
  getReport: (eventId) => api.get(`/events/${eventId}/checklist-report`),
  getMarshalChecklist: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/checklist`),
  getCheckpointChecklist: (eventId, locationId) => api.get(`/events/${eventId}/locations/${locationId}/checklist`),
  getAreaChecklist: (eventId, areaId) => api.get(`/events/${eventId}/areas/${areaId}/checklist`),
  complete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/complete`, data),
  uncomplete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/uncomplete`, data),
};

// Notes API
export const notesApi = {
  create: (eventId, data) => api.post(`/events/${eventId}/notes`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/notes`),
  getById: (eventId, noteId) => api.get(`/events/${eventId}/notes/${noteId}`),
  update: (eventId, noteId, data) => api.put(`/events/${eventId}/notes/${noteId}`, data),
  delete: (eventId, noteId) => api.delete(`/events/${eventId}/notes/${noteId}`),
  getMarshalNotes: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/notes`),
  getMyNotes: (eventId) => api.get(`/events/${eventId}/my-notes`),
};

// Contacts API
export const contactsApi = {
  // Admin endpoints
  create: (eventId, data) => api.post(`/events/${eventId}/contacts`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/contacts`),
  getById: (eventId, contactId) => api.get(`/events/${eventId}/contacts/${contactId}`),
  update: (eventId, contactId, data) => api.put(`/events/${eventId}/contacts/${contactId}`, data),
  delete: (eventId, contactId) => api.delete(`/events/${eventId}/contacts/${contactId}`),
  getRoles: (eventId) => api.get(`/events/${eventId}/contact-roles`),
  // Marshal endpoints
  getMarshalContacts: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/contacts`),
  getMyContacts: (eventId) => api.get(`/events/${eventId}/my-contacts`),
};

// Incidents API
export const incidentsApi = {
  // Create new incident (marshal)
  create: (eventId, data) => api.post(`/events/${eventId}/incidents`, data),
  // Get all incidents for event (admin)
  getAll: (eventId, params = {}) => api.get(`/events/${eventId}/incidents`, { params }),
  // Get incidents for area (area lead)
  getForArea: (eventId, areaId) => api.get(`/events/${eventId}/areas/${areaId}/incidents`),
  // Get single incident
  get: (eventId, incidentId) => api.get(`/events/${eventId}/incidents/${incidentId}`),
  // Update status (admin/area lead)
  updateStatus: (eventId, incidentId, data) => api.patch(`/events/${eventId}/incidents/${incidentId}`, data),
  // Add note (admin/area lead)
  addNote: (eventId, incidentId, note) => api.post(`/events/${eventId}/incidents/${incidentId}/notes`, { note }),
};

export default api;
