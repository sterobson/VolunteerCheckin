<template>
  <div>
    <h3>{{ terms.areas }} ({{ areas.length }})</h3>
    <div class="button-group">
      <button @click="$emit('add-area')" class="btn btn-small btn-primary">
        Add {{ terms.area.toLowerCase() }}
      </button>
    </div>

    <div class="areas-list">
      <div
        v-for="area in sortedAreas"
        :key="area.id"
        class="area-item"
        :style="{ borderLeftColor: area.color || '#667eea' }"
        @click="$emit('select-area', area)"
      >
        <div class="area-info">
          <div class="area-header">
            <strong>{{ area.name }}</strong>
            <span v-if="area.isDefault" class="default-badge">Default</span>
          </div>
          <p v-if="area.description" class="area-description">
            {{ area.description }}
          </p>
          <div class="area-stats">
            <span class="stat-badge">
              {{ formatAreaCheckpointCount(area) }}
            </span>
            <span class="stat-badge">
              {{ getContactCount(area) }} contact{{ getContactCount(area) === 1 ? '' : 's' }}
            </span>
          </div>
        </div>
      </div>

      <div v-if="areas.length === 0" class="empty-state">
        <p>No {{ terms.areas.toLowerCase() }} yet. Click "Add {{ terms.area.toLowerCase() }}" to create one.</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import { alphanumericCompare } from '../../../utils/sortUtils';
import { useTerminology, getSingularTerm, getPluralTerm } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  areas: {
    type: Array,
    required: true,
  },
  checkpoints: {
    type: Array,
    default: () => [],
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

defineEmits(['add-area', 'select-area']);

const sortedAreas = computed(() => {
  return [...props.areas].sort((a, b) => {
    // Sort by display order first
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }

    // Then by name (alphanumeric)
    return alphanumericCompare(a.name, b.name);
  });
});

const getCheckpointCount = (area) => {
  if (area.checkpointCount !== undefined) {
    return area.checkpointCount;
  }
  // Fallback: count from checkpoints array
  return props.checkpoints.filter((c) => c.areaId === area.id).length;
};

const getContactCount = (area) => {
  if (!area.contacts) return 0;
  // Parse ContactsJson if it's a string
  const contacts = typeof area.contacts === 'string'
    ? JSON.parse(area.contacts)
    : area.contacts;
  return Array.isArray(contacts) ? contacts.length : 0;
};

// Get the effective people term for an area
const getAreaPeopleTerm = (area) => {
  return area.peopleTerm || props.eventPeopleTerm || 'Marshals';
};

// Get the effective checkpoint term for an area (resolves "Person points" dynamically)
const getAreaCheckpointTerm = (area, count) => {
  const storedTerm = area.checkpointTerm || props.eventCheckpointTerm || 'Checkpoints';
  const peopleTerm = getAreaPeopleTerm(area);

  if (count === 1) {
    return getSingularTerm('checkpoint', storedTerm, peopleTerm);
  }
  return getPluralTerm('checkpoint', storedTerm, peopleTerm);
};

// Format checkpoint count with area-specific terminology (sentence case)
const formatAreaCheckpointCount = (area) => {
  const count = getCheckpointCount(area);
  const term = getAreaCheckpointTerm(area, count).toLowerCase();
  return `${count} ${term}`;
};
</script>

<style scoped>
h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.button-group {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.areas-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.area-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-left: 4px solid #667eea;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
  background: white;
}

.area-item:hover {
  border-color: #667eea;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.2);
}

.area-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.area-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.area-header strong {
  font-size: 1rem;
  color: #333;
}

.default-badge {
  padding: 0.2rem 0.5rem;
  background: #ffc107;
  border-radius: 4px;
  font-size: 0.7rem;
  font-weight: 600;
  color: #333;
  text-transform: uppercase;
}

.area-description {
  margin: 0;
  color: #666;
  font-size: 0.9rem;
}

.area-stats {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.stat-badge {
  padding: 0.25rem 0.75rem;
  background: #f0f0f0;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #666;
  font-weight: 500;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: #999;
}

.empty-state p {
  margin: 0;
  font-size: 0.9rem;
}
</style>
