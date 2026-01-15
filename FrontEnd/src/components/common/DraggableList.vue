<template>
  <div
    ref="listRef"
    class="draggable-list"
    :class="{ 'draggable-grid': layout === 'grid', 'is-dragging': isDragging }"
  >
    <div
      v-for="(element, index) in localItems"
      :key="element[itemKey]"
      :data-id="element[itemKey]"
      class="draggable-item"
    >
      <slot name="item" :element="element" :index="index" />
      <div v-if="isDragging && showOrderOverlay" class="order-overlay">
        {{ index + 1 }}
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onMounted, onUnmounted, nextTick } from 'vue';
import Sortable from 'sortablejs';

const props = defineProps({
  items: {
    type: Array,
    required: true,
  },
  itemKey: {
    type: String,
    default: 'id',
  },
  pinnedKey: {
    type: String,
    default: null,
  },
  disabled: {
    type: Boolean,
    default: false,
  },
  layout: {
    type: String,
    default: 'list', // 'list' or 'grid'
  },
  showOrderOverlay: {
    type: Boolean,
    default: true,
  },
});

const emit = defineEmits(['reorder']);

const listRef = ref(null);
const isDragging = ref(false);
let sortableInstance = null;

// Local copy of items
const localItems = ref([...props.items]);

// Sync local items when props change
watch(() => props.items, (newItems) => {
  localItems.value = [...newItems];
}, { deep: true });

// Watch disabled state
watch(() => props.disabled, (newDisabled) => {
  if (sortableInstance) {
    sortableInstance.option('disabled', newDisabled);
  }
});

/**
 * Handle sort end - reorder local items and emit changes
 */
const onSortEnd = (evt) => {
  isDragging.value = false;

  const { oldIndex, newIndex } = evt;

  if (oldIndex === newIndex) return;

  // Move item in local array
  const item = localItems.value.splice(oldIndex, 1)[0];
  localItems.value.splice(newIndex, 0, item);

  // Calculate new display orders for all items
  const changes = localItems.value.map((item, index) => ({
    id: item[props.itemKey],
    displayOrder: index + 1,
  }));

  emit('reorder', { items: localItems.value, changes });
};

/**
 * Handle drag start
 */
const onSortStart = () => {
  isDragging.value = true;
};

/**
 * Move callback to enforce pinning boundaries
 */
const onMove = (evt) => {
  if (!props.pinnedKey) return true;

  const draggedId = evt.dragged.dataset.id;
  const draggedItem = localItems.value.find(item => String(item[props.itemKey]) === draggedId);
  if (!draggedItem) return true;

  const draggedIsPinned = draggedItem[props.pinnedKey];
  const relatedId = evt.related.dataset.id;
  const relatedItem = localItems.value.find(item => String(item[props.itemKey]) === relatedId);
  if (!relatedItem) return true;

  const relatedIsPinned = relatedItem[props.pinnedKey];

  // Pinned items can only be reordered among pinned items
  // Non-pinned items can only be reordered among non-pinned items
  return draggedIsPinned === relatedIsPinned;
};

const initSortable = () => {
  if (!listRef.value) return;

  sortableInstance = Sortable.create(listRef.value, {
    animation: 200,
    handle: '.drag-handle',
    ghostClass: 'sortable-ghost',
    chosenClass: 'sortable-chosen',
    dragClass: 'sortable-drag',
    disabled: props.disabled,
    onMove: onMove,
    onStart: onSortStart,
    onEnd: onSortEnd,
  });
};

const destroySortable = () => {
  if (sortableInstance) {
    sortableInstance.destroy();
    sortableInstance = null;
  }
};

onMounted(() => {
  nextTick(() => {
    initSortable();
  });
});

onUnmounted(() => {
  destroySortable();
});
</script>

<style>
/* Global styles for drag states - not scoped so they work on slotted content */
.draggable-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.draggable-list.draggable-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 0.75rem;
}

@media (max-width: 640px) {
  .draggable-list.draggable-grid {
    grid-template-columns: 1fr;
  }
}

.draggable-item {
  position: relative;
}

/* Order overlay - shown during drag */
.order-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 4rem;
  font-weight: 700;
  color: var(--text-muted, rgba(0, 0, 0, 0.15));
  pointer-events: none;
  z-index: 5;
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.7);
}

@media (prefers-color-scheme: dark) {
  .order-overlay {
    background: rgba(0, 0, 0, 0.5);
    color: rgba(255, 255, 255, 0.3);
  }
}

/* Dark mode support via CSS variable */
.dark .order-overlay,
[data-theme="dark"] .order-overlay {
  background: rgba(0, 0, 0, 0.5);
  color: rgba(255, 255, 255, 0.3);
}

.sortable-ghost {
  opacity: 0.4;
}

.sortable-ghost .order-overlay {
  display: none;
}

.sortable-chosen {
  box-shadow: var(--shadow-lg, 0 10px 15px -3px rgba(0, 0, 0, 0.1));
  z-index: 10;
}

.sortable-chosen .order-overlay {
  display: none;
}

.sortable-drag {
  cursor: grabbing !important;
}

.sortable-drag .order-overlay {
  display: none;
}
</style>
