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
const localStyleType = ref(props.styleType);
const localStyleColor = ref(props.styleColor || '');
const localBackgroundShape = ref(props.styleBackgroundShape || 'circle');
const localBackgroundColor = ref(props.styleBackgroundColor || '');
const localBorderColor = ref(props.styleBorderColor || '');
const localIconColor = ref(props.styleIconColor || '');
const localSize = ref(props.styleSize || '100');

// Determine the content icon - if styleType is a content icon, use it; otherwise 'none'
const isContentIcon = (type) => {
  if (!type || type === 'default') return false;
  const config = getIconTypeConfig(type);
  return config.category === 'content';
};

const localContentIcon = ref(isContentIcon(props.styleType) ? props.styleType : 'none');

// Modal state
const isCustomizationModalOpen = ref(false);

// Check if custom style is selected (anything other than 'default')
const isCustom = computed(() => localStyleType.value !== 'default');

// Generate preview SVG for the "Default" option
const defaultPreviewSvg = computed(() => {
  const size = 24;
  // If there's an inherited style, show that as the default preview
  if (props.inheritedStyleType && props.inheritedStyleType !== 'default') {
    return generateCheckpointSvg({
      type: props.inheritedStyleType,
      backgroundShape: props.inheritedBackgroundShape || 'circle',
      backgroundColor: props.inheritedBackgroundColor || props.inheritedStyleColor || '',
      borderColor: props.inheritedBorderColor || '',
      iconColor: props.inheritedIconColor || '',
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
    const hasContentIcon = localContentIcon.value && localContentIcon.value !== 'none';
    const effectiveType = hasContentIcon ? localContentIcon.value : localBackgroundShape.value;
    const config = getIconTypeConfig(effectiveType);
    const bgColor = localBackgroundColor.value || config.defaultColor || '#667eea';

    return generateCheckpointSvg({
      type: effectiveType,
      backgroundShape: localBackgroundShape.value,
      backgroundColor: bgColor,
      borderColor: localBorderColor.value || '',
      iconColor: localIconColor.value || '',
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
  localContentIcon.value = 'none';
  emit('update:styleType', 'default');
  // Clear customizations
  localBackgroundShape.value = 'circle';
  localBackgroundColor.value = '';
  localBorderColor.value = '';
  localIconColor.value = '';
  localSize.value = '100';
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
  // Update local state
  localContentIcon.value = customization.iconType === customization.backgroundShape ? 'none' : customization.iconType;
  localBackgroundShape.value = customization.backgroundShape;
  localBackgroundColor.value = customization.backgroundColor;
  localBorderColor.value = customization.borderColor;
  localIconColor.value = customization.iconColor;
  localSize.value = customization.size;

  // The styleType is what gets saved - it's the effective type (icon if present, otherwise shape)
  localStyleType.value = customization.iconType;

  emit('update:styleType', customization.iconType);
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
  if (newVal && newVal !== 'default') {
    localContentIcon.value = isContentIcon(newVal) ? newVal : 'none';
  } else {
    localContentIcon.value = 'none';
  }
});

watch(() => props.styleColor, (newVal) => {
  localStyleColor.value = newVal || '';
});

watch(() => props.styleBackgroundShape, (newVal) => {
  localBackgroundShape.value = newVal || 'circle';
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
  localSize.value = newVal || '100';
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
