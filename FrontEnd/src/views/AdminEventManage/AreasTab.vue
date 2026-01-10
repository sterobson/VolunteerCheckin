<template>
  <div class="areas-tab">
    <div class="content-grid">
      <div class="map-section">
        <CommonMap
          :locations="checkpoints"
          :route="route"
          :areas="areas"
          :selected-area-id="selectedAreaId"
          :clickable="true"
          :mode="drawingMode ? 'draw-polygon' : 'view'"
          show-filters
          height="100%"
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
            :event-people-term="eventPeopleTerm"
            :event-checkpoint-term="eventCheckpointTerm"
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
import CommonMap from '../../components/common/CommonMap.vue';
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
  eventPeopleTerm: {
    type: String,
    default: 'Marshals',
  },
  eventCheckpointTerm: {
    type: String,
    default: 'Checkpoints',
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
  background: var(--accent-danger);
  color: white;
}

.btn-danger:hover {
  background: var(--danger-hover);
}

/* Mobile responsive adjustments */
@media (max-width: 768px) {
  /* Mobile styles can be added here if needed */
}
</style>
