<template>
  <div class="gradient-picker">
    <label v-if="label" class="picker-label">{{ label }}</label>

    <!-- Gradient Preview Bar -->
    <div
      class="gradient-preview"
      :style="{ background: gradientStyle }"
      @click="toggleOpen"
    >
      <span class="preview-text" :style="{ color: contrastTextColor }">
        {{ previewText }}
      </span>
    </div>

    <!-- Expanded Editor -->
    <div v-if="isOpen" class="gradient-editor">
      <div class="editor-header">
        <span>{{ label || 'Edit gradient' }}</span>
        <button type="button" class="editor-close" @click="isOpen = false">&times;</button>
      </div>

      <div class="editor-body">
        <!-- Start Color -->
        <div class="color-row">
          <span class="color-label">Start colour</span>
          <div class="color-selector">
            <button
              type="button"
              class="color-button"
              :class="{ active: activeColor === 'start' }"
              @click="toggleColorPicker('start')"
            >
              <span class="color-swatch" :style="{ backgroundColor: effectiveStartColor }"></span>
              <span class="color-name">{{ getColorName(startColor, effectiveStartColor) }}</span>
              <span class="arrow">&#9662;</span>
            </button>
          </div>
        </div>

        <!-- End Color -->
        <div class="color-row">
          <span class="color-label">End colour</span>
          <div class="color-selector">
            <button
              type="button"
              class="color-button"
              :class="{ active: activeColor === 'end' }"
              @click="toggleColorPicker('end')"
            >
              <span class="color-swatch" :style="{ backgroundColor: effectiveEndColor }"></span>
              <span class="color-name">{{ getColorName(endColor, effectiveEndColor) }}</span>
              <span class="arrow">&#9662;</span>
            </button>
          </div>
        </div>

        <!-- Color Grid (shown when picking a color) -->
        <div v-if="activeColor" class="color-grid-section">
          <div class="grid-header">
            Select {{ activeColor }} colour
          </div>
          <div class="color-grid">
            <button
              v-if="showDefault"
              type="button"
              class="color-option default-option"
              :class="{ selected: (activeColor === 'start' ? startColor : endColor) === '' }"
              @click="selectColor('')"
            >
              <span class="default-icon">↩</span>
            </button>
            <button
              v-for="color in colors"
              :key="color.hex"
              type="button"
              class="color-option"
              :class="{ selected: isColorSelected(color.hex) }"
              :style="{ backgroundColor: color.hex }"
              :title="color.name"
              @click="selectColor(color.hex)"
            >
              <span v-if="isColorSelected(color.hex)" class="check-mark">✓</span>
            </button>
            <!-- Custom hex option -->
            <button
              type="button"
              class="color-option custom-option"
              :class="{ selected: isCurrentColorCustom }"
              :style="isCurrentColorCustom ? { backgroundColor: activeColor === 'start' ? startColor : endColor } : {}"
              title="Custom colour"
              @click="toggleCustomInput"
            >
              <svg class="custom-icon" viewBox="0 0 24 24" fill="currentColor">
                <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
              </svg>
              <span v-if="isCurrentColorCustom" class="check-mark">✓</span>
            </button>
          </div>
          <!-- Custom hex input -->
          <div v-if="showCustomInput" class="custom-hex-input">
            <label>Enter hex code</label>
            <div class="hex-input-row">
              <span class="hex-prefix">#</span>
              <input
                ref="hexInput"
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
            <span v-if="customHexValue && !isValidHex" class="hex-error">
              Invalid hex code
            </span>
          </div>
        </div>

        <!-- Result Preview -->
        <div class="result-preview">
          <div
            class="preview-bar"
            :style="{ background: gradientStyle }"
          >
            <span :style="{ color: contrastTextColor }">Preview</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, nextTick } from 'vue';
import { AREA_COLORS } from '../constants/areaColors';
import { getContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';

const props = defineProps({
  startColor: {
    type: String,
    default: '',
  },
  endColor: {
    type: String,
    default: '',
  },
  label: {
    type: String,
    default: '',
  },
  previewText: {
    type: String,
    default: 'Click to edit',
  },
  defaultStartColor: {
    type: String,
    default: () => DEFAULT_COLORS.headerGradientStart,
  },
  defaultEndColor: {
    type: String,
    default: () => DEFAULT_COLORS.headerGradientEnd,
  },
  showDefault: {
    type: Boolean,
    default: true,
  },
  colors: {
    type: Array,
    default: () => AREA_COLORS,
  },
});

const emit = defineEmits(['update:startColor', 'update:endColor']);

const isOpen = ref(false);
const activeColor = ref(null); // 'start' or 'end'
const showCustomInput = ref(false);
const customHexValue = ref('');
const hexInput = ref(null);

const effectiveStartColor = computed(() => {
  return props.startColor || props.defaultStartColor;
});

const effectiveEndColor = computed(() => {
  return props.endColor || props.defaultEndColor;
});

const gradientStyle = computed(() => {
  return `linear-gradient(135deg, ${effectiveStartColor.value} 0%, ${effectiveEndColor.value} 100%)`;
});

const contrastTextColor = computed(() => {
  // Use end color for contrast calculation (where text typically appears)
  return getContrastTextColor(effectiveEndColor.value);
});

// Check if current active color is a custom color (not in palette)
const isCurrentColorCustom = computed(() => {
  if (!activeColor.value) return false;
  const currentColor = activeColor.value === 'start' ? props.startColor : props.endColor;
  if (!currentColor) return false;
  return !props.colors.some(c => c.hex.toLowerCase() === currentColor.toLowerCase());
});

// Validate hex input (3 or 6 hex characters)
const isValidHex = computed(() => {
  if (!customHexValue.value) return false;
  const hex = customHexValue.value.replace(/^#/, '');
  return /^[0-9A-Fa-f]{6}$/.test(hex) || /^[0-9A-Fa-f]{3}$/.test(hex);
});

function toggleOpen() {
  isOpen.value = !isOpen.value;
  if (!isOpen.value) {
    activeColor.value = null;
    showCustomInput.value = false;
  }
}

function toggleColorPicker(which) {
  activeColor.value = activeColor.value === which ? null : which;
  showCustomInput.value = false;
}

function isColorSelected(hex) {
  const currentColor = activeColor.value === 'start' ? props.startColor : props.endColor;
  return currentColor && currentColor.toLowerCase() === hex.toLowerCase();
}

function selectColor(hex) {
  showCustomInput.value = false;
  if (activeColor.value === 'start') {
    emit('update:startColor', hex);
  } else if (activeColor.value === 'end') {
    emit('update:endColor', hex);
  }
  activeColor.value = null;
}

function toggleCustomInput() {
  showCustomInput.value = !showCustomInput.value;
  if (showCustomInput.value) {
    // Pre-fill with current custom color if exists
    if (isCurrentColorCustom.value) {
      const currentColor = activeColor.value === 'start' ? props.startColor : props.endColor;
      customHexValue.value = currentColor.replace(/^#/, '');
    } else {
      customHexValue.value = '';
    }
    nextTick(() => {
      hexInput.value?.focus();
    });
  }
}

function onHexInput() {
  // Remove any non-hex characters and # prefix
  customHexValue.value = customHexValue.value.replace(/[^0-9A-Fa-f]/g, '').toUpperCase();
}

function applyCustomHex() {
  if (!isValidHex.value) return;
  let hex = customHexValue.value;
  // Expand 3-char hex to 6-char
  if (hex.length === 3) {
    hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2];
  }
  selectColor('#' + hex);
}

function getColorName(value, effectiveValue) {
  if (!value) {
    return 'Default';
  }
  const found = props.colors.find(c => c.hex.toLowerCase() === effectiveValue.toLowerCase());
  return found ? found.name : value;
}
</script>

<style scoped>
.gradient-picker {
  position: relative;
}

.picker-label {
  display: block;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 0.25rem;
}

.gradient-preview {
  height: 48px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  border: 2px solid transparent;
  transition: all 0.2s;
}

.gradient-preview:hover {
  border-color: rgba(255, 255, 255, 0.5);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

.preview-text {
  font-weight: 500;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}

.gradient-editor {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  margin-top: 4px;
  background: var(--card-bg);
  border: 1px solid var(--border-medium);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 100;
}

.editor-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-lighter);
  font-weight: 500;
}

.editor-close {
  background: none;
  border: none;
  font-size: 1.25rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.editor-close:hover {
  color: var(--text-dark);
}

.editor-body {
  padding: 1rem;
}

.color-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 0.75rem;
}

.color-label {
  font-size: 0.9rem;
  color: var(--text-darker);
}

.color-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  min-width: 130px;
  transition: all 0.2s;
}

.color-button:hover {
  border-color: var(--brand-primary);
}

.color-button.active {
  border-color: var(--brand-primary);
  box-shadow: 0 0 0 2px var(--brand-shadow);
}

.color-swatch {
  width: 18px;
  height: 18px;
  border-radius: 4px;
  border: 1px solid rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}

.color-name {
  flex: 1;
  text-align: left;
  font-size: 0.85rem;
}

.arrow {
  color: var(--text-muted);
  font-size: 0.65rem;
}

.color-grid-section {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--border-lighter);
}

.grid-header {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.color-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 4px;
}

.color-option {
  width: 28px;
  height: 28px;
  border-radius: 4px;
  border: 2px solid transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
  padding: 0;
}

.color-option:hover {
  transform: scale(1.15);
}

.color-option.selected {
  border-color: var(--text-dark);
  box-shadow: 0 0 0 1px var(--card-bg), 0 0 0 3px var(--text-dark);
}

.default-option {
  background: linear-gradient(135deg, var(--bg-hover) 50%, var(--border-medium) 50%);
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.check-mark {
  color: white;
  text-shadow: 0 0 2px rgba(0, 0, 0, 0.5);
  font-weight: bold;
  font-size: 0.75rem;
}

.result-preview {
  margin-top: 1rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--border-lighter);
}

.preview-bar {
  height: 36px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.85rem;
  font-weight: 500;
}

/* Custom color option */
.custom-option {
  background: linear-gradient(135deg, #ff6b6b 0%, #ffd93d 25%, #6bcb77 50%, #4d96ff 75%, #9b59b6 100%);
  position: relative;
}

.custom-option .custom-icon {
  width: 16px;
  height: 16px;
  color: white;
  filter: drop-shadow(0 1px 1px rgba(0, 0, 0, 0.3));
}

.custom-option.selected {
  border-color: var(--text-dark);
  box-shadow: 0 0 0 1px var(--card-bg), 0 0 0 3px var(--text-dark);
}

.custom-option.selected .custom-icon {
  display: none;
}

/* Custom hex input */
.custom-hex-input {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--border-lighter);
}

.custom-hex-input label {
  display: block;
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
}

.hex-input-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.hex-prefix {
  font-family: monospace;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.hex-input {
  flex: 1;
  font-family: monospace;
  font-size: 0.85rem;
  padding: 0.35rem 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  background: var(--input-bg);
  color: var(--text-primary);
  text-transform: uppercase;
  max-width: 70px;
}

.hex-input:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.hex-input.invalid {
  border-color: var(--danger, #dc3545);
}

.hex-preview {
  width: 24px;
  height: 24px;
  border-radius: 4px;
  border: 1px solid rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}

.hex-apply-btn {
  padding: 0.35rem 0.6rem;
  font-size: 0.75rem;
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
  font-size: 0.7rem;
  color: var(--danger, #dc3545);
  margin-top: 0.25rem;
}
</style>
