<template>
  <div class="course-tab">
    <div class="content-grid">
      <div class="map-section">
        <CommonMap
          :locations="locationStatuses"
          :route="route"
          :areas="areas"
          :route-color="routeColor"
          :route-style="routeStyle"
          :clickable="true"
          show-filters
          height="100%"
          @map-click="$emit('map-click', $event)"
          @location-click="$emit('location-click', $event)"
        />
      </div>

      <div class="sidebar">
        <div class="section route-settings-section">
          <h3 class="section-title">Route display</h3>
          <div class="route-settings">
            <div class="setting-row">
              <BrandingColorPicker
                :model-value="routeColor"
                label="Route colour"
                :default-color="DEFAULT_ROUTE_COLOR"
                :show-contrast-preview="false"
                @update:model-value="$emit('update:routeColor', $event)"
              />
            </div>
            <div class="setting-row">
              <label class="setting-label">Route style</label>
              <select
                class="style-select"
                :value="routeStyle || 'line'"
                @change="$emit('update:routeStyle', $event.target.value)"
              >
                <option value="line">Solid line</option>
                <option value="dash">Dashed</option>
                <option value="dot">Dotted</option>
              </select>
            </div>
          </div>
        </div>

        <div class="section">
          <CheckpointsList
            :locations="locationStatuses"
            :areas="areas"
            @add-checkpoint-manually="$emit('add-checkpoint-manually')"
            @import-checkpoints="$emit('import-checkpoints')"
            @select-location="$emit('select-location', $event)"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import CommonMap from '../../components/common/CommonMap.vue';
import CheckpointsList from '../../components/event-manage/lists/CheckpointsList.vue';
import BrandingColorPicker from '../../components/BrandingColorPicker.vue';

// Default route color (Leaflet's default blue)
const DEFAULT_ROUTE_COLOR = '#3388ff';

const props = defineProps({
  locationStatuses: {
    type: Array,
    required: true,
  },
  route: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  routeColor: {
    type: String,
    default: '',
  },
  routeStyle: {
    type: String,
    default: '',
  },
});

const emit = defineEmits([
  'map-click',
  'location-click',
  'add-checkpoint-manually',
  'import-checkpoints',
  'select-location',
  'update:routeColor',
  'update:routeStyle',
]);
</script>

<style scoped>
.course-tab {
  width: 100%;
}

.content-grid {
  display: grid;
  grid-template-columns: 1fr 400px;
  gap: 2rem;
  height: calc(100vh - 250px);
}

.map-section {
  position: relative;
  border-radius: 8px;
  overflow: hidden;
  box-shadow: var(--shadow-md);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section {
  background: var(--card-bg);
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: var(--shadow-md);
}

.route-settings-section {
  flex-shrink: 0;
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
}

.route-settings {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.setting-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.setting-label {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.style-select {
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--border-medium);
  border-radius: 6px;
  background: var(--card-bg);
  font-size: 0.9rem;
  color: var(--text-dark);
  cursor: pointer;
  min-width: 140px;
}

.style-select:hover {
  border-color: var(--brand-primary);
}

.style-select:focus {
  outline: none;
  border-color: var(--brand-primary);
  box-shadow: 0 0 0 2px var(--brand-shadow);
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
    height: auto;
  }

  .map-section {
    height: 400px;
  }
}
</style>
