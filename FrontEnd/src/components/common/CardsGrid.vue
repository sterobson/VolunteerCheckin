<template>
  <div v-if="isEmpty" class="empty-state">
    <p>{{ emptyMessage }}</p>
    <p v-if="emptyHint" class="empty-hint">{{ emptyHint }}</p>
  </div>

  <div v-else class="cards-grid" :class="gridClass">
    <slot></slot>
  </div>
</template>

<script setup>
import { defineProps } from 'vue';

defineProps({
  isEmpty: {
    type: Boolean,
    default: false,
  },
  emptyMessage: {
    type: String,
    default: 'No items.',
  },
  emptyHint: {
    type: String,
    default: '',
  },
  gridClass: {
    type: String,
    default: '',
  },
});
</script>

<style scoped>
.cards-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 0.75rem;
}

@media (max-width: 1400px) {
  .cards-grid {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 1024px) {
  .cards-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 640px) {
  .cards-grid {
    grid-template-columns: 1fr;
  }
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: var(--text-muted);
}

.empty-state p {
  margin: 0 0 0.5rem 0;
}

.empty-hint {
  font-size: 0.85rem;
  color: var(--text-muted);
  font-style: italic;
}
</style>
