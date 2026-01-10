<template>
  <CardsGrid
    :is-empty="areas.length === 0"
    :empty-message="emptyMessage"
    :empty-hint="emptyHint"
  >
    <AreaCard
      v-for="area in areas"
      :key="area.id"
      :area="area"
      :checkpoints="checkpoints"
      :contacts="contacts"
      :event-people-term="eventPeopleTerm"
      :event-checkpoint-term="eventCheckpointTerm"
      :clickable="true"
      @click="$emit('select', $event)"
    />
  </CardsGrid>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import CardsGrid from '../common/CardsGrid.vue';
import AreaCard from './AreaCard.vue';

defineProps({
  areas: {
    type: Array,
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
  emptyMessage: {
    type: String,
    default: 'No areas.',
  },
  emptyHint: {
    type: String,
    default: '',
  },
});

defineEmits(['select']);
</script>
