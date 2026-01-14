<template>
  <div class="assignment-pills">
    <span
      v-for="(badge, index) in badges"
      :key="index"
      class="scope-badge"
      @click="$emit('click')"
    >
      {{ badge }}
    </span>
  </div>
</template>

<script setup>
import { computed } from 'vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  scopeConfigurations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  maxExpandedItems: {
    type: Number,
    default: 5,
  },
});

defineEmits(['click']);

// Map scope types to display names
const formatScope = (scope) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'EveryoneInAreas': `Everyone in ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `Everyone at ${termsLower.value.checkpoints}`,
    'SpecificPeople': `Specific ${termsLower.value.people}`,
    'EveryAreaLead': `Every ${termsLower.value.area} lead`,
    'OnePerCheckpoint': `One per ${termsLower.value.checkpoint}`,
    'OnePerArea': `One per ${termsLower.value.area}`,
    'OneLeadPerArea': `One lead per ${termsLower.value.area}`,
    'AreaLead': `${terms.value.area} lead`, // Legacy support
  };
  return scopeMap[scope] || scope;
};

// Get the prefix for a scope type when expanding into individual pills
// Uses appropriate prepositions: "in" for areas, "at" for checkpoints
const getScopePrefix = (scope) => {
  const prefixMap = {
    'Everyone': '',
    'EveryoneInAreas': 'Everyone in',
    'EveryoneAtCheckpoints': 'Everyone at',
    'SpecificPeople': '',
    'EveryAreaLead': `${termsLower.value.area} lead in`,
    'OnePerCheckpoint': 'One at',
    'OnePerArea': 'One in',
    'OneLeadPerArea': 'Lead in',
  };
  return prefixMap[scope] || '';
};

// Look up item name by ID and type
const getItemName = (id, itemType) => {
  if (itemType === 'Area') {
    const area = props.areas.find(a => a.id === id);
    return area?.name || id;
  }
  if (itemType === 'Checkpoint') {
    const location = props.locations.find(l => l.id === id);
    return location?.name || id;
  }
  if (itemType === 'Marshal') {
    const marshal = props.marshals.find(m => m.id === id);
    return marshal?.name || id;
  }
  return id;
};

// Expand a scope config into individual badges if there are few enough items
const expandScopeConfig = (config) => {
  if (!config) return [];

  const scopeName = formatScope(config.scope);
  const ids = config.ids || [];

  // If no itemType or no IDs, return the scope name as-is
  if (config.itemType === null || ids.length === 0) {
    return [scopeName];
  }

  // Handle "ALL" cases - return single badge
  if (ids.includes('ALL_MARSHALS')) {
    return ['Everyone'];
  }
  if (ids.includes('ALL_AREAS')) {
    return [`${scopeName} (All ${termsLower.value.areas})`];
  }
  if (ids.includes('ALL_CHECKPOINTS')) {
    return [`${scopeName} (All ${termsLower.value.checkpoints})`];
  }

  // If more than maxExpandedItems, return summary badge
  if (ids.length > props.maxExpandedItems) {
    return [`${scopeName} (${ids.length})`];
  }

  // Expand into individual badges with item names
  const prefix = getScopePrefix(config.scope);
  return ids.map(id => {
    const itemName = getItemName(id, config.itemType);
    return prefix ? `${prefix} ${itemName}` : itemName;
  });
};

// Computed badges array
const badges = computed(() => {
  if (!props.scopeConfigurations || props.scopeConfigurations.length === 0) {
    return [];
  }

  const result = [];
  for (const config of props.scopeConfigurations) {
    result.push(...expandScopeConfig(config));
  }
  return result;
});
</script>

<style scoped>
.assignment-pills {
  display: flex;
  gap: 0.375rem;
  flex-wrap: wrap;
  min-width: 0;
}

.scope-badge {
  padding: 0.2rem 0.6rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 500;
  white-space: nowrap;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* Only add cursor and hover effects if there's a click handler */
.assignment-pills:has(.scope-badge) .scope-badge {
  cursor: pointer;
  transition: background-color 0.2s;
}

.scope-badge:hover {
  background: var(--accent-primary-hover);
}
</style>
