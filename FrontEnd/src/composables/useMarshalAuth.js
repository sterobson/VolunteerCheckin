import { ref, computed } from 'vue';
import { authApi, setAuthContext, clearAuthErrorCount } from '../services/api';
import {
  getSession,
  saveSession,
  removeSession,
  getMagicCode,
  saveMagicCode,
  touchSession
} from '../services/marshalSessionService';

/**
 * Composable for managing marshal authentication and session
 */
export function useMarshalAuth({
  router,
  route,
  onAuthSuccess,
}) {
  // Auth state
  const isAuthenticated = ref(false);
  const authenticating = ref(false);
  const loginError = ref(null);
  const currentPerson = ref(null);
  const currentMarshalId = ref(null);
  const userClaims = ref(null);

  // Current marshal name for display
  const currentMarshalName = computed(() => currentPerson.value?.name || null);

  // Area lead area IDs computed from claims
  const areaLeadAreaIds = computed(() => {
    if (!userClaims.value?.eventRoles) return [];
    const areaLeadRoles = userClaims.value.eventRoles.filter(r => r.role === 'EventAreaLead');
    return areaLeadRoles.flatMap(r => r.areaIds || []);
  });

  const isAreaLead = computed(() => areaLeadAreaIds.value.length > 0);

  // Check if user is an event admin
  const isEventAdmin = computed(() => {
    if (!userClaims.value?.eventRoles) return false;
    return userClaims.value.eventRoles.some(r => r.role === 'EventAdmin');
  });

  // Check if user can switch to admin mode
  const canSwitchToAdmin = computed(() => {
    return userClaims.value?.authMethod === 'SecureEmailLink' && isEventAdmin.value;
  });

  // Check if user is admin but logged in via magic code
  const isAdminButNeedsReauth = computed(() => {
    return userClaims.value?.authMethod === 'MarshalMagicCode' && isEventAdmin.value;
  });

  // Heartbeat interval
  let heartbeatInterval = null;

  const startHeartbeat = () => {
    if (heartbeatInterval) return;

    heartbeatInterval = setInterval(async () => {
      if (userClaims.value?.marshalId) {
        try {
          await authApi.getMe(route.params.eventId);
        } catch (err) {
          console.debug('Heartbeat failed:', err);
        }
      }
    }, 5 * 60 * 1000); // 5 minutes
  };

  const stopHeartbeat = () => {
    if (heartbeatInterval) {
      clearInterval(heartbeatInterval);
      heartbeatInterval = null;
    }
  };

  const authenticateWithMagicCode = async (eventId, code, isReauth = false) => {
    authenticating.value = true;
    loginError.value = null;
    console.log('Authenticating with magic code:', { eventId, code, isReauth });

    // Reset auth error count to prevent any accumulated errors from affecting this flow
    clearAuthErrorCount();

    try {
      // Set auth context before making API call (needed for interceptor)
      setAuthContext('marshal', eventId);

      const response = await authApi.marshalLogin(eventId, code);
      console.log('Auth response:', response.data);

      if (response.data.success) {
        // Store session in the marshal session service (separate from admin token)
        saveSession(eventId, {
          token: response.data.sessionToken,
          marshalId: response.data.marshalId,
          marshalName: response.data.person?.name || null,
          // eventName will be set later when event data is loaded
        });

        // Store magic code for future re-authentication
        saveMagicCode(eventId, response.data.marshalId, code);

        currentPerson.value = response.data.person;
        currentMarshalId.value = response.data.marshalId;
        isAuthenticated.value = true;
        console.log('Authenticated as marshal:', currentMarshalId.value, currentPerson.value?.name);

        // Fetch full claims to get role information
        try {
          const claimsResponse = await authApi.getMe(eventId);
          userClaims.value = claimsResponse.data;
          console.log('User claims:', userClaims.value);
        } catch (claimsError) {
          console.warn('Failed to fetch claims:', claimsError);
        }

        // Clear the code from URL to prevent re-authentication attempts
        router.replace({ path: route.path, query: {} });

        // Start heartbeat to keep LastAccessedDate updated
        startHeartbeat();

        // Notify caller that auth succeeded
        if (onAuthSuccess) {
          await onAuthSuccess();
        }
      } else {
        loginError.value = response.data.message || 'Authentication failed';
      }
    } catch (error) {
      console.error('Authentication failed:', error);
      loginError.value = error.response?.data?.message || 'Invalid or expired login link';
    } finally {
      authenticating.value = false;
    }
  };

  const checkExistingSession = async (eventId) => {
    // Set auth context for this event
    setAuthContext('marshal', eventId);

    const session = getSession(eventId);
    const marshalId = session?.marshalId;
    const storedCode = marshalId ? getMagicCode(eventId, marshalId) : null;

    if (!session?.token) {
      // No session, but check if we have a stored code to re-authenticate
      if (storedCode) {
        console.log('No session but have stored code, attempting re-authentication...');
        clearAuthErrorCount(); // Reset error count before re-auth
        await authenticateWithMagicCode(eventId, storedCode, true);
        return isAuthenticated.value;
      }
      return false;
    }

    try {
      const response = await authApi.getMe(eventId);

      if (response.data && response.data.marshalId) {
        userClaims.value = response.data;
        currentPerson.value = {
          personId: response.data.personId,
          name: response.data.personName,
          email: response.data.personEmail,
        };
        currentMarshalId.value = response.data.marshalId;
        isAuthenticated.value = true;

        // Update session access time
        touchSession(eventId, response.data.marshalId);

        // Start heartbeat to keep LastAccessedDate updated
        startHeartbeat();

        return true;
      }
    } catch (error) {
      console.log('Session validation failed, attempting re-authentication...', error);

      // Session invalid - try to re-authenticate with stored magic code
      if (storedCode) {
        try {
          clearAuthErrorCount(); // Reset error count before re-auth attempt
          await authenticateWithMagicCode(eventId, storedCode, true);
          if (isAuthenticated.value) {
            console.log('Re-authentication successful');
            return true;
          }
        } catch (reAuthError) {
          console.error('Re-authentication failed:', reAuthError);
        }
      }

      // Re-auth failed or no stored code, clear this event's session
      if (session?.sessionKey) {
        removeSession(session.sessionKey);
      }
    }

    return false;
  };

  /**
   * Close the current marshal session and navigate to the session selector.
   * This does NOT remove the session - the user can return to it later.
   */
  const handleClose = (onCloseComplete) => {
    // Stop heartbeat
    stopHeartbeat();

    // Clear auth context
    setAuthContext(null);

    // Clear local auth state (but keep session in storage for later)
    isAuthenticated.value = false;
    currentPerson.value = null;
    currentMarshalId.value = null;
    userClaims.value = null;

    if (onCloseComplete) {
      onCloseComplete();
    }

    // Navigate to session selector
    router.push('/marshal');
  };

  /**
   * Fully remove a marshal session (used when clicking X on selector).
   * This logs out from the backend and removes all session data.
   * @param {string} sessionKey - The full session key (eventId:marshalId)
   */
  const handleRemoveSession = async (sessionKey) => {
    // Parse the key to get eventId for context setting
    const [eventId] = sessionKey.split(':');

    // Set context to the session being removed for the logout API call
    setAuthContext('marshal', eventId);
    try {
      await authApi.logout();
    } catch (error) {
      // Ignore logout errors
    }

    // Remove session from storage
    removeSession(sessionKey);

    // Clear auth context
    setAuthContext(null);
  };

  /**
   * Update the session with event details (for display in selector)
   */
  const updateSessionEventInfo = (eventId, eventName, eventDate) => {
    const session = getSession(eventId);
    if (session && session.marshalId) {
      saveSession(eventId, {
        marshalId: session.marshalId,
        marshalName: session.marshalName,
        token: session.token,
        eventName,
        eventDate
      });
    }
  };

  // Switch to admin mode
  const switchToAdminMode = () => {
    router.push(`/admin/events/${route.params.eventId}`);
  };

  return {
    // State
    isAuthenticated,
    authenticating,
    loginError,
    currentPerson,
    currentMarshalId,
    userClaims,

    // Computed
    currentMarshalName,
    areaLeadAreaIds,
    isAreaLead,
    isEventAdmin,
    canSwitchToAdmin,
    isAdminButNeedsReauth,

    // Functions
    authenticateWithMagicCode,
    checkExistingSession,
    handleClose,
    handleRemoveSession,
    updateSessionEventInfo,
    switchToAdminMode,
    startHeartbeat,
    stopHeartbeat,
  };
}
