<template>
  <BaseModal
    :show="show"
    title="Emergency information"
    size="medium"
    @close="handleClose"
  >
    <!-- Emergency/Urgent notes -->
    <div v-if="sortedNotes.length > 0" class="emergency-notes-section">
      <h2 class="section-header">Emergency notes</h2>
      <div class="emergency-notes-list">
        <div
          v-for="note in sortedNotes"
          :key="note.noteId || note.NoteId"
          class="emergency-note-item"
          :class="{ 'is-emergency': (note.priority || note.Priority) === 'Emergency' }"
        >
          <div class="note-header">
            <h3>{{ note.title || note.Title }}</h3>
            <span class="priority-badge" :class="(note.priority || note.Priority).toLowerCase()">
              {{ note.priority || note.Priority }}
            </span>
          </div>
          <div v-if="note.content || note.Content" class="note-content">{{ note.content || note.Content }}</div>
        </div>
      </div>
    </div>

    <!-- Emergency contacts list -->
    <div v-if="filteredContacts && filteredContacts.length > 0" class="emergency-contacts-section">
      <h2 class="section-header">Emergency contacts</h2>
      <div class="emergency-contacts-list">
        <div
          v-for="contact in filteredContacts"
          :key="contact.contactId || contact.name"
          class="emergency-contact-item"
        >
          <div class="contact-header">
            <h3>{{ contact.name }}</h3>
            <span v-if="getContactRoles(contact).length > 0" class="role-badge">{{ getContactRoles(contact).map(formatRoleName).join(', ') }}</span>
          </div>

          <div class="contact-details">
            <div v-if="contact.phone" class="info-section">
              <label>Phone number</label>
              <a :href="`tel:${contact.phone}`" class="phone-link">
                {{ contact.phone }}
              </a>
            </div>

            <div v-if="contact.email" class="info-section">
              <label>Email</label>
              <a :href="`mailto:${contact.email}`" class="email-link">
                {{ contact.email }}
              </a>
            </div>

            <div v-if="contact.notes || contact.details" class="info-section">
              <label>Details</label>
              <p>{{ contact.notes || contact.details }}</p>
            </div>
          </div>

          <a v-if="contact.phone" :href="`tel:${contact.phone}`" class="btn btn-danger btn-full">
            Call {{ contact.name }}
          </a>
        </div>
      </div>
    </div>

    <!-- No contacts or notes message -->
    <div v-if="filteredContacts.length === 0 && sortedNotes.length === 0" class="no-contacts">
      <p>No emergency information has been set up for this event.</p>
    </div>

    <!-- Close button -->
    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">Close</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import BaseModal from '../../BaseModal.vue';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  contacts: {
    type: Array,
    default: () => [],
  },
  notes: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close']);

// Filter and sort notes: only show those with showInEmergencyInfo
// Sort by: pinned first, then display order, then priority (Emergency first)
const sortedNotes = computed(() => {
  if (!props.notes) return [];
  return [...props.notes]
    .filter(n => n.showInEmergencyInfo || n.ShowInEmergencyInfo)
    .sort((a, b) => {
      // Pinned first
      const aPinned = a.isPinned || a.IsPinned;
      const bPinned = b.isPinned || b.IsPinned;
      if (aPinned !== bPinned) {
        return aPinned ? -1 : 1;
      }

      // Then by display order
      const orderA = a.displayOrder || a.DisplayOrder || 0;
      const orderB = b.displayOrder || b.DisplayOrder || 0;
      if (orderA !== orderB) {
        return orderA - orderB;
      }

      // Then by priority (Emergency comes before Urgent)
      const aPriority = a.priority || a.Priority;
      const bPriority = b.priority || b.Priority;
      if (aPriority === 'Emergency' && bPriority !== 'Emergency') return -1;
      if (bPriority === 'Emergency' && aPriority !== 'Emergency') return 1;
      return 0;
    });
});

// Filter and sort contacts: only show those with showInEmergencyInfo
// Sort by: pinned first, then display order, then name
const filteredContacts = computed(() => {
  if (!props.contacts) return [];
  return props.contacts
    .filter(c => c.showInEmergencyInfo || c.ShowInEmergencyInfo)
    .sort((a, b) => {
      // Pinned first
      const aPinned = a.isPinned || a.IsPinned;
      const bPinned = b.isPinned || b.IsPinned;
      if (aPinned !== bPinned) {
        return aPinned ? -1 : 1;
      }

      // Then by display order
      const orderA = a.displayOrder || a.DisplayOrder || 0;
      const orderB = b.displayOrder || b.DisplayOrder || 0;
      if (orderA !== orderB) {
        return orderA - orderB;
      }

      // Then by name
      const nameA = a.name || a.Name || '';
      const nameB = b.name || b.Name || '';
      return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
    });
});

const handleClose = () => {
  emit('close');
};

const formatRoleName = (role) => {
  if (!role) return '';
  const roleMap = {
    'EmergencyContact': 'Emergency Contact',
    'EventDirector': 'Event Director',
    'MedicalLead': 'Medical Lead',
    'SafetyOfficer': 'Safety Officer',
    'Logistics': 'Logistics',
  };
  return roleMap[role] || role.replace(/([A-Z])/g, ' $1').trim();
};

// Get roles from contact - handles both array and legacy single role
const getContactRoles = (contact) => {
  if (contact.roles && Array.isArray(contact.roles) && contact.roles.length > 0) {
    return contact.roles;
  }
  if (contact.role) {
    return [contact.role];
  }
  return [];
};
</script>

<style scoped>
.section-header {
  font-size: 1rem;
  color: var(--text-dark);
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid var(--danger);
}

.emergency-notes-section {
  margin-bottom: 2rem;
}

.emergency-notes-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.emergency-note-item {
  padding: 1rem;
  background: var(--warning-bg);
  border: 1px solid var(--warning);
  border-radius: 8px;
}

.emergency-note-item.is-emergency {
  background: var(--danger-bg);
  border-color: var(--danger);
}

.emergency-note-item .note-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.75rem;
  margin-bottom: 0.5rem;
}

.emergency-note-item h3 {
  margin: 0;
  font-size: 1rem;
  color: var(--text-dark);
}

.priority-badge {
  padding: 0.2rem 0.5rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  white-space: nowrap;
}

.priority-badge.emergency {
  background: var(--danger);
  color: var(--card-bg);
}

.priority-badge.urgent {
  background: var(--warning);
  color: var(--warning-text);
}

.note-content {
  font-size: 0.9rem;
  color: var(--text-dark);
  line-height: 1.5;
  white-space: pre-wrap;
}

.emergency-contacts-section {
  margin-bottom: 1rem;
}

.emergency-contacts-list {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.emergency-contact-item {
  padding: 1.5rem;
  background: var(--bg-secondary);
  border-radius: 8px;
  border: 1px solid var(--border-color);
}

.contact-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.emergency-contact-item h3 {
  margin: 0;
  color: var(--text-dark);
  font-size: 1.1rem;
}

.role-badge {
  padding: 0.25rem 0.6rem;
  background: var(--danger);
  color: var(--card-bg);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
  white-space: nowrap;
}

.contact-details {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.info-section label {
  display: block;
  font-weight: 500;
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin-bottom: 0.25rem;
}

.info-section p {
  margin: 0;
  color: var(--text-dark);
}

.phone-link {
  color: var(--link-color);
  text-decoration: none;
  font-weight: 500;
  font-size: 1.1rem;
}

.phone-link:hover {
  text-decoration: underline;
}

.email-link {
  color: var(--link-color);
  text-decoration: none;
  font-size: 0.95rem;
}

.email-link:hover {
  text-decoration: underline;
}

.no-contacts {
  text-align: center;
  padding: 2rem;
  color: var(--text-secondary);
}

.no-contacts p {
  margin: 0;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
  text-decoration: none;
  display: inline-block;
  text-align: center;
}

.btn-danger {
  background: var(--danger);
  color: var(--card-bg);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

.btn-full {
  width: 100%;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
