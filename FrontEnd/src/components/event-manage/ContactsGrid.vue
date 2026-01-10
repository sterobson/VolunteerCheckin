<template>
  <CardsGrid
    :is-empty="contacts.length === 0"
    :empty-message="emptyMessage"
    :empty-hint="emptyHint"
  >
    <ContactCard
      v-for="contact in contacts"
      :key="contact.contactId"
      :contact="contact"
      :is-pending="contact.isPending"
      :is-marked-for-removal="contact.isMarkedForRemoval"
      :show-remove-button="showRemoveButton"
      :remove-title="getRemoveTitle(contact)"
      :show-notes="showNotes"
      :show-scopes="showScopes"
      :clickable="isClickable(contact)"
      @click="$emit('select', contact)"
      @remove="$emit('remove', $event)"
      @undo-remove="$emit('undo-remove', $event)"
    />
  </CardsGrid>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import CardsGrid from '../common/CardsGrid.vue';
import ContactCard from './ContactCard.vue';

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
});

defineEmits(['select', 'remove', 'undo-remove']);

const getRemoveTitle = (contact) => {
  if (typeof props.removeTitle === 'function') {
    return props.removeTitle(contact);
  }
  return props.removeTitle;
};

const isClickable = (contact) => {
  return !contact.isPending;
};
</script>
