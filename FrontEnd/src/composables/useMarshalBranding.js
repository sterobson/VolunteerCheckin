import { computed } from 'vue';
import { getContrastTextColor, getGradientContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';

/**
 * Composable for managing marshal view branding/theming
 * Extracts color and style computations from event data
 *
 * @param {Ref<Object>} event - Reactive reference to the event object
 * @returns {Object} Branding-related computed properties
 */
export function useMarshalBranding(event) {
  // Page background gradient
  const pageBackgroundStyle = computed(() => {
    const start = event.value?.brandingPageGradientStart || DEFAULT_COLORS.pageGradientStart;
    const end = event.value?.brandingPageGradientEnd || DEFAULT_COLORS.pageGradientEnd;
    return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
  });

  // Header gradient
  const headerStyle = computed(() => {
    const start = event.value?.brandingHeaderGradientStart || DEFAULT_COLORS.headerGradientStart;
    const end = event.value?.brandingHeaderGradientEnd || DEFAULT_COLORS.headerGradientEnd;
    return { background: `linear-gradient(135deg, ${start} 0%, ${end} 100%)` };
  });

  // Header text color (contrasts with gradient)
  const headerTextColor = computed(() => {
    const start = event.value?.brandingHeaderGradientStart || DEFAULT_COLORS.headerGradientStart;
    const end = event.value?.brandingHeaderGradientEnd || DEFAULT_COLORS.headerGradientEnd;
    return getGradientContrastTextColor(start, end);
  });

  // Accent color for buttons and highlights
  const accentColor = computed(() => {
    return event.value?.brandingAccentColor || DEFAULT_COLORS.accentColor;
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

  // Logo URL
  const brandingLogoUrl = computed(() => event.value?.brandingLogoUrl || '');

  // Logo position: 'left', 'right', or 'cover'
  const brandingLogoPosition = computed(() => event.value?.brandingLogoPosition || 'left');

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
