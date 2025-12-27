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

    <EventFormModal
      :show="showCreateEvent || !!editingEvent"
      :eventData="eventForm"
      :isEditing="!!editingEvent"
      @close="closeModal"
      @submit="handleSaveEvent"
    />

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
import EventFormModal from '../components/event-manage/modals/EventFormModal.vue';

const router = useRouter();
const authStore = useAuthStore();
const eventsStore = useEventsStore();

const loading = ref(true);
const showCreateEvent = ref(false);
const editingEvent = ref(null);

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

const handleSaveEvent = async (formData) => {
  try {
    const eventData = {
      ...formData,
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

const closeModal = () => {
  showCreateEvent.value = false;
  editingEvent.value = null;
  eventForm.value = {
    name: '',
    description: '',
    eventDate: '',
    timeZoneId: 'UTC',
    emergencyContacts: [],
  };
};

const goToEvent = (eventId) => {
  if (!eventId) {
    console.error('Cannot navigate to event: eventId is undefined');
    return;
  }
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
</style>
