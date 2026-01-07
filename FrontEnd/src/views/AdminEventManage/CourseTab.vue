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
