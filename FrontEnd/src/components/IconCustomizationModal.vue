<template>
  <div v-if="isOpen" class="modal-overlay" @click.self="cancel">
    <div class="modal-content">
      <div class="modal-header">
        <h3>Customise marker style</h3>
        <button class="close-btn" @click="cancel">&times;</button>
      </div>

      <div class="modal-body">
        <!-- Live Preview over map background -->
        <div class="preview-section">
          <div class="map-preview">
            <div class="map-background"></div>
            <div class="preview-icon" :style="{ transform: `rotate(${getEffectiveMapRotationValue(localMapRotation)}deg)` }" v-html="previewSvg"></div>
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
                <div class="icon-preview-small" v-html="getShapeButtonPreview(localBackgroundShape)"></div>
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

          <!-- Row 4: Map Rotation -->
          <button
            type="button"
            class="option-button option-button-full"
            :class="{ active: activePopup === 'mapRotation' }"
            @click="togglePopup('mapRotation')"
          >
            <div class="option-button-content">
              <span class="option-button-label">Map rotation</span>
              <span class="option-button-value">{{ getMapRotationLabel(localMapRotation) }}</span>
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
              v-for="shape in availableShapes"
              :key="shape.value"
              type="button"
              class="popup-grid-item"
              :class="{ selected: localBackgroundShape === shape.value }"
              @click="selectShape(shape.value)"
            >
              <div class="popup-item-preview" v-html="getShapePopupPreview(shape.value)"></div>
              <span v-if="shape.showLabel">{{ shape.label }}</span>
            </button>
          </div>
        </div>

        <!-- Background Color Popup -->
        <div v-if="activePopup === 'bgColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select background</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="popup-grid">
            <button
              v-for="color in backgroundColorOptions"
              :key="color.hex"
              type="button"
              class="popup-grid-item"
              :class="{ selected: isBackgroundColorSelected(color.hex) }"
              :title="color.name"
              @click="selectBackgroundColor(color.hex)"
            >
              <div class="popup-item-preview" v-html="getBackgroundColorPopupPreview(color.hex)"></div>
              <span v-if="color.showLabel">{{ color.name }}</span>
            </button>
          </div>
        </div>

        <!-- Border Color Popup -->
        <div v-if="activePopup === 'borderColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select border</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="popup-grid">
            <button
              v-for="color in borderColorOptions"
              :key="color.hex"
              type="button"
              class="popup-grid-item"
              :class="{ selected: localBorderColor === color.hex }"
              :title="color.name"
              @click="selectBorderColor(color.hex)"
            >
              <div class="popup-item-preview" v-html="getBorderColorPopupPreview(color.hex)"></div>
              <span v-if="color.showLabel">{{ color.name }}</span>
            </button>
          </div>
        </div>

        <!-- Icon Popup -->
        <div v-if="activePopup === 'icon'" class="centered-popup icon-popup">
          <div class="popup-header">
            <span>Select icon</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="popup-grid">
            <button
              v-for="iconType in availableContentIcons"
              :key="iconType.value"
              type="button"
              class="popup-grid-item"
              :class="{ selected: localIconType === iconType.value }"
              @click="selectIcon(iconType.value)"
            >
              <div class="popup-item-preview" v-html="getIconPopupPreview(iconType.value)"></div>
              <span v-if="iconType.showLabel">{{ iconType.label }}</span>
            </button>
          </div>
        </div>

        <!-- Icon Color Popup -->
        <div v-if="activePopup === 'iconColor'" class="centered-popup">
          <div class="popup-header">
            <span>Select colour</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="popup-grid">
            <button
              v-for="color in iconColorOptions"
              :key="color.hex"
              type="button"
              class="popup-grid-item"
              :class="{ selected: isIconColorSelected(color.hex) }"
              :title="color.name"
              @click="selectIconColor(color.hex)"
            >
              <div class="popup-item-preview" v-html="getIconColorPopupPreview(color.hex)"></div>
              <span v-if="color.showLabel">{{ color.name }}</span>
            </button>
          </div>
        </div>

        <!-- Size Popup -->
        <div v-if="activePopup === 'size'" class="centered-popup">
          <div class="popup-header">
            <span>Select size</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="size-grid">
            <button
              v-for="sizeOption in availableSizes"
              :key="sizeOption.value"
              type="button"
              class="size-grid-item"
              :class="{ selected: localSize === sizeOption.value }"
              @click="selectSize(sizeOption.value)"
            >
              <div class="size-preview" v-html="getSizePopupPreview(sizeOption.value)"></div>
              <span>{{ sizeOption.label }}</span>
            </button>
          </div>
        </div>

        <!-- Map Rotation Popup -->
        <div v-if="activePopup === 'mapRotation'" class="centered-popup">
          <div class="popup-header">
            <span>Set map rotation</span>
            <button type="button" class="popup-close" @click="closePopups">&times;</button>
          </div>
          <div class="rotation-content">
            <!-- Rotation Preview -->
            <div class="rotation-preview-container">
              <div class="rotation-preview-icon" :style="{ transform: `rotate(${getEffectiveMapRotationValue(localMapRotation)}deg)` }" v-html="previewSvg"></div>
            </div>
            <p class="rotation-description">
              Rotate the marker on the map. This rotation is added to any map rotation.
            </p>
            <div class="rotation-control">
              <button
                type="button"
                class="rotation-preset"
                :class="{ selected: localMapRotation === 'default' || localMapRotation === '' }"
                @click="selectMapRotation('default')"
              >
                Default
              </button>
              <div class="rotation-slider-container">
                <input
                  type="range"
                  min="-180"
                  max="180"
                  step="5"
                  class="rotation-slider"
                  :value="localMapRotation === 'default' || localMapRotation === '' ? 0 : localMapRotation"
                  @input="updateMapRotationFromSlider(parseInt($event.target.value, 10))"
                />
                <div class="rotation-value">
                  {{ localMapRotation === 'default' || localMapRotation === '' ? '0' : localMapRotation }}°
                </div>
              </div>
            </div>
            <div class="rotation-presets">
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === -90 }" @click="selectMapRotation(-90)">-90°</button>
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === -45 }" @click="selectMapRotation(-45)">-45°</button>
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === 0 }" @click="selectMapRotation(0)">0°</button>
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === 45 }" @click="selectMapRotation(45)">+45°</button>
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === 90 }" @click="selectMapRotation(90)">+90°</button>
              <button type="button" class="rotation-preset-btn" :class="{ selected: localMapRotation === 180 }" @click="selectMapRotation(180)">180°</button>
            </div>
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
  mapRotation: { type: [String, Number], default: '' },
  level: { type: String, default: 'checkpoint' },
  // Inherited values from parent (area/event) - used as defaults
  inheritedType: { type: String, default: '' },
  inheritedBackgroundShape: { type: String, default: '' },
  inheritedBackgroundColor: { type: String, default: '' },
  inheritedBorderColor: { type: String, default: '' },
  inheritedIconColor: { type: String, default: '' },
  inheritedSize: { type: String, default: '' },
  inheritedMapRotation: { type: [String, Number], default: '' },
});

const emit = defineEmits(['update:isOpen', 'apply', 'cancel']);

// Local state for editing
const localBackgroundShape = ref(props.backgroundShape || 'circle');
const localIconType = ref(props.iconType || 'none');
const localBackgroundColor = ref(props.backgroundColor || '');
const localBorderColor = ref(props.borderColor || '');
const localIconColor = ref(props.iconColor || '');
const localSize = ref(props.size || '100');
const localMapRotation = ref(props.mapRotation ?? '');

// Popup state
const activePopup = ref(null);

// Helper to get the effective/resolved value for each property
const getEffectiveShape = (shapeValue) => {
  if (shapeValue === 'default') {
    return props.inheritedBackgroundShape || 'circle';
  }
  if (shapeValue === 'none') return 'none';
  return shapeValue || 'circle';
};

const getEffectiveBackgroundColorValue = (colorValue) => {
  if (colorValue === 'default' || !colorValue) {
    return getDefaultColor();
  }
  if (colorValue === 'none') return 'transparent';
  return colorValue;
};

const getEffectiveBorderColorValue = (colorValue) => {
  if (colorValue === 'default') {
    return props.inheritedBorderColor || '#ffffff';
  }
  if (colorValue === 'none' || !colorValue) return 'transparent';
  return colorValue;
};

const getEffectiveIconType = (iconValue) => {
  if (iconValue === 'default') {
    // Get inherited icon type (content icons only)
    const inheritedType = props.inheritedType;
    if (inheritedType && inheritedType !== 'default') {
      const config = getIconTypeConfig(inheritedType);
      if (config.category === 'content') return inheritedType;
    }
    return 'none';
  }
  if (iconValue === 'none' || !iconValue) return 'none';
  return iconValue;
};

const getEffectiveIconColorValue = (colorValue) => {
  if (colorValue === 'default' || !colorValue) {
    return props.inheritedIconColor || '#ffffff';
  }
  if (colorValue === 'none') return 'transparent';
  return colorValue;
};

const getEffectiveSizeValue = (sizeValue) => {
  if (sizeValue === 'default') {
    return props.inheritedSize || '100';
  }
  return sizeValue || '100';
};

const getEffectiveMapRotationValue = (rotationValue) => {
  if (rotationValue === 'default' || rotationValue === '') {
    const inherited = props.inheritedMapRotation;
    if (inherited !== '' && inherited !== undefined && inherited !== null) {
      return parseInt(inherited, 10) || 0;
    }
    return 0;
  }
  return parseInt(rotationValue, 10) || 0;
};

// Available shapes: None, Default, then all shapes (excluding 'none' from BACKGROUND_SHAPES to avoid duplicate)
const availableShapes = computed(() => {
  const inheritedShape = props.inheritedBackgroundShape || 'circle';
  const inheritedLabel = BACKGROUND_SHAPES.find(s => s.value === inheritedShape)?.label || 'Circle';
  return [
    { value: 'none', label: 'None', showLabel: true },
    { value: 'default', label: `Default (${inheritedLabel})`, showLabel: true },
    ...BACKGROUND_SHAPES.filter(s => s.value !== 'none').map(s => ({ ...s, showLabel: false })),
  ];
});

// Available icons: None, Default (if inherited), then all content icons
const availableContentIcons = computed(() => {
  const contentIcons = CHECKPOINT_ICON_TYPES.filter(t => t.category === 'content');
  const options = [
    { value: 'none', label: 'None', category: 'content', showLabel: true },
  ];

  // Add Default option if there's an inherited content icon
  const inheritedType = props.inheritedType;
  if (inheritedType && inheritedType !== 'default') {
    const config = getIconTypeConfig(inheritedType);
    if (config.category === 'content') {
      options.push({ value: 'default', label: `Default (${config.label || inheritedType})`, category: 'content', showLabel: true });
    }
  }

  options.push(...contentIcons.map(i => ({ ...i, showLabel: false })));
  return options;
});

// Available sizes: Default, then all sizes (simplified labels without %)
const availableSizes = computed(() => {
  const sizeLabels = { '33': 'Small', '66': 'Medium', '100': 'Normal', '150': 'Large' };
  const inheritedSizeLabel = sizeLabels[props.inheritedSize] || 'Normal';
  return [
    { value: 'default', label: `Default (${inheritedSizeLabel})` },
    ...ICON_SIZES.map(s => ({ value: s.value, label: sizeLabels[s.value] || s.label })),
  ];
});

// Check if shape is set (resolved, not 'none')
const hasShape = computed(() => {
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  return effectiveShape !== 'none';
});

// Check if icon is set (resolved, not 'none')
const hasIcon = computed(() => {
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  return effectiveIcon !== 'none';
});

// System default color when nothing is set in the hierarchy
const SYSTEM_DEFAULT_BACKGROUND_COLOR = '#667eea';

// Get default background color using cascade: system → inherited (event/area)
// Using computed for proper reactivity tracking
const defaultInheritedColor = computed(() => {
  // props.inheritedBackgroundColor is already combined (inheritedBackgroundColor || inheritedStyleColor) from parent
  const inherited = props.inheritedBackgroundColor;

  // Use inherited if it's a valid hex color (not empty, not 'default')
  if (inherited && inherited !== 'default' && inherited.startsWith('#')) {
    return inherited;
  }

  // Fall back to system default
  return SYSTEM_DEFAULT_BACKGROUND_COLOR;
});

const getDefaultColor = () => defaultInheritedColor.value;

// Effective colors (with inheritance fallback)
const effectiveBackgroundColor = computed(() => {
  if (localBackgroundColor.value && localBackgroundColor.value !== 'default') {
    return localBackgroundColor.value;
  }
  return getDefaultColor();
});

const effectiveBorderColor = computed(() => {
  if (localBorderColor.value === 'none') return 'transparent';
  if (localBorderColor.value) return localBorderColor.value;
  // Fall back to inherited border color
  if (props.inheritedBorderColor && props.inheritedBorderColor !== 'none') {
    return props.inheritedBorderColor;
  }
  return '#ffffff';
});

const effectiveIconColor = computed(() => {
  if (localIconColor.value) return localIconColor.value;
  // Fall back to inherited icon color
  if (props.inheritedIconColor) {
    return props.inheritedIconColor;
  }
  return '#ffffff';
});

// Color options - None, Default, then colors (only show names for None/Default)
const backgroundColorOptions = computed(() => {
  const defaultColor = defaultInheritedColor.value;
  const defaultLabel = AREA_COLORS.find(c => c.hex.toLowerCase() === defaultColor.toLowerCase())?.name || 'Custom';
  return [
    { hex: 'none', name: 'None', showLabel: true },
    { hex: 'default', name: `Default (${defaultLabel})`, showLabel: true },
    ...AREA_COLORS.map(c => ({ hex: c.hex, name: c.name, showLabel: false })),
  ];
});

const borderColorOptions = computed(() => {
  const defaultColor = props.inheritedBorderColor || '#ffffff';
  const defaultLabel = defaultColor === '#ffffff' ? 'White'
    : (AREA_COLORS.find(c => c.hex === defaultColor)?.name || 'White');
  return [
    { hex: 'none', name: 'None', showLabel: true },
    { hex: 'default', name: `Default (${defaultLabel})`, showLabel: true },
    { hex: '#ffffff', name: 'White', showLabel: false },
    { hex: '#000000', name: 'Black', showLabel: false },
    ...AREA_COLORS.filter(c => c.hex !== '#ffffff').map(c => ({ hex: c.hex, name: c.name, showLabel: false })),
  ];
});

const iconColorOptions = computed(() => {
  const defaultColor = props.inheritedIconColor || '#ffffff';
  const defaultLabel = defaultColor === '#ffffff' ? 'White'
    : (AREA_COLORS.find(c => c.hex === defaultColor)?.name || 'White');
  return [
    { hex: 'none', name: 'None', showLabel: true },
    { hex: 'default', name: `Default (${defaultLabel})`, showLabel: true },
    { hex: '#ffffff', name: 'White', showLabel: false },
    { hex: '#000000', name: 'Black', showLabel: false },
    ...AREA_COLORS.filter(c => c.hex !== '#ffffff').map(c => ({ hex: c.hex, name: c.name, showLabel: false })),
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
  if (value === 'none') return 'None';
  if (value === 'default') {
    const inherited = props.inheritedBackgroundShape;
    if (inherited) {
      const shape = BACKGROUND_SHAPES.find(s => s.value === inherited);
      return `Default (${shape?.label || 'Circle'})`;
    }
    return 'Default (Circle)';
  }
  const shape = BACKGROUND_SHAPES.find(s => s.value === value);
  return shape ? shape.label : 'Circle';
};

const getIconLabel = (value) => {
  if (value === 'none') return 'None';
  if (value === 'default') {
    const inheritedType = props.inheritedType;
    if (inheritedType && inheritedType !== 'default') {
      const config = getIconTypeConfig(inheritedType);
      if (config.category === 'content') {
        return `Default (${config.label || inheritedType})`;
      }
    }
    return 'Default';
  }
  const icon = CHECKPOINT_ICON_TYPES.find(t => t.value === value);
  return icon ? icon.label : 'None';
};

const getColorLabel = (value) => {
  if (value === 'none') return 'None';
  if (!value || value === 'default') {
    const inherited = defaultInheritedColor.value;
    const color = AREA_COLORS.find(c => c.hex.toLowerCase() === inherited.toLowerCase());
    return `Default (${color?.name || 'Custom'})`;
  }
  const color = [...AREA_COLORS].find(c => c.hex.toLowerCase() === value.toLowerCase());
  return color ? color.name : value;
};

const getBorderColorLabel = (value) => {
  if (value === 'none') return 'None';
  if (value === 'default') {
    const inherited = props.inheritedBorderColor;
    if (inherited && inherited !== 'none') {
      const color = inherited === '#ffffff' ? 'White' : (AREA_COLORS.find(c => c.hex === inherited)?.name || 'Custom');
      return `Default (${color})`;
    }
    return 'Default (White)';
  }
  if (!value || value === '#ffffff') return 'White';
  const color = [...borderColorOptions.value].find(c => c.hex === value);
  return color ? color.name : value;
};

const getIconColorLabel = (value) => {
  if (value === 'none') return 'None';
  if (value === 'default') {
    const inherited = props.inheritedIconColor;
    if (inherited) {
      const color = inherited === '#ffffff' ? 'White' : (AREA_COLORS.find(c => c.hex === inherited)?.name || 'Custom');
      return `Default (${color})`;
    }
    return 'Default (White)';
  }
  if (!value || value === '#ffffff') return 'White';
  const color = [...iconColorOptions.value].find(c => c.hex === value);
  return color ? color.name : value;
};

const getSizeLabel = (value) => {
  const sizeLabels = { '33': 'Small', '66': 'Medium', '100': 'Normal', '150': 'Large' };
  if (value === 'default') {
    const inherited = props.inheritedSize;
    const inheritedLabel = sizeLabels[inherited] || 'Normal';
    return `Default (${inheritedLabel})`;
  }
  return sizeLabels[value] || 'Normal';
};

const getMapRotationLabel = (value) => {
  if (value === 'default' || value === '' || value === undefined || value === null) {
    const inherited = props.inheritedMapRotation;
    if (inherited !== '' && inherited !== undefined && inherited !== null) {
      return `Default (${inherited}°)`;
    }
    return 'Default (0°)';
  }
  return `${value}°`;
};

// ===========================================
// BUTTON PREVIEW GENERATORS (show just the element)
// ===========================================

// Shape button preview - shows just the shape with current background color
const getShapeButtonPreview = (shapeValue) => {
  if (shapeValue === 'none') {
    return `<svg width="20" height="20" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="16" height="16" rx="2" fill="none" stroke="#ccc" stroke-width="1.5" stroke-dasharray="3"/>
    </svg>`;
  }

  const effectiveShape = getEffectiveShape(shapeValue);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);

  if (backgroundShapeGenerators[effectiveShape]) {
    const svgContent = backgroundShapeGenerators[effectiveShape]({
      backgroundColor: bgColor,
      borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    });
    return `<svg width="20" height="20" viewBox="0 0 32 32" xmlns="http://www.w3.org/2000/svg">${svgContent}</svg>`;
  }
  return '';
};

// Icon button preview - shows just the icon symbol
const getIconButtonPreview = (iconTypeValue) => {
  const effectiveIcon = getEffectiveIconType(iconTypeValue);
  if (effectiveIcon === 'none') {
    return `<svg width="18" height="18" viewBox="0 0 18 18" xmlns="http://www.w3.org/2000/svg">
      <rect x="1" y="1" width="16" height="16" rx="2" fill="none" stroke="#ccc" stroke-width="1.5" stroke-dasharray="3"/>
    </svg>`;
  }

  // Show icon on neutral background
  return generateCheckpointSvg({
    type: effectiveIcon,
    backgroundShape: 'circle',
    backgroundColor: '#94a3b8',
    borderColor: 'none',
    iconColor: '#fff',
    size: '75',
  });
};

// ===========================================
// POPUP PREVIEW GENERATORS (show complete icon with one property changed)
// ===========================================

// Generate complete icon preview with a specific shape
const getShapePopupPreview = (shapeValue) => {
  const effectiveShape = getEffectiveShape(shapeValue);
  if (effectiveShape === 'none') {
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="none" stroke="#ccc" stroke-width="2" stroke-dasharray="4"/>
    </svg>`;
  }

  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);

  const iconType = effectiveIcon !== 'none' ? effectiveIcon : effectiveShape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: effectiveShape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: '100',
    outputSize: 28,
  });
};

// Generate complete icon preview with a specific background color
const getBackgroundColorPopupPreview = (colorValue) => {
  const bgColor = getEffectiveBackgroundColorValue(colorValue);
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);

  if (effectiveShape === 'none') {
    // Just show a color swatch
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="${bgColor === 'transparent' ? '#e2e8f0' : bgColor}"/>
    </svg>`;
  }

  const iconType = effectiveIcon !== 'none' ? effectiveIcon : effectiveShape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: effectiveShape,
    backgroundColor: bgColor === 'transparent' ? '#e2e8f0' : bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: '100',
    outputSize: 28,
  });
};

// Generate complete icon preview with a specific border color
const getBorderColorPopupPreview = (colorValue) => {
  const borderClr = getEffectiveBorderColorValue(colorValue);
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);

  if (effectiveShape === 'none') {
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="#e2e8f0" stroke="${borderClr}" stroke-width="2"/>
    </svg>`;
  }

  const iconType = effectiveIcon !== 'none' ? effectiveIcon : effectiveShape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: effectiveShape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: '100',
    outputSize: 28,
  });
};

// Generate complete icon preview with a specific icon
const getIconPopupPreview = (iconTypeValue) => {
  const effectiveIcon = getEffectiveIconType(iconTypeValue);
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);

  if (effectiveIcon === 'none' && effectiveShape === 'none') {
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="#e2e8f0"/>
      <line x1="8" y1="8" x2="20" y2="20" stroke="#a0aec0" stroke-width="2" stroke-linecap="round"/>
    </svg>`;
  }

  const shape = effectiveShape !== 'none' ? effectiveShape : 'circle';
  const iconType = effectiveIcon !== 'none' ? effectiveIcon : shape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: shape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: '100',
    outputSize: 42,
  });
};

// Generate complete icon preview with a specific icon color
const getIconColorPopupPreview = (colorValue) => {
  const iconClr = getEffectiveIconColorValue(colorValue);
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);

  if (effectiveIcon === 'none') {
    // Just show a color swatch
    return `<svg width="28" height="28" viewBox="0 0 28 28" xmlns="http://www.w3.org/2000/svg">
      <rect x="2" y="2" width="24" height="24" rx="4" fill="${iconClr === 'transparent' ? '#e2e8f0' : iconClr}"/>
    </svg>`;
  }

  const shape = effectiveShape !== 'none' ? effectiveShape : 'circle';

  return generateCheckpointSvg({
    type: effectiveIcon,
    backgroundShape: shape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr === 'transparent' ? '#e2e8f0' : iconClr,
    size: '100',
    outputSize: 28,
  });
};

// Generate complete icon preview at a specific size
const getSizePopupPreview = (sizeValue) => {
  const sizeVal = getEffectiveSizeValue(sizeValue);
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);

  if (effectiveShape === 'none' && effectiveIcon === 'none') {
    return '';
  }

  const shape = effectiveShape !== 'none' ? effectiveShape : 'circle';
  const iconType = effectiveIcon !== 'none' ? effectiveIcon : shape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: shape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: sizeVal,
  });
};

// ===========================================
// MAIN PREVIEW SVG - shows current complete icon
// ===========================================
const previewSvg = computed(() => {
  const effectiveShape = getEffectiveShape(localBackgroundShape.value);
  const bgColor = getEffectiveBackgroundColorValue(localBackgroundColor.value);
  const borderClr = getEffectiveBorderColorValue(localBorderColor.value);
  const effectiveIcon = getEffectiveIconType(localIconType.value);
  const iconClr = getEffectiveIconColorValue(localIconColor.value);
  const sizeVal = getEffectiveSizeValue(localSize.value);

  // If both shape and icon are 'none', nothing to show
  if (effectiveShape === 'none' && effectiveIcon === 'none') {
    return '';
  }

  // If shape is 'none' but we have an icon, render icon without background
  if (effectiveShape === 'none' && effectiveIcon !== 'none') {
    return generateCheckpointSvg({
      type: effectiveIcon,
      backgroundShape: 'none',
      backgroundColor: bgColor,
      borderColor: 'none',
      iconColor: iconClr,
      size: sizeVal,
    });
  }

  // Normal case: shape with or without icon
  const iconType = effectiveIcon !== 'none' ? effectiveIcon : effectiveShape;

  return generateCheckpointSvg({
    type: iconType,
    backgroundShape: effectiveShape,
    backgroundColor: bgColor,
    borderColor: borderClr === 'transparent' ? 'none' : borderClr,
    iconColor: iconClr,
    size: sizeVal,
  });
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
  localBackgroundColor.value = value;
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

const selectMapRotation = (value) => {
  localMapRotation.value = value;
  closePopups();
};

// Separate handler for slider - updates value without closing popup
const updateMapRotationFromSlider = (value) => {
  localMapRotation.value = value;
};

// Sync local state when props change
watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    // Check if ANY custom value is set (not empty)
    const hasCustomShape = props.backgroundShape && props.backgroundShape !== 'default' && props.backgroundShape !== 'circle';
    const hasCustomIcon = props.iconType && props.iconType !== 'none' && props.iconType !== 'default';
    const hasCustomBgColor = props.backgroundColor && props.backgroundColor !== '';
    const hasCustomBorderColor = props.borderColor && props.borderColor !== '';
    const hasCustomIconColor = props.iconColor && props.iconColor !== '';
    const hasCustomSize = props.size && props.size !== '' && props.size !== '100';
    const hasCustomMapRotation = props.mapRotation !== '' && props.mapRotation !== undefined && props.mapRotation !== null && props.mapRotation !== 0;

    const hasAnyCustomValue = hasCustomShape || hasCustomIcon || hasCustomBgColor
      || hasCustomBorderColor || hasCustomIconColor || hasCustomSize || hasCustomMapRotation;

    if (!hasAnyCustomValue) {
      // No custom values set - initialize all to 'default' for inheritance
      localBackgroundShape.value = 'default';
      localIconType.value = 'default';
      localBackgroundColor.value = 'default';
      localBorderColor.value = 'default';
      localIconColor.value = 'default';
      localSize.value = 'default';
      localMapRotation.value = 'default';
    } else {
      // Load saved values - map empty strings to 'default' for display
      localBackgroundShape.value = props.backgroundShape || 'default';
      localIconType.value = props.iconType || 'default';
      // For colors, empty string means 'default' (inherit)
      localBackgroundColor.value = props.backgroundColor || 'default';
      localBorderColor.value = props.borderColor || 'default';
      localIconColor.value = props.iconColor || 'default';
      localSize.value = props.size || 'default';
      localMapRotation.value = (props.mapRotation !== '' && props.mapRotation !== undefined && props.mapRotation !== null) ? props.mapRotation : 'default';
    }
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

  // Convert 'default' to empty string (meaning "use inherited value")
  // Keep 'none' as-is to indicate explicit no value
  const resolveValue = (value) => {
    if (value === 'default') return '';
    return value;
  };

  // Each property is independent - emit them separately
  // iconType is ONLY for content icons (water, food, etc.), not shapes
  // 'none' = explicitly no icon (override parent)
  // '' (empty) = inherit from parent
  // specific value = that content icon
  let iconTypeValue = '';
  if (localIconType.value === 'none') {
    iconTypeValue = 'none'; // Explicitly no icon
  } else if (localIconType.value && localIconType.value !== 'default') {
    iconTypeValue = localIconType.value; // Specific content icon
  }
  // else: 'default' or empty = inherit (emit empty string)

  // For mapRotation: 'default' means inherit, otherwise emit the numeric value
  let mapRotationValue = '';
  if (localMapRotation.value !== 'default' && localMapRotation.value !== '' && localMapRotation.value !== undefined) {
    mapRotationValue = parseInt(localMapRotation.value, 10) || 0;
  }

  emit('apply', {
    iconType: iconTypeValue,
    backgroundShape: resolveValue(localBackgroundShape.value),
    backgroundColor: resolveValue(localBackgroundColor.value),
    borderColor: resolveValue(localBorderColor.value),
    iconColor: resolveValue(localIconColor.value),
    size: resolveValue(localSize.value),
    mapRotation: mapRotationValue,
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
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: var(--card-bg);
  border-radius: 12px;
  width: 90%;
  max-width: 400px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-lg);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.25rem;
  border-bottom: 1px solid var(--border-color);
}

.modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-primary);
  font-weight: 600;
}

.close-btn {
  background: none;
  border: none;
  font-size: 1.5rem;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.close-btn:hover {
  color: var(--text-primary);
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
  /* Base map color - light tan/beige like streets */
  background-color: #f5f3ef;
  background-image:
    /* Main roads - white/light gray */
    linear-gradient(90deg, transparent 0%, transparent 45%, #fff 45%, #fff 55%, transparent 55%, transparent 100%),
    linear-gradient(0deg, transparent 0%, transparent 42%, #fff 42%, #fff 58%, transparent 58%, transparent 100%),
    /* Secondary roads */
    linear-gradient(90deg, transparent 0%, transparent 20%, #fafafa 20%, #fafafa 23%, transparent 23%, transparent 77%, #fafafa 77%, #fafafa 80%, transparent 80%, transparent 100%),
    linear-gradient(0deg, transparent 0%, transparent 18%, #fafafa 18%, #fafafa 21%, transparent 21%, transparent 79%, #fafafa 79%, #fafafa 82%, transparent 82%, transparent 100%),
    /* Parks/green areas */
    radial-gradient(ellipse 60px 40px at 15% 30%, #d4edda 0%, #d4edda 60%, transparent 60%),
    radial-gradient(ellipse 50px 35px at 85% 65%, #d4edda 0%, #d4edda 60%, transparent 60%),
    /* Water feature */
    radial-gradient(ellipse 40px 25px at 70% 25%, #cce5ff 0%, #cce5ff 50%, transparent 50%),
    /* Building blocks */
    radial-gradient(ellipse 25px 20px at 30% 70%, #e9e9e9 0%, #e9e9e9 80%, transparent 80%),
    radial-gradient(ellipse 20px 15px at 55% 80%, #e9e9e9 0%, #e9e9e9 80%, transparent 80%);
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
  border: 1px solid var(--border-color);
  border-radius: 8px;
  background: var(--card-bg);
  cursor: pointer;
  transition: all 0.15s;
  min-height: 48px;
}

.option-button:hover:not(.disabled) {
  border-color: var(--border-color);
  background: var(--bg-tertiary);
}

.option-button.active {
  border-color: var(--accent-primary);
  background: var(--bg-active);
}

.option-button.disabled {
  opacity: 0.5;
  cursor: not-allowed;
  background: var(--bg-tertiary);
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
  color: var(--text-secondary);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.025em;
}

.option-button-value {
  font-size: 0.8rem;
  color: var(--text-primary);
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
  border: 1px solid var(--border-color);
  flex-shrink: 0;
}

.color-swatch.no-border {
  background: repeating-linear-gradient(
    45deg,
    var(--bg-tertiary),
    var(--bg-tertiary) 2px,
    var(--card-bg) 2px,
    var(--card-bg) 4px
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
  color: var(--text-secondary);
  margin-left: 0.25rem;
}

/* Centered Popup Panels - Overlay */
.centered-popup {
  position: absolute;
  top: 0.5rem;
  left: 0.5rem;
  right: 0.5rem;
  bottom: 0.5rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  box-shadow: var(--shadow-lg);
  z-index: 20;
  display: flex;
  flex-direction: column;
}

.popup-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  border-bottom: 1px solid var(--border-color);
  font-size: 0.8rem;
  font-weight: 500;
  color: var(--text-secondary);
}

.popup-close {
  background: none;
  border: none;
  font-size: 1.25rem;
  color: var(--text-secondary);
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.popup-close:hover {
  color: var(--text-primary);
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
  background: var(--card-bg);
  cursor: pointer;
  transition: all 0.15s;
}

.popup-grid-item:hover {
  background: var(--bg-tertiary);
}

.popup-grid-item.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
}

.popup-grid-item span {
  font-size: 0.65rem;
  color: var(--text-secondary);
  text-align: center;
}

.popup-item-preview {
  width: 36px;
  height: 36px;
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
  border: 2px solid var(--border-color);
  cursor: pointer;
  transition: all 0.15s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.color-option:hover {
  transform: scale(1.1);
  border-color: var(--border-color);
}

.color-option.selected {
  border-color: var(--accent-primary);
  box-shadow: 0 0 0 2px var(--bg-active);
}

.color-option.none-option {
  background: var(--card-bg);
}

.none-icon {
  width: 16px;
  height: 16px;
  border: 2px dashed var(--border-color);
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
  color: var(--text-secondary);
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
  background: var(--card-bg);
  cursor: pointer;
  transition: all 0.15s;
}

.icon-grid-item:hover {
  background: var(--bg-tertiary);
}

.icon-grid-item.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
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
  color: var(--text-secondary);
  text-align: center;
  line-height: 1.1;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Size Grid */
.size-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.75rem;
  padding: 0.75rem;
  flex: 1;
  align-content: start;
  overflow-y: auto;
}

.size-grid-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border: 2px solid transparent;
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  transition: all 0.15s;
}

.size-grid-item:hover {
  background: var(--bg-tertiary);
}

.size-grid-item.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
}

.size-grid-item span {
  font-size: 0.7rem;
  color: var(--text-secondary);
  text-align: center;
}

.size-preview {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 36px;
}

/* Footer */
.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.25rem;
  border-top: 1px solid var(--border-color);
}

.btn-cancel {
  padding: 0.5rem 1rem;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  background: var(--card-bg);
  color: var(--text-secondary);
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
}

.btn-cancel:hover {
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.btn-apply {
  padding: 0.5rem 1.5rem;
  border: none;
  border-radius: 6px;
  background: var(--accent-primary);
  color: var(--bg-primary);
  font-weight: 500;
  cursor: pointer;
  transition: all 0.15s;
}

.btn-apply:hover {
  opacity: 0.9;
}

/* Full-width button for map rotation */
.option-button-full {
  grid-column: span 2;
}

/* Rotation Controls */
.rotation-content {
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.rotation-preview-container {
  display: flex;
  justify-content: center;
  align-items: center;
  padding: 0.5rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
  min-height: 60px;
}

.rotation-preview-icon {
  transition: transform 0.15s ease-out;
  filter: drop-shadow(0 2px 4px var(--shadow-color));
}

.rotation-description {
  margin: 0;
  font-size: 0.8rem;
  color: var(--text-secondary);
  text-align: center;
}

.rotation-control {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.rotation-preset {
  padding: 0.5rem 1rem;
  border: 2px solid var(--border-color);
  border-radius: 6px;
  background: var(--card-bg);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  color: var(--text-secondary);
  transition: all 0.15s;
  white-space: nowrap;
}

.rotation-preset:hover {
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.rotation-preset.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
  color: var(--accent-primary);
}

.rotation-slider-container {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.rotation-slider {
  flex: 1;
  height: 6px;
  -webkit-appearance: none;
  appearance: none;
  background: linear-gradient(to right, var(--border-color) 0%, var(--accent-primary) 50%, var(--border-color) 100%);
  border-radius: 3px;
  cursor: pointer;
}

.rotation-slider::-webkit-slider-thumb {
  -webkit-appearance: none;
  appearance: none;
  width: 18px;
  height: 18px;
  background: var(--accent-primary);
  border-radius: 50%;
  cursor: pointer;
  border: 2px solid var(--card-bg);
  box-shadow: var(--shadow-md);
}

.rotation-slider::-moz-range-thumb {
  width: 18px;
  height: 18px;
  background: var(--accent-primary);
  border-radius: 50%;
  cursor: pointer;
  border: 2px solid var(--card-bg);
  box-shadow: var(--shadow-md);
}

.rotation-value {
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
  min-width: 45px;
  text-align: right;
}

.rotation-presets {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  justify-content: center;
}

.rotation-preset-btn {
  padding: 0.375rem 0.75rem;
  border: 1px solid var(--border-color);
  border-radius: 4px;
  background: var(--card-bg);
  cursor: pointer;
  font-size: 0.75rem;
  color: var(--text-secondary);
  transition: all 0.15s;
}

.rotation-preset-btn:hover {
  background: var(--bg-tertiary);
  border-color: var(--border-color);
}

.rotation-preset-btn.selected {
  border-color: var(--accent-primary);
  background: var(--bg-active);
  color: var(--accent-primary);
}
</style>
