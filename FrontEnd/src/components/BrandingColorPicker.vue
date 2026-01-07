<template>
  <div class="branding-color-picker">
    <label v-if="label" class="picker-label">{{ label }}</label>
    <button
      type="button"
      class="color-button"
      :class="{ active: isOpen }"
      @click="toggleOpen"
    >
      <span
        class="color-swatch"
        :style="{ backgroundColor: effectiveColor }"
      ></span>
      <span class="color-name">{{ colorName }}</span>
      <span class="arrow">&#9662;</span>
    </button>

    <div v-if="isOpen" class="color-popup">
      <div class="popup-header">
        <span>{{ label || 'Select colour' }}</span>
        <button type="button" class="popup-close" @click="isOpen = false">&times;</button>
      </div>
      <div class="color-grid">
        <button
          v-if="showDefault"
          type="button"
          class="color-option default-option"
          :class="{ selected: modelValue === '' || modelValue === 'default' }"
          @click="selectColor('')"
        >
          <span class="default-icon">↩</span>
          <span>Default</span>
        </button>
        <button
          v-for="color in colors"
          :key="color.hex"
          type="button"
          class="color-option"
          :class="{ selected: modelValue === color.hex }"
          :style="{ backgroundColor: color.hex }"
          :title="color.name"
          @click="selectColor(color.hex)"
        >
          <span v-if="modelValue === color.hex" class="check-mark">✓</span>
        </button>
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
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import { AREA_COLORS } from '../constants/areaColors';
import { getContrastTextColor, DEFAULT_COLORS } from '../utils/colorContrast';

const props = defineProps({
  modelValue: {
    type: String,
    default: '',
  },
  label: {
    type: String,
    default: '',
  },
  defaultColor: {
    type: String,
    default: '',
  },
  showDefault: {
    type: Boolean,
    default: true,
  },
  showContrastPreview: {
    type: Boolean,
    default: true,
  },
  colors: {
    type: Array,
    default: () => AREA_COLORS,
  },
});

const emit = defineEmits(['update:modelValue']);

const isOpen = ref(false);

const effectiveColor = computed(() => {
  if (props.modelValue && props.modelValue !== 'default') {
    return props.modelValue;
  }
  return props.defaultColor || DEFAULT_COLORS.headerGradientStart;
});

const colorName = computed(() => {
  if (!props.modelValue || props.modelValue === '' || props.modelValue === 'default') {
    return 'Default';
  }
  const found = props.colors.find(c => c.hex.toLowerCase() === props.modelValue.toLowerCase());
  return found ? found.name : props.modelValue;
});

const contrastTextColor = computed(() => {
  return getContrastTextColor(effectiveColor.value);
});

function toggleOpen() {
  isOpen.value = !isOpen.value;
}

function selectColor(color) {
  emit('update:modelValue', color);
  isOpen.value = false;
}
</script>

<style scoped>
.branding-color-picker {
  position: relative;
}

.picker-label {
  display: block;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 0.25rem;
}

.color-button {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  min-width: 140px;
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
  width: 20px;
  height: 20px;
  border-radius: 4px;
  border: 1px solid rgba(0, 0, 0, 0.1);
  flex-shrink: 0;
}

.color-name {
  flex: 1;
  text-align: left;
  font-size: 0.9rem;
}

.arrow {
  color: var(--text-muted);
  font-size: 0.7rem;
}

.color-popup {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 4px;
  background: var(--card-bg);
  border: 1px solid var(--border-medium);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 100;
  min-width: 280px;
}

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
</style>
