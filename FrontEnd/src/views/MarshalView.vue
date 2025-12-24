<template>
  <div class="marshal-view">
    <header class="header">
      <h1 v-if="event">{{ event.name }}</h1>
      <button v-if="selectedMarshal" @click="showEmergency = true" class="btn-emergency">
        Emergency Info
      </button>
    </header>

    <div class="container">
      <!-- Marshal Selection -->
      <div v-if="!selectedMarshal" class="selection-card">
        <h2>Who are you?</h2>
        <p class="instruction">Select your name to view your assignment</p>

        <div v-if="loading" class="loading">Loading...</div>

        <div v-else class="marshal-list">
          <button
            v-for="assignment in assignments"
            :key="assignment.id"
            @click="selectMarshal(assignment)"
            class="marshal-button"
          >
            {{ assignment.marshalName }}
          </button>
        </div>
      </div>

      <!-- Marshal Dashboard -->
      <div v-else class="dashboard">
        <div class="welcome-card">
          <h2>Welcome, {{ selectedMarshal.marshalName }}!</h2>
          <button @click="changeMarshal" class="btn-link">Not you? Click here</button>
        </div>

        <!-- Assignment Info -->
        <div class="assignment-card">
          <h3>Your Assignment</h3>
          <div class="location-info">
            <strong>{{ assignedLocation?.name }}</strong>
            <p>{{ assignedLocation?.description }}</p>
          </div>

          <div v-if="selectedMarshal.isCheckedIn" class="checked-in-status">
            <span class="check-icon">✓</span>
            <div>
              <strong>Checked In</strong>
              <p class="check-time">{{ formatTime(selectedMarshal.checkInTime) }}</p>
              <p class="check-method">Method: {{ selectedMarshal.checkInMethod }}</p>
            </div>
          </div>

          <div v-else class="check-in-actions">
            <button
              @click="handleCheckIn"
              class="btn btn-primary btn-large"
              :disabled="checkingIn"
            >
              {{ checkingIn ? 'Checking in...' : 'Check In with GPS' }}
            </button>

            <button
              @click="handleManualCheckIn"
              class="btn btn-secondary"
              :disabled="checkingIn"
            >
              Manual Check-In
            </button>

            <div v-if="checkInError" class="error">{{ checkInError }}</div>
          </div>
        </div>

        <!-- Map View -->
        <div class="map-card">
          <h3>All Locations</h3>
          <MapView
            :locations="allLocations"
            :center="mapCenter"
            :zoom="14"
          />
        </div>
      </div>
    </div>

    <!-- Emergency Contact Modal -->
    <div v-if="showEmergency" class="modal" @click.self="showEmergency = false">
      <div class="modal-content emergency-modal">
        <h2>Emergency Contact Information</h2>

        <div v-if="event.emergencyContacts && event.emergencyContacts.length > 0" class="emergency-contacts-list">
          <div
            v-for="(contact, index) in event.emergencyContacts"
            :key="index"
            class="emergency-contact-item"
          >
            <h3>{{ contact.name }}</h3>

            <div class="contact-details">
              <div class="info-section">
                <label>Phone Number</label>
                <a :href="`tel:${contact.phone}`" class="phone-link">
                  {{ contact.phone }}
                </a>
              </div>

              <div v-if="contact.details" class="info-section">
                <label>Details</label>
                <p>{{ contact.details }}</p>
              </div>
            </div>

            <a :href="`tel:${contact.phone}`" class="btn btn-danger btn-full">
              Call {{ contact.name }}
            </a>
          </div>
        </div>

        <div v-else class="no-contacts">
          <p>No emergency contacts have been set up for this event.</p>
        </div>

        <div class="modal-actions">
          <button @click="showEmergency = false" class="btn btn-secondary">Close</button>
        </div>
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
import { ref, computed, onMounted } from 'vue';
import { useRoute } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi } from '../services/api';
import MapView from '../components/MapView.vue';
import ConfirmModal from '../components/ConfirmModal.vue';

const route = useRoute();
const eventsStore = useEventsStore();

const event = ref(null);
const assignments = ref([]);
const allLocations = ref([]);
const selectedMarshal = ref(null);
const assignedLocation = ref(null);
const loading = ref(true);
const checkingIn = ref(false);
const checkInError = ref(null);
const showEmergency = ref(false);
const userLocation = ref(null);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

const mapCenter = computed(() => {
  if (assignedLocation.value) {
    return {
      lat: assignedLocation.value.latitude,
      lng: assignedLocation.value.longitude,
    };
  }
  return { lat: 51.505, lng: -0.09 };
});

const loadEventData = async () => {
  loading.value = true;
  try {
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    await eventsStore.fetchAssignments(route.params.eventId);
    assignments.value = eventsStore.assignments;

    const statusData = await eventsStore.fetchEventStatus(route.params.eventId);
    allLocations.value = statusData.locations;
  } catch (error) {
    console.error('Failed to load event data:', error);
  } finally {
    loading.value = false;
  }
};

const selectMarshal = (assignment) => {
  selectedMarshal.value = assignment;
  assignedLocation.value = allLocations.value.find(
    (loc) => loc.id === assignment.locationId
  );

  localStorage.setItem(`marshal_${route.params.eventId}`, assignment.id);

  if ('geolocation' in navigator) {
    navigator.geolocation.getCurrentPosition(
      (position) => {
        userLocation.value = {
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        };
      },
      (error) => {
        console.warn('Geolocation error:', error);
      }
    );
  }
};

const changeMarshal = () => {
  selectedMarshal.value = null;
  assignedLocation.value = null;
  localStorage.removeItem(`marshal_${route.params.eventId}`);
};

const handleCheckIn = async () => {
  checkingIn.value = true;
  checkInError.value = null;

  try {
    if (!('geolocation' in navigator)) {
      throw new Error('Geolocation is not supported by your browser');
    }

    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 10000,
      });
    });

    const response = await checkInApi.checkIn({
      assignmentId: selectedMarshal.value.id,
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      manualCheckIn: false,
    });

    selectedMarshal.value = response.data;
    await loadEventData();
  } catch (error) {
    if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = 'Failed to check in. Please try manual check-in.';
    }
  } finally {
    checkingIn.value = false;
  }
};

const handleManualCheckIn = async () => {
  confirmModalTitle.value = 'Manual Check-In';
  confirmModalMessage.value = 'Are you sure you want to check in manually? This should only be used if GPS is not available.';
  confirmModalCallback.value = async () => {
    checkingIn.value = true;
    checkInError.value = null;

    try {
      const response = await checkInApi.checkIn({
        assignmentId: selectedMarshal.value.id,
        latitude: null,
        longitude: null,
        manualCheckIn: true,
      });

      selectedMarshal.value = response.data;
      await loadEventData();
    } catch (error) {
      checkInError.value = 'Failed to check in manually. Please contact the admin.';
    } finally {
      checkingIn.value = false;
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

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

onMounted(async () => {
  await loadEventData();

  const savedMarshalId = localStorage.getItem(`marshal_${route.params.eventId}`);
  if (savedMarshalId) {
    const assignment = assignments.value.find((a) => a.id === savedMarshalId);
    if (assignment) {
      selectMarshal(assignment);
    }
  }
});
</script>

<style scoped>
.marshal-view {
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.header {
  background: rgba(255, 255, 255, 0.1);
  backdrop-filter: blur(10px);
  padding: 1.5rem 2rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  color: white;
}

.header h1 {
  margin: 0;
}

.btn-emergency {
  background: #ff4444;
  color: white;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  transition: all 0.3s;
}

.btn-emergency:hover {
  background: #cc0000;
}

.container {
  padding: 2rem;
  max-width: 800px;
  margin: 0 auto;
}

.selection-card,
.welcome-card,
.assignment-card,
.map-card {
  background: white;
  border-radius: 12px;
  padding: 2rem;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
  margin-bottom: 2rem;
}

.selection-card h2,
.welcome-card h2,
.assignment-card h3,
.map-card h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 2rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: #666;
}

.marshal-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1rem;
}

.marshal-button {
  padding: 1.5rem;
  background: #f5f7fa;
  border: 2px solid #e0e0e0;
  border-radius: 8px;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 600;
  color: #333;
  transition: all 0.3s;
}

.marshal-button:hover {
  background: #667eea;
  color: white;
  border-color: #667eea;
  transform: translateY(-2px);
}

.welcome-card {
  text-align: center;
}

.btn-link {
  background: none;
  border: none;
  color: #667eea;
  cursor: pointer;
  font-size: 0.875rem;
  text-decoration: underline;
  margin-top: 0.5rem;
}

.location-info {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: #f5f7fa;
  border-radius: 8px;
}

.location-info strong {
  font-size: 1.125rem;
  color: #333;
}

.location-info p {
  margin: 0.5rem 0 0 0;
  color: #666;
}

.checked-in-status {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem;
  background: #f1f8f4;
  border: 2px solid #4caf50;
  border-radius: 8px;
}

.check-icon {
  font-size: 2rem;
  color: #4caf50;
}

.checked-in-status strong {
  color: #4caf50;
  font-size: 1.125rem;
}

.check-time,
.check-method {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.875rem;
}

.check-in-actions {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.btn {
  padding: 1rem 2rem;
  border: none;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
  font-size: 1rem;
  transition: all 0.3s;
  text-decoration: none;
  display: inline-block;
  text-align: center;
}

.btn-large {
  font-size: 1.125rem;
  padding: 1.25rem 2rem;
}

.btn-primary {
  background: #667eea;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #5568d3;
  transform: translateY(-2px);
}

.btn-secondary {
  background: #e0e0e0;
  color: #333;
}

.btn-secondary:hover:not(:disabled) {
  background: #d0d0d0;
}

.btn-danger {
  background: #ff4444;
  color: white;
}

.btn-danger:hover {
  background: #cc0000;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error {
  padding: 1rem;
  background: #fee;
  color: #c33;
  border-radius: 6px;
  font-size: 0.875rem;
}

.map-card {
  height: 500px;
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
}

.modal-content h2 {
  margin: 0 0 1.5rem 0;
  color: #333;
}

.emergency-info {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  margin-bottom: 2rem;
}

.info-section label {
  display: block;
  color: #666;
  font-size: 0.875rem;
  margin-bottom: 0.5rem;
}

.info-section strong,
.info-section p {
  margin: 0;
  color: #333;
  font-size: 1.125rem;
}

.phone-link {
  color: #667eea;
  font-size: 1.5rem;
  font-weight: 600;
  text-decoration: none;
}

.phone-link:hover {
  text-decoration: underline;
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

.emergency-contacts-list {
  display: flex;
  flex-direction: column;
  gap: 2rem;
  margin-bottom: 2rem;
}

.emergency-contact-item {
  border: 2px solid #e0e0e0;
  border-radius: 12px;
  padding: 1.5rem;
  background: #f9f9f9;
}

.emergency-contact-item h3 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1.25rem;
}

.contact-details {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.btn-full {
  width: 100%;
  text-align: center;
}

.no-contacts {
  text-align: center;
  padding: 3rem 2rem;
  color: #999;
}

@media (max-width: 768px) {
  .marshal-list {
    grid-template-columns: 1fr;
  }

  .map-card {
    height: 400px;
  }
}
</style>
