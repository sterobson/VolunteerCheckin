/**
 * Branding preset themes for marshal mode customization.
 * Each preset defines colors for header, page background, and accent elements.
 */

export const BRANDING_PRESETS = [
  {
    name: 'Default',
    headerGradientStart: '#667eea',
    headerGradientEnd: '#764ba2',
    accentColor: '#667eea',
    pageGradientStart: '#667eea',
    pageGradientEnd: '#764ba2',
  },
  {
    name: 'Wasp',
    headerGradientStart: '#333333',
    headerGradientEnd: '#333333',
    accentColor: '#FFD700',
    pageGradientStart: '#FFD700',
    pageGradientEnd: '#333333',
  },
  {
    name: 'Ocean',
    headerGradientStart: '#0077be',
    headerGradientEnd: '#00a8e8',
    accentColor: '#0077be',
    pageGradientStart: '#0077be',
    pageGradientEnd: '#00a8e8',
  },
  {
    name: 'Forest',
    headerGradientStart: '#228b22',
    headerGradientEnd: '#32cd32',
    accentColor: '#228b22',
    pageGradientStart: '#228b22',
    pageGradientEnd: '#32cd32',
  },
  {
    name: 'Sunset',
    headerGradientStart: '#ff6b35',
    headerGradientEnd: '#f7931e',
    accentColor: '#ff6b35',
    pageGradientStart: '#ff6b35',
    pageGradientEnd: '#f7931e',
  },
  {
    name: 'Berry',
    headerGradientStart: '#8e2de2',
    headerGradientEnd: '#4a00e0',
    accentColor: '#8e2de2',
    pageGradientStart: '#8e2de2',
    pageGradientEnd: '#4a00e0',
  },
  {
    name: 'Coral',
    headerGradientStart: '#ff758c',
    headerGradientEnd: '#ff7eb3',
    accentColor: '#ff758c',
    pageGradientStart: '#ff758c',
    pageGradientEnd: '#ff7eb3',
  },
  {
    name: 'Steel',
    headerGradientStart: '#485563',
    headerGradientEnd: '#29323c',
    accentColor: '#485563',
    pageGradientStart: '#485563',
    pageGradientEnd: '#29323c',
  },
  {
    name: 'Mint',
    headerGradientStart: '#11998e',
    headerGradientEnd: '#38ef7d',
    accentColor: '#11998e',
    pageGradientStart: '#11998e',
    pageGradientEnd: '#38ef7d',
  },
  {
    name: 'Cherry',
    headerGradientStart: '#eb3349',
    headerGradientEnd: '#f45c43',
    accentColor: '#eb3349',
    pageGradientStart: '#eb3349',
    pageGradientEnd: '#f45c43',
  },
  {
    name: 'Midnight',
    headerGradientStart: '#232526',
    headerGradientEnd: '#414345',
    accentColor: '#5c5c5c',
    pageGradientStart: '#232526',
    pageGradientEnd: '#414345',
  },
  {
    name: 'Lavender',
    headerGradientStart: '#a18cd1',
    headerGradientEnd: '#fbc2eb',
    accentColor: '#a18cd1',
    pageGradientStart: '#a18cd1',
    pageGradientEnd: '#fbc2eb',
  },
  {
    name: 'Gold',
    headerGradientStart: '#f7971e',
    headerGradientEnd: '#ffd200',
    accentColor: '#f7971e',
    pageGradientStart: '#f7971e',
    pageGradientEnd: '#ffd200',
  },
];

/**
 * Gets the default branding values.
 * @returns {Object} Default branding configuration
 */
export function getDefaultBranding() {
  return { ...BRANDING_PRESETS[0] };
}

/**
 * Applies a preset to branding values, preserving logo settings.
 * @param {Object} currentBranding - Current branding values
 * @param {Object} preset - Preset to apply
 * @returns {Object} Updated branding values
 */
export function applyPreset(currentBranding, preset) {
  return {
    ...currentBranding,
    headerGradientStart: preset.headerGradientStart,
    headerGradientEnd: preset.headerGradientEnd,
    accentColor: preset.accentColor,
    pageGradientStart: preset.pageGradientStart,
    pageGradientEnd: preset.pageGradientEnd,
    // Preserve logo settings when changing theme
    logoUrl: currentBranding.logoUrl || '',
    logoPosition: currentBranding.logoPosition || '',
  };
}
