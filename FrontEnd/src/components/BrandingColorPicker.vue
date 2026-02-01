<template>
  <div class="branding-color-picker" ref="pickerRef">
    <label v-if="label" class="picker-label">{{ label }}</label>
    <button
      type="button"
      class="color-button"
      ref="buttonRef"
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

    <!-- Non-teleported popup (default) -->
    <div v-if="isOpen && !teleport" class="color-popup">
      <ColorPickerPopupContent
        :label="label"
        :model-value="modelValue"
        :colors="colors"
        :show-default="showDefault"
        :show-contrast-preview="showContrastPreview"
        :effective-color="effectiveColor"
        :contrast-text-color="contrastTextColor"
        @close="isOpen = false"
        @select="selectColor"
      />
    </div>

    <!-- Teleported popup (for use inside modals) -->
    <Teleport to="body">
      <div v-if="isOpen && teleport" class="color-popup color-popup-fixed" :style="popupPosition">
        <ColorPickerPopupContent
          :label="label"
          :model-value="modelValue"
          :colors="colors"
          :show-default="showDefault"
          :show-contrast-preview="showContrastPreview"
          :effective-color="effectiveColor"
          :contrast-text-color="contrastTextColor"
          @close="isOpen = false"
          @select="selectColor"
        />
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import ColorPickerPopupContent from './ColorPickerPopupContent.vue';
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
  teleport: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['update:modelValue']);

const isOpen = ref(false);
const buttonRef = ref(null);
const pickerRef = ref(null);
const popupPosition = ref({});

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
  if (found) return found.name;
  // It's a custom color - show the hex value
  return props.modelValue;
});

const contrastTextColor = computed(() => {
  return getContrastTextColor(effectiveColor.value);
});

function toggleOpen() {
  if (!isOpen.value && props.teleport && buttonRef.value) {
    // Calculate position for teleported popup
    const rect = buttonRef.value.getBoundingClientRect();
    popupPosition.value = {
      position: 'fixed',
      top: `${rect.bottom + 4}px`,
      left: `${rect.left}px`,
      zIndex: 10000,
    };
  }
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
</style>

<!-- Global styles for teleported popup -->
<style>
.color-popup-fixed {
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-medium, #ddd);
  border-radius: 8px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
  min-width: 280px;
}
</style>
