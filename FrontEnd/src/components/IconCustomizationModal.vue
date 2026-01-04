<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="cancel">
    <div class="modal-content">
      <div class="modal-header">
        <h3>Customise icon style</h3>
        <button class="close-btn" @click="cancel">&times;</button>
      </div>

      <div class="modal-body">
        <!-- Live Preview over map background -->
        <div class="preview-section">
          <div class="map-preview">
            <div class="map-background"></div>
            <div class="preview-icon" v-html="previewSvg"></div>
          </div>
        </div>

        <!-- Option Buttons - Two Column Layout -->
        <!-- Order: Shape, Background, Border, Icon, Colour, Size -->
        <div class="options-grid">
          <!-- Row 1: Shape and Background -->
          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'shape' }"
            @click="togglePopup('shape')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Shape</span>
              <div class="option-button-value icon-value">
                <div class="icon-preview-small" v-html="getShapePreviewSvg(localBackgroundShape)"></div>
                <span>{{ getShapeLabel(localBackgroundShape) }}</span>
              </div>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>

          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'bgColor' }"
            @click="togglePopup('bgColor')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Background</span>
              <div class="option-button-value color-value">
                <span
                  class="color-swatch"
                  :style="{ backgroundColor: effectiveBackgroundColor }"
                ></span>
                <span>{{ getColorLabel(localBackgroundColor) }}</span>
              </div>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>

          <!-- Row 2: Border and Icon -->
          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'borderColor', disabled: !hasShape }"
            :disabled="!hasShape"
            @click="togglePopup('borderColor')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Border</span>
              <div class="option-button-value color-value">
                <span
                  class="color-swatch"
                  :class="{ 'no-border': localBorderColor === 'none' }"
                  :style="localBorderColor !== 'none' ? { backgroundColor: effectiveBorderColor } : {}"
                ></span>
                <span>{{ getBorderColorLabel(localBorderColor) }}</span>
              </div>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>

          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'icon' }"
            @click="togglePopup('icon')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Icon</span>
              <div class="option-button-value icon-value">
                <div class="icon-preview-small" v-html="getIconButtonPreview(localIconType)"></div>
                <span>{{ getIconLabel(localIconType) }}</span>
              </div>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>

          <!-- Row 3: Colour and Size -->
          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'iconColor', disabled: !hasIcon }"
            :disabled="!hasIcon"
            @click="togglePopup('iconColor')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Colour</span>
              <div class="option-button-value color-value">
                <span
                  class="color-swatch"
                  :style="{ backgroundColor: effectiveIconColor }"
                ></span>
                <span>{{ getIconColorLabel(localIconColor) }}</span>
              </div>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>

          <button
            type="button"
            class="option-button"
            :class="{ active: activePopup === 'size' }"
            @click="togglePopup('size')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Size</span>
              <span class="option-button-value">{{ getSizeLabel(localSize) }}</span>
            </div>
            <span class="option-button-arrow">&#9662;</span>
          </button>
        </div>

        <!-- Centered Popup Panels -->
        <!-- Shape Popup -->
        <div v-if="activePopup === 'shape'" class="centered-popup">
          <div class="popup-header">
            <span>Select shape</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="popup-grid">
            <button
              v-for="shape in BACKGROUND_SHAPES"
              :key="shape.value"
              type="button"
              class="popup-grid-item"
              :class="{ selected: localBackgroundShape === shape.value }"
              @click="selectShape(shape.value)"
            >
              <div class="popup-item-preview" v-html="getShapePreviewSvg(shape.value)"></div>
              <span>{{ shape.label }}</span>
            </button>
          </div>
        </div>

        <!-- Background Color Popup -->
        <div v-if="activePopup === 'bgColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select background</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="color-grid">
            <button
              v-for="color in backgroundColorOptions"
              :key="color.hex"
              type="button"
              class="color-option"
              :class="{ selected: isBackgroundColorSelected(color.hex) }"
              :style="color.hex !== 'default' ? { backgroundColor: color.hex } : {}"
              :title="color.name"
              @click="selectBackgroundColor(color.hex)"
            >
              <span v-if="color.hex === 'default'" class="default-indicator">D</span>
            </button>
          </div>
        </div>

        <!-- Border Color Popup -->
        <div v-if="activePopup === 'borderColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select border</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="color-grid">
            <button
              type="button"
              class="color-option none-option"
              :class="{ selected: localBorderColor === 'none' }"
              title="None"
              @click="selectBorderColor('none')"
            >
              <div class="none-icon"></div>
            </button>
            <button
              v-for="color in borderColorOptions"
              :key="color.hex"
              type="button"
              class="color-option"
              :class="{ selected: localBorderColor === color.hex }"
              :style="{ backgroundColor: color.hex }"
              :title="color.name"
              @click="selectBorderColor(color.hex)"
            ></button>
          </div>
        </div>

        <!-- Icon Popup -->
        <div v-if="activePopup === 'icon'" class="centered-popup icon-popup">
          <div class="popup-header">
            <span>Select icon</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="icon-grid">
            <button
              v-for="iconType in availableContentIcons"
              :key="iconType.value"
              type="button"
              class="icon-grid-item"
              :class="{ selected: localIconType === iconType.value }"
              @click="selectIcon(iconType.value)"
            >
              <div class="icon-grid-preview" v-html="getIconDropdownPreview(iconType.value)"></div>
              <span class="icon-grid-label">{{ iconType.label }}</span>
            </button>
          </div>
        </div>

        <!-- Icon Color Popup -->
        <div v-if="activePopup === 'iconColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select colour</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="color-grid">
            <button
              v-for="color in iconColorOptions"
              :key="color.hex"
              type="button"
              class="color-option"
              :class="{ selected: isIconColorSelected(color.hex) }"
              :style="{ backgroundColor: color.hex }"
              :title="color.name"
              @click="selectIconColor(color.hex)"
            ></button>
          </div>
        </div>

        <!-- Size Popup -->
        <div v-if="activePopup === 'size'" class="centered-popup">
          <div class="popup-header">
            <span>Select size</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="size-list">
            <button
              v-for="sizeOption in ICON_SIZES"
              :key="sizeOption.value"
              type="button"
              class="size-option"
              :class="{ selected: localSize === sizeOption.value }"
              @click="selectSize(sizeOption.value)"
            >
              {{ sizeOption.label }}
            </button>
          </div>
        </div>
      </div>

      <div class="modal-footer">
        <button type="button" class="btn-cancel" @click="cancel">Cancel</button>
        <button type="button" class="btn-apply" @click="apply">Apply</button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { AREA_COLORS } from '../constants/areaColors';
import {
  BACKGROUND_SHAPES,
  ICON_SIZES,
  CHECKPOINT_ICON_TYPES,
  getIconTypeConfig,
  generateCheckpointSvg,
  backgroundShapeGenerators,
} from '../constants/checkpointIcons';

const props = defineProps({
  isOpen: { type: Boolean, default: false },
  iconType: { type: String, default: 'none' },
  backgroundShape: { type: String, default: 'circle' },
  backgroundColor: { type: String, default: '' },
  borderColor: { type: String, default: '' },
  iconColor: { type: String, default: '' },
  size: { type: String, default: '100' },
  level: { type: String, default: 'checkpoint' },
});

const emit = defineEmits(['update:isOpen', 'apply', 'cancel']);

// Local state for editing
const localBackgroundShape = ref(props.backgroundShape || 'circle');
const localIconType = ref(props.iconType || 'none');
const localBackgroundColor = ref(props.backgroundColor || '');
const localBorderColor = ref(props.borderColor || '');
const localIconColor = ref(props.iconColor || '');
const localSize = ref(props.size || '100');

// Popup state
const activePopup = ref(null);

// Content icons (icons that go ON TOP of a shape) - excludes shape-only types
const availableContentIcons = computed(() => {
  const contentIcons = CHECKPOINT_ICON_TYPES.filter(t =>
    t.category === 'content' || t.value === 'default'
  );
  // Add "None" option at the start for no icon (just shape)
  return [
    { value: 'none', label: 'None', category: 'content' },
    ...contentIcons.filter(t => t.value !== 'default'),
  ];
});

// Check if shape is set (not 'none')
const hasShape = computed(() => localBackgroundShape.value !== 'none');

// Check if icon is set (not 'none')
const hasIcon = computed(() => localIconType.value !== 'none');

// Get default color based on icon type, or a standard default
const getDefaultColor = () => {
  if (hasIcon.value) {
    const config = getIconTypeConfig(localIconType.value);
    return config.defaultColor || '#667eea';
  }
  return '#667eea';
};

// Effective colors (with defaults)
const effectiveBackgroundColor = computed(() => {
  if (localBackgroundColor.value && localBackgroundColor.value !== 'default') {
    return localBackgroundColor.value;
  }
  return getDefaultColor();
});

const effectiveBorderColor = computed(() => {
  if (localBorderColor.value === 'none') return 'transparent';
  if (localBorderColor.value) return localBorderColor.value;
  return '#ffffff';
});

const effectiveIconColor = computed(() => {
  if (localIconColor.value) return localIconColor.value;
  return '#ffffff';
});

// Color options
const backgroundColorOptions = computed(() => {
  return [
    { hex: 'default', name: 'Default' },
    ...AREA_COLORS.map(c => ({ hex: c.hex, name: c.name })),
  ];
});

const borderColorOptions = computed(() => {
  return [
    { hex: '#ffffff', name: 'White' },
    { hex: '#000000', name: 'Black' },
    ...AREA_COLORS.filter(c => c.hex !== '#ffffff').map(c => ({ hex: c.hex, name: c.name })),
  ];
});

const iconColorOptions = computed(() => {
  return [
    { hex: '#ffffff', name: 'White' },
    { hex: '#000000', name: 'Black' },
    ...AREA_COLORS.filter(c => c.hex !== '#ffffff').map(c => ({ hex: c.hex, name: c.name })),
  ];
});

// Selection check helpers
const isBackgroundColorSelected = (hex) => {
  if (hex === 'default') {
    return !localBackgroundColor.value || localBackgroundColor.value === 'default';
  }
  return localBackgroundColor.value === hex;
};

const isIconColorSelected = (hex) => {
  if (hex === '#ffffff') {
    return !localIconColor.value || localIconColor.value === '#ffffff';
  }
  return localIconColor.value === hex;
};

// Get labels
const getShapeLabel = (value) => {
  const shape = BACKGROUND_SHAPES.find(s => s.value === value);
  return shape ? shape.label : 'Circle';
};

const getIconLabel = (value) => {
  if (value === 'none') return 'None';
  const icon = CHECKPOINT_ICON_TYPES.find(t => t.value === value);
  return icon ? icon.label : 'None';
};

const getColorLabel = (value) => {
  if (!value || value === 'default') return 'Default';
  const color = [...AREA_COLORS].find(c => c.hex === value);
  return color ? color.name : value;
};

const getBorderColorLabel = (value) => {
  if (value === 'none') return 'None';
  if (!value || value === '#ffffff') return 'White';
  const color = [...borderColorOptions.value].find(c => c.hex === value);
  return color ? color.name : value;
};

const getIconColorLabel = (value) => {
  if (!value || value === '#ffffff') return 'White';
  const color = [...iconColorOptions.value].find(c => c.hex === value);
  return color ? color.name : value;
};

const getSizeLabel = (value) => {
  const size = ICON_SIZES.find(s => s.value === value);
  return size ? size.label : 'Normal (100%)';
};

// Preview generators
const getShapePreviewSvg = (shapeValue) => {
  if (shapeValue === 'none') {
    return `<svg width="20" height="20" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="16" height="16" rx="2" fill="none" stroke="#ccc" stroke-width="1.5" stroke-dasharray="3"/>
    </svg>`;
  }

  if (backgroundShapeGenerators[shapeValue]) {
    const svgContent = backgroundShapeGenerators[shapeValue]({
      backgroundColor: '#667eea',
      borderColor: '#fff',
    });
    return `<svg width="20" height="20" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">${svgContent}</svg>`;
  }

  return '';
};

// Preview for the Icon button (shows current selection with full styling)
const getIconButtonPreview = (iconTypeValue) => {
  if (iconTypeValue === 'none') {
    return `<svg width="18" height="18" viewBox="0 0 18 18" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="16" height="16" rx="2" fill="none" stroke="#ccc" stroke-width="1.5" stroke-dasharray="3"/>
    </svg>`;
  }

  const config = getIconTypeConfig(iconTypeValue);
  return generateCheckpointSvg({
    type: iconTypeValue,
    backgroundShape: 'circle',
    backgroundColor: config.defaultColor || '#667eea',
    borderColor: '#fff',
    iconColor: '#fff',
    size: '75',
  });
};

// Preview for icons in the dropdown (white icon on blue rounded square)
const getIconDropdownPreview = (iconTypeValue) => {
  if (iconTypeValue === 'none') {
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="#e2e8f0"/>
      <line x1="8" y1="8" x2="20" y2="20" stroke="#a0aec0" stroke-width="2" stroke-linecap="round"/>
    </svg>`;
  }

  // All icons shown as white on blue rounded square
  return generateCheckpointSvg({
    type: iconTypeValue,
    backgroundShape: 'square',
    backgroundColor: '#3182ce',
    borderColor: 'none',
    iconColor: '#fff',
    size: '100',
  });
};

// Main preview SVG - combines shape + icon
const previewSvg = computed(() => {
  const bgColor = localBackgroundColor.value === 'default' || !localBackgroundColor.value
    ? getDefaultColor()
    : localBackgroundColor.value;

  // If we have an icon, use the icon type with the background shape
  if (hasIcon.value) {
    return generateCheckpointSvg({
      type: localIconType.value,
      backgroundShape: localBackgroundShape.value,
      backgroundColor: bgColor,
      borderColor: localBorderColor.value === 'none' ? 'none' : (localBorderColor.value || '#fff'),
      iconColor: localIconColor.value || '#fff',
      size: localSize.value,
    });
  }

  // If no icon, just render the shape
  if (hasShape.value) {
    return generateCheckpointSvg({
      type: localBackgroundShape.value,
      backgroundShape: localBackgroundShape.value,
      backgroundColor: bgColor,
      borderColor: localBorderColor.value === 'none' ? 'none' : (localBorderColor.value || '#fff'),
      iconColor: '#fff',
      size: localSize.value,
    });
  }

  // Nothing to show
  return '';
});

// Popup control
const togglePopup = (popup) => {
  activePopup.value = activePopup.value === popup ? null : popup;
};

const closePopups = () => {
  activePopup.value = null;
};

// Selection handlers
const selectShape = (value) => {
  localBackgroundShape.value = value;
  closePopups();
};

const selectBackgroundColor = (value) => {
  localBackgroundColor.value = value === 'default' ? '' : value;
  closePopups();
};

const selectBorderColor = (value) => {
  localBorderColor.value = value;
  closePopups();
};

const selectIcon = (value) => {
  localIconType.value = value;
  closePopups();
};

const selectIconColor = (value) => {
  localIconColor.value = value;
  closePopups();
};

const selectSize = (value) => {
  localSize.value = value;
  closePopups();
};

// Sync local state when props change
watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    localBackgroundShape.value = props.backgroundShape || 'circle';
    localIconType.value = props.iconType || 'none';
    localBackgroundColor.value = props.backgroundColor || '';
    localBorderColor.value = props.borderColor || '';
    localIconColor.value = props.iconColor || '';
    localSize.value = props.size || '100';
    activePopup.value = null;
  }
});

const cancel = () => {
  closePopups();
  emit('update:isOpen', false);
  emit('cancel');
};

const apply = () => {
  closePopups();
  const effectiveType = hasIcon.value ? localIconType.value : localBackgroundShape.value;

  emit('apply', {
    iconType: effectiveType,
    backgroundShape: localBackgroundShape.value,
    backgroundColor: localBackgroundColor.value,
    borderColor: localBorderColor.value,
    iconColor: localIconColor.value,
    size: localSize.value,
  });
  emit('update:isOpen', false);
};
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: white;
  border-radius: 12px;
  width: 90%;
  max-width: 400px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid #e2e8f0;
}

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: #1a202c;
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #718096;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.close-btn:hover {
  color: #1a202c;
}

.modal-body {
  padding: 1rem 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  position: relative;
  min-height: 280px;
}

/* Map Preview */
.preview-section {
  display: flex;
  justify-content: center;
}

.map-preview {
  position: relative;
  width: 100%;
  height: 80px;
  border-radius: 8px;
  overflow: hidden;
}

.map-background {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background:
    linear-gradient(90deg, transparent 48%, #e8e8e8 48%, #e8e8e8 52%, transparent 52%),
    linear-gradient(0deg, transparent 45%, #e8e8e8 45%, #e8e8e8 55%, transparent 55%),
    linear-gradient(135deg, #d4d4d4 25%, transparent 25%),
    linear-gradient(225deg, #d4d4d4 25%, transparent 25%),
    linear-gradient(45deg, #c9c9c9 25%, transparent 25%),
    linear-gradient(315deg, #c9c9c9 25%, transparent 25%),
    #f0f0f0;
  background-size:
    100% 100%,
    100% 100%,
    20px 20px,
    20px 20px,
    20px 20px,
    20px 20px,
    100% 100%;
}

.map-background::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background:
    radial-gradient(ellipse 30px 20px at 15% 25%, #c8e6c9 0%, transparent 70%),
    radial-gradient(ellipse 25px 15px at 80% 70%, #c8e6c9 0%, transparent 70%),
    radial-gradient(ellipse 35px 12px at 65% 20%, #b3d9f7 0%, transparent 70%);
}

.preview-icon {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.3));
}

/* Options Grid - Two Columns */
.options-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.5rem;
}

.option-button {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.625rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: white;
  cursor: pointer;
  transition: all 0.15s;
  min-height: 48px;
}

.option-button:hover:not(.disabled) {
  border-color: #cbd5e0;
  background: #f8fafc;
}

.option-button.active {
  border-color: #3182ce;
  background: #ebf8ff;
}

.option-button.disabled {
  opacity: 0.5;
  cursor: not-allowed;
  background: #f7fafc;
}

.option-button-content {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 0.125rem;
  min-width: 0;
  flex: 1;
}

.option-button-label {
  font-size: 0.65rem;
  color: #718096;
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}

.option-button-value {
  font-size: 0.8rem;
  color: #1a202c;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.option-button-value.color-value,
.option-button-value.icon-value {
  display: flex;
  align-items: center;
  gap: 0.375rem;
}

.color-swatch {
  width: 14px;
  height: 14px;
  border-radius: 3px;
  border: 1px solid #e2e8f0;
  flex-shrink: 0;
}

.color-swatch.no-border {
  background: repeating-linear-gradient(
    45deg,
    #f0f0f0,
    #f0f0f0 2px,
    white 2px,
    white 4px
  );
}

.icon-preview-small {
  width: 18px;
  height: 18px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.option-button-arrow {
  font-size: 0.65rem;
  color: #a0aec0;
  margin-left: 0.25rem;
}

/* Centered Popup Panels - Overlay */
.centered-popup {
  position: absolute;
  top: 0.5rem;
  left: 0.5rem;
  right: 0.5rem;
  bottom: 0.5rem;
  background: white;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.2);
  z-index: 20;
  display: flex;
  flex-direction: column;
}

.popup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid #e2e8f0;
  font-size: 0.8rem;
  font-weight: 500;
  color: #4a5568;
}

.popup-close {
  background: none;
  border: none;
  font-size: 1.25rem;
  color: #a0aec0;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.popup-close:hover {
  color: #4a5568;
}

/* Shape/Size Grid */
.popup-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.5rem;
  padding: 0.75rem;
  flex: 1;
  align-content: start;
  overflow-y: auto;
}

.popup-grid-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem 0.25rem;
  border: 2px solid transparent;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.15s;
}

.popup-grid-item:hover {
  background: #f7fafc;
}

.popup-grid-item.selected {
  border-color: #3182ce;
  background: #ebf8ff;
}

.popup-grid-item span {
  font-size: 0.65rem;
  color: #4a5568;
  text-align: center;
}

.popup-item-preview {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Color Grid */
.color-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.75rem;
  flex: 1;
  align-content: start;
  overflow-y: auto;
}

.color-option {
  width: 36px;
  height: 36px;
  border-radius: 6px;
  border: 2px solid #e2e8f0;
  cursor: pointer;
  transition: all 0.15s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.color-option:hover {
  transform: scale(1.1);
  border-color: #cbd5e0;
}

.color-option.selected {
  border-color: #3182ce;
  box-shadow: 0 0 0 2px #bee3f8;
}

.color-option.none-option {
  background: white;
}

.none-icon {
  width: 16px;
  height: 16px;
  border: 2px dashed #cbd5e0;
  border-radius: 3px;
  position: relative;
}

.none-icon::after {
  content: '';
  position: absolute;
  top: 50%;
  left: -2px;
  right: -2px;
  height: 2px;
  background: #ef4444;
  transform: rotate(-45deg);
}

.default-indicator {
  font-size: 0.7rem;
  font-weight: 600;
  color: #718096;
}

/* Icon Popup - Larger overlay */
.icon-popup {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 90%;
  max-width: 400px;
  max-height: 80vh;
  bottom: auto;
  right: auto;
}

/* Icon Grid */
.icon-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.5rem;
  padding: 0.75rem;
  flex: 1;
  align-content: start;
  overflow-y: auto;
  overflow-x: hidden;
}

.icon-grid-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.375rem;
  border: 2px solid transparent;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.15s;
}

.icon-grid-item:hover {
  background: #f7fafc;
}

.icon-grid-item.selected {
  border-color: #3182ce;
  background: #ebf8ff;
}

.icon-grid-preview {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-grid-label {
  font-size: 0.6rem;
  color: #4a5568;
  text-align: center;
  line-height: 1.1;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Size List */
.size-list {
  display: flex;
  flex-direction: column;
  padding: 0.75rem;
  flex: 1;
  overflow-y: auto;
}

.size-option {
  padding: 0.5rem 0.75rem;
  border: none;
  border-radius: 4px;
  background: white;
  cursor: pointer;
  font-size: 0.8rem;
  color: #4a5568;
  text-align: left;
  transition: all 0.15s;
}

.size-option:hover {
  background: #f7fafc;
}

.size-option.selected {
  background: #ebf8ff;
  color: #3182ce;
  font-weight: 500;
}

/* Footer */
.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.25rem;
  border-top: 1px solid #e2e8f0;
}

.btn-cancel {
  padding: 0.5rem 1rem;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: white;
  color: #4a5568;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
}

.btn-cancel:hover {
  background: #f7fafc;
  border-color: #cbd5e0;
}

.btn-apply {
  padding: 0.5rem 1.5rem;
  border: none;
  border-radius: 6px;
  background: #3182ce;
  color: white;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
}

.btn-apply:hover {
  background: #2c5282;
}
</style>
