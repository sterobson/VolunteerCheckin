<template>
  <div class="accordion-section" v-if="contacts.length > 0 || loading">
    <button
      class="accordion-header"
      :class="{ active: isExpanded }"
      @click="$emit('toggle')"
    >
      <span class="accordion-title">
        <span class="section-icon" v-html="getIcon('contacts')"></span>
        Your {{ contacts.length === 1 ? 'contact' : 'contacts' }}<span v-if="contacts.length > 1" class="header-count"> ({{ contacts.length }})</span>
      </span>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="accordion-content">
      <div v-if="loading" class="loading">Loading contacts...</div>
      <div v-else class="contact-list">
        <ContactCard
          v-for="contact in contacts"
          :key="contact.contactId"
          :name="contact.name"
          :roles="contact.roles"
          :role="contact.role"
          :phone="contact.phone"
          :email="contact.email"
          :notes="contact.notes"
          :is-primary="contact.isPrimary"
        />
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits } from 'vue';
import { getIcon } from '../../utils/icons';
import ContactCard from './ContactCard.vue';

defineProps({
  contacts: {
    type: Array,
    required: true,
  },
  loading: {
    type: Boolean,
    default: false,
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
});

defineEmits(['toggle']);
</script>

<style scoped>
/* Accordion styles */
.accordion-section {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.header-count {
  font-style: italic;
  opacity: 0.6;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* Component-specific styles */
.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.loading {
  text-align: center;
  padding: 1rem;
  color: var(--text-secondary);
}
</style>
