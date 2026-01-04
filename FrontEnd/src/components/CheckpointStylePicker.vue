<template>
  <div class="checkpoint-style-picker">
    <div class="form-group">
      <label>{{ iconLabel }}</label>
      <div class="icon-type-grid">
        <button
          v-for="iconType in CHECKPOINT_ICON_TYPES"
          :key="iconType.value"
          type="button"
          class="icon-type-button"
          :class="{ selected: localStyleType === iconType.value }"
          :title="getIconLabel(iconType)"
          @click="selectType(iconType.value)"
        >
          <div class="icon-preview" v-html="getPreviewSvg(iconType)"></div>
          <span class="icon-label">{{ getIconLabel(iconType) }}</span>
        </button>
      </div>
    </div>

    <div v-if="showColorPicker" class="form-group">
      <label>{{ colorLabel }}</label>
      <div class="color-picker">
        <button
          v-for="colorOption in AREA_COLORS"
          :key="colorOption.hex"
          type="button"
          class="color-swatch-button"
          :class="{ selected: localStyleColor === colorOption.hex }"
          :title="colorOption.name"
          @click="selectColor(colorOption.hex)"
        >
          <div class="swatch-shape" v-html="getColorSwatchSvg(colorOption.hex)"></div>
        </button>
      </div>
    </div>

    <div v-if="showPreview && localStyleType !== 'default'" class="live-preview">
      <label>Preview:</label>
      <div class="preview-container" v-html="previewHtml"></div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { AREA_COLORS } from '../constants/areaColors';
import {
  CHECKPOINT_ICON_TYPES,
  isColorizable,
  getIconColor,
  shapeSvgGenerators,
  fixedIconSvgs,
  getCheckpointMarkerHtml,
} from '../constants/checkpointIcons';

const props = defineProps({
  styleType: {
    type: String,
    default: 'default',
  },
  styleColor: {
    type: String,
    default: '',
  },
  iconLabel: {
    type: String,
    default: 'Icon style',
  },
  colorLabel: {
    type: String,
    default: 'Icon color',
  },
  showPreview: {
    type: Boolean,
    default: true,
  },
  // Inherited style from parent (area for checkpoints, event for areas)
  inheritedStyleType: {
    type: String,
    default: 'default',
  },
  inheritedStyleColor: {
    type: String,
    default: '',
  },
  // Custom label for the "Default" option
  defaultLabel: {
    type: String,
    default: 'Default',
  },
});

const emit = defineEmits(['update:styleType', 'update:styleColor', 'change']);

const localStyleType = ref(props.styleType);
const localStyleColor = ref(props.styleColor || '#667eea');

// Show color picker only for colorizable types
const showColorPicker = computed(() => {
  return isColorizable(localStyleType.value);
});

// Get label for an icon type (uses custom defaultLabel for 'default' type)
const getIconLabel = (iconType) => {
  if (iconType.value === 'default') {
    return props.defaultLabel;
  }
  return iconType.label;
};

// Generate SVG for color swatch using the selected shape
const getColorSwatchSvg = (colorHex) => {
  const size = 28;

  // Use the selected shape type for the color swatches
  if (shapeSvgGenerators[localStyleType.value]) {
    return shapeSvgGenerators[localStyleType.value](colorHex, size);
  }

  // Fallback to circle if shape not found
  return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
    <circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 - 2}" fill="${colorHex}" stroke="#fff" stroke-width="1.5"/>
  </svg>`;
};

// Generate preview SVG for each icon type button
const getPreviewSvg = (iconType) => {
  const size = 24;

  if (iconType.value === 'default') {
    // If there's an inherited style, show that as the default preview
    if (props.inheritedStyleType && props.inheritedStyleType !== 'default') {
      // Show the inherited style
      if (fixedIconSvgs[props.inheritedStyleType]) {
        return fixedIconSvgs[props.inheritedStyleType](size);
      }
      if (shapeSvgGenerators[props.inheritedStyleType]) {
        const color = props.inheritedStyleColor || '#667eea';
        return shapeSvgGenerators[props.inheritedStyleType](color, size);
      }
    }
    // Show a generic circle with gradient for truly default (no inheritance)
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="defaultGradient" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:#10B981"/>
          <stop offset="50%" style="stop-color:#F59E0B"/>
          <stop offset="100%" style="stop-color:#EF4444"/>
        </linearGradient>
      </defs>
      <circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 - 2}" fill="url(#defaultGradient)" stroke="#fff" stroke-width="1.5"/>
    </svg>`;
  }

  // Fixed icons
  if (fixedIconSvgs[iconType.value]) {
    return fixedIconSvgs[iconType.value](size);
  }

  // Colorizable shapes - use selected color or default
  if (shapeSvgGenerators[iconType.value]) {
    const color = localStyleType.value === iconType.value ? localStyleColor.value : '#667eea';
    return shapeSvgGenerators[iconType.value](color, size);
  }

  return '';
};

// Generate full preview with status badge
const previewHtml = computed(() => {
  if (localStyleType.value === 'default') {
    return '';
  }

  const color = getIconColor(localStyleType.value, localStyleColor.value);
  const html = getCheckpointMarkerHtml(localStyleType.value, color, 1, 2, 40);
  return html || '';
});

const selectType = (type) => {
  localStyleType.value = type;
  emit('update:styleType', type);
  emitChange();
};

const selectColor = (colorHex) => {
  localStyleColor.value = colorHex;
  emit('update:styleColor', colorHex);
  emitChange();
};

const emitChange = () => {
  emit('change', {
    styleType: localStyleType.value,
    styleColor: localStyleColor.value,
  });
};

// Sync local state with props
watch(() => props.styleType, (newVal) => {
  localStyleType.value = newVal;
});

watch(() => props.styleColor, (newVal) => {
  localStyleColor.value = newVal || '#667eea';
});
</script>

<style scoped>
.checkpoint-style-picker {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.form-group label {
  font-weight: 500;
  color: #333;
  font-size: 0.9rem;
}

.icon-type-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(70px, 1fr));
  gap: 0.5rem;
}

.icon-type-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem;
  border: 2px solid #e2e8f0;
  border-radius: 8px;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
}

.icon-type-button:hover {
  border-color: #cbd5e0;
  background: #f7fafc;
}

.icon-type-button.selected {
  border-color: #007bff;
  background: #e7f3ff;
}

.icon-preview {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-label {
  font-size: 0.7rem;
  color: #666;
  text-align: center;
  line-height: 1.2;
}

.icon-type-button.selected .icon-label {
  color: #007bff;
  font-weight: 500;
}

.color-picker {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.color-swatch-button {
  width: 36px;
  height: 36px;
  padding: 2px;
  border: 2px solid transparent;
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.color-swatch-button:hover {
  transform: scale(1.1);
  border-color: #cbd5e0;
}

.color-swatch-button.selected {
  border-color: #007bff;
  background: #e7f3ff;
}

.swatch-shape {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.live-preview {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1rem;
  background: #f8f9fa;
  border-radius: 8px;
  align-items: center;
}

.live-preview label {
  font-size: 0.85rem;
  color: #666;
}

.preview-container {
  display: flex;
  align-items: center;
  justify-content: center;
}
</style>
