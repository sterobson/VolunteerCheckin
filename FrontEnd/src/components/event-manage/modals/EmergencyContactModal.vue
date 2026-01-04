<template>
  <BaseModal
    :show="show"
    title="Emergency Information"
    size="medium"
    @close="handleClose"
  >
    <!-- Emergency/Urgent notes -->
    <div v-if="notes && notes.length > 0" class="emergency-notes-section">
      <h2 class="section-header">Emergency Notes</h2>
      <div class="emergency-notes-list">
        <div
          v-for="note in sortedNotes"
          :key="note.noteId"
          class="emergency-note-item"
          :class="{ 'is-emergency': note.priority === 'Emergency' }"
        >
          <div class="note-header">
            <h3>{{ note.title }}</h3>
            <span class="priority-badge" :class="note.priority.toLowerCase()">
              {{ note.priority }}
            </span>
          </div>
          <div v-if="note.content" class="note-content">{{ note.content }}</div>
        </div>
      </div>
    </div>

    <!-- Emergency contacts list -->
    <div v-if="contacts && contacts.length > 0" class="emergency-contacts-section">
      <h2 class="section-header">Emergency Contacts</h2>
      <div class="emergency-contacts-list">
        <div
          v-for="contact in contacts"
          :key="contact.contactId || contact.name"
          class="emergency-contact-item"
        >
          <div class="contact-header">
            <h3>{{ contact.name }}</h3>
            <span v-if="contact.role" class="role-badge">{{ formatRoleName(contact.role) }}</span>
          </div>

          <div class="contact-details">
            <div v-if="contact.phone" class="info-section">
              <label>Phone Number</label>
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
    <div v-if="(!contacts || contacts.length === 0) && (!notes || notes.length === 0)" class="no-contacts">
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

// Sort notes: Emergency first, then Urgent
const sortedNotes = computed(() => {
  if (!props.notes) return [];
  return [...props.notes].sort((a, b) => {
    // Emergency comes before Urgent
    if (a.priority === 'Emergency' && b.priority !== 'Emergency') return -1;
    if (b.priority === 'Emergency' && a.priority !== 'Emergency') return 1;
    return 0;
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
</script>

<style scoped>
.section-header {
  font-size: 1rem;
  color: #333;
  margin: 0 0 1rem 0;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid #dc3545;
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
  background: #fff3cd;
  border: 1px solid #ffc107;
  border-radius: 8px;
}

.emergency-note-item.is-emergency {
  background: #f8d7da;
  border-color: #dc3545;
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
  color: #333;
}

.priority-badge {
  padding: 0.2rem 0.5rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  white-space: nowrap;
}

.priority-badge.emergency {
  background: #dc3545;
  color: white;
}

.priority-badge.urgent {
  background: #ffc107;
  color: #856404;
}

.note-content {
  font-size: 0.9rem;
  color: #333;
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
  background: #f8f9fa;
  border-radius: 8px;
  border: 1px solid #dee2e6;
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
  color: #333;
  font-size: 1.1rem;
}

.role-badge {
  padding: 0.25rem 0.6rem;
  background: #dc3545;
  color: white;
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
  color: #666;
  font-size: 0.9rem;
  margin-bottom: 0.25rem;
}

.info-section p {
  margin: 0;
  color: #333;
}

.phone-link {
  color: #007bff;
  text-decoration: none;
  font-weight: 500;
  font-size: 1.1rem;
}

.phone-link:hover {
  text-decoration: underline;
}

.email-link {
  color: #007bff;
  text-decoration: none;
  font-size: 0.95rem;
}

.email-link:hover {
  text-decoration: underline;
}

.no-contacts {
  text-align: center;
  padding: 2rem;
  color: #666;
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
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

.btn-full {
  width: 100%;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
