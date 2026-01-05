<template>
  <div class="checkpoint-style-picker">
    <div class="form-group">
      <label>{{ iconLabel }}</label>
      <div class="style-options">
        <button
          type="button"
          class="style-option-button"
          :class="{ selected: !isCustom }"
          @click="selectDefault"
        >
          <div class="option-preview" v-html="defaultPreviewSvg"></div>
          <span class="option-label">{{ defaultLabel }}</span>
        </button>
        <button
          type="button"
          class="style-option-button"
          :class="{ selected: isCustom }"
          @click="openCustomModal"
        >
          <div class="option-preview" v-html="customPreviewSvg"></div>
          <span class="option-label">Custom</span>
        </button>
      </div>
    </div>

    <!-- Icon Customization Modal -->
    <IconCustomizationModal
      v-model:isOpen="isCustomizationModalOpen"
      :icon-type="localContentIcon"
      :background-shape="localBackgroundShape"
      :background-color="localBackgroundColor"
      :border-color="localBorderColor"
      :icon-color="localIconColor"
      :size="localSize"
      :level="level"
      :inherited-type="inheritedStyleType"
      :inherited-background-shape="inheritedBackgroundShape"
      :inherited-background-color="inheritedBackgroundColor || inheritedStyleColor"
      :inherited-border-color="inheritedBorderColor"
      :inherited-icon-color="inheritedIconColor"
      :inherited-size="inheritedSize"
      @apply="handleCustomizationApply"
      @cancel="handleCustomizationCancel"
    />
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import {
  getIconTypeConfig,
  generateCheckpointSvg,
} from '../constants/checkpointIcons';
import IconCustomizationModal from './IconCustomizationModal.vue';

const props = defineProps({
  styleType: { type: String, default: 'default' },
  styleColor: { type: String, default: '' },
  styleBackgroundShape: { type: String, default: '' },
  styleBackgroundColor: { type: String, default: '' },
  styleBorderColor: { type: String, default: '' },
  styleIconColor: { type: String, default: '' },
  styleSize: { type: String, default: '' },
  iconLabel: { type: String, default: 'Icon style' },
  showPreview: { type: Boolean, default: true },
  // Inherited style from parent (area for checkpoints, event for areas)
  inheritedStyleType: { type: String, default: 'default' },
  inheritedStyleColor: { type: String, default: '' },
  inheritedBackgroundShape: { type: String, default: '' },
  inheritedBackgroundColor: { type: String, default: '' },
  inheritedBorderColor: { type: String, default: '' },
  inheritedIconColor: { type: String, default: '' },
  inheritedSize: { type: String, default: '' },
  // Custom label for the "Default" option
  defaultLabel: { type: String, default: 'Default' },
  // Level: 'event', 'area', or 'checkpoint' - determines available icons
  level: { type: String, default: 'checkpoint' },
});

const emit = defineEmits([
  'update:styleType',
  'update:styleColor',
  'update:styleBackgroundShape',
  'update:styleBackgroundColor',
  'update:styleBorderColor',
  'update:styleIconColor',
  'update:styleSize',
  'change',
]);

// Local state
// Empty string means 'inherit from parent' - use 'default' for UI display
const localStyleType = ref(props.styleType);
const localStyleColor = ref(props.styleColor || '');
const localBackgroundShape = ref(props.styleBackgroundShape || 'default');
const localBackgroundColor = ref(props.styleBackgroundColor || '');
const localBorderColor = ref(props.styleBorderColor || '');
const localIconColor = ref(props.styleIconColor || '');
const localSize = ref(props.styleSize || 'default');

// Determine the content icon - if styleType is a content icon, use it; otherwise 'none'
const isContentIcon = (type) => {
  if (!type || type === 'default') return false;
  const config = getIconTypeConfig(type);
  return config.category === 'content';
};

// Only use styleType as content icon if it's actually a content icon (not 'default' or 'custom')
// Use 'default' (inherit from parent) when no specific icon is set, not 'none' (explicitly no icon)
const localContentIcon = ref(
  props.styleType && props.styleType !== 'default' && props.styleType !== 'custom' && isContentIcon(props.styleType)
    ? props.styleType
    : 'default'
);

// Modal state
const isCustomizationModalOpen = ref(false);

// Check if custom style is selected (anything other than 'default')
// This includes 'custom' (style without content icon) or any content icon value
const isCustom = computed(() => {
  if (localStyleType.value && localStyleType.value !== 'default') return true;
  // Also check if any individual property is customized
  if (localBackgroundShape.value && localBackgroundShape.value !== 'circle') return true;
  if (localBackgroundColor.value) return true;
  if (localBorderColor.value) return true;
  if (localIconColor.value) return true;
  if (localSize.value && localSize.value !== '100') return true;
  return false;
});

// System default color
const SYSTEM_DEFAULT_COLOR = '#667eea';

// Helper to check if a value is a valid inherited value (not empty, not 'default')
const isValidInheritedValue = (value) => {
  return value && value !== '' && value !== 'default';
};

// Helper to check if a value is a valid hex color
const isValidHexColor = (value) => {
  return value && value.startsWith('#');
};

// Generate preview SVG for the "Default" option - shows combined inherited properties
const defaultPreviewSvg = computed(() => {
  const size = 24;

  // Check if there are ANY inherited style properties (valid values that aren't 'default')
  const hasInheritedType = isValidInheritedValue(props.inheritedStyleType);
  const hasInheritedShape = isValidInheritedValue(props.inheritedBackgroundShape);
  const hasInheritedColor = isValidHexColor(props.inheritedBackgroundColor) || isValidHexColor(props.inheritedStyleColor);
  const hasInheritedBorder = isValidHexColor(props.inheritedBorderColor);
  const hasInheritedIconColor = isValidHexColor(props.inheritedIconColor);
  const hasInheritedSize = isValidInheritedValue(props.inheritedSize);

  const hasAnyInherited = hasInheritedType || hasInheritedShape || hasInheritedColor
    || hasInheritedBorder || hasInheritedIconColor || hasInheritedSize;

  if (hasAnyInherited) {
    // Resolve the effective type: inherited type, or inherited shape if no type
    const effectiveType = hasInheritedType
      ? props.inheritedStyleType
      : (hasInheritedShape ? props.inheritedBackgroundShape : 'circle');

    // Cascade: inherited background color → inherited style color → system default
    const bgColor = (isValidHexColor(props.inheritedBackgroundColor) ? props.inheritedBackgroundColor : null)
      || (isValidHexColor(props.inheritedStyleColor) ? props.inheritedStyleColor : null)
      || SYSTEM_DEFAULT_COLOR;

    return generateCheckpointSvg({
      type: effectiveType,
      backgroundShape: props.inheritedBackgroundShape || 'circle',
      backgroundColor: bgColor,
      borderColor: props.inheritedBorderColor || '#ffffff',
      iconColor: props.inheritedIconColor || '#ffffff',
      size: '75',
    });
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
});

// Generate preview SVG for the "Custom" option
const customPreviewSvg = computed(() => {
  if (isCustom.value) {
    // Determine what to render based on content icon vs shape-only
    // 'none' = explicitly no icon, 'default' = inherit from parent
    const hasLocalContentIcon = localContentIcon.value
      && localContentIcon.value !== 'none'
      && localContentIcon.value !== 'default';

    // Check if there's an inherited content icon we should show
    let inheritedContentIcon = null;
    if (props.inheritedStyleType && props.inheritedStyleType !== 'default' && props.inheritedStyleType !== 'custom') {
      const config = getIconTypeConfig(props.inheritedStyleType);
      if (config.category === 'content') {
        inheritedContentIcon = props.inheritedStyleType;
      }
    }

    // If local icon is 'default' (inherit), use inherited icon if available
    // If local icon is 'none' (explicitly no icon), don't show any icon
    const shouldInheritIcon = localContentIcon.value === 'default' && inheritedContentIcon;
    const hasContentIcon = hasLocalContentIcon || shouldInheritIcon;
    const contentIconToUse = hasLocalContentIcon ? localContentIcon.value : (shouldInheritIcon ? inheritedContentIcon : null);

    // Resolve effective shape (use inherited if 'default' or not set)
    const effectiveShape = localBackgroundShape.value && localBackgroundShape.value !== 'default'
      ? localBackgroundShape.value
      : (props.inheritedBackgroundShape || 'circle');

    const effectiveType = hasContentIcon ? contentIconToUse : effectiveShape;

    // Use saved background color, or inherited, or default
    // Background color is INDEPENDENT of icon - don't use icon's defaultColor
    const bgColor = localBackgroundColor.value
      || props.inheritedBackgroundColor
      || props.inheritedStyleColor
      || '#667eea';

    // Use saved border/icon colors, or inherited, or defaults
    const borderClr = localBorderColor.value || props.inheritedBorderColor || '#ffffff';
    const iconClr = localIconColor.value || props.inheritedIconColor || '#ffffff';

    return generateCheckpointSvg({
      type: effectiveType,
      backgroundShape: effectiveShape,
      backgroundColor: bgColor,
      borderColor: borderClr,
      iconColor: iconClr,
      size: '75',
    });
  }
  // Show a placeholder for custom (gear icon or similar)
  return `<svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
    <circle cx="12" cy="12" r="10" fill="#e2e8f0" stroke="#cbd5e0" stroke-width="1.5"/>
    <path d="M12 8v8M8 12h8" stroke="#94a3b8" stroke-width="2" stroke-linecap="round"/>
  </svg>`;
});

const selectDefault = () => {
  localStyleType.value = 'default';
  localContentIcon.value = 'default'; // Inherit from parent
  emit('update:styleType', 'default');
  // Clear customizations - use 'default' for UI display, emit '' for data
  localBackgroundShape.value = 'default';
  localBackgroundColor.value = '';
  localBorderColor.value = '';
  localIconColor.value = '';
  localSize.value = 'default';
  emit('update:styleBackgroundShape', '');
  emit('update:styleBackgroundColor', '');
  emit('update:styleBorderColor', '');
  emit('update:styleIconColor', '');
  emit('update:styleSize', '');
  emitChange();
};

const openCustomModal = () => {
  isCustomizationModalOpen.value = true;
};

const handleCustomizationApply = (customization) => {
  // Update local state - each property is independent
  // iconType: 'none' = explicitly no icon, '' = inherit from parent, other = specific icon
  if (customization.iconType === 'none') {
    localContentIcon.value = 'none'; // Explicitly no icon
  } else if (customization.iconType) {
    localContentIcon.value = customization.iconType; // Specific content icon
  } else {
    // Empty string = inherit from parent
    localContentIcon.value = 'default';
  }

  // Empty string = inherit from parent, use 'default' for UI display
  localBackgroundShape.value = customization.backgroundShape || 'default';
  localBackgroundColor.value = customization.backgroundColor || '';
  localBorderColor.value = customization.borderColor || '';
  localIconColor.value = customization.iconColor || '';
  localSize.value = customization.size || 'default';

  // Determine if we have a custom style (any non-default value)
  // Note: 'none' for iconType IS a custom value (explicitly no icon)
  const hasCustomStyle = customization.iconType === 'none'
    || (customization.iconType && customization.iconType !== '')
    || customization.backgroundShape
    || customization.backgroundColor
    || customization.borderColor
    || customization.iconColor
    || customization.size;

  // styleType: use content icon if set (not 'none'), otherwise 'custom' if any customization, otherwise 'default'
  if (customization.iconType && customization.iconType !== 'none') {
    localStyleType.value = customization.iconType; // Specific content icon
  } else if (hasCustomStyle) {
    localStyleType.value = 'custom'; // Has custom properties but no specific icon
  } else {
    localStyleType.value = 'default'; // Everything inherited
  }

  // Emit all properties separately
  emit('update:styleType', localStyleType.value);
  emit('update:styleBackgroundShape', customization.backgroundShape);
  emit('update:styleBackgroundColor', customization.backgroundColor);
  emit('update:styleBorderColor', customization.borderColor);
  emit('update:styleIconColor', customization.iconColor);
  emit('update:styleSize', customization.size);

  emitChange();
};

const handleCustomizationCancel = () => {
  // Just close the modal, don't change anything
};

const emitChange = () => {
  emit('change', {
    styleType: localStyleType.value,
    styleColor: localBackgroundColor.value || localStyleColor.value,
    styleBackgroundShape: localBackgroundShape.value,
    styleBackgroundColor: localBackgroundColor.value,
    styleBorderColor: localBorderColor.value,
    styleIconColor: localIconColor.value,
    styleSize: localSize.value,
  });
};

// Sync local state with props
watch(() => props.styleType, (newVal) => {
  localStyleType.value = newVal;
  // Only set content icon if it's actually a content icon type (not 'custom' or 'default')
  if (newVal && newVal !== 'default' && newVal !== 'custom' && isContentIcon(newVal)) {
    localContentIcon.value = newVal;
  } else {
    // Use 'default' (inherit) not 'none' (explicitly no icon)
    localContentIcon.value = 'default';
  }
});

watch(() => props.styleColor, (newVal) => {
  localStyleColor.value = newVal || '';
});

watch(() => props.styleBackgroundShape, (newVal) => {
  localBackgroundShape.value = newVal || 'default';
});

watch(() => props.styleBackgroundColor, (newVal) => {
  localBackgroundColor.value = newVal || '';
});

watch(() => props.styleBorderColor, (newVal) => {
  localBorderColor.value = newVal || '';
});

watch(() => props.styleIconColor, (newVal) => {
  localIconColor.value = newVal || '';
});

watch(() => props.styleSize, (newVal) => {
  localSize.value = newVal || 'default';
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

.style-options {
  display: flex;
  gap: 0.75rem;
}

.style-option-button {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  border: 2px solid #e2e8f0;
  border-radius: 8px;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 80px;
}

.style-option-button:hover {
  border-color: #cbd5e0;
  background: #f7fafc;
}

.style-option-button.selected {
  border-color: #007bff;
  background: #e7f3ff;
}

.option-preview {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.option-label {
  font-size: 0.8rem;
  color: #666;
  font-weight: 500;
}

.style-option-button.selected .option-label {
  color: #007bff;
}
</style>
