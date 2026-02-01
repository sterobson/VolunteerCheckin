<template>
  <CardsGrid
    :is-empty="checkpoints.length === 0"
    :empty-message="emptyMessage"
    :empty-hint="emptyHint"
  >
    <CheckpointCard
      v-for="checkpoint in checkpoints"
      :key="checkpoint.id"
      :checkpoint="checkpoint"
      :areas="areas"
      :clickable="true"
      :hide-area-pills="hideAreaPills"
      @click="$emit('select', $event)"
    />
  </CardsGrid>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import CardsGrid from '../common/CardsGrid.vue';
import CheckpointCard from './CheckpointCard.vue';

defineProps({
  checkpoints: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  emptyMessage: {
    type: String,
    default: 'No checkpoints.',
  },
  emptyHint: {
    type: String,
    default: '',
  },
  hideAreaPills: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['select']);
</script>
