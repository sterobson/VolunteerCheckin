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
        <button @click="showUploadRoute = true" class="btn btn-secondary">Upload Route</button>
        <button @click="shareEvent" class="btn btn-primary">Share Marshal Link</button>
      </div>
    </header>

    <div class="container">
      <div class="content-grid">
        <div class="map-section">
          <MapView
            :locations="locationStatuses"
            :route="event?.route || []"
            :clickable="true"
            @map-click="handleMapClick"
            @location-click="handleLocationClick"
          />
        </div>

        <div class="sidebar">
          <div class="section">
            <h3>Locations ({{ locations.length }})</h3>
            <div class="button-group">
              <button @click="showAddLocation = true" class="btn btn-small btn-primary">
                Add Location
              </button>
              <button @click="showImportLocations = true" class="btn btn-small btn-secondary">
                Import CSV
              </button>
            </div>

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

          <div class="section">
            <h3>Event Administrators</h3>
            <button @click="showAddAdmin = true" class="btn btn-small btn-primary">
              Add Administrator
            </button>

            <div class="admins-list">
              <div
                v-for="admin in eventAdmins"
                :key="admin.userEmail"
                class="admin-item"
              >
                <div class="admin-info">
                  <strong>{{ admin.userEmail }}</strong>
                  <span class="admin-role">{{ admin.role }}</span>
                </div>
                <button
                  v-if="eventAdmins.length > 1"
                  @click="removeAdmin(admin.userEmail)"
                  class="btn btn-small btn-danger"
                >
                  Remove
                </button>
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
    <div v-if="showAddLocation" class="modal-sidebar">
      <div class="modal-sidebar-content">
        <h2>Add Location</h2>
        <p class="instruction">
          <strong>👆 Click on the map</strong> to set the location, or enter coordinates manually
        </p>

        <div v-if="locationForm.latitude !== 0 && locationForm.longitude !== 0" class="location-set-notice">
          ✓ Location set on map
        </div>

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

    <!-- Add Admin Modal -->
    <div v-if="showAddAdmin" class="modal" @click.self="closeAdminModal">
      <div class="modal-content">
        <h2>Add Event Administrator</h2>
        <p class="instruction">Enter the email address of the administrator to add</p>

        <form @submit.prevent="handleAddAdmin">
          <div class="form-group">
            <label>Email Address</label>
            <input
              v-model="adminForm.email"
              type="email"
              required
              class="form-input"
              placeholder="admin@example.com"
            />
          </div>

          <div class="modal-actions">
            <button type="button" @click="closeAdminModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">Add Administrator</button>
          </div>
        </form>
      </div>
    </div>

    <!-- Upload Route Modal -->
    <div v-if="showUploadRoute" class="modal" @click.self="closeRouteModal">
      <div class="modal-content">
        <h2>Upload GPX Route</h2>
        <p class="instruction">Upload a GPX file to display the event route on the map</p>

        <form @submit.prevent="handleUploadRoute">
          <div class="form-group">
            <label>GPX File</label>
            <input
              type="file"
              accept=".gpx"
              @change="handleFileChange"
              required
              class="form-input"
            />
          </div>

          <div v-if="uploadError" class="error">{{ uploadError }}</div>

          <div class="modal-actions">
            <button type="button" @click="closeRouteModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary" :disabled="uploading">
              {{ uploading ? 'Uploading...' : 'Upload Route' }}
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Import Locations Modal -->
    <div v-if="showImportLocations" class="modal" @click.self="closeImportModal">
      <div class="modal-content">
        <h2>Import Locations from CSV</h2>
        <p class="instruction">Upload a CSV file with columns: Label, Lat/Latitude, Long/Longitude, Marshals (optional)</p>

        <div class="csv-example">
          <strong>Example CSV format:</strong>
          <pre>Label,Latitude,Longitude,Marshals
Checkpoint 1,51.505,-0.09,"John Doe, Jane Smith"
Checkpoint 2,51.510,-0.10,Bob Wilson</pre>
        </div>

        <form @submit.prevent="handleImportLocations">
          <div class="form-group">
            <label>CSV File</label>
            <input
              type="file"
              accept=".csv"
              @change="handleCsvFileChange"
              required
              class="form-input"
            />
          </div>

          <div class="form-group">
            <label class="checkbox-label">
              <input type="checkbox" v-model="deleteExistingLocations" />
              Delete existing locations before import
            </label>
          </div>

          <div v-if="importError" class="error">{{ importError }}</div>
          <div v-if="importResult" class="import-result">
            <p>Created {{ importResult.locationsCreated }} locations and {{ importResult.assignmentsCreated }} assignments</p>
            <div v-if="importResult.errors.length > 0" class="import-errors">
              <strong>Errors:</strong>
              <ul>
                <li v-for="(error, index) in importResult.errors" :key="index">{{ error }}</li>
              </ul>
            </div>
          </div>

          <div class="modal-actions">
            <button type="button" @click="closeImportModal" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary" :disabled="importing">
              {{ importing ? 'Importing...' : 'Import Locations' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi, eventAdminsApi, eventsApi, locationsApi } from '../services/api';
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
const eventAdmins = ref([]);
const showAddAdmin = ref(false);
const showUploadRoute = ref(false);
const selectedGpxFile = ref(null);
const uploading = ref(false);
const uploadError = ref(null);
const showImportLocations = ref(false);
const selectedCsvFile = ref(null);
const deleteExistingLocations = ref(false);
const importing = ref(false);
const importError = ref(null);
const importResult = ref(null);

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

const adminForm = ref({
  email: '',
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

    await loadEventAdmins();
  } catch (error) {
    console.error('Failed to load event data:', error);
  }
};

const loadEventAdmins = async () => {
  try {
    const response = await eventAdminsApi.getAdmins(route.params.eventId);
    eventAdmins.value = response.data;
  } catch (error) {
    console.error('Failed to load event admins:', error);
  }
};

const handleAddAdmin = async () => {
  try {
    await eventAdminsApi.addAdmin(route.params.eventId, adminForm.value.email);
    await loadEventAdmins();
    closeAdminModal();
  } catch (error) {
    console.error('Failed to add admin:', error);
    alert(error.response?.data?.message || 'Failed to add administrator. Please try again.');
  }
};

const removeAdmin = async (userEmail) => {
  if (confirm(`Remove ${userEmail} as an administrator?`)) {
    try {
      await eventAdminsApi.removeAdmin(route.params.eventId, userEmail);
      await loadEventAdmins();
    } catch (error) {
      console.error('Failed to remove admin:', error);
      alert(error.response?.data?.message || 'Failed to remove administrator. Please try again.');
    }
  }
};

const closeAdminModal = () => {
  showAddAdmin.value = false;
  adminForm.value = { email: '' };
};

const handleFileChange = (event) => {
  selectedGpxFile.value = event.target.files[0];
  uploadError.value = null;
};

const handleUploadRoute = async () => {
  if (!selectedGpxFile.value) {
    uploadError.value = 'Please select a GPX file';
    return;
  }

  uploading.value = true;
  uploadError.value = null;

  try {
    await eventsApi.uploadGpx(route.params.eventId, selectedGpxFile.value);
    await loadEventData();
    closeRouteModal();
  } catch (error) {
    console.error('Failed to upload route:', error);
    uploadError.value = error.response?.data?.message || 'Failed to upload route. Please try again.';
  } finally {
    uploading.value = false;
  }
};

const closeRouteModal = () => {
  showUploadRoute.value = false;
  selectedGpxFile.value = null;
  uploadError.value = null;
};

const handleCsvFileChange = (event) => {
  selectedCsvFile.value = event.target.files[0];
  importError.value = null;
  importResult.value = null;
};

const handleImportLocations = async () => {
  if (!selectedCsvFile.value) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importing.value = true;
  importError.value = null;
  importResult.value = null;

  try {
    const response = await locationsApi.importCsv(
      route.params.eventId,
      selectedCsvFile.value,
      deleteExistingLocations.value
    );
    importResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import locations:', error);
    importError.value = error.response?.data?.message || 'Failed to import locations. Please try again.';
  } finally {
    importing.value = false;
  }
};

const closeImportModal = () => {
  showImportLocations.value = false;
  selectedCsvFile.value = null;
  deleteExistingLocations.value = false;
  importError.value = null;
  importResult.value = null;
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
.assignments-list,
.admins-list {
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

.admin-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.admin-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.admin-role {
  font-size: 0.75rem;
  color: #667eea;
  font-weight: 600;
}

.button-group {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
}

.csv-example {
  background: #f5f7fa;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 1.5rem;
}

.csv-example pre {
  margin: 0.5rem 0 0 0;
  font-size: 0.75rem;
  color: #333;
  overflow-x: auto;
}

.import-result {
  background: #f1f8f4;
  border: 1px solid #4caf50;
  padding: 1rem;
  border-radius: 6px;
  margin-top: 1rem;
}

.import-result p {
  margin: 0 0 0.5rem 0;
  color: #4caf50;
  font-weight: 600;
}

.import-errors {
  margin-top: 0.5rem;
}

.import-errors ul {
  margin: 0.5rem 0 0 0;
  padding-left: 1.5rem;
  font-size: 0.875rem;
  color: #c33;
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

.modal-sidebar {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 400px;
  background: white;
  box-shadow: -2px 0 10px rgba(0, 0, 0, 0.3);
  z-index: 1000;
  overflow-y: auto;
}

.modal-sidebar-content {
  padding: 2rem;
}

.location-set-notice {
  background: #f1f8f4;
  color: #4caf50;
  padding: 0.5rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  font-size: 0.875rem;
  font-weight: 600;
}

.instruction {
  background: #f5f7fa;
  padding: 0.75rem;
  border-radius: 6px;
  margin-bottom: 1rem;
  font-size: 0.875rem;
  color: #666;
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
