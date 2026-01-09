<template>
  <div class="course-tab">
    <div class="content-grid">
      <div class="map-section">
        <MapView
          :locations="locationStatuses"
          :route="route"
          :clickable="true"
          @map-click="$emit('map-click', $event)"
          @location-click="$emit('location-click', $event)"
        />
        <button class="fullscreen-btn" @click="$emit('fullscreen')" title="Fullscreen">
          <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/>
          </svg>
        </button>
      </div>

      <div class="sidebar">
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
import MapView from '../../components/MapView.vue';
import CheckpointsList from '../../components/event-manage/lists/CheckpointsList.vue';

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
});

const emit = defineEmits([
  'map-click',
  'location-click',
  'add-checkpoint-manually',
  'import-checkpoints',
  'select-location',
  'fullscreen',
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

.fullscreen-btn {
  position: absolute;
  top: 10px;
  right: 10px;
  z-index: 1000;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  padding: 8px;
  cursor: pointer;
  color: var(--text-primary);
  box-shadow: var(--shadow-md);
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s, transform 0.1s;
}

.fullscreen-btn:hover {
  background: var(--bg-hover);
  transform: scale(1.05);
}

.fullscreen-btn:active {
  transform: scale(0.95);
}

.fullscreen-btn svg {
  display: block;
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
