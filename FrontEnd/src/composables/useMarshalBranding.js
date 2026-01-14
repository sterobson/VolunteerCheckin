import { computed, watch, ref } from 'vue';
import { getContrastTextColor, getGradientContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';
import { API_BASE_URL } from '../config';

const BRANDING_CACHE_KEY = 'marshal-branding-cache';

/**
 * Get cached branding for an event from localStorage
 */
function getCachedBranding(eventId) {
  if (!eventId || typeof window === 'undefined') return null;
  try {
    const cached = localStorage.getItem(`${BRANDING_CACHE_KEY}-${eventId}`);
    return cached ? JSON.parse(cached) : null;
  } catch {
    return null;
  }
}

/**
 * Save branding to localStorage for an event
 */
function saveBrandingCache(eventId, branding) {
  if (!eventId || typeof window === 'undefined') return;
  try {
    localStorage.setItem(`${BRANDING_CACHE_KEY}-${eventId}`, JSON.stringify(branding));
  } catch {
    // Ignore storage errors
  }
}

/**
 * Composable for managing marshal view branding/theming
 * Extracts color and style computations from event data
 * Caches branding to localStorage for faster initial load
 *
 * @param {Ref<Object>} event - Reactive reference to the event object
 * @param {string} eventId - Event ID for caching (optional, but recommended)
 * @returns {Object} Branding-related computed properties
 */
export function useMarshalBranding(event, eventId = null) {
  // Try to load cached branding immediately for faster initial display
  const cachedBranding = ref(eventId ? getCachedBranding(eventId) : null);

  // Watch for event changes and update cache
  watch(() => event.value, (newEvent) => {
    if (newEvent && eventId) {
      const branding = {
        brandingPageGradientStart: newEvent.brandingPageGradientStart,
        brandingPageGradientEnd: newEvent.brandingPageGradientEnd,
        brandingHeaderGradientStart: newEvent.brandingHeaderGradientStart,
        brandingHeaderGradientEnd: newEvent.brandingHeaderGradientEnd,
        brandingAccentColor: newEvent.brandingAccentColor,
        brandingLogoUrl: newEvent.brandingLogoUrl,
        brandingLogoPosition: newEvent.brandingLogoPosition,
      };
      saveBrandingCache(eventId, branding);
      cachedBranding.value = branding;
    }
  }, { immediate: true });

  // Helper to get branding value with fallback chain: event -> cache -> default
  const getBrandingValue = (key, defaultValue) => {
    return event.value?.[key] || cachedBranding.value?.[key] || defaultValue;
  };
  // Page background gradient
  const pageBackgroundStyle = computed(() => {
    const start = getBrandingValue('brandingPageGradientStart', DEFAULT_COLORS.pageGradientStart);
    const end = getBrandingValue('brandingPageGradientEnd', DEFAULT_COLORS.pageGradientEnd);
    return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
  });

  // Header gradient
  const headerStyle = computed(() => {
    const start = getBrandingValue('brandingHeaderGradientStart', DEFAULT_COLORS.headerGradientStart);
    const end = getBrandingValue('brandingHeaderGradientEnd', DEFAULT_COLORS.headerGradientEnd);
    return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
  });

  // Header text color (contrasts with gradient)
  const headerTextColor = computed(() => {
    const start = getBrandingValue('brandingHeaderGradientStart', DEFAULT_COLORS.headerGradientStart);
    const end = getBrandingValue('brandingHeaderGradientEnd', DEFAULT_COLORS.headerGradientEnd);
    return getGradientContrastTextColor(start, end);
  });

  // Accent color for buttons and highlights
  const accentColor = computed(() => {
    return getBrandingValue('brandingAccentColor', DEFAULT_COLORS.accentColor);
  });

  // Text color that contrasts with accent
  const accentTextColor = computed(() => {
    return getContrastTextColor(accentColor.value);
  });

  // Style object for accent-colored buttons
  const accentButtonStyle = computed(() => ({
    background: accentColor.value,
    color: accentTextColor.value,
  }));

  // Logo URL - resolve relative /api URLs to the actual API base URL
  const brandingLogoUrl = computed(() => {
    const url = getBrandingValue('brandingLogoUrl', '');
    if (!url) return '';
    // If URL starts with /api, replace with the actual API base URL
    if (url.startsWith('/api')) {
      // API_BASE_URL might be '/api' (dev) or 'https://xxx.azurewebsites.net/api' (prod)
      // Remove the /api prefix from the stored URL and append to API_BASE_URL
      return API_BASE_URL + url.substring(4); // Remove '/api' prefix
    }
    return url;
  });

  // Logo position: 'left', 'right', or 'cover'
  const brandingLogoPosition = computed(() => getBrandingValue('brandingLogoPosition', 'left'));

  return {
    pageBackgroundStyle,
    headerStyle,
    headerTextColor,
    accentColor,
    accentTextColor,
    accentButtonStyle,
    brandingLogoUrl,
    brandingLogoPosition,
  };
}
