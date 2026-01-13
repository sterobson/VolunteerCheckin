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
            hasLogoToShow ? `logo-${effectiveLogoPosition}` : 'logo-none'
          ]"
          :style="headerStyle"
          @click.stop="selectRegion('header')"
        >
          <!-- Cover logo background -->
          <div v-if="hasLogoToShow && effectiveLogoPosition === 'cover'" class="logo-cover-bg">
            <img :src="displayLogoUrl" alt="Logo" class="logo-cover-img" />
          </div>

          <!-- Cover mode logo edit button (only when logo exists in cover mode) -->
          <button
            v-if="hasLogoToShow && effectiveLogoPosition === 'cover'"
            type="button"
            class="logo-cover-edit-btn"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectRegion('logo')"
            title="Edit logo"
          >
            <span>✎</span>
          </button>

          <!-- Left logo area - shown when logo exists on left -->
          <div
            v-if="hasLogoToShow && effectiveLogoPosition === 'left'"
            class="mockup-logo logo-left"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectRegion('logo')"
          >
            <img :src="displayLogoUrl" alt="Logo" class="logo-img" />
          </div>

          <!-- No logo: left clickable area -->
          <div
            v-if="!hasLogoToShow"
            class="logo-add-area logo-add-left"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectLogoRegion('left')"
          >
            <span class="logo-add-text">+ Logo</span>
          </div>

          <!-- Center content -->
          <div class="header-center">
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

          <!-- Right logo area - shown when logo exists on right -->
          <div
            v-if="hasLogoToShow && effectiveLogoPosition === 'right'"
            class="mockup-logo logo-right"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectRegion('logo')"
          >
            <img :src="displayLogoUrl" alt="Logo" class="logo-img" />
          </div>

          <!-- No logo: right clickable area -->
          <div
            v-if="!hasLogoToShow"
            class="logo-add-area logo-add-right"
            :class="{ 'region-active': activeRegion === 'logo' }"
            @click.stop="selectLogoRegion('right')"
          >
            <span class="logo-add-text">+ Logo</span>
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
    <div v-show="activeRegion" class="editor-overlay" @click.self="activeRegion = null">
      <div ref="editorModalRef" class="editor-modal">
        <div class="editor-header">
          <span>{{ regionLabels[activeRegion] }}</span>
          <button type="button" class="editor-close" @click="activeRegion = null">&times;</button>
        </div>

        <div class="editor-content">
          <!-- Header Gradient Editor -->
          <template v-if="activeRegion === 'header'">
            <p class="editor-description">Choose the colour for the header.</p>
            <div class="color-grid-inline">
              <div class="color-field">
                <label>{{ headerGradientEnabled ? 'Start colour' : 'Colour' }}</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'hs-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.headerGradientStart === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updateHeaderColor('start', color.hex)"
                  >
                    <span v-if="branding.headerGradientStart === color.hex" class="check">✓</span>
                  </button>
                  <!-- Custom hex option -->
                  <div class="custom-color-wrapper">
                    <button
                      type="button"
                      class="color-swatch-btn custom-color-btn"
                      :class="{ selected: isCustomColor(branding.headerGradientStart) }"
                      :style="{ backgroundColor: getCustomBgColor(branding.headerGradientStart) }"
                      title="Custom colour"
                      @click="toggleCustomHexInput('headerStart', $event)"
                    >
                      <svg class="custom-icon" :style="{ color: getCustomIconColor(branding.headerGradientStart) }" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
                      </svg>
                    </button>
                    <!-- Custom hex input popup -->
                    <div v-if="showCustomHexInput === 'headerStart'" class="custom-hex-popup" :style="popupPosition">
                      <div class="hex-popup-header">
                        <span>Custom colour</span>
                        <button type="button" class="hex-popup-close" @click="showCustomHexInput = null">&times;</button>
                      </div>
                      <div class="hex-input-row">
                        <span class="hex-prefix">#</span>
                        <input
                          ref="hexInputRef"
                          v-model="customHexValue"
                          type="text"
                          maxlength="6"
                          placeholder="000000"
                          class="hex-input"
                          :class="{ invalid: customHexValue && !isValidHex }"
                          @input="onHexInput"
                          @keydown.enter="applyCustomHex"
                        />
                        <div
                          class="hex-preview"
                          :style="{ backgroundColor: isValidHex ? '#' + customHexValue : '#ccc' }"
                        ></div>
                        <button
                          type="button"
                          class="hex-apply-btn"
                          :disabled="!isValidHex"
                          @click="applyCustomHex"
                        >
                          Apply
                        </button>
                      </div>
                      <span v-if="customHexValue && !isValidHex" class="hex-error">Invalid hex code</span>
                    </div>
                  </div>
                </div>
                <label class="gradient-toggle">
                  <input type="checkbox" v-model="headerGradientEnabled" @change="onHeaderGradientToggle" />
                  <span>Use gradient</span>
                </label>
              </div>
              <div v-if="headerGradientEnabled" class="color-field">
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
                  <!-- Custom hex option -->
                  <div class="custom-color-wrapper">
                    <button
                      type="button"
                      class="color-swatch-btn custom-color-btn"
                      :class="{ selected: isCustomColor(branding.headerGradientEnd) }"
                      :style="{ backgroundColor: getCustomBgColor(branding.headerGradientEnd) }"
                      title="Custom colour"
                      @click="toggleCustomHexInput('headerEnd', $event)"
                    >
                      <svg class="custom-icon" :style="{ color: getCustomIconColor(branding.headerGradientEnd) }" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
                      </svg>
                    </button>
                    <!-- Custom hex input popup -->
                    <div v-if="showCustomHexInput === 'headerEnd'" class="custom-hex-popup" :style="popupPosition">
                      <div class="hex-popup-header">
                        <span>Custom colour</span>
                        <button type="button" class="hex-popup-close" @click="showCustomHexInput = null">&times;</button>
                      </div>
                      <div class="hex-input-row">
                        <span class="hex-prefix">#</span>
                        <input
                          ref="hexInputRef"
                          v-model="customHexValue"
                          type="text"
                          maxlength="6"
                          placeholder="000000"
                          class="hex-input"
                          :class="{ invalid: customHexValue && !isValidHex }"
                          @input="onHexInput"
                          @keydown.enter="applyCustomHex"
                        />
                        <div
                          class="hex-preview"
                          :style="{ backgroundColor: isValidHex ? '#' + customHexValue : '#ccc' }"
                        ></div>
                        <button
                          type="button"
                          class="hex-apply-btn"
                          :disabled="!isValidHex"
                          @click="applyCustomHex"
                        >
                          Apply
                        </button>
                      </div>
                      <span v-if="customHexValue && !isValidHex" class="hex-error">Invalid hex code</span>
                    </div>
                  </div>
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
          </template>
          <!-- LogoUploader - always mounted since modal uses v-show, shown only when editing logo -->
          <LogoUploader
            v-show="activeRegion === 'logo'"
            ref="logoUploaderRef"
            :model-value="branding.logoUrl"
            :event-id="eventId"
            :admin-email="adminEmail"
            staged
            @update:model-value="updateBranding('logoUrl', $event)"
            @staged-change="handleLogoStagedChange"
          />

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
              <!-- Custom hex option -->
              <div class="custom-color-wrapper">
                <button
                  type="button"
                  class="color-swatch-btn custom-color-btn"
                  :class="{ selected: isCustomColor(branding.accentColor) }"
                  :style="{ backgroundColor: getCustomBgColor(branding.accentColor) }"
                  title="Custom colour"
                  @click="toggleCustomHexInput('accent', $event)"
                >
                  <svg class="custom-icon" :style="{ color: getCustomIconColor(branding.accentColor) }" viewBox="0 0 24 24" fill="currentColor">
                    <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
                  </svg>
                </button>
                <!-- Custom hex input popup -->
                <div v-if="showCustomHexInput === 'accent'" class="custom-hex-popup" :style="popupPosition">
                  <div class="hex-popup-header">
                    <span>Custom colour</span>
                    <button type="button" class="hex-popup-close" @click="showCustomHexInput = null">&times;</button>
                  </div>
                  <div class="hex-input-row">
                    <span class="hex-prefix">#</span>
                    <input
                      ref="hexInputRef"
                      v-model="customHexValue"
                      type="text"
                      maxlength="6"
                      placeholder="000000"
                      class="hex-input"
                      :class="{ invalid: customHexValue && !isValidHex }"
                      @input="onHexInput"
                      @keydown.enter="applyCustomHex"
                    />
                    <div
                      class="hex-preview"
                      :style="{ backgroundColor: isValidHex ? '#' + customHexValue : '#ccc' }"
                    ></div>
                    <button
                      type="button"
                      class="hex-apply-btn"
                      :disabled="!isValidHex"
                      @click="applyCustomHex"
                    >
                      Apply
                    </button>
                  </div>
                  <span v-if="customHexValue && !isValidHex" class="hex-error">Invalid hex code</span>
                </div>
              </div>
            </div>
          </template>

          <!-- Page Background Editor -->
          <template v-if="activeRegion === 'page'">
            <p class="editor-description">Choose the colour for the page background.</p>
            <div class="color-grid-inline">
              <div class="color-field">
                <label>{{ pageGradientEnabled ? 'Start colour' : 'Colour' }}</label>
                <div class="color-swatches">
                  <button
                    v-for="color in colors"
                    :key="'ps-' + color.hex"
                    type="button"
                    class="color-swatch-btn"
                    :class="{ selected: branding.pageGradientStart === color.hex }"
                    :style="{ backgroundColor: color.hex }"
                    :title="color.name"
                    @click="updatePageColor('start', color.hex)"
                  >
                    <span v-if="branding.pageGradientStart === color.hex" class="check">✓</span>
                  </button>
                  <!-- Custom hex option -->
                  <div class="custom-color-wrapper">
                    <button
                      type="button"
                      class="color-swatch-btn custom-color-btn"
                      :class="{ selected: isCustomColor(branding.pageGradientStart) }"
                      :style="{ backgroundColor: getCustomBgColor(branding.pageGradientStart) }"
                      title="Custom colour"
                      @click="toggleCustomHexInput('pageStart', $event)"
                    >
                      <svg class="custom-icon" :style="{ color: getCustomIconColor(branding.pageGradientStart) }" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
                      </svg>
                    </button>
                    <!-- Custom hex input popup -->
                    <div v-if="showCustomHexInput === 'pageStart'" class="custom-hex-popup" :style="popupPosition">
                      <div class="hex-popup-header">
                        <span>Custom colour</span>
                        <button type="button" class="hex-popup-close" @click="showCustomHexInput = null">&times;</button>
                      </div>
                      <div class="hex-input-row">
                        <span class="hex-prefix">#</span>
                        <input
                          ref="hexInputRef"
                          v-model="customHexValue"
                          type="text"
                          maxlength="6"
                          placeholder="000000"
                          class="hex-input"
                          :class="{ invalid: customHexValue && !isValidHex }"
                          @input="onHexInput"
                          @keydown.enter="applyCustomHex"
                        />
                        <div
                          class="hex-preview"
                          :style="{ backgroundColor: isValidHex ? '#' + customHexValue : '#ccc' }"
                        ></div>
                        <button
                          type="button"
                          class="hex-apply-btn"
                          :disabled="!isValidHex"
                          @click="applyCustomHex"
                        >
                          Apply
                        </button>
                      </div>
                      <span v-if="customHexValue && !isValidHex" class="hex-error">Invalid hex code</span>
                    </div>
                  </div>
                </div>
                <label class="gradient-toggle">
                  <input type="checkbox" v-model="pageGradientEnabled" @change="onPageGradientToggle" />
                  <span>Use gradient</span>
                </label>
              </div>
              <div v-if="pageGradientEnabled" class="color-field">
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
                  <!-- Custom hex option -->
                  <div class="custom-color-wrapper">
                    <button
                      type="button"
                      class="color-swatch-btn custom-color-btn"
                      :class="{ selected: isCustomColor(branding.pageGradientEnd) }"
                      :style="{ backgroundColor: getCustomBgColor(branding.pageGradientEnd) }"
                      title="Custom colour"
                      @click="toggleCustomHexInput('pageEnd', $event)"
                    >
                      <svg class="custom-icon" :style="{ color: getCustomIconColor(branding.pageGradientEnd) }" viewBox="0 0 24 24" fill="currentColor">
                        <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
                      </svg>
                    </button>
                    <!-- Custom hex input popup -->
                    <div v-if="showCustomHexInput === 'pageEnd'" class="custom-hex-popup" :style="popupPosition">
                      <div class="hex-popup-header">
                        <span>Custom colour</span>
                        <button type="button" class="hex-popup-close" @click="showCustomHexInput = null">&times;</button>
                      </div>
                      <div class="hex-input-row">
                        <span class="hex-prefix">#</span>
                        <input
                          ref="hexInputRef"
                          v-model="customHexValue"
                          type="text"
                          maxlength="6"
                          placeholder="000000"
                          class="hex-input"
                          :class="{ invalid: customHexValue && !isValidHex }"
                          @input="onHexInput"
                          @keydown.enter="applyCustomHex"
                        />
                        <div
                          class="hex-preview"
                          :style="{ backgroundColor: isValidHex ? '#' + customHexValue : '#ccc' }"
                        ></div>
                        <button
                          type="button"
                          class="hex-apply-btn"
                          :disabled="!isValidHex"
                          @click="applyCustomHex"
                        >
                          Apply
                        </button>
                      </div>
                      <span v-if="customHexValue && !isValidHex" class="hex-error">Invalid hex code</span>
                    </div>
                  </div>
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
import { ref, computed, watch, nextTick } from 'vue';
import LogoUploader from './LogoUploader.vue';
import { BRANDING_PRESETS, applyPreset as applyPresetUtil } from '../constants/brandingPresets';
import { getContrastTextColor, getGradientContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';
import { AREA_COLORS } from '../constants/areaColors';
import { API_BASE_URL } from '../config';

// Resolve /api URLs to the actual API base URL (for cross-origin deployments)
const resolveApiUrl = (url) => {
  if (!url) return '';
  if (url.startsWith('/api')) {
    return API_BASE_URL + url.substring(4);
  }
  return url;
};

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

const emit = defineEmits(['update:branding', 'logo-staged-change']);

const activeRegion = ref(null);
const presets = BRANDING_PRESETS;

// Gradient toggle states - initialized based on whether colors differ
const headerGradientEnabled = ref(false);
const pageGradientEnabled = ref(false);

// Logo uploader ref for staged upload handling
const logoUploaderRef = ref(null);

// Track staged logo state for mockup preview
const logoPendingDelete = ref(false);
const stagedLogoUrl = ref('');

// Handle staged logo changes from LogoUploader
const handleLogoStagedChange = (stagedState) => {
  // Handle both object format (new) and boolean format (legacy)
  if (typeof stagedState === 'object' && stagedState !== null) {
    logoPendingDelete.value = stagedState.isPendingDelete;
    stagedLogoUrl.value = stagedState.displayUrl || '';
    emit('logo-staged-change', stagedState.hasPendingChanges);
  } else {
    // Fallback to querying the ref if we get a boolean
    if (logoUploaderRef.value) {
      logoPendingDelete.value = logoUploaderRef.value.isPendingDelete();
      stagedLogoUrl.value = logoUploaderRef.value.getDisplayUrl() || '';
    }
    emit('logo-staged-change', !!stagedState);
  }
};

// Custom hex input state
const showCustomHexInput = ref(null); // 'headerStart', 'headerEnd', 'pageStart', 'pageEnd', 'accent'
const customHexValue = ref('');
const hexInputRef = ref(null);
const editorModalRef = ref(null);
const popupPosition = ref({ left: '50%', right: 'auto', transform: 'translateX(-50%)' });

// Check if a color is custom (not in the palette)
const isCustomColor = (color) => {
  if (!color) return false;
  return !colors.some(c => c.hex.toLowerCase() === color.toLowerCase());
};

// Get icon color for custom color button with sufficient contrast
const getCustomIconColor = (color) => {
  if (!color || !isCustomColor(color)) {
    return '#666666'; // Dark gray for default light gray background
  }
  return getContrastTextColor(color);
};

// Get background color for custom button
const getCustomBgColor = (color) => {
  if (!color || !isCustomColor(color)) {
    return '#e0e0e0'; // Light gray when no custom color
  }
  return color;
};

// Validate hex input
const isValidHex = computed(() => {
  if (!customHexValue.value) return false;
  const hex = customHexValue.value.replace(/^#/, '');
  return /^[0-9A-Fa-f]{6}$/.test(hex) || /^[0-9A-Fa-f]{3}$/.test(hex);
});

const toggleCustomHexInput = (field, event) => {
  if (showCustomHexInput.value === field) {
    showCustomHexInput.value = null;
  } else {
    showCustomHexInput.value = field;
    // Pre-fill with current custom color if exists
    let currentColor = '';
    switch (field) {
      case 'headerStart': currentColor = props.branding.headerGradientStart; break;
      case 'headerEnd': currentColor = props.branding.headerGradientEnd; break;
      case 'pageStart': currentColor = props.branding.pageGradientStart; break;
      case 'pageEnd': currentColor = props.branding.pageGradientEnd; break;
      case 'accent': currentColor = props.branding.accentColor; break;
    }
    if (currentColor && isCustomColor(currentColor)) {
      customHexValue.value = currentColor.replace(/^#/, '');
    } else {
      customHexValue.value = '';
    }

    // Calculate popup position to stay within modal boundaries
    if (event && editorModalRef.value) {
      const button = event.currentTarget;
      const modal = editorModalRef.value;
      const buttonRect = button.getBoundingClientRect();
      const modalRect = modal.getBoundingClientRect();
      const popupWidth = 220; // min-width of popup
      const padding = 12; // padding from modal edges

      // Calculate button's center position relative to modal
      const buttonCenterX = buttonRect.left + buttonRect.width / 2 - modalRect.left;
      const modalWidth = modalRect.width;

      // Check if centered popup would overflow right edge
      const centeredLeft = buttonCenterX - popupWidth / 2;
      const centeredRight = buttonCenterX + popupWidth / 2;

      if (centeredRight > modalWidth - padding) {
        // Align to right edge
        popupPosition.value = { left: 'auto', right: '0', transform: 'none' };
      } else if (centeredLeft < padding) {
        // Align to left edge
        popupPosition.value = { left: '0', right: 'auto', transform: 'none' };
      } else {
        // Center it
        popupPosition.value = { left: '50%', right: 'auto', transform: 'translateX(-50%)' };
      }
    }

    nextTick(() => hexInputRef.value?.focus());
  }
};

const onHexInput = () => {
  customHexValue.value = customHexValue.value.replace(/[^0-9A-Fa-f]/g, '').toUpperCase();
};

const applyCustomHex = () => {
  if (!isValidHex.value || !showCustomHexInput.value) return;
  let hex = customHexValue.value;
  // Expand 3-char hex to 6-char
  if (hex.length === 3) {
    hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
  }
  const color = '#' + hex;

  switch (showCustomHexInput.value) {
    case 'headerStart':
      updateHeaderColor('start', color);
      break;
    case 'headerEnd':
      updateBranding('headerGradientEnd', color);
      break;
    case 'pageStart':
      updatePageColor('start', color);
      break;
    case 'pageEnd':
      updateBranding('pageGradientEnd', color);
      break;
    case 'accent':
      updateBranding('accentColor', color);
      break;
  }
  showCustomHexInput.value = null;
};

// Watch activeRegion to initialize gradient toggles when opening editors
watch(activeRegion, (region) => {
  // Reset custom hex input when region changes
  showCustomHexInput.value = null;
  customHexValue.value = '';
  if (region === 'header') {
    // Enable gradient if start and end colors are different
    const start = props.branding.headerGradientStart || DEFAULT_COLORS.headerGradientStart;
    const end = props.branding.headerGradientEnd || DEFAULT_COLORS.headerGradientEnd;
    headerGradientEnabled.value = start !== end;
  } else if (region === 'page') {
    // Enable gradient if start and end colors are different
    const start = props.branding.pageGradientStart || DEFAULT_COLORS.pageGradientStart;
    const end = props.branding.pageGradientEnd || DEFAULT_COLORS.pageGradientEnd;
    pageGradientEnabled.value = start !== end;
  }
});

const regionLabels = {
  header: 'Header colour',
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

// Cache-busting key that changes when logo URL changes
const logoCacheKey = ref(Date.now());
watch(() => props.branding.logoUrl, () => {
  logoCacheKey.value = Date.now();
  // Reset staged state when branding URL changes from props (e.g., after save)
  logoPendingDelete.value = false;
  stagedLogoUrl.value = '';
});

// Check if we should show a logo in the mockup preview
const hasLogoToShow = computed(() => {
  // If pending deletion, don't show
  if (logoPendingDelete.value) return false;
  // Show if we have a staged URL or a server URL
  return !!(stagedLogoUrl.value || props.branding.logoUrl);
});

// Logo URL for mockup preview - uses staged URL if available
const displayLogoUrl = computed(() => {
  // Don't return URL if logo is pending deletion
  if (logoPendingDelete.value) return '';

  // Use staged URL if available (includes blob URLs for new uploads)
  if (stagedLogoUrl.value) {
    // Blob URLs don't need cache busting
    if (stagedLogoUrl.value.startsWith('blob:')) {
      return stagedLogoUrl.value;
    }
    const resolvedUrl = resolveApiUrl(stagedLogoUrl.value);
    const separator = resolvedUrl.includes('?') ? '&' : '?';
    return `${resolvedUrl}${separator}_t=${logoCacheKey.value}`;
  }
  // Fall back to server URL
  if (!props.branding.logoUrl) return '';
  const resolvedUrl = resolveApiUrl(props.branding.logoUrl);
  const separator = resolvedUrl.includes('?') ? '&' : '?';
  return `${resolvedUrl}${separator}_t=${logoCacheKey.value}`;
});

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

// Select logo region with a default position hint (for when clicking empty logo areas)
function selectLogoRegion(defaultPosition) {
  // Only set the default position if there's no logo yet
  if (!hasLogoToShow.value && defaultPosition) {
    updateBranding('logoPosition', defaultPosition);
  }
  selectRegion('logo');
}

function updateBranding(key, value) {
  emit('update:branding', {
    ...props.branding,
    [key]: value,
  });
}

// Update header color - if gradient disabled, set both start and end to same color
function updateHeaderColor(which, color) {
  if (headerGradientEnabled.value) {
    updateBranding(which === 'start' ? 'headerGradientStart' : 'headerGradientEnd', color);
  } else {
    // Set both colors to the same value
    emit('update:branding', {
      ...props.branding,
      headerGradientStart: color,
      headerGradientEnd: color,
    });
  }
}

// Update page color - if gradient disabled, set both start and end to same color
function updatePageColor(which, color) {
  if (pageGradientEnabled.value) {
    updateBranding(which === 'start' ? 'pageGradientStart' : 'pageGradientEnd', color);
  } else {
    // Set both colors to the same value
    emit('update:branding', {
      ...props.branding,
      pageGradientStart: color,
      pageGradientEnd: color,
    });
  }
}

// When header gradient is toggled off, set end color to match start
function onHeaderGradientToggle() {
  if (!headerGradientEnabled.value) {
    const startColor = props.branding.headerGradientStart || DEFAULT_COLORS.headerGradientStart;
    updateBranding('headerGradientEnd', startColor);
  }
}

// When page gradient is toggled off, set end color to match start
function onPageGradientToggle() {
  if (!pageGradientEnabled.value) {
    const startColor = props.branding.pageGradientStart || DEFAULT_COLORS.pageGradientStart;
    updateBranding('pageGradientEnd', startColor);
  }
}

function applyPreset(preset) {
  emit('update:branding', applyPresetUtil(props.branding, preset));
  activeRegion.value = null;
}

// Methods for parent to handle staged logo upload on save
async function uploadStagedLogo() {
  if (!logoUploaderRef.value) {
    return { success: true, logoUrl: props.branding.logoUrl };
  }
  return await logoUploaderRef.value.uploadStagedFile();
}

async function deleteStagedLogo() {
  if (!logoUploaderRef.value) {
    return { success: true };
  }
  return await logoUploaderRef.value.deleteStagedLogo();
}

function hasLogoChanges() {
  return logoUploaderRef.value?.hasPendingChanges() || false;
}

function isLogoPendingDelete() {
  return logoUploaderRef.value?.isPendingDelete() || false;
}

// Reset logo staged state (for when branding is reset)
function resetLogo() {
  if (logoUploaderRef.value) {
    logoUploaderRef.value.resetStagedState();
  }
  logoPendingDelete.value = false;
  stagedLogoUrl.value = '';
}

// Expose methods for parent component
defineExpose({
  uploadStagedLogo,
  deleteStagedLogo,
  hasLogoChanges,
  isLogoPendingDelete,
  resetLogo,
});
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
/* Force light mode colors since marshal view doesn't support dark mode */
.mockup-frame {
  width: 280px;
  margin: 0 auto;
  border: 6px solid #333;
  border-radius: 28px;
  overflow: hidden;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.15);
  background: #333;

  /* Override CSS variables to always use light mode values */
  /* Marshal view doesn't support dark mode, so mockup should always show light mode */
  --card-bg: #ffffff;
  --bg-secondary: #f8f9fa;
  --text-dark: #212529;
  --text-secondary: #6c757d;
  --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.1);
  --border-light: #e9ecef;
  --border-lighter: #f1f3f4;
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

.mockup-header.logo-none {
  display: grid;
  grid-template-columns: 1fr auto 1fr;
  padding: 0;
  height: 100px;
  gap: 0;
}

.mockup-header.logo-none .header-center {
  flex: none;
}

/* Clickable areas to add logo when none exists */
.logo-add-area {
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  position: relative;
  height: 100%;
}

.logo-add-area::after {
  content: '';
  position: absolute;
  inset: 0;
  border: 2px dashed transparent;
  pointer-events: none;
  transition: border-color 0.2s;
}

.logo-add-area:hover::after,
.logo-add-area.region-active::after {
  border-color: rgba(255, 255, 255, 0.8);
}

.logo-add-text {
  font-size: 0.65rem;
  color: rgba(255, 255, 255, 0.7);
  padding: 0.5rem;
  text-align: center;
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

/* Cover mode logo edit button */
.logo-cover-edit-btn {
  position: absolute;
  top: 8px;
  right: 8px;
  z-index: 2;
  background: rgba(255, 255, 255, 0.9);
  border: 2px dashed transparent;
  border-radius: 6px;
  padding: 0.3rem 0.5rem;
  font-size: 0.7rem;
  cursor: pointer;
  transition: all 0.2s;
  color: var(--text-dark);
}

.logo-cover-edit-btn:hover {
  background: white;
  border-color: var(--text-dark);
}

.logo-cover-edit-btn.region-active {
  border-color: var(--brand-primary);
  background: white;
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

/* Custom color button wrapper for popup positioning */
.custom-color-wrapper {
  position: relative;
  display: inline-block;
}

/* Custom color button - background set via inline style */
.custom-color-btn .custom-icon {
  width: 18px;
  height: 18px;
  /* Color set via inline style for contrast */
}

/* Custom hex input popup */
.custom-hex-popup {
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  margin-top: 8px;
  padding: 0;
  background: var(--card-bg);
  border: 1px solid var(--border-medium);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 10;
  min-width: 220px;
}

.hex-popup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--border-lighter);
  font-size: 0.85rem;
  font-weight: 500;
}

.hex-popup-close {
  background: none;
  border: none;
  font-size: 1.25rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.hex-popup-close:hover {
  color: var(--text-dark);
}

.custom-hex-popup .hex-input-row {
  padding: 0.75rem;
}

.custom-hex-popup .hex-error {
  padding: 0 0.75rem 0.75rem;
  margin-top: -0.5rem;
}

.hex-input-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.hex-prefix {
  font-family: monospace;
  font-size: 1rem;
  color: var(--text-secondary);
}

.hex-input {
  flex: 1;
  font-family: monospace;
  font-size: 0.9rem;
  padding: 0.4rem 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  background: var(--card-bg);
  color: var(--text-primary);
  text-transform: uppercase;
  max-width: 80px;
}

.hex-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.hex-input.invalid {
  border-color: var(--danger, #dc3545);
}

.hex-preview {
  width: 28px;
  height: 28px;
  border-radius: 4px;
  border: 1px solid rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}

.hex-apply-btn {
  padding: 0.4rem 0.75rem;
  font-size: 0.8rem;
  background: var(--brand-primary);
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-weight: 500;
}

.hex-apply-btn:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.hex-apply-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.hex-error {
  display: block;
  font-size: 0.75rem;
  color: var(--danger, #dc3545);
  margin-top: 0.25rem;
}

/* Gradient Toggle */
.gradient-toggle {
  display: inline-flex;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  cursor: pointer;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.gradient-toggle input[type="checkbox"] {
  width: 14px;
  height: 14px;
  margin: 0;
  margin-top: 1px;
  margin-right: 0.5rem;
  cursor: pointer;
  accent-color: var(--brand-primary);
  flex-shrink: 0;
}

.gradient-toggle span {
  line-height: 1;
}

.gradient-toggle:hover {
  color: var(--text-dark);
}

/* Instructions */
.instructions {
  font-size: 0.8rem;
  color: var(--text-light);
  margin: 0;
  text-align: center;
}
</style>
