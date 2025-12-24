<template>
  <div>
    <h3>Checkpoints ({{ locations.length }})</h3>
    <div class="button-group">
      <button @click="$emit('add-checkpoint')" class="btn btn-small btn-primary">
        Add checkpoint
      </button>
      <button @click="$emit('import-checkpoints')" class="btn btn-small btn-secondary">
        Import CSV
      </button>
    </div>

    <div class="locations-list">
      <div
        v-for="location in sortedLocations"
        :key="location.id"
        class="location-item"
        :class="{
          'location-full': location.checkedInCount >= location.requiredMarshals,
          'location-missing': location.checkedInCount === 0
        }"
        @click="$emit('select-location', location)"
      >
        <div class="location-info">
          <strong>{{ location.name }}</strong>
          <span class="location-description">
            {{ location.description }}
          </span>
          <span class="location-status">
            {{ location.checkedInCount }}/{{ location.requiredMarshals }}
          </span>
        </div>
        <div class="location-assignments">
          <span
            v-for="assignment in location.assignments"
            :key="assignment.id"
            class="assignment-badge"
            :class="{ 'checked-in': assignment.isCheckedIn }"
          >
            {{ assignment.marshalName }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';

const props = defineProps({
  locations: {
    type: Array,
    required: true,
  },
});

defineEmits(['add-checkpoint', 'import-checkpoints', 'select-location']);

const sortedLocations = computed(() => {
  return [...props.locations].sort((a, b) => {
    const aName = a.name;
    const bName = b.name;

    // Check if both names are purely numeric
    const aNum = parseFloat(aName);
    const bNum = parseFloat(bName);

    const aIsNum = !isNaN(aNum) && String(aNum) === aName.trim();
    const bIsNum = !isNaN(bNum) && String(bNum) === bName.trim();

    // If both are numbers, sort numerically
    if (aIsNum && bIsNum) {
      return aNum - bNum;
    }

    // Otherwise, sort alphabetically (case-insensitive)
    return aName.localeCompare(bName, undefined, { numeric: true, sensitivity: 'base' });
  });
});
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

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}

.locations-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.location-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
}

.location-item:hover {
  border-color: #667eea;
}

.location-item.location-full {
  border-color: #4caf50;
  background: #f1f8f4;
}

.location-item.location-missing {
  border-color: #ff4444;
  background: #fff1f1;
}

.location-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
  min-width: 0; /* allow children to shrink instead of forcing width */
}

.location-description {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1 1 auto;  /* take remaining space, but shrink if needed */
  min-width: 0;    /* critical in flex layouts for ellipsis */
  margin: 0 0.5rem; /* optional: space between name and status */
}

.location-status {
  font-weight: 600;
  color: #667eea;
}

.location-assignments {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.assignment-badge {
  padding: 0.25rem 0.75rem;
  background: #e0e0e0;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #666;
}

.assignment-badge.checked-in {
  background: #4caf50;
  color: white;
}
</style>
