<template>
  <div
    class="area-card"
    :class="{
      'is-clickable': clickable,
      'is-default': area.isDefault,
    }"
    :style="{ borderLeftColor: area.color || '#667eea' }"
    @click="handleClick"
  >
    <div class="area-card-header">
      <div class="area-card-title" :class="{ clickable }">
        <div class="area-name-row">
          <span class="area-name">{{ area.name }}</span>
          <span v-if="area.isDefault" class="default-badge">Default</span>
        </div>
        <span v-if="area.description" class="area-description">
          {{ area.description }}
        </span>
      </div>
    </div>

    <div class="area-card-stats">
      <span class="stat-badge">
        {{ checkpointCountText }}
      </span>
      <span class="stat-badge">
        {{ contactCount }} contact{{ contactCount === 1 ? '' : 's' }}
      </span>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import { getSingularTerm, getPluralTerm } from '../../composables/useTerminology';

const props = defineProps({
  area: {
    type: Object,
    required: true,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  contacts: {
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
  clickable: {
    type: Boolean,
    default: true,
  },
});

const emit = defineEmits(['click']);

// Get checkpoint count for this area
const checkpointCount = computed(() => {
  if (props.area.checkpointCount !== undefined) {
    return props.area.checkpointCount;
  }
  // Fallback: count from checkpoints array
  return props.checkpoints.filter(c => {
    const areaIds = c.areaIds || c.AreaIds || [];
    return areaIds.includes(props.area.id) || c.areaId === props.area.id;
  }).length;
});

// Get contact count for this area
const contactCount = computed(() => {
  return props.contacts.filter(contact => {
    if (!contact.scopeConfigurations) return false;

    return contact.scopeConfigurations.some(config => {
      if (config.scope === 'EveryoneInAreas' && config.itemType === 'Area') {
        if (config.ids?.includes('ALL_AREAS')) return true;
        return config.ids?.includes(props.area.id);
      }
      return false;
    });
  }).length;
});

// Get the effective people term for the area
const areaPeopleTerm = computed(() => {
  return props.area.peopleTerm || props.eventPeopleTerm || 'Marshals';
});

// Get the effective checkpoint term with correct singular/plural
const checkpointTerm = computed(() => {
  const storedTerm = props.area.checkpointTerm || props.eventCheckpointTerm || 'Checkpoints';
  const count = checkpointCount.value;

  if (count === 1) {
    return getSingularTerm('checkpoint', storedTerm, areaPeopleTerm.value);
  }
  return getPluralTerm('checkpoint', storedTerm, areaPeopleTerm.value);
});

// Format checkpoint count text
const checkpointCountText = computed(() => {
  return `${checkpointCount.value} ${checkpointTerm.value.toLowerCase()}`;
});

const handleClick = () => {
  if (props.clickable) {
    emit('click', props.area);
  }
};
</script>

<style scoped>
.area-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-left: 4px solid var(--accent-primary);
  border-radius: 8px;
  padding: 0.875rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.area-card.is-clickable {
  cursor: pointer;
}

.area-card.is-clickable:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-sm);
}

.area-card.is-default {
  background: var(--status-warning-bg);
}

.area-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.area-card-title {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.area-card-title.clickable:hover .area-name {
  color: var(--accent-primary);
}

.area-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.area-name {
  font-weight: 600;
  color: var(--text-primary);
  transition: color 0.15s;
}

.default-badge {
  padding: 0.15rem 0.4rem;
  background: var(--warning);
  border-radius: 4px;
  font-size: 0.65rem;
  font-weight: 600;
  color: var(--text-primary);
  text-transform: uppercase;
}

.area-description {
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

.area-card-stats {
  display: flex;
  gap: 0.375rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.stat-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  border-radius: 12px;
  font-size: 0.75rem;
  color: var(--text-secondary);
  font-weight: 500;
}
</style>
