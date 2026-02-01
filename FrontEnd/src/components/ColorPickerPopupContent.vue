<template>
  <div class="popup-content">
    <div class="popup-header">
      <span>{{ label || 'Select colour' }}</span>
      <button type="button" class="popup-close" @click="$emit('close')">&times;</button>
    </div>
    <div class="color-grid">
      <button
        v-if="showDefault"
        type="button"
        class="color-option default-option"
        :class="{ selected: modelValue === '' || modelValue === 'default' }"
        @click="$emit('select', '')"
      >
        <span class="default-icon">↩</span>
        <span>Default</span>
      </button>
      <button
        v-for="color in colors"
        :key="color.hex"
        type="button"
        class="color-option"
        :class="{ selected: isColorSelected(color.hex) }"
        :style="{ backgroundColor: color.hex }"
        :title="color.name"
        @click="$emit('select', color.hex)"
      >
        <span v-if="isColorSelected(color.hex)" class="check-mark">✓</span>
      </button>
      <!-- Custom hex option -->
      <button
        type="button"
        class="color-option custom-option"
        :class="{ selected: isCustomColor }"
        :style="isCustomColor ? { backgroundColor: modelValue } : {}"
        title="Custom colour"
        @click="toggleCustomInput"
      >
        <svg class="custom-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M17.66 5.41l.92.92-2.69 2.69-.92-.92 2.69-2.69M17.67 3c-.26 0-.51.1-.71.29l-3.12 3.12-1.93-1.91-1.41 1.41 1.42 1.42L3 16.25V21h4.75l8.92-8.92 1.42 1.42 1.41-1.41-1.92-1.92 3.12-3.12c.4-.4.4-1.03.01-1.42l-2.34-2.34c-.2-.19-.45-.29-.7-.29zM6.92 19L5 17.08l8.06-8.06 1.92 1.92L6.92 19z"/>
        </svg>
        <span v-if="isCustomColor" class="check-mark">✓</span>
      </button>
    </div>
    <!-- Custom hex input -->
    <div v-if="showCustomInput" class="custom-hex-input">
      <label>Enter hex code</label>
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
      <span v-if="customHexValue && !isValidHex" class="hex-error">
        Invalid hex code
      </span>
    </div>
    <div v-if="showContrastPreview" class="contrast-preview">
      <span class="preview-label">Text will appear:</span>
      <span
        class="preview-text"
        :style="{
          backgroundColor: effectiveColor,
          color: contrastTextColor
        }"
      >
        Sample Text
      </span>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, nextTick } from 'vue';

const props = defineProps({
  label: {
    type: String,
    default: '',
  },
  modelValue: {
    type: String,
    default: '',
  },
  colors: {
    type: Array,
    required: true,
  },
  showDefault: {
    type: Boolean,
    default: true,
  },
  showContrastPreview: {
    type: Boolean,
    default: true,
  },
  effectiveColor: {
    type: String,
    required: true,
  },
  contrastTextColor: {
    type: String,
    required: true,
  },
});

const emit = defineEmits(['close', 'select']);

const showCustomInput = ref(false);
const customHexValue = ref('');
const hexInputRef = ref(null);

// Check if a color from the palette is selected
function isColorSelected(hex) {
  return props.modelValue && props.modelValue.toLowerCase() === hex.toLowerCase();
}

// Check if current value is a custom color (not in palette and not default)
const isCustomColor = computed(() => {
  if (!props.modelValue || props.modelValue === '' || props.modelValue === 'default') {
    return false;
  }
  return !props.colors.some(c => c.hex.toLowerCase() === props.modelValue.toLowerCase());
});

// Validate hex input (3 or 6 hex characters)
const isValidHex = computed(() => {
  if (!customHexValue.value) return false;
  const hex = customHexValue.value.replace(/^#/, '');
  return /^[0-9A-Fa-f]{6}$/.test(hex) || /^[0-9A-Fa-f]{3}$/.test(hex);
});

function toggleCustomInput() {
  showCustomInput.value = !showCustomInput.value;
  if (showCustomInput.value) {
    // Pre-fill with current custom color if exists
    if (isCustomColor.value) {
      customHexValue.value = props.modelValue.replace(/^#/, '');
    } else {
      customHexValue.value = '';
    }
    nextTick(() => {
      hexInputRef.value?.focus();
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
  emit('select', '#' + hex);
  showCustomInput.value = false;
}
</script>

<style scoped>
.popup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem 1rem;
  border-bottom: 1px solid var(--border-lighter);
  font-weight: 500;
}

.popup-close {
  background: none;
  border: none;
  font-size: 1.25rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.popup-close:hover {
  color: var(--text-dark);
}

.color-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 6px;
  padding: 1rem;
}

.color-option {
  width: 36px;
  height: 36px;
  border-radius: 6px;
  border: 2px solid transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.15s;
}

.color-option:hover {
  transform: scale(1.1);
}

.color-option.selected {
  border-color: var(--text-dark);
  box-shadow: 0 0 0 2px var(--card-bg), 0 0 0 4px var(--text-dark);
}

.default-option {
  background: linear-gradient(135deg, var(--bg-hover) 50%, var(--border-medium) 50%);
  flex-direction: column;
  gap: 2px;
  font-size: 0.6rem;
  color: var(--text-secondary);
}

.default-icon {
  font-size: 0.9rem;
}

.check-mark {
  color: white;
  text-shadow: 0 0 2px rgba(0, 0, 0, 0.5);
  font-weight: bold;
}

.contrast-preview {
  padding: 0.75rem 1rem;
  border-top: 1px solid var(--border-lighter);
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.preview-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.preview-text {
  padding: 0.25rem 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  font-weight: 500;
}

/* Custom color option */
.custom-option {
  background: linear-gradient(135deg, #ff6b6b 0%, #ffd93d 25%, #6bcb77 50%, #4d96ff 75%, #9b59b6 100%);
  position: relative;
}

.custom-option .custom-icon {
  width: 20px;
  height: 20px;
  color: white;
  filter: drop-shadow(0 1px 1px rgba(0, 0, 0, 0.3));
}

.custom-option.selected {
  border-color: var(--text-dark);
  box-shadow: 0 0 0 2px var(--card-bg), 0 0 0 4px var(--text-dark);
}

.custom-option.selected .custom-icon {
  display: none;
}

/* Custom hex input */
.custom-hex-input {
  padding: 0.75rem 1rem;
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
  background: var(--input-bg);
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
</style>
