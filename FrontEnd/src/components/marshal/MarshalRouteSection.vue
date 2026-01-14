<template>
  <div class="accordion-section">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('course')"></span>
        {{ terms.course }}
        <span v-if="userLocation" class="location-status active">GPS Active</span>
        <span v-else class="location-status">GPS Inactive</span>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content map-content">
      <!-- Map selection mode banner -->
      <div v-if="selectingLocation" class="map-selection-banner">
        <span>Click on the map to set the new location for <strong>{{ selectingLocationName }}</strong></span>
        <button @click="$emit('cancel-select')" class="btn btn-secondary btn-sm">Cancel</button>
      </div>
      <div class="map-wrapper">
        <CommonMap
          ref="mapRef"
          :locations="locations"
          :route="route"
          :center="center"
          :zoom="15"
          :user-location="userLocation"
          :highlight-location-ids="highlightLocationIds"
          :marshal-mode="true"
          :simplify-non-highlighted="true"
          :clickable="clickable"
          :show-fullscreen="true"
          :fullscreen-title="terms.course"
          :fullscreen-header-style="branding.headerStyle"
          :fullscreen-header-text-color="branding.headerTextColor"
          :toolbar-actions="toolbarActions"
          :hide-recenter-button="true"
          :class="{ 'selecting-location': selectingLocation }"
          height="100%"
          @map-click="(e) => $emit('map-click', e)"
          @location-click="(e) => $emit('location-click', e)"
          @action-click="(e) => $emit('action-click', e)"
          @visibility-change="(e) => $emit('visibility-change', e)"
        >
          <!-- Selection mode banner -->
          <template v-if="selectingLocation" #fullscreen-banner>
            <span>Click on the map to set the new location for <strong>{{ selectingLocationName }}</strong></span>
            <button @click="$emit('cancel-select')" class="btn btn-secondary btn-sm">Cancel</button>
          </template>
        </CommonMap>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, ref, computed, defineExpose, inject, toValue, reactive } from 'vue';
import { getIcon } from '../../utils/icons';
import { useTerminology } from '../../composables/useTerminology';
import CommonMap from '../common/CommonMap.vue';

const { terms } = useTerminology();
const injectedBranding = inject('marshalBranding', {
  headerStyle: {},
  headerTextColor: '',
});

// Unwrap refs from injected branding (they may be computed refs or plain values)
// Use reactive() so Vue auto-unwraps the computed refs in templates
const branding = reactive({
  headerStyle: computed(() => toValue(injectedBranding.headerStyle)),
  headerTextColor: computed(() => toValue(injectedBranding.headerTextColor)),
});

defineProps({
  isExpanded: {
    type: Boolean,
    default: false,
  },
  userLocation: {
    type: Object,
    default: null,
  },
  selectingLocation: {
    type: Boolean,
    default: false,
  },
  selectingLocationName: {
    type: String,
    default: '',
  },
  locations: {
    type: Array,
    default: () => [],
  },
  route: {
    type: Array,
    default: () => [],
  },
  center: {
    type: Object,
    default: null,
  },
  highlightLocationIds: {
    type: Array,
    default: () => [],
  },
  clickable: {
    type: Boolean,
    default: false,
  },
  toolbarActions: {
    type: Array,
    default: () => [],
  },
});

defineEmits(['toggle', 'cancel-select', 'map-click', 'location-click', 'action-click', 'visibility-change']);

const mapRef = ref(null);

// Expose map methods directly for parent to access
defineExpose({
  mapRef,
  recenterOnLocation: (lat, lng, zoom) => mapRef.value?.recenterOnLocation?.(lat, lng, zoom),
  recenterOnUserLocation: () => mapRef.value?.recenterOnUserLocation?.(),
});
</script>

<style scoped>
/* Accordion styles */
.accordion-section {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 0;
  border-top: 1px solid var(--border-light);
}

/* Component-specific styles */
.location-status {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  border-radius: 12px;
  background: var(--btn-cancel-bg);
  color: var(--text-secondary);
  font-weight: 500;
}

.location-status.active {
  background: var(--success-bg-light);
  color: var(--checked-in-text);
}

.map-content {
  padding: 0;
}

.map-wrapper {
  height: 350px;
}

.map-selection-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: var(--warning-bg);
  color: var(--warning-text);
  font-size: 0.9rem;
}

.btn {
  padding: 0.4rem 0.75rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.85rem;
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover {
  background: var(--btn-cancel-hover);
}

.btn-sm {
  padding: 0.3rem 0.6rem;
  font-size: 0.8rem;
}

:deep(.selecting-location) {
  cursor: crosshair;
}

@media (max-width: 768px) {
  .map-wrapper {
    height: 300px;
  }
}
</style>
