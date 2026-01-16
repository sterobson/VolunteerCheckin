import { ref } from 'vue';

/**
 * Global loading overlay state for API operations.
 * Shows "Loading..." or "Saving..." based on request method.
 *
 * Timing behavior:
 * - Saving (POST/PUT/PATCH/DELETE): Shows after 500ms delay
 * - Loading (GET): Shows after 3000ms delay
 * - Hide: Waits 500ms after last request completes before hiding
 *         (resets if a new request starts during the wait)
 */

const DELAY_SAVING_MS = 500;
const DELAY_LOADING_MS = 3000;
const HIDE_DELAY_MS = 500;

// Global reactive state
const activeRequests = ref(new Map());
const showOverlay = ref(false);
const overlayMessage = ref('');

// When true, skip showing overlay for loading (GET) requests - only show for saving
let skipLoadingOverlayForGets = false;

let requestCounter = 0;
let hideTimeoutId = null;

/**
 * Determine the message and delay based on HTTP method
 */
function getRequestInfo(method) {
  const upperMethod = (method || 'get').toUpperCase();
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(upperMethod)) {
    return { message: 'Saving...', delay: DELAY_SAVING_MS };
  }
  return { message: 'Loading...', delay: DELAY_LOADING_MS };
}

/**
 * Cancel any pending hide timeout
 */
function cancelHideTimeout() {
  if (hideTimeoutId) {
    clearTimeout(hideTimeoutId);
    hideTimeoutId = null;
  }
}

/**
 * Start tracking a request
 * @param {object} config - Axios request config
 * @returns {number|null} Request ID, or null if skipped
 */
export function startRequest(config) {
  // Skip loading overlay for background requests (e.g., polling, auto-updates)
  if (config.skipLoadingOverlay) {
    return null;
  }

  // Skip loading overlay for GET requests when in marshal mode
  const upperMethod = (config.method || 'get').toUpperCase();
  if (skipLoadingOverlayForGets && upperMethod === 'GET') {
    return null;
  }

  // Cancel any pending hide when a new request starts
  cancelHideTimeout();

  const requestId = ++requestCounter;
  const { message, delay } = getRequestInfo(config.method);

  const timeoutId = setTimeout(() => {
    // Only show overlay if request is still active
    if (activeRequests.value.has(requestId)) {
      overlayMessage.value = message;
      showOverlay.value = true;
    }
  }, delay);

  activeRequests.value.set(requestId, { config, message, timeoutId });

  // Store request ID in config for later retrieval
  config._loadingRequestId = requestId;

  return requestId;
}

/**
 * End tracking a request
 * @param {number|null} requestId - The request ID from startRequest
 */
export function endRequest(requestId) {
  // Skip if request was not tracked (background request)
  if (requestId === null) {
    return;
  }

  const request = activeRequests.value.get(requestId);
  if (request) {
    clearTimeout(request.timeoutId);
    activeRequests.value.delete(requestId);

    // Check if we should hide the overlay
    if (activeRequests.value.size === 0) {
      // Delay hiding to prevent flicker if another request starts soon
      hideTimeoutId = setTimeout(() => {
        // Double-check no new requests started during the delay
        if (activeRequests.value.size === 0) {
          showOverlay.value = false;
          overlayMessage.value = '';
        }
        hideTimeoutId = null;
      }, HIDE_DELAY_MS);
    } else {
      // Update message to show the next request's message
      const nextReq = activeRequests.value.values().next().value;
      if (nextReq) {
        overlayMessage.value = nextReq.message;
      }
    }
  }
}

/**
 * Get the request ID from a config object
 * @param {object} config - Axios request/response config
 * @returns {number|undefined} Request ID
 */
export function getRequestId(config) {
  return config?._loadingRequestId;
}

/**
 * Get the reactive overlay state for use in components
 */
export function useGlobalLoadingOverlay() {
  return {
    showOverlay,
    overlayMessage,
  };
}

/**
 * Set whether to skip the loading overlay for GET requests.
 * When true, only saving operations (POST/PUT/PATCH/DELETE) will show the overlay.
 * Use this for marshal mode where we have a separate bottom loading indicator.
 * @param {boolean} skip - Whether to skip loading overlay for GET requests
 */
export function setSkipLoadingOverlayForGets(skip) {
  skipLoadingOverlayForGets = skip;
}
