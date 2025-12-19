<template>
  <div class="admin-event-manage">
    <header class="header">
      <div class="header-left">
        <button @click="goBack" class="btn-back">← Back</button>
        <div v-if="event">
          <h1>{{ event.name }}</h1>
          <p class="event-date">{{ formatDate(event.eventDate) }}</p>
        </div>
      </div>
      <div class="header-actions">
        <button @click="shareEvent" class="btn btn-primary">Share Marshal Link</button>
      </div>
    </header>

    <div class="container">
      <div class="content-grid">
        <div class="map-section">
          <MapView
            :locations="locationStatuses"
            :clickable="true"
            @map-click="handleMapClick"
            @location-click="handleLocationClick"
          />
        </div>

        <div class="sidebar">
          <div class="section">
            <h3>Locations ({{ locations.length }})</h3>
            <button @click="showAddLocation = true" class="btn btn-small btn-primary">
              Add Location
            </button>

            <div class="locations-list">
              <div
                v-for="location in locationStatuses"
                :key="location.id"
                class="location-item"
                :class="{
                  'location-full': location.checkedInCount >= location.requiredMarshals,
                  'location-missing': location.checkedInCount === 0
                }"
                @click="selectLocation(location)"
              >
                <div class="location-info">
                  <strong>{{ location.name }}</strong>
                  <span class="location-status">
                    {{ location.checkedInCount }}/{{ location.requiredMarshals }}
                  </span>
                </div>
                <div class="location-assignments">
                  <span
                    v-for="assignment in location.assignments"
                    :key="assignment.id"
                    class="assignment-badge"
                    :class="{ 'checked-in': assignment.isCheckedIn }"
                  >
                    {{ assignment.marshalName }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <div v-if="selectedLocation" class="section">
            <h3>{{ selectedLocation.name }}</h3>
            <button @click="showAddAssignment = true" class="btn btn-small btn-primary">
              Assign Marshal
            </button>

            <div class="assignments-list">
              <div
                v-for="assignment in selectedLocation.assignments"
                :key="assignment.id"
                class="assignment-item"
                :class="{ 'checked-in': assignment.isCheckedIn }"
              >
                <div class="assignment-info">
                  <strong>{{ assignment.marshalName }}</strong>
                  <span v-if="assignment.isCheckedIn" class="check-in-info">
                    ✓ {{ formatTime(assignment.checkInTime) }}
                    <span class="check-in-method">({{ assignment.checkInMethod }})</span>
                  </span>
                </div>
                <div class="assignment-actions">
                  <button
                    @click="toggleCheckIn(assignment)"
                    class="btn btn-small"
                    :class="assignment.isCheckedIn ? 'btn-danger' : 'btn-secondary'"
                  >
                    {{ assignment.isCheckedIn ? 'Undo' : 'Check In' }}
                  </button>
                  <button
                    @click="deleteAssignment(assignment)"
                    class="btn btn-small btn-danger"
                  >
                    Delete
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Add Location Modal -->
    <div v-if="showAddLocation" class="modal" @click.self="closeLocationModal">
      <div class="modal-content">
        <h2>Add Location</h2>
        <p class="instruction">Click on the map to set the location, or enter coordinates manually</p>

        <form @submit.prevent="handleSaveLocation">
          <div class="form-group">
            <label>Name</label>
            <input
              v-model="locationForm.name"
              type="text"
              required
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label>Description</label>
            <input
              v-model="locationForm.description"
              type="text"
              class="form-input"
            />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>Latitude</label>
              <input
                v-model.number="locationForm.latitude"
                type="number"
                step="any"
                required
                class="form-input"
              />
            </div>

            <div class="form-group">
              <label>Longitude</label>
              <input
                v-model.number="locationForm.longitude"
                type="number"
                step="any"
                required
                class="form-input"
              />
            </div>
          </div>

          <div class="form-group">
            <label>Required Marshals</label>
            <input
              v-model.number="locationForm.requiredMarshals"
              type="number"
              min="1"
              required
              class="form-input"
            />
          </div>

          <div class="modal-actions">
            <button type="button" @click="closeLocationModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">Add Location</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Add Assignment Modal -->
    <div v-if="showAddAssignment" class="modal" @click.self="closeAssignmentModal">
      <div class="modal-content">
        <h2>Assign Marshal to {{ selectedLocation?.name }}</h2>

        <form @submit.prevent="handleSaveAssignment">
          <div class="form-group">
            <label>Marshal Name</label>
            <input
              v-model="assignmentForm.marshalName"
              type="text"
              required
              class="form-input"
              placeholder="Enter marshal's name"
            />
          </div>

          <div class="modal-actions">
            <button type="button" @click="closeAssignmentModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">Assign</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Share Link Modal -->
    <div v-if="showShareLink" class="modal" @click.self="showShareLink = false">
      <div class="modal-content">
        <h2>Marshal Check-in Link</h2>
        <p class="instruction">Share this link with marshals so they can check in:</p>

        <div class="share-link-container">
          <input
            :value="marshalLink"
            readonly
            class="form-input"
            ref="linkInput"
          />
          <button @click="copyLink" class="btn btn-primary">
            {{ linkCopied ? 'Copied!' : 'Copy' }}
          </button>
        </div>

        <div class="modal-actions">
          <button @click="showShareLink = false" class="btn btn-secondary">Close</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi } from '../services/api';
import { startSignalRConnection, stopSignalRConnection } from '../services/signalr';
import MapView from '../components/MapView.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);
const selectedLocation = ref(null);
const showAddLocation = ref(false);
const showAddAssignment = ref(false);
const showShareLink = ref(false);
const linkCopied = ref(false);
const linkInput = ref(null);

const locationForm = ref({
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
});

const assignmentForm = ref({
  marshalName: '',
});

const marshalLink = computed(() => {
  return `${window.location.origin}/event/${route.params.eventId}`;
});

const loadEventData = async () => {
  try {
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    await eventsStore.fetchLocations(route.params.eventId);
    locations.value = eventsStore.locations;

    await eventsStore.fetchEventStatus(route.params.eventId);
    locationStatuses.value = eventsStore.eventStatus.locations;
  } catch (error) {
    console.error('Failed to load event data:', error);
  }
};

const handleMapClick = (coords) => {
  if (showAddLocation.value) {
    locationForm.value.latitude = coords.lat;
    locationForm.value.longitude = coords.lng;
  }
};

const handleLocationClick = (location) => {
  selectLocation(location);
};

const selectLocation = (location) => {
  selectedLocation.value = location;
};

const handleSaveLocation = async () => {
  try {
    await eventsStore.createLocation({
      eventId: route.params.eventId,
      ...locationForm.value,
    });

    await loadEventData();
    closeLocationModal();
  } catch (error) {
    console.error('Failed to save location:', error);
    alert('Failed to save location. Please try again.');
  }
};

const closeLocationModal = () => {
  showAddLocation.value = false;
  locationForm.value = {
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
  };
};

const handleSaveAssignment = async () => {
  try {
    await eventsStore.createAssignment({
      eventId: route.params.eventId,
      locationId: selectedLocation.value.id,
      marshalName: assignmentForm.value.marshalName,
    });

    await loadEventData();
    closeAssignmentModal();
  } catch (error) {
    console.error('Failed to save assignment:', error);
    alert('Failed to save assignment. Please try again.');
  }
};

const closeAssignmentModal = () => {
  showAddAssignment.value = false;
  assignmentForm.value = {
    marshalName: '',
  };
};

const toggleCheckIn = async (assignment) => {
  try {
    await checkInApi.adminCheckIn(assignment.id);
    await loadEventData();
  } catch (error) {
    console.error('Failed to toggle check-in:', error);
    alert('Failed to toggle check-in. Please try again.');
  }
};

const deleteAssignment = async (assignment) => {
  if (confirm(`Remove ${assignment.marshalName} from this location?`)) {
    try {
      await eventsStore.deleteAssignment(route.params.eventId, assignment.id);
      await loadEventData();
    } catch (error) {
      console.error('Failed to delete assignment:', error);
      alert('Failed to delete assignment. Please try again.');
    }
  }
};

const shareEvent = () => {
  showShareLink.value = true;
  linkCopied.value = false;
};

const copyLink = () => {
  if (linkInput.value) {
    linkInput.value.select();
    document.execCommand('copy');
    linkCopied.value = true;

    setTimeout(() => {
      linkCopied.value = false;
    }, 2000);
  }
};

const goBack = () => {
  router.push({ name: 'AdminDashboard' });
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

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

const handleCheckinUpdate = (data) => {
  if (data.eventId === route.params.eventId) {
    eventsStore.updateAssignmentCheckIn(data.assignment);
    loadEventData();
  }
};

onMounted(async () => {
  await loadEventData();
  startSignalRConnection(handleCheckinUpdate);
});

onUnmounted(() => {
  stopSignalRConnection();
});
</script>

<style scoped>
.admin-event-manage {
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

.header-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.btn-back {
  background: none;
  border: none;
  color: #667eea;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 600;
  padding: 0.5rem 1rem;
}

.btn-back:hover {
  background: #f0f0f0;
  border-radius: 6px;
}

.header h1 {
  margin: 0;
  color: #333;
}

.event-date {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.875rem;
}

.container {
  padding: 2rem;
}

.content-grid {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 2rem;
  max-width: 1400px;
  margin: 0 auto;
}

.map-section {
  background: white;
  border-radius: 12px;
  padding: 1rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  height: calc(100vh - 200px);
}

.sidebar {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.section {
  background: white;
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.section h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.locations-list,
.assignments-list {
  margin-top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.location-item,
.assignment-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
}

.location-item:hover {
  border-color: #667eea;
}

.location-item.location-full {
  border-color: #4caf50;
  background: #f1f8f4;
}

.location-item.location-missing {
  border-color: #ff4444;
  background: #fff1f1;
}

.location-info {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.location-status {
  font-weight: 600;
  color: #667eea;
}

.location-assignments {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.assignment-badge {
  padding: 0.25rem 0.75rem;
  background: #e0e0e0;
  border-radius: 12px;
  font-size: 0.75rem;
  color: #666;
}

.assignment-badge.checked-in {
  background: #4caf50;
  color: white;
}

.assignment-item {
  cursor: default;
}

.assignment-item.checked-in {
  border-color: #4caf50;
  background: #f1f8f4;
}

.assignment-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-bottom: 0.5rem;
}

.check-in-info {
  font-size: 0.875rem;
  color: #4caf50;
}

.check-in-method {
  color: #666;
  font-size: 0.75rem;
}

.assignment-actions {
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
  margin: 0 0 1rem 0;
  color: #333;
}

.instruction {
  color: #666;
  margin-bottom: 1.5rem;
  font-size: 0.875rem;
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

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
}

.share-link-container {
  display: flex;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.share-link-container .form-input {
  flex: 1;
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .map-section {
    height: 400px;
  }
}
</style>
