<template>
  <div class="areas-tab">
    <div class="content-grid">
      <div class="map-section">
        <MapView
          :locations="checkpoints"
          :route="route"
          :areas="areas"
          :selectedAreaId="selectedAreaId"
          :clickable="true"
          :drawingMode="drawingMode"
          @map-click="$emit('map-click', $event)"
          @area-click="$emit('area-click', $event)"
          @polygon-complete="$emit('polygon-complete', $event)"
        />
      </div>

      <div class="sidebar">
        <div class="section">
          <AreasList
            :areas="areas"
            :checkpoints="checkpoints"
            @add-area="$emit('add-area')"
            @select-area="$emit('select-area', $event)"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import MapView from '../../components/MapView.vue';
import AreasList from '../../components/event-manage/lists/AreasList.vue';

const props = defineProps({
  areas: {
    type: Array,
    required: true,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  route: {
    type: Array,
    default: () => [],
  },
  selectedAreaId: {
    type: String,
    default: null,
  },
  drawingMode: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits([
  'map-click',
  'area-click',
  'add-area',
  'select-area',
  'polygon-complete',
  'cancel-drawing',
]);
</script>

<style scoped>
.areas-tab {
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
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.section {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
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

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

/* Mobile responsive adjustments */
@media (max-width: 768px) {
  /* Mobile styles can be added here if needed */
}
</style>
