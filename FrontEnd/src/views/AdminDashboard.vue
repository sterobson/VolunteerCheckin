<template>
  <div class="admin-dashboard">
    <header class="header">
      <h1>Admin Dashboard</h1>
      <div class="header-actions">
        <span class="user-email">{{ authStore.adminEmail }}</span>
        <button @click="handleLogout" class="btn btn-secondary">Logout</button>
      </div>
    </header>

    <div class="container">
      <div class="section">
        <div class="section-header">
          <h2>Your Events</h2>
          <button @click="showCreateEvent = true" class="btn btn-primary">
            Create New Event
          </button>
        </div>

        <div v-if="loading" class="loading">Loading events...</div>

        <div v-else-if="events.length === 0" class="empty">
          <p>No events yet. Create your first event to get started!</p>
        </div>

        <div v-else class="events-grid">
          <div
            v-for="event in events"
            :key="event.id"
            class="event-card"
            @click="goToEvent(event.id)"
          >
            <h3>{{ event.name }}</h3>
            <p class="event-date">{{ formatDate(event.eventDate) }}</p>
            <p class="event-desc">{{ event.description }}</p>
            <div class="event-actions">
              <button
                @click.stop="editEvent(event)"
                class="btn btn-small btn-secondary"
              >
                Edit
              </button>
              <button
                @click.stop="confirmDelete(event)"
                class="btn btn-small btn-danger"
              >
                Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Create/Edit Event Modal -->
    <div v-if="showCreateEvent || editingEvent" class="modal" @click.self="closeModal">
      <div class="modal-content modal-wide">
        <h2>{{ editingEvent ? 'Edit Event' : 'Create New Event' }}</h2>

        <!-- Tabs -->
        <div class="tabs">
          <button
            type="button"
            @click="activeTab = 'basic'"
            class="tab-button"
            :class="{ active: activeTab === 'basic' }"
          >
            Basic Details
          </button>
          <button
            type="button"
            @click="activeTab = 'emergency'"
            class="tab-button"
            :class="{ active: activeTab === 'emergency' }"
          >
            Emergency Contacts
          </button>
        </div>

        <form @submit.prevent="handleSaveEvent">
          <!-- Basic Details Tab -->
          <div v-show="activeTab === 'basic'" class="tab-content">
            <div class="form-group">
              <label>Event Name *</label>
              <input
                v-model="eventForm.name"
                type="text"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>Description</label>
              <textarea
                v-model="eventForm.description"
                rows="3"
                class="form-input"
              ></textarea>
            </div>

            <div class="form-group">
              <label>Event Date *</label>
              <input
                v-model="eventForm.eventDate"
                type="datetime-local"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>Time Zone *</label>
              <select
                v-model="eventForm.timeZoneId"
                required
                class="form-input"
              >
                <option value="UTC">UTC</option>
                <option value="America/New_York">Eastern Time (ET)</option>
                <option value="America/Chicago">Central Time (CT)</option>
                <option value="America/Denver">Mountain Time (MT)</option>
                <option value="America/Los_Angeles">Pacific Time (PT)</option>
                <option value="America/Anchorage">Alaska Time (AKT)</option>
                <option value="Pacific/Honolulu">Hawaii Time (HT)</option>
                <option value="Europe/London">London (GMT/BST)</option>
                <option value="Europe/Paris">Paris (CET/CEST)</option>
                <option value="Asia/Tokyo">Tokyo (JST)</option>
                <option value="Australia/Sydney">Sydney (AEDT/AEST)</option>
              </select>
            </div>
          </div>

          <!-- Emergency Contacts Tab -->
          <div v-show="activeTab === 'emergency'" class="tab-content">
            <div class="emergency-contacts-section">
              <div class="section-header-small">
                <h3>Emergency Contacts</h3>
                <button
                  type="button"
                  @click="addEmergencyContact"
                  class="btn btn-small btn-primary"
                >
                  + Add Contact
                </button>
              </div>

              <div v-if="eventForm.emergencyContacts.length === 0" class="empty-state">
                <p>No emergency contacts added yet. Click "Add Contact" to add one.</p>
              </div>

              <div
                v-for="(contact, index) in eventForm.emergencyContacts"
                :key="index"
                class="emergency-contact-card"
              >
                <div class="card-header">
                  <h4>Contact {{ index + 1 }}</h4>
                  <button
                    type="button"
                    @click="removeEmergencyContact(index)"
                    class="btn-remove"
                  >
                    ✕
                  </button>
                </div>

                <div class="form-group">
                  <label>Name *</label>
                  <input
                    v-model="contact.name"
                    type="text"
                    required
                    class="form-input"
                    placeholder="Contact person name"
                  />
                </div>

                <div class="form-group">
                  <label>Phone *</label>
                  <input
                    v-model="contact.phone"
                    type="tel"
                    required
                    class="form-input"
                    placeholder="+1 (555) 123-4567"
                  />
                </div>

                <div class="form-group">
                  <label>Additional Details</label>
                  <textarea
                    v-model="contact.details"
                    rows="2"
                    class="form-input"
                    placeholder="Role, availability, or other important information..."
                  ></textarea>
                </div>
              </div>
            </div>
          </div>

          <div class="modal-actions">
            <button type="button" @click="closeModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">
              {{ editingEvent ? 'Update' : 'Create' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <ConfirmModal
      :show="showConfirmModal"
      :title="confirmModalTitle"
      :message="confirmModalMessage"
      @confirm="handleConfirmModalConfirm"
      @cancel="handleConfirmModalCancel"
    />
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';
import { useEventsStore } from '../stores/events';
import ConfirmModal from '../components/ConfirmModal.vue';

const router = useRouter();
const authStore = useAuthStore();
const eventsStore = useEventsStore();

const loading = ref(true);
const showCreateEvent = ref(false);
const editingEvent = ref(null);
const activeTab = ref('basic');

const eventForm = ref({
  name: '',
  description: '',
  eventDate: '',
  timeZoneId: 'UTC',
  emergencyContacts: [],
});

const events = ref([]);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

const loadEvents = async () => {
  loading.value = true;
  try {
    await eventsStore.fetchEvents();
    events.value = eventsStore.events;
  } catch (error) {
    console.error('Failed to load events:', error);
  } finally {
    loading.value = false;
  }
};

const handleSaveEvent = async () => {
  try {
    const eventData = {
      ...eventForm.value,
      adminEmail: authStore.adminEmail,
    };

    if (editingEvent.value) {
      await eventsStore.updateEvent(editingEvent.value.id, eventData);
    } else {
      await eventsStore.createEvent(eventData);
    }

    events.value = eventsStore.events;
    closeModal();
  } catch (error) {
    console.error('Failed to save event:', error);
    alert('Failed to save event. Please try again.');
  }
};

const editEvent = (event) => {
  editingEvent.value = event;
  eventForm.value = {
    name: event.name,
    description: event.description,
    eventDate: formatDateForInput(event.eventDate),
    timeZoneId: event.timeZoneId || 'UTC',
    emergencyContacts: event.emergencyContacts ? JSON.parse(JSON.stringify(event.emergencyContacts)) : [],
  };
  activeTab.value = 'basic';
};

const confirmDelete = async (event) => {
  confirmModalTitle.value = 'Delete Event';
  confirmModalMessage.value = `Are you sure you want to delete "${event.name}"?`;
  confirmModalCallback.value = async () => {
    try {
      await eventsStore.deleteEvent(event.id);
      events.value = eventsStore.events;
    } catch (error) {
      console.error('Failed to delete event:', error);
      alert('Failed to delete event. Please try again.');
    }
  };
  showConfirmModal.value = true;
};

const handleConfirmModalConfirm = () => {
  showConfirmModal.value = false;
  if (confirmModalCallback.value) {
    confirmModalCallback.value();
    confirmModalCallback.value = null;
  }
};

const handleConfirmModalCancel = () => {
  showConfirmModal.value = false;
  confirmModalCallback.value = null;
};

const addEmergencyContact = () => {
  eventForm.value.emergencyContacts.push({
    name: '',
    phone: '',
    details: '',
  });
};

const removeEmergencyContact = (index) => {
  eventForm.value.emergencyContacts.splice(index, 1);
};

const closeModal = () => {
  showCreateEvent.value = false;
  editingEvent.value = null;
  activeTab.value = 'basic';
  eventForm.value = {
    name: '',
    description: '',
    eventDate: '',
    timeZoneId: 'UTC',
    emergencyContacts: [],
  };
};

const goToEvent = (eventId) => {
  router.push({ name: 'AdminEventManage', params: { eventId } });
};

const handleLogout = () => {
  authStore.logout();
  router.push({ name: 'Home' });
};

const formatDate = (dateString) => {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatDateForInput = (dateString) => {
  const date = new Date(dateString);
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${year}-${month}-${day}T${hours}:${minutes}`;
};

onMounted(() => {
  loadEvents();
});
</script>

<style scoped>
.admin-dashboard {
  min-height: 100vh;
  background: #f5f7fa;
}

.header {
  background: white;
  padding: 1.5rem 2rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header h1 {
  margin: 0;
  color: #333;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.user-email {
  color: #666;
  font-size: 0.875rem;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 2rem;
}

.section {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.section-header h2 {
  margin: 0;
  color: #333;
}

.loading,
.empty {
  text-align: center;
  padding: 3rem;
  color: #666;
}

.events-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1.5rem;
}

.event-card {
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  padding: 1.5rem;
  cursor: pointer;
  transition: all 0.3s;
}

.event-card:hover {
  border-color: #667eea;
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.2);
}

.event-card h3 {
  margin: 0 0 0.5rem 0;
  color: #333;
}

.event-date {
  color: #667eea;
  font-size: 0.875rem;
  margin: 0 0 0.5rem 0;
  font-weight: 600;
}

.event-desc {
  color: #666;
  margin: 0 0 1rem 0;
  font-size: 0.875rem;
}

.event-actions {
  display: flex;
  gap: 0.5rem;
}

.btn {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-small {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover {
  background: #5568d3;
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover {
  background: #d0d0d0;
}

.btn-danger {
  background: #ff4444;
  color: white;
}

.btn-danger:hover {
  background: #cc0000;
}

.modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 2rem;
}

.modal-content {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-content h2 {
  margin: 0 0 1.5rem 0;
  color: #333;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  color: #333;
  font-weight: 600;
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 1rem;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

.form-input:focus {
  outline: none;
  border-color: #667eea;
}

textarea.form-input {
  resize: vertical;
  font-family: inherit;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding-top: 1.5rem;
  background: white;
  position: sticky;
  bottom: 0;
  border-top: 1px solid #e0e0e0;
  margin-left: -2rem;
  margin-right: -2rem;
  margin-bottom: -2rem;
  padding-left: 2rem;
  padding-right: 2rem;
  padding-bottom: 2rem;
}

.modal-wide {
  max-width: 700px;
}

.tabs {
  display: flex;
  border-bottom: 2px solid #e0e0e0;
  margin-bottom: 2rem;
  gap: 0.5rem;
}

.tab-button {
  padding: 0.75rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-weight: 600;
  color: #666;
  transition: all 0.3s;
}

.tab-button:hover {
  color: #667eea;
}

.tab-button.active {
  color: #667eea;
  border-bottom-color: #667eea;
}

.tab-content {
  min-height: 300px;
}

.emergency-contacts-section {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.section-header-small {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.section-header-small h3 {
  margin: 0;
  color: #333;
  font-size: 1.125rem;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: #999;
  border: 2px dashed #e0e0e0;
  border-radius: 8px;
}

.emergency-contact-card {
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  padding: 1.5rem;
  background: #f9f9f9;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.card-header h4 {
  margin: 0;
  color: #333;
  font-size: 1rem;
}

.btn-remove {
  background: #ff4444;
  color: white;
  border: none;
  width: 28px;
  height: 28px;
  border-radius: 50%;
  cursor: pointer;
  font-size: 1rem;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.3s;
}

.btn-remove:hover {
  background: #cc0000;
  transform: scale(1.1);
}
</style>
