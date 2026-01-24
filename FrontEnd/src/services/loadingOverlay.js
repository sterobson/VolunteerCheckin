import { ref, computed } from 'vue';

/**
 * Global loading state for API operations.
 *
 * Behavior:
 * - Saving (POST/PUT/PATCH/DELETE): Shows overlay after 500ms delay
 * - Loading (GET): No overlay, but inputs disabled after 3000ms delay
 * - Inputs are disabled when any request exceeds its delay threshold
 * - Hide/re-enable: Waits 500ms after last request completes
 */

const DELAY_SAVING_MS = 500;
const DELAY_LOADING_MS = 3000;
const HIDE_DELAY_MS = 500;

// Global reactive state
const activeRequests = ref(new Map());
const showOverlay = ref(false);
const overlayMessage = ref('');
const shouldDisableInputs = ref(false);

let requestCounter = 0;
let hideTimeoutId = null;
let disableTimeoutId = null;

/**
 * Determine the message and delay based on HTTP method and config
 * @param {string} method - HTTP method
 * @param {object} config - Axios request config (may contain loadingMessage override)
 */
function getRequestInfo(method, config) {
  const upperMethod = (method || 'get').toUpperCase();
  if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(upperMethod)) {
    const message = config?.loadingMessage || 'Saving...';
    return { message, delay: DELAY_SAVING_MS, isSaving: true };
  }
  return { message: 'Loading...', delay: DELAY_LOADING_MS, isSaving: false };
}

/**
 * Cancel any pending hide timeout
 */
function cancelHideTimeout() {
  if (hideTimeoutId) {
    clearTimeout(hideTimeoutId);
    hideTimeoutId = null;
  }
  if (disableTimeoutId) {
    clearTimeout(disableTimeoutId);
    disableTimeoutId = null;
  }
}

/**
 * Start tracking a request
 * @param {object} config - Axios request config
 * @returns {number|null} Request ID, or null if skipped
 */
export function startRequest(config) {
  // Skip for background requests (e.g., polling, auto-updates)
  if (config.skipLoadingOverlay) {
    return null;
  }

  // Cancel any pending hide when a new request starts
  cancelHideTimeout();

  const requestId = ++requestCounter;
  const { message, delay, isSaving } = getRequestInfo(config.method, config);

  // Set up timeout for showing overlay (saving only) and disabling inputs (all)
  const overlayTimeoutId = isSaving ? setTimeout(() => {
    // Only show overlay if request is still active and it's a saving operation
    if (activeRequests.value.has(requestId)) {
      overlayMessage.value = message;
      showOverlay.value = true;
    }
  }, delay) : null;

  const disableInputsTimeoutId = setTimeout(() => {
    // Disable inputs if request is still active
    if (activeRequests.value.has(requestId)) {
      shouldDisableInputs.value = true;
    }
  }, delay);

  activeRequests.value.set(requestId, {
    config,
    message,
    isSaving,
    overlayTimeoutId,
    disableInputsTimeoutId,
  });

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
    if (request.overlayTimeoutId) {
      clearTimeout(request.overlayTimeoutId);
    }
    if (request.disableInputsTimeoutId) {
      clearTimeout(request.disableInputsTimeoutId);
    }
    activeRequests.value.delete(requestId);

    // Check if we should hide the overlay and re-enable inputs
    if (activeRequests.value.size === 0) {
      // Delay hiding to prevent flicker if another request starts soon
      hideTimeoutId = setTimeout(() => {
        // Double-check no new requests started during the delay
        if (activeRequests.value.size === 0) {
          showOverlay.value = false;
          overlayMessage.value = '';
          shouldDisableInputs.value = false;
        }
        hideTimeoutId = null;
      }, HIDE_DELAY_MS);
    } else {
      // Update message to show the next saving request's message (if any)
      for (const req of activeRequests.value.values()) {
        if (req.isSaving) {
          overlayMessage.value = req.message;
          break;
        }
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

// Computed state for whether we're in a saving operation
const isSaving = computed(() => showOverlay.value && overlayMessage.value === 'Saving...');

/**
 * Get the reactive loading state for use in components.
 * - showOverlay: true when saving overlay should be visible (after delay)
 * - overlayMessage: "Saving..." when saving
 * - isSaving: true when a save operation has exceeded the delay threshold
 * - shouldDisableInputs: true when any operation has exceeded its delay threshold
 */
export function useGlobalLoadingOverlay() {
  return {
    showOverlay,
    overlayMessage,
    isSaving,
    shouldDisableInputs,
  };
}

/**
 * @deprecated No longer used - GET requests never show overlay now
 */
export function setSkipLoadingOverlayForGets(_skip) {
  // No-op: GET requests no longer show overlay by design
}
