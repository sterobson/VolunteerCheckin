import axios from 'axios';
import { API_BASE_URL } from '../config';
import {
  getCachedEventData,
  cacheEventData,
  queueAction,
  getPendingActionsCount
} from './offlineDb';
import { getSession } from './marshalSessionService';
import { startRequest, endRequest, getRequestId } from './loadingOverlay';

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

// Auth context tracking - determines which token to use for API calls
// type: 'admin' | 'marshal' | 'sample' | null
// eventId: only relevant when type is 'marshal' or 'sample'
// sampleCode: only relevant when type is 'sample'
let currentAuthContext = {
  type: null,
  eventId: null,
  sampleCode: null
};

/**
 * Set the current authentication context
 * Call this when entering admin or marshal views
 * @param {'admin'|'marshal'|'sample'|null} type
 * @param {string|null} eventId - Required when type is 'marshal' or 'sample'
 * @param {string|null} sampleCode - Required when type is 'sample'
 */
export function setAuthContext(type, eventId = null, sampleCode = null) {
  currentAuthContext = { type, eventId, sampleCode };
}

/**
 * Get the current authentication context
 * @returns {{type: string|null, eventId: string|null, sampleCode: string|null}}
 */
export function getAuthContext() {
  return { ...currentAuthContext };
}

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

/**
 * Detect auth context from current URL hash
 * This is more reliable than the context variable for edge cases
 */
function detectAuthContextFromUrl() {
  const hash = window.location.hash || '';
  const search = window.location.search || '';

  // Check for sample query parameter in either hash or search
  // Hash routing: #/admin/event/123?sample=abc
  // History routing: /admin/event/123?sample=abc
  const hashParams = hash.includes('?') ? new URLSearchParams(hash.split('?')[1]) : null;
  const searchParams = new URLSearchParams(search);
  const sampleCode = hashParams?.get('sample') || searchParams.get('sample');

  // Check if on admin routes with sample code - this is a sample event
  if (hash.includes('/admin/') && sampleCode) {
    const adminEventMatch = hash.match(/#\/admin\/event\/([^/?]+)/);
    return {
      type: 'sample',
      eventId: adminEventMatch ? adminEventMatch[1] : null,
      sampleCode
    };
  }

  // Check if on admin routes (without sample code)
  if (hash.includes('/admin/')) {
    return { type: 'admin', eventId: null };
  }

  // Check if on marshal routes - extract eventId from /event/{eventId}
  const marshalMatch = hash.match(/#\/event\/([^/?]+)/);
  if (marshalMatch) {
    return { type: 'marshal', eventId: marshalMatch[1] };
  }

  // Check if on marshal selector
  if (hash.includes('/marshal')) {
    return { type: null, eventId: null };
  }

  // Default to stored context or null
  return currentAuthContext;
}

// Endpoints that should NOT include auth headers (login endpoints use their own auth)
const NO_AUTH_ENDPOINTS = [
  '/auth/marshal-login',
  '/auth/request-login',
  '/auth/verify-token',
  '/auth/instant-login',
];

// Add interceptor to include auth headers
api.interceptors.request.use((config) => {
  // Skip auth headers for login endpoints
  const isLoginEndpoint = NO_AUTH_ENDPOINTS.some(endpoint => config.url?.includes(endpoint));
  if (isLoginEndpoint) {
    return config;
  }

  // Detect context from URL (more reliable than stored context)
  const context = detectAuthContextFromUrl();

  // Include sample event code if in sample mode (use detected context for reliability)
  const sampleCode = context.sampleCode || currentAuthContext.sampleCode;
  const isSampleMode = (context.type === 'sample' || currentAuthContext.type === 'sample') && sampleCode;
  if (isSampleMode) {
    config.headers['X-Sample-Code'] = sampleCode;
  }

  // Determine which token to use based on auth context
  let token = null;

  if (context.type === 'marshal' && context.eventId) {
    // Use marshal token for this specific event
    const session = getSession(context.eventId);
    if (session?.token) {
      token = session.token;
    }
  }

  // Fall back to admin session token if no marshal token or admin context
  if (!token) {
    token = localStorage.getItem('sessionToken');
  }

  if (token) {
    config.headers['Authorization'] = `Bearer ${token}`;
  }

  // Add admin email header for admin context requests
  const adminEmail = localStorage.getItem('adminEmail');
  if (adminEmail && (context.type === 'admin' || context.type === 'sample' || !context.type)) {
    config.headers['X-Admin-Email'] = adminEmail;
  }

  // Start tracking this request for loading overlay
  startRequest(config);

  return config;
});

// Track consecutive auth errors to detect session expiration
let authErrorCount = 0;
const AUTH_ERROR_THRESHOLD = 2; // Redirect after this many consecutive auth errors

/**
 * Handle authentication errors
 * For admin context: redirects to login after threshold
 * For marshal context: handled by useMarshalAuth (re-auth with magic code)
 */
function handleAuthError() {
  authErrorCount++;

  if (authErrorCount >= AUTH_ERROR_THRESHOLD) {
    // Detect context from URL (more reliable than stored context)
    const context = detectAuthContextFromUrl();

    // Only auto-redirect for admin context (not marshal or sample)
    // Marshal auth errors are handled by useMarshalAuth which can attempt re-auth
    // Sample event auth errors should stay on the page (the sample code might just be invalid)
    if (context.type !== 'marshal' && context.type !== 'sample') {
      // Clear admin auth data
      localStorage.removeItem('adminEmail');
      localStorage.removeItem('sessionToken');

      // Redirect to home with login modal (avoid redirect loop if already on home)
      // Use hash check since app uses hash-based routing
      const currentHash = window.location.hash || '';
      if (!currentHash.includes('login') && !currentHash.includes('/verify')) {
        window.location.href = `${import.meta.env.BASE_URL}#/?login=true`;
      }
    }
    // For marshal context, the error will propagate to useMarshalAuth
    // which will handle re-auth or redirect to selector
    // For sample context, errors will be shown to the user on the page
  }
}

/**
 * Reset auth error count on successful request
 */
function resetAuthErrorCount() {
  authErrorCount = 0;
}

/**
 * Explicitly reset auth error count (e.g., before re-authentication attempts)
 */
export function clearAuthErrorCount() {
  authErrorCount = 0;
}

/**
 * Determine if a request URL is for a marshal-specific endpoint
 * Used to avoid clearing admin credentials for marshal request failures
 */
function isMarshalRequest(config) {
  if (!config?.url) return false;

  // Check if this request was sent with a marshal token by looking at the Authorization header
  // and comparing with the admin token
  const adminToken = localStorage.getItem('sessionToken');
  const authHeader = config.headers?.Authorization || config.headers?.authorization;

  if (authHeader && adminToken) {
    const requestToken = authHeader.replace('Bearer ', '');
    // If the request token differs from admin token, it must be a marshal token
    if (requestToken !== adminToken) {
      return true;
    }
  }

  // Check if this is a login-related request (these don't have auth headers)
  const url = config.url || '';
  if (url.includes('/auth/marshal-login')) {
    return true;
  }

  // Check if the request was made while on a marshal route
  // This handles cases where the request was initiated from marshal context
  const hash = window.location.hash || '';
  if (hash.match(/#\/event\/[^/?]+/)) {
    return true;
  }

  // Check if we're on the marshal selector page
  if (hash === '#/marshal' || hash.startsWith('#/marshal?')) {
    return true;
  }

  return false;
}

// Response interceptor to cache successful responses
api.interceptors.response.use(
  (response) => {
    // End tracking this request for loading overlay
    const requestId = getRequestId(response.config);
    if (requestId) {
      endRequest(requestId);
    }

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

    // End tracking this request for loading overlay
    const requestId = getRequestId(config);
    if (requestId) {
      endRequest(requestId);
    }

    // Handle authentication errors (401 Unauthorized, 403 Forbidden)
    if (error.response && (error.response.status === 401 || error.response.status === 403)) {
      // Only call handleAuthError if this was NOT a marshal request
      // This prevents admin logout when marshal requests fail
      if (!isMarshalRequest(config)) {
        handleAuthError();
      }
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

// Get the frontend base URL for magic links (without the /#/ - backend adds that)
// Uses VITE_FRONTEND_URL if set at build time, otherwise detects from current URL
const getFrontendUrl = () => {
  // If explicitly set at build/deploy time, use that
  if (import.meta.env.VITE_FRONTEND_URL) {
    // Strip any trailing /#/ or / - backend will add /#/route
    return import.meta.env.VITE_FRONTEND_URL.replace(/\/?#?\/?$/, '');
  }

  // Otherwise detect from current URL - just use origin (protocol + host)
  return window.location.origin;
};

// Check if this build uses hash routing (matches router logic)
const usesHashRouting = () => {
  const isLocalhost = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
  return !isLocalhost && import.meta.env.VITE_USE_HASH_ROUTING === 'true';
};

// Auth API
export const authApi = {
  requestLogin: (email) => api.post('/auth/request-login', { email, frontendUrl: getFrontendUrl(), useHashRouting: usesHashRouting() }),
  verifyToken: (token) => api.post('/auth/verify-token', { token }),
  verifyCode: (email, code) => api.post('/auth/verify-code', { email, code }),
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
  getAllSummary: () => api.get('/events/summary'),
  getById: (id) => api.get(`/events/${id}`),
  update: (id, data) => api.put(`/events/${id}`, data),
  delete: (id) => api.delete(`/events/${id}`),
  requestDeletion: (id) => api.post(`/events/${id}/request-deletion`),
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
  getByEvent: (eventId, config = {}) => api.get(`/events/${eventId}/locations`, config),
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
  updatePosition: (eventId, locationId, data, config = {}) => api.post(`/locations/${eventId}/${locationId}/update-position`, data, config),
  getDynamicCheckpoints: (eventId, config = {}) => api.get(`/events/${eventId}/dynamic-checkpoints`, config),
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
  getMagicLink: (eventId, marshalId) => api.get(`/marshals/${eventId}/${marshalId}/magic-link`, { params: { frontendUrl: getFrontendUrl(), useHashRouting: usesHashRouting() } }),
  sendMagicLink: (eventId, marshalId, options = {}) => api.post(`/marshals/${eventId}/${marshalId}/send-magic-link`, {
    frontendUrl: getFrontendUrl(),
    useHashRouting: usesHashRouting(),
    email: options.email || null,
    includeDetails: options.includeDetails || false,
  }, { loadingMessage: 'Sending...' }),
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
  getDetailedReport: (eventId) => api.get(`/events/${eventId}/checklist-report/detailed`),
  getMarshalChecklist: (eventId, marshalId) => api.get(`/events/${eventId}/marshals/${marshalId}/checklist`),
  getCheckpointChecklist: (eventId, locationId) => api.get(`/events/${eventId}/locations/${locationId}/checklist`),
  getAreaChecklist: (eventId, areaId) => api.get(`/events/${eventId}/areas/${areaId}/checklist`),
  complete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/complete`, data),
  uncomplete: (eventId, itemId, data) => api.post(`/checklist-items/${eventId}/${itemId}/uncomplete`, data),
  reorder: (eventId, items) => api.post(`/events/${eventId}/checklist-items/reorder`, { items }),
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
  reorder: (eventId, items) => api.post(`/events/${eventId}/notes/reorder`, { items }),
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
  reorder: (eventId, items) => api.post(`/events/${eventId}/contacts/reorder`, { items }),
};

// Role Definitions API
export const roleDefinitionsApi = {
  // Get all role definitions for an event
  getAll: (eventId) => api.get(`/events/${eventId}/role-definitions`),
  // Create a new role definition
  create: (eventId, data) => api.post(`/events/${eventId}/role-definitions`, data),
  // Update an existing role definition
  update: (eventId, roleId, data) => api.put(`/events/${eventId}/role-definitions/${roleId}`, data),
  // Delete a role definition (must not be in use)
  delete: (eventId, roleId) => api.delete(`/events/${eventId}/role-definitions/${roleId}`),
  // Get unified list of people for a role
  getPeople: (eventId, roleId) => api.get(`/events/${eventId}/role-definitions/${roleId}/people`),
  // Update role assignments (add/remove people)
  updatePeople: (eventId, roleId, data) => api.put(`/events/${eventId}/role-definitions/${roleId}/people`, data),
  // Reorder role definitions
  reorder: (eventId, items) => api.post(`/events/${eventId}/role-definitions/reorder`, { items }),
  // Get current marshal's assigned roles with notes (marshal mode)
  getMyRoles: (eventId) => api.get(`/events/${eventId}/my-roles`),
};

// Incidents API
export const incidentsApi = {
  // Create new incident (marshal)
  create: (eventId, data) => api.post(`/events/${eventId}/incidents`, data),
  // Get incidents visible to current user (filtered by role on backend)
  getAll: (eventId, params = {}) => api.get(`/events/${eventId}/incidents`, { params }),
  // Get single incident
  get: (eventId, incidentId) => api.get(`/events/${eventId}/incidents/${incidentId}`),
  // Update status (admin/area lead)
  updateStatus: (eventId, incidentId, data) => api.patch(`/events/${eventId}/incidents/${incidentId}`, data),
  // Add note (admin/area lead)
  addNote: (eventId, incidentId, note) => api.post(`/events/${eventId}/incidents/${incidentId}/notes`, { note }),
};

// Layers API
export const layersApi = {
  create: (eventId, data) => api.post(`/events/${eventId}/layers`, data),
  getByEvent: (eventId) => api.get(`/events/${eventId}/layers`),
  getById: (eventId, layerId) => api.get(`/events/${eventId}/layers/${layerId}`),
  update: (eventId, layerId, data) => api.put(`/events/${eventId}/layers/${layerId}`, data),
  delete: (eventId, layerId) => api.delete(`/events/${eventId}/layers/${layerId}`),
  reorder: (eventId, items) => api.post(`/events/${eventId}/layers/reorder`, { items }),
  uploadGpx: (eventId, layerId, file) => {
    const formData = new FormData();
    formData.append('gpx', file);
    return api.post(`/events/${eventId}/layers/${layerId}/upload-gpx`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },
};

// Sample Events API (anonymous, no auth required)
export const sampleEventsApi = {
  // Create a new sample event
  create: (deviceFingerprint, timeZoneId) => api.post('/sample-event', { timeZoneId }, {
    headers: { 'X-Device-Fingerprint': deviceFingerprint }
  }),
  // Validate an admin code
  validate: (code) => api.get(`/sample-event/validate/${code}`),
  // Recover an existing sample event by device fingerprint
  recover: (deviceFingerprint) => api.get('/sample-event/recover', {
    headers: { 'X-Device-Fingerprint': deviceFingerprint }
  }),
};

export default api;
