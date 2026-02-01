/**
 * Service for managing multiple marshal sessions.
 * Sessions are stored per marshal (keyed by eventId:marshalId), allowing users
 * to be authenticated as multiple marshals, even for the same event.
 */

const STORAGE_KEY = 'marshalSessions';
const SESSION_MAX_AGE_DAYS = 30;

/**
 * Create a session key from eventId and marshalId
 */
function makeSessionKey(eventId, marshalId) {
  return `${eventId}:${marshalId}`;
}

/**
 * Parse a session key into eventId and marshalId
 */
function parseSessionKey(key) {
  const [eventId, marshalId] = key.split(':');
  return { eventId, marshalId };
}

/**
 * Get all stored marshal sessions
 * @returns {Object} Map of sessionKey -> session data
 */
export function getSessions() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
  } catch {
    return {};
  }
}

/**
 * Get session for a specific event and marshal
 * @param {string} eventId
 * @param {string} marshalId
 * @returns {Object|null} Session data or null if not found
 */
export function getSessionByMarshal(eventId, marshalId) {
  const sessions = getSessions();
  const key = makeSessionKey(eventId, marshalId);
  return sessions[key] || null;
}

/**
 * Get the most recently accessed session for an event
 * (Used when we only have eventId from the URL)
 * @param {string} eventId
 * @returns {Object|null} Session data with sessionKey, or null if not found
 */
export function getSession(eventId) {
  const sessions = getSessions();

  // Find all sessions for this event
  const eventSessions = Object.entries(sessions)
    .filter(([key]) => key.startsWith(`${eventId}:`))
    .map(([key, data]) => ({ sessionKey: key, ...data }));

  if (eventSessions.length === 0) {
    return null;
  }

  // Return the most recently accessed one
  eventSessions.sort((a, b) =>
    new Date(b.lastAccessed) - new Date(a.lastAccessed)
  );

  return eventSessions[0];
}

/**
 * Get all sessions for a specific event
 * @param {string} eventId
 * @returns {Array} Array of sessions for this event
 */
export function getSessionsForEvent(eventId) {
  const sessions = getSessions();

  return Object.entries(sessions)
    .filter(([key]) => key.startsWith(`${eventId}:`))
    .map(([key, data]) => {
      const { marshalId } = parseSessionKey(key);
      return { sessionKey: key, eventId, marshalId, ...data };
    });
}

/**
 * Save or update a marshal session
 * @param {string} eventId
 * @param {Object} sessionData - { token, marshalId, marshalName, eventName, eventDate }
 */
export function saveSession(eventId, sessionData) {
  if (!sessionData.marshalId) {
    console.error('Cannot save session without marshalId');
    return;
  }

  const sessions = getSessions();
  const key = makeSessionKey(eventId, sessionData.marshalId);

  sessions[key] = {
    ...sessions[key],
    ...sessionData,
    eventId, // Store eventId in the data for convenience
    lastAccessed: new Date().toISOString()
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
}

/**
 * Remove a session by its key
 * @param {string} sessionKey - The full session key (eventId:marshalId)
 */
export function removeSession(sessionKey) {
  const sessions = getSessions();
  const { eventId, marshalId } = parseSessionKey(sessionKey);

  delete sessions[sessionKey];
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));

  // Also remove the magic code for this marshal
  localStorage.removeItem(`marshalCode_${eventId}_${marshalId}`);
}

/**
 * Remove session by eventId and marshalId
 * @param {string} eventId
 * @param {string} marshalId
 */
export function removeSessionByMarshal(eventId, marshalId) {
  removeSession(makeSessionKey(eventId, marshalId));
}

/**
 * Update the lastAccessed timestamp for a session
 * @param {string} eventId
 * @param {string} marshalId
 */
export function touchSession(eventId, marshalId) {
  const sessions = getSessions();
  const key = makeSessionKey(eventId, marshalId);

  if (sessions[key]) {
    sessions[key].lastAccessed = new Date().toISOString();
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
  }
}

/**
 * Remove sessions older than SESSION_MAX_AGE_DAYS
 * Call this on app initialization
 */
export function cleanupOldSessions() {
  const sessions = getSessions();
  const cutoff = new Date();
  cutoff.setDate(cutoff.getDate() - SESSION_MAX_AGE_DAYS);

  let changed = false;
  for (const key of Object.keys(sessions)) {
    const lastAccessed = new Date(sessions[key].lastAccessed);
    if (lastAccessed < cutoff) {
      const { eventId, marshalId } = parseSessionKey(key);
      delete sessions[key];
      localStorage.removeItem(`marshalCode_${eventId}_${marshalId}`);
      changed = true;
    }
  }

  if (changed) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
  }
}

/**
 * Migrate legacy storage format to new unified format
 * Call this on app initialization
 */
export function migrateLegacyStorage() {
  const sessions = getSessions();
  const keysToRemove = [];

  // Migrate old marshal_{eventId} keys
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i);
    if (key && key.startsWith('marshal_') && !key.startsWith('marshalCode_') && !key.startsWith('marshalSessions')) {
      const eventId = key.replace('marshal_', '');
      const marshalId = localStorage.getItem(key);
      if (marshalId) {
        const sessionKey = makeSessionKey(eventId, marshalId);
        if (!sessions[sessionKey]) {
          sessions[sessionKey] = {
            marshalId: marshalId,
            eventId: eventId,
            token: null, // Token will be refreshed on next access
            lastAccessed: new Date().toISOString()
          };
        }
      }
      keysToRemove.push(key);
    }
  }

  // Migrate old eventId-only keyed sessions to new format
  const oldFormatKeys = Object.keys(sessions).filter(key => !key.includes(':'));
  for (const eventId of oldFormatKeys) {
    const sessionData = sessions[eventId];
    if (sessionData && sessionData.marshalId) {
      const newKey = makeSessionKey(eventId, sessionData.marshalId);
      if (!sessions[newKey]) {
        sessions[newKey] = {
          ...sessionData,
          eventId
        };
      }
      delete sessions[eventId];
    }
  }

  if (keysToRemove.length > 0 || oldFormatKeys.length > 0) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));
    keysToRemove.forEach(key => localStorage.removeItem(key));
  }
}

/**
 * Get sessions sorted for display in the selector
 * Sorted by event date (upcoming first), then by last accessed
 * @returns {Array} Array of session objects with sessionKey included
 */
export function getSessionsForDisplay() {
  const sessions = getSessions();
  return Object.entries(sessions)
    .map(([sessionKey, data]) => {
      const { eventId, marshalId } = parseSessionKey(sessionKey);
      return { sessionKey, eventId, marshalId, ...data };
    })
    .filter(session => session.token) // Only show sessions with valid tokens
    .sort((a, b) => {
      // Sort by event date if available (upcoming first)
      if (a.eventDate && b.eventDate) {
        return new Date(a.eventDate) - new Date(b.eventDate);
      }
      // Fall back to last accessed (most recent first)
      return new Date(b.lastAccessed) - new Date(a.lastAccessed);
    });
}

/**
 * Check if any marshal sessions exist
 * @returns {boolean}
 */
export function hasSessions() {
  const sessions = getSessions();
  return Object.values(sessions).some(session => session.token);
}

/**
 * Get the stored magic code for re-authentication
 * @param {string} eventId
 * @param {string} marshalId
 * @returns {string|null}
 */
export function getMagicCode(eventId, marshalId) {
  // Try new format first
  const newKey = `marshalCode_${eventId}_${marshalId}`;
  const code = localStorage.getItem(newKey);
  if (code) return code;

  // Fall back to old format for backwards compatibility
  return localStorage.getItem(`marshalCode_${eventId}`);
}

/**
 * Store the magic code for future re-authentication
 * @param {string} eventId
 * @param {string} marshalId
 * @param {string} code
 */
export function saveMagicCode(eventId, marshalId, code) {
  localStorage.setItem(`marshalCode_${eventId}_${marshalId}`, code);
}

/**
 * Remove all sessions for a specific event
 * This includes all marshal sessions and magic codes for the event
 * @param {string} eventId
 */
export function removeAllSessionsForEvent(eventId) {
  const sessions = getSessions();
  const keysToRemove = [];

  // Find all sessions for this event and remove them
  for (const key of Object.keys(sessions)) {
    if (key.startsWith(`${eventId}:`)) {
      const { marshalId } = parseSessionKey(key);
      delete sessions[key];
      // Also remove the magic code for this marshal
      keysToRemove.push(`marshalCode_${eventId}_${marshalId}`);
    }
  }

  // Save updated sessions
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sessions));

  // Remove magic codes
  keysToRemove.forEach(key => localStorage.removeItem(key));

  // Also remove the sample event entry if it exists
  localStorage.removeItem(`sampleEvent_${eventId}`);
}

export const marshalSessionService = {
  getSessions,
  getSession,
  getSessionByMarshal,
  getSessionsForEvent,
  saveSession,
  removeSession,
  removeSessionByMarshal,
  removeAllSessionsForEvent,
  touchSession,
  cleanupOldSessions,
  migrateLegacyStorage,
  getSessionsForDisplay,
  hasSessions,
  getMagicCode,
  saveMagicCode
};

export default marshalSessionService;
