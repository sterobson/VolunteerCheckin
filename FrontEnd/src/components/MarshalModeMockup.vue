<template>
  <div class="marshal-mockup-container">
    <!-- Preset Themes -->
    <div class="presets-section">
      <span class="presets-label">Quick themes:</span>
      <div class="presets-row">
        <button
          v-for="preset in presets"
          :key="preset.name"
          type="button"
          class="preset-btn"
          :title="preset.name"
          @click="applyPreset(preset)"
        >
          <span
            class="preset-preview"
            :style="{ background: `linear-gradient(135deg, ${preset.pageGradientStart} 0%, ${preset.pageGradientEnd} 100%)` }"
          ></span>
          <span class="preset-name">{{ preset.name }}</span>
        </button>
      </div>
    </div>

    <!-- Interactive Preview -->
    <div class="mockup-frame">
      <!-- Page Background (clickable) -->
      <div
        class="mockup-page"
        :class="{ 'region-active': activeRegion === 'page' }"
        :style="pageBackgroundStyle"
        @click="selectRegion('page')"
      >
        <!-- Header (clickable) -->
        <div
          class="mockup-header"
          :class="[
            { 'region-active': activeRegion === 'header' },
            `logo-${effectiveLogoPosition}`
          ]"
          :style="headerStyle"
          @click.stop="selectRegion('header')"
        >
          <!-- Cover logo background -->
          <div v-if="branding.logoUrl && effectiveLogoPosition === 'cover'" class="logo-cover-bg">
            <img :src="branding.logoUrl" alt="Logo" class="logo-cover-img" />
          </div>

          <!-- Left logo -->
          <div
            v-if="effectiveLogoPosition === 'left'"
            class="mockup-logo logo-left"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectRegion('logo')"
          >
            <img v-if="branding.logoUrl" :src="branding.logoUrl" alt="Logo" class="logo-img" />
            <span v-else class="logo-placeholder">+ Logo</span>
          </div>

          <!-- Center content -->
          <div class="header-center" :class="{ 'region-active': activeRegion === 'logo' && !branding.logoUrl && effectiveLogoPosition !== 'left' && effectiveLogoPosition !== 'right' }" @click.stop="selectRegion('logo')">
            <!-- Title -->
            <div class="mockup-title" :style="{ color: headerTextColor }">
              <span class="event-name">{{ eventName || 'Event Name' }}</span>
            </div>

            <!-- Date -->
            <div class="mockup-date" :style="{ color: headerTextColor }">
              {{ eventDate || 'Event Date' }}
            </div>

            <!-- Buttons -->
            <div class="mockup-buttons">
              <span class="btn-emergency">Emergency</span>
              <span class="btn-logout" :style="{ color: headerTextColor }">Logout</span>
            </div>
          </div>

          <!-- Right logo -->
          <div
            v-if="effectiveLogoPosition === 'right'"
            class="mockup-logo logo-right"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectRegion('logo')"
          >
            <img v-if="branding.logoUrl" :src="branding.logoUrl" alt="Logo" class="logo-img" />
            <span v-else class="logo-placeholder">+ Logo</span>
          </div>
        </div>

        <!-- Body Content -->
        <div class="mockup-body">
          <div class="mockup-card">
            <div class="card-header">Your checkpoints (2)</div>
            <div class="card-content">
              <!-- Accent Button (clickable) -->
              <div
                class="mockup-checkin-btn"
                :class="{ 'region-active': activeRegion === 'accent' }"
                :style="accentButtonStyle"
                @click.stop="selectRegion('accent')"
              >
                GPS Check-In
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Region Editor Modal -->
    <div v-if="activeRegion" class="editor-overlay" @click.self="activeRegion = null">
      <div class="editor-modal">
        <div class="editor-header">
          <span>{{ regionLabels[activeRegion] }}</span>
          <button type="button" class="editor-close" @click="activeRegion = null">&times;</button>
        </div>

        <div class="editor-content">
          <!-- Header Gradient Editor -->
          <template v-if="activeRegion === 'header'">
            <p class="editor-description">Choose the colours for the header gradient.</p>
            <div class="color-grid-inline">
              <div class="color-field">
                <label>Start colour</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'hs-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.headerGradientStart === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updateBranding('headerGradientStart', color.hex)"
                  >
                    <span v-if="branding.headerGradientStart === color.hex" class="check">✓</span>
                  </button>
                </div>
              </div>
              <div class="color-field">
                <label>End colour</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'he-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.headerGradientEnd === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updateBranding('headerGradientEnd', color.hex)"
                  >
                    <span v-if="branding.headerGradientEnd === color.hex" class="check">✓</span>
                  </button>
                </div>
              </div>
            </div>
          </template>

          <!-- Logo Editor -->
          <template v-if="activeRegion === 'logo'">
            <div class="logo-position-section">
              <label class="editor-label">Position</label>
              <div class="position-buttons">
                <button
                  type="button"
                  class="position-btn"
                  :class="{ selected: effectiveLogoPosition === 'left' }"
                  @click="updateBranding('logoPosition', 'left')"
                >
                  Left
                </button>
                <button
                  type="button"
                  class="position-btn"
                  :class="{ selected: effectiveLogoPosition === 'cover' }"
                  @click="updateBranding('logoPosition', 'cover')"
                >
                  Cover
                </button>
                <button
                  type="button"
                  class="position-btn"
                  :class="{ selected: effectiveLogoPosition === 'right' }"
                  @click="updateBranding('logoPosition', 'right')"
                >
                  Right
                </button>
              </div>
            </div>
            <LogoUploader
              :model-value="branding.logoUrl"
              :event-id="eventId"
              :admin-email="adminEmail"
              @update:model-value="updateBranding('logoUrl', $event)"
            />
          </template>

          <!-- Accent Color Editor -->
          <template v-if="activeRegion === 'accent'">
            <p class="editor-description">Choose the accent colour for buttons.</p>
            <div class="color-swatches">
              <button
                v-for="color in colors"
                :key="'ac-' + color.hex"
                type="button"
                class="color-swatch-btn"
                :class="{ selected: branding.accentColor === color.hex }"
                :style="{ backgroundColor: color.hex }"
                :title="color.name"
                @click="updateBranding('accentColor', color.hex)"
              >
                <span v-if="branding.accentColor === color.hex" class="check">✓</span>
              </button>
            </div>
          </template>

          <!-- Page Background Editor -->
          <template v-if="activeRegion === 'page'">
            <p class="editor-description">Choose the colours for the page background gradient.</p>
            <div class="color-grid-inline">
              <div class="color-field">
                <label>Start colour</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'ps-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.pageGradientStart === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updateBranding('pageGradientStart', color.hex)"
                  >
                    <span v-if="branding.pageGradientStart === color.hex" class="check">✓</span>
                  </button>
                </div>
              </div>
              <div class="color-field">
                <label>End colour</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'pe-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.pageGradientEnd === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updateBranding('pageGradientEnd', color.hex)"
                  >
                    <span v-if="branding.pageGradientEnd === color.hex" class="check">✓</span>
                  </button>
                </div>
              </div>
            </div>
          </template>
        </div>

        <div class="editor-footer">
          <button type="button" class="btn-done" @click="activeRegion = null">Done</button>
        </div>
      </div>
    </div>

    <!-- Instructions -->
    <p class="instructions">
      Click on different parts of the preview to customise them. The emergency button stays red for safety.
    </p>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import LogoUploader from './LogoUploader.vue';
import { BRANDING_PRESETS, applyPreset as applyPresetUtil } from '../constants/brandingPresets';
import { getContrastTextColor, getGradientContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';
import { AREA_COLORS } from '../constants/areaColors';

const colors = AREA_COLORS;

const props = defineProps({
  branding: {
    type: Object,
    required: true,
  },
  eventName: {
    type: String,
    default: '',
  },
  eventDate: {
    type: String,
    default: '',
  },
  eventId: {
    type: String,
    required: true,
  },
  adminEmail: {
    type: String,
    required: true,
  },
});

const emit = defineEmits(['update:branding']);

const activeRegion = ref(null);
const presets = BRANDING_PRESETS;

const regionLabels = {
  header: 'Header gradient',
  logo: 'Event logo',
  accent: 'Accent colour',
  page: 'Page background',
};

// Computed styles
const effectiveHeaderStart = computed(() =>
  props.branding.headerGradientStart || DEFAULT_COLORS.headerGradientStart
);

const effectiveHeaderEnd = computed(() =>
  props.branding.headerGradientEnd || DEFAULT_COLORS.headerGradientEnd
);

const effectiveAccentColor = computed(() =>
  props.branding.accentColor || DEFAULT_COLORS.accentColor
);

const effectivePageStart = computed(() =>
  props.branding.pageGradientStart || DEFAULT_COLORS.pageGradientStart
);

const effectivePageEnd = computed(() =>
  props.branding.pageGradientEnd || DEFAULT_COLORS.pageGradientEnd
);

const effectiveLogoPosition = computed(() =>
  props.branding.logoPosition || 'left'
);

const headerStyle = computed(() => ({
  background: `linear-gradient(135deg, ${effectiveHeaderStart.value} 0%, ${effectiveHeaderEnd.value} 100%)`,
}));

const headerTextColor = computed(() =>
  getGradientContrastTextColor(effectiveHeaderStart.value, effectiveHeaderEnd.value)
);

const accentButtonStyle = computed(() => ({
  background: effectiveAccentColor.value,
  color: getContrastTextColor(effectiveAccentColor.value),
}));

const pageBackgroundStyle = computed(() => ({
  background: `linear-gradient(135deg, ${effectivePageStart.value} 0%, ${effectivePageEnd.value} 100%)`,
}));

function selectRegion(region) {
  activeRegion.value = activeRegion.value === region ? null : region;
}

function updateBranding(key, value) {
  emit('update:branding', {
    ...props.branding,
    [key]: value,
  });
}

function applyPreset(preset) {
  emit('update:branding', applyPresetUtil(props.branding, preset));
  activeRegion.value = null;
}
</script>

<style scoped>
.marshal-mockup-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

/* Presets Section */
.presets-section {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.presets-label {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.presets-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.preset-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.35rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  transition: all 0.2s;
}

.preset-btn:hover {
  border-color: var(--brand-primary);
  transform: translateY(-1px);
}

.preset-preview {
  width: 40px;
  height: 24px;
  border-radius: 4px;
}

.preset-name {
  font-size: 0.65rem;
  color: var(--text-secondary);
  max-width: 50px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Mockup Frame - Phone shaped */
.mockup-frame {
  width: 280px;
  margin: 0 auto;
  border: 6px solid var(--mockup-frame);
  border-radius: 28px;
  overflow: hidden;
  box-shadow: var(--shadow-lg);
  background: var(--mockup-frame);
}

.mockup-page {
  height: 520px;
  padding: 0;
  cursor: pointer;
  position: relative;
  border-radius: 22px;
  overflow: hidden;
}

.mockup-page::before {
  content: 'Page background';
  position: absolute;
  bottom: 8px;
  right: 8px;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.5);
  pointer-events: none;
}

/* Header - Flexible layout for logo positioning */
.mockup-header {
  display: flex;
  align-items: center;
  padding: 1rem;
  gap: 0.5rem;
  cursor: pointer;
  position: relative;
  text-align: center;
}

.mockup-header.logo-left,
.mockup-header.logo-right {
  flex-direction: row;
  padding-top: 0;
  padding-bottom: 0;
  height: 100px;
}

.mockup-header.logo-left {
  padding-left: 0;
}

.mockup-header.logo-right {
  padding-right: 0;
}

.mockup-header.logo-cover {
  flex-direction: column;
}

.header-center {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.25rem;
  position: relative;
  z-index: 1;
}

.mockup-header::after {
  content: '';
  position: absolute;
  inset: 0;
  border: 2px dashed transparent;
  border-radius: 0;
  pointer-events: none;
  transition: border-color 0.2s;
}

.mockup-header:hover::after,
.mockup-header.region-active::after {
  border-color: rgba(255, 255, 255, 0.5);
}

.mockup-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  position: relative;
  overflow: hidden;
  flex-shrink: 0;
  height: 100%;
  aspect-ratio: 1 / 1;
}

.mockup-logo.logo-left {
  margin-right: 0;
}

.mockup-logo.logo-right {
  margin-left: 0;
}

/* Cover logo background */
.logo-cover-bg {
  position: absolute;
  inset: 0;
  overflow: hidden;
  z-index: 0;
}

.logo-cover-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  opacity: 0.3;
}

.mockup-logo::after {
  content: '';
  position: absolute;
  inset: 0;
  border: 2px dashed transparent;
  pointer-events: none;
  transition: border-color 0.2s;
}

.mockup-logo:hover::after,
.mockup-logo.region-active::after {
  border-color: rgba(255, 255, 255, 0.8);
}

.logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
}

.logo-placeholder {
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.7);
  padding: 0.5rem;
  text-align: center;
}

.mockup-title {
  display: flex;
  flex-direction: column;
  align-items: center;
}

.event-name {
  font-weight: 600;
  font-size: 1rem;
}

.mockup-date {
  font-size: 0.8rem;
  opacity: 0.85;
}

.mockup-buttons {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.25rem;
}

.btn-emergency {
  background: var(--emergency-bg);
  color: white;
  padding: 0.4rem 0.75rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.btn-logout {
  background: rgba(255, 255, 255, 0.2);
  padding: 0.4rem 0.75rem;
  border-radius: 4px;
  font-size: 0.75rem;
}

/* Body */
.mockup-body {
  padding: 1rem;
}

.mockup-card {
  background: var(--card-bg);
  border-radius: 8px;
  overflow: hidden;
  box-shadow: var(--shadow-sm);
}

.card-header {
  padding: 0.5rem 0.75rem;
  background: var(--bg-secondary);
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--text-dark);
  border-bottom: 1px solid var(--border-lighter);
}

.card-content {
  padding: 0.75rem;
}

.mockup-checkin-btn {
  display: inline-block;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.8rem;
  font-weight: 500;
  cursor: pointer;
  position: relative;
}

.mockup-checkin-btn::after {
  content: '';
  position: absolute;
  inset: -2px;
  border: 2px dashed transparent;
  border-radius: 8px;
  pointer-events: none;
  transition: border-color 0.2s;
}

.mockup-checkin-btn:hover::after,
.mockup-checkin-btn.region-active::after {
  border-color: var(--text-dark);
}

/* Region Active State */
.region-active {
  outline: none;
}

/* Editor Modal Overlay */
.editor-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.editor-modal {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-xl);
  max-width: 400px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid var(--border-color);
  font-weight: 600;
  font-size: 1.1rem;
}

.editor-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  transition: background 0.2s;
}

.editor-close:hover {
  background: var(--bg-hover);
  color: var(--text-dark);
}

.editor-content {
  padding: 1.25rem;
}

.editor-description {
  margin: 0 0 1rem 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.editor-label {
  display: block;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-dark);
  margin-bottom: 0.5rem;
}

.logo-position-section {
  margin-bottom: 1rem;
}

.position-buttons {
  display: flex;
  gap: 0.5rem;
}

.position-btn {
  flex: 1;
  padding: 0.5rem 0.75rem;
  border: 2px solid var(--border-medium);
  background: var(--card-bg);
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.85rem;
  transition: all 0.2s;
}

.position-btn:hover {
  border-color: var(--brand-primary);
  background: var(--brand-primary-bg);
}

.position-btn.selected {
  border-color: var(--brand-primary);
  background: var(--brand-primary);
  color: var(--btn-primary-text);
}

.editor-footer {
  padding: 1rem 1.25rem;
  border-top: 1px solid var(--border-color);
  display: flex;
  justify-content: flex-end;
}

.btn-done {
  background: var(--brand-primary);
  color: var(--btn-primary-text);
  border: none;
  padding: 0.6rem 1.5rem;
  border-radius: 6px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-done:hover {
  background: var(--brand-primary-hover);
}

/* Color Swatches */
.color-grid-inline {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.color-field label {
  display: block;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-dark);
  margin-bottom: 0.5rem;
}

.color-swatches {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.color-swatch-btn {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  border: 2px solid transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  padding: 0;
}

.color-swatch-btn:hover {
  transform: scale(1.1);
  box-shadow: var(--shadow-md);
}

.color-swatch-btn.selected {
  border-color: var(--text-dark);
  box-shadow: 0 0 0 2px var(--card-bg), 0 0 0 4px var(--text-dark);
}

.color-swatch-btn .check {
  color: var(--btn-primary-text);
  text-shadow: var(--shadow-xs);
  font-weight: bold;
  font-size: 0.9rem;
}

/* Instructions */
.instructions {
  font-size: 0.8rem;
  color: var(--text-light);
  margin: 0;
  text-align: center;
}
</style>
