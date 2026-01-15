<template>
  <CardsGrid
    :is-empty="contacts.length === 0"
    :empty-message="emptyMessage"
    :empty-hint="emptyHint"
  >
    <DraggableList
      v-if="contacts.length > 0"
      :items="sortedContacts"
      item-key="contactId"
      pinned-key="isPinned"
      layout="grid"
      :disabled="!allowReorder"
      :show-order-overlay="allowReorder"
      @reorder="handleReorder"
    >
      <template #item="{ element: contact }">
        <div class="contact-card-wrapper">
          <DragHandle v-if="allowReorder" />
          <ContactCard
            :contact="contact"
            :is-pending="contact.isPending"
            :is-marked-for-removal="contact.isMarkedForRemoval"
            :show-remove-button="showRemoveButton"
            :remove-title="getRemoveTitle(contact)"
            :show-notes="showNotes"
            :show-scopes="showScopes"
            :clickable="isClickable(contact)"
            :areas="areas"
            :locations="locations"
            :marshals="marshals"
            :role-definitions="roleDefinitions"
            @click="$emit('select', contact)"
            @remove="$emit('remove', $event)"
            @undo-remove="$emit('undo-remove', $event)"
          />
        </div>
      </template>
    </DraggableList>
  </CardsGrid>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import CardsGrid from '../common/CardsGrid.vue';
import ContactCard from './ContactCard.vue';
import DraggableList from '../common/DraggableList.vue';
import DragHandle from '../common/DragHandle.vue';

const props = defineProps({
  contacts: {
    type: Array,
    required: true,
  },
  showRemoveButton: {
    type: Boolean,
    default: false,
  },
  removeTitle: {
    type: [String, Function],
    default: 'Remove',
  },
  showNotes: {
    type: Boolean,
    default: false,
  },
  showScopes: {
    type: Boolean,
    default: false,
  },
  emptyMessage: {
    type: String,
    default: 'No contacts.',
  },
  emptyHint: {
    type: String,
    default: '',
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
  roleDefinitions: {
    type: Array,
    default: () => [],
  },
  allowReorder: {
    type: Boolean,
    default: false,
  },
});

const emit = defineEmits(['select', 'remove', 'undo-remove', 'reorder']);

// Sort contacts by pinned status, then displayOrder, then name
const sortedContacts = computed(() => {
  return [...props.contacts].sort((a, b) => {
    // Pinned first
    if (a.isPinned !== b.isPinned) {
      return a.isPinned ? -1 : 1;
    }
    // Then by displayOrder
    const orderA = a.displayOrder || 0;
    const orderB = b.displayOrder || 0;
    if (orderA !== orderB) return orderA - orderB;
    // Then by name
    return (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' });
  });
});

const getRemoveTitle = (contact) => {
  if (typeof props.removeTitle === 'function') {
    return props.removeTitle(contact);
  }
  return props.removeTitle;
};

const isClickable = (contact) => {
  return !contact.isPending;
};

const handleReorder = ({ changes }) => {
  emit('reorder', changes);
};
</script>

<style scoped>
.contact-card-wrapper {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  height: 100%;
}

.contact-card-wrapper :deep(.contact-card) {
  flex: 1;
  min-width: 0;
}
</style>
