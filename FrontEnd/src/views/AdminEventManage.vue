<template>
  <div class="admin-event-manage">
    <header class="header">
      <div class="header-left">
        <button @click="goBack" class="btn-back">← Back</button>
        <div v-if="event">
          <h1>{{ event.name }}</h1>
          <p class="event-date">{{ formatEventDate(event.eventDate) }}</p>
        </div>
      </div>
      <div class="header-actions">
        <button @click="showUploadRoute = true" class="btn btn-secondary">Upload route</button>
        <button @click="shareEvent" class="btn btn-primary">Share marshal link</button>
      </div>
    </header>

    <div class="container">
      <!-- Tabs Navigation -->
      <div class="tabs-nav">
        <button
          class="tab-button"
          :class="{ active: activeTab === 'course' }"
          @click="switchTab('course')"
        >
          Course
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'marshals' }"
          @click="switchTab('marshals')"
        >
          Marshals
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'details' }"
          @click="switchTab('details')"
        >
          Event details
        </button>
      </div>

      <!-- Tab Components -->
      <div class="tab-content-wrapper">
        <CourseTab
          v-if="activeTab === 'course'"
          :location-statuses="locationStatuses"
          :route="event?.route || []"
          @map-click="handleMapClick"
          @location-click="handleLocationClick"
          @add-checkpoint="showAddLocation = true"
          @import-checkpoints="showImportLocations = true"
          @select-location="selectLocation"
        />

        <MarshalsTab
          v-if="activeTab === 'marshals'"
          :marshals="marshals"
          :assignments="allAssignments"
          :locations="locationStatuses"
          @add-marshal="addNewMarshal"
          @import-marshals="showImportMarshals = true"
          @select-marshal="selectMarshal"
          @delete-marshal="handleDeleteMarshalFromGrid"
          @change-assignment-status="changeAssignmentStatus"
        />

        <EventDetailsTab
          v-if="activeTab === 'details'"
          :event-data="eventDetailsForm"
          :admins="eventAdmins"
          :form-dirty="eventDetailsFormDirty"
          @submit="handleUpdateEvent"
          @add-admin="showAddAdmin = true"
          @remove-admin="removeAdmin"
          @update:form-dirty="markEventDetailsFormDirty"
        />
      </div>
    </div>

    <!-- Add Location Modal -->
    <div v-if="showAddLocation" class="modal-sidebar">
      <div class="modal-sidebar-content">
        <button @click="tryCloseModal(() => closeLocationModal())" class="modal-close-btn">✕</button>
        <h2>Add location</h2>
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
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>Description</label>
            <input
              v-model="locationForm.description"
              type="text"
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="form-group">
            <label>What3Words (optional)</label>
            <input
              v-model="locationForm.what3Words"
              type="text"
              class="form-input"
              placeholder="e.g. filled.count.soap or filled/count/soap"
              @input="markFormDirty"
            />
            <small v-if="locationForm.what3Words && !isValidWhat3Words(locationForm.what3Words)" class="form-error">
              Invalid format. Must be word.word.word or word/word/word (lowercase letters only, 1-20 characters each)
            </small>
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
                @input="markFormDirty"
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
                @input="markFormDirty"
              />
            </div>
          </div>

          <div class="form-group">
            <label>Required marshals</label>
            <input
              v-model.number="locationForm.requiredMarshals"
              type="number"
              min="1"
              required
              class="form-input"
              @input="markFormDirty"
            />
          </div>

          <div class="modal-actions">
            <button type="button" @click="tryCloseModal(() => closeLocationModal())" class="btn btn-secondary">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary">Add location</button>
          </div>
        </form>
      </div>
    </div>

    <!-- All Modals -->
    <EditLocationModal
      :show="showEditLocation"
      :location="selectedLocation"
      :assignments="selectedLocation?.assignments || []"
      :event-date="event?.eventDate || ''"
      :isDirty="formDirty"
      @close="closeEditLocationModal"
      @save="handleUpdateLocation"
      @delete="handleDeleteLocation"
      @move-location="startMoveLocation"
      @remove-assignment="handleRemoveLocationAssignment"
      @assign-marshal="showAssignMarshalModal = true"
      @update:isDirty="markFormDirty"
    />

    <ShareLinkModal
      :show="showShareLink"
      :link="marshalLink"
      :isDirty="false"
      @close="showShareLink = false"
    />

    <AddAdminModal
      :show="showAddAdmin"
      :isDirty="formDirty"
      @close="closeAdminModal"
      @submit="handleAddAdminSubmit"
      @update:isDirty="markFormDirty"
    />

    <UploadRouteModal
      :show="showUploadRoute"
      :uploading="uploading"
      :error="uploadError"
      :isDirty="formDirty"
      @close="closeRouteModal"
      @submit="handleUploadRouteSubmit"
      @update:isDirty="markFormDirty"
    />

    <ImportLocationsModal
      :show="showImportLocations"
      :importing="importing"
      :error="importError"
      :result="importResult"
      :isDirty="formDirty"
      @close="closeImportModal"
      @submit="handleImportLocationsSubmit"
      @update:isDirty="markFormDirty"
    />

    <EditMarshalModal
      :show="showEditMarshal"
      :marshal="selectedMarshal"
      :assignments="getMarshalAssignmentsForEdit()"
      :availableLocations="availableMarshalLocations"
      :isEditing="!!selectedMarshal"
      :isDirty="formDirty"
      @close="closeEditMarshalModal"
      @save="handleSaveMarshal"
      @delete="handleDeleteMarshal"
      @remove-assignment="handleRemoveMarshalAssignment"
      @assign-to-location="assignMarshalToLocation"
      @update:isDirty="markFormDirty"
    />

    <ImportMarshalsModal
      :show="showImportMarshals"
      :importing="importingMarshals"
      :error="importError"
      :result="importMarshalsResult"
      :isDirty="formDirty"
      @close="closeImportMarshalsModal"
      @submit="handleImportMarshalsSubmit"
      @update:isDirty="markFormDirty"
    />

    <AssignmentConflictModal
      :show="showAssignmentConflict"
      :conflictData="assignmentConflictData"
      @close="showAssignmentConflict = false"
      @choice="handleConflictChoice"
    />

    <AssignMarshalModal
      :show="showAssignMarshalModal"
      :locationName="selectedLocation?.name || ''"
      :marshals="availableMarshalsForAssignment"
      @close="closeAssignMarshalModal"
      @assign-existing="handleAssignExistingMarshal"
      @create-and-assign="handleAddNewMarshalInline"
    />

    <ShiftCheckpointTimesModal
      :show="showShiftCheckpointTimes"
      :old-event-date="oldEventDateForShift"
      :new-event-date="newEventDateForShift"
      :affected-checkpoints-count="checkpointsWithCustomTimes"
      @confirm="handleShiftCheckpointTimesConfirm"
      @cancel="handleShiftCheckpointTimesCancel"
    />

    <!-- Move Checkpoint Overlay -->
    <div v-if="isMovingLocation" class="move-checkpoint-overlay">
      <div class="move-checkpoint-instructions">
        <h3>Move checkpoint: {{ editLocationForm.name }}</h3>
        <p>Click on the map to set the new location for this checkpoint</p>
        <div class="move-checkpoint-actions">
          <button @click="cancelMoveLocation" class="btn btn-danger">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <InfoModal
      :show="showInfoModal"
      :title="infoModalTitle"
      :message="infoModalMessage"
      @close="showInfoModal = false"
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
// NOTE: This file has been refactored to use tab components
// Original file: 2,981 lines → Current file: ~900 lines (70% reduction)
// Tab components: CourseTab.vue, MarshalsTab.vue, EventDetailsTab.vue

import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi } from '../services/api';
import { startSignalRConnection, stopSignalRConnection } from '../services/signalr';

// New composables and utilities
import { useTabs } from '../composables/useTabs';
import { useConfirmDialog } from '../composables/useConfirmDialog';
import { formatDate as formatEventDate, formatDateForInput } from '../utils/dateFormatters';
import { isValidWhat3Words } from '../utils/validators';
import { calculateDistance } from '../utils/coordinateUtils';
import { CHECK_IN_RADIUS_METERS } from '../constants/app';

// Tab Components
import CourseTab from './AdminEventManage/CourseTab.vue';
import MarshalsTab from './AdminEventManage/MarshalsTab.vue';
import EventDetailsTab from './AdminEventManage/EventDetailsTab.vue';

// Modal Components
import ShareLinkModal from '../components/event-manage/modals/ShareLinkModal.vue';
import AddAdminModal from '../components/event-manage/modals/AddAdminModal.vue';
import UploadRouteModal from '../components/event-manage/modals/UploadRouteModal.vue';
import ImportLocationsModal from '../components/event-manage/modals/ImportLocationsModal.vue';
import ImportMarshalsModal from '../components/event-manage/modals/ImportMarshalsModal.vue';
import EditLocationModal from '../components/event-manage/modals/EditLocationModal.vue';
import EditMarshalModal from '../components/event-manage/modals/EditMarshalModal.vue';
import AssignmentConflictModal from '../components/event-manage/modals/AssignmentConflictModal.vue';
import AssignMarshalModal from '../components/event-manage/modals/AssignMarshalModal.vue';
import ShiftCheckpointTimesModal from '../components/event-manage/modals/ShiftCheckpointTimesModal.vue';
import InfoModal from '../components/InfoModal.vue';
import ConfirmModal from '../components/ConfirmModal.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

// Use composables
const { activeTab, switchTab } = useTabs('course', ['course', 'marshals', 'details']);
const {
  showConfirmModal,
  confirmModalTitle,
  confirmModalMessage,
  showConfirm,
  handleConfirmModalConfirm,
  handleConfirmModalCancel,
} = useConfirmDialog();

// State
const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);
const selectedLocation = ref(null);
const showAddLocation = ref(false);
const showShareLink = ref(false);
const eventAdmins = ref([]);
const showAddAdmin = ref(false);
const showUploadRoute = ref(false);
const uploading = ref(false);
const uploadError = ref(null);
const showImportLocations = ref(false);
const showImportMarshals = ref(false);
const importing = ref(false);
const importingMarshals = ref(false);
const importError = ref(null);
const importResult = ref(null);
const importMarshalsResult = ref(null);
const showEditLocation = ref(false);
const isMovingLocation = ref(false);
const editLocationForm = ref({
  id: '',
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  what3Words: '',
});
const showAssignMarshalModal = ref(false);
const showAssignmentConflict = ref(false);
const assignmentConflictData = ref({
  marshalName: '',
  locations: [],
  marshal: null,
});
const conflictResolveCallback = ref(null);
const marshals = ref([]);
const showEditMarshal = ref(false);
const selectedMarshal = ref(null);
const editMarshalForm = ref({
  id: '',
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});
const formDirty = ref(false);
const pendingAssignments = ref([]);
const pendingDeleteAssignments = ref([]);
const pendingMarshalAssignments = ref([]);
const pendingMarshalDeleteAssignments = ref([]);
const showInfoModal = ref(false);
const infoModalTitle = ref('');
const infoModalMessage = ref('');
const eventDetailsForm = ref({
  name: '',
  description: '',
  eventDate: '',
  timeZoneId: 'UTC',
});
const eventDetailsFormDirty = ref(false);
const showShiftCheckpointTimes = ref(false);
const oldEventDateForShift = ref('');
const newEventDateForShift = ref('');
const checkpointsWithCustomTimes = ref(0);
const locationForm = ref({
  name: '',
  description: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  what3Words: '',
});

// Computed
const marshalLink = computed(() => {
  return `${window.location.origin}/event/${route.params.eventId}`;
});

const allAssignments = computed(() => {
  const assignments = [];
  locationStatuses.value.forEach(location => {
    if (location.assignments) {
      assignments.push(...location.assignments);
    }
  });
  return assignments;
});

const availableMarshalLocations = computed(() => {
  if (!selectedMarshal.value) return locationStatuses.value;

  const assignedLocationIds = selectedMarshal.value.assignedLocationIds || [];
  const pendingLocationIds = pendingMarshalAssignments.value.map(p => p.locationId);
  const deleteLocationIds = pendingMarshalDeleteAssignments.value;

  return locationStatuses.value.filter(
    loc => !assignedLocationIds.includes(loc.id) &&
           !pendingLocationIds.includes(loc.id) ||
           deleteLocationIds.includes(loc.id)
  );
});

const availableMarshalsForAssignment = computed(() => {
  let marshalsList = marshals.value;

  if (selectedLocation.value) {
    const assignedMarshalNames = selectedLocation.value.assignments.map(a => a.marshalName);
    const pendingMarshalNames = pendingAssignments.value.map(p => p.marshalName);
    marshalsList = marshals.value.filter(
      marshal => !assignedMarshalNames.includes(marshal.name) && !pendingMarshalNames.includes(marshal.name)
    );
  }

  return marshalsList.slice().sort((a, b) => a.name.localeCompare(b.name));
});

// Methods
const goBack = () => {
  router.push('/admin/dashboard');
};

const shareEvent = () => {
  showShareLink.value = true;
};

const markFormDirty = () => {
  formDirty.value = true;
};

const markEventDetailsFormDirty = () => {
  eventDetailsFormDirty.value = true;
};

const handleUpdateEvent = async () => {
  try {
    const oldEventDate = event.value.eventDate;
    const newEventDate = eventDetailsForm.value.eventDate;

    if (oldEventDate !== newEventDate) {
      const checkpointsWithTimes = locations.value.filter(
        loc => loc.startTime || loc.endTime
      );

      if (checkpointsWithTimes.length > 0) {
        oldEventDateForShift.value = oldEventDate;
        newEventDateForShift.value = newEventDate;
        checkpointsWithCustomTimes.value = checkpointsWithTimes.length;
        showShiftCheckpointTimes.value = true;
        return;
      }
    }

    await performEventUpdate(false, null);
  } catch (error) {
    console.error('Failed to update event:', error);
    alert('Failed to update event. Please try again.');
  }
};

const performEventUpdate = async (shouldShiftCheckpoints, timeDeltaMs) => {
  try {
    await eventsStore.updateEvent(route.params.eventId, {
      ...eventDetailsForm.value,
    });

    if (shouldShiftCheckpoints && timeDeltaMs !== null) {
      const totalSeconds = Math.floor(Math.abs(timeDeltaMs) / 1000);
      const isNegative = timeDeltaMs < 0;
      const hours = Math.floor(totalSeconds / 3600);
      const minutes = Math.floor((totalSeconds % 3600) / 60);
      const seconds = totalSeconds % 60;

      const timeSpanString = `${isNegative ? '-' : ''}${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;

      await eventsStore.bulkUpdateLocationTimes(route.params.eventId, timeSpanString);
    }

    await loadEventData();
    eventDetailsFormDirty.value = false;
  } catch (error) {
    console.error('Failed to update event:', error);
    alert('Failed to update event. Please try again.');
  }
};

const handleShiftCheckpointTimesConfirm = async ({ shouldShift, timeDelta }) => {
  showShiftCheckpointTimes.value = false;
  await performEventUpdate(shouldShift, timeDelta);
};

const handleShiftCheckpointTimesCancel = () => {
  showShiftCheckpointTimes.value = false;
  loadEventData();
};

const tryCloseModal = (closeFunction) => {
  if (formDirty.value || pendingAssignments.value.length > 0 || pendingDeleteAssignments.value.length > 0 ||
      pendingMarshalAssignments.value.length > 0 || pendingMarshalDeleteAssignments.value.length > 0) {
    showConfirm('Unsaved Changes', 'You have unsaved changes. Are you sure you want to close without saving?', async () => {
      formDirty.value = false;
      pendingAssignments.value = [];
      pendingDeleteAssignments.value = [];
      pendingMarshalAssignments.value = [];
      pendingMarshalDeleteAssignments.value = [];
      // Reload data to revert optimistic UI updates
      await loadEventData();
      closeFunction();
    });
  } else {
    closeFunction();
  }
};

const loadEventData = async () => {
  try {
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    if (event.value) {
      eventDetailsForm.value = {
        name: event.value.name || '',
        description: event.value.description || '',
        eventDate: formatDateForInput(event.value.eventDate),
        timeZoneId: event.value.timeZoneId || 'UTC',
      };
      eventDetailsFormDirty.value = false;
    }

    await eventsStore.fetchLocations(route.params.eventId);
    locations.value = eventsStore.locations;

    await eventsStore.fetchEventStatus(route.params.eventId);
    locationStatuses.value = eventsStore.eventStatus.locations;

    await loadEventAdmins();
    await loadMarshals();
  } catch (error) {
    console.error('Failed to load event data:', error);
  }
};

const loadMarshals = async () => {
  try {
    const response = await marshalsApi.getByEvent(route.params.eventId);
    marshals.value = response.data;
  } catch (error) {
    console.error('Failed to load marshals:', error);
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

const handleAddAdminSubmit = async (email) => {
  try {
    await eventAdminsApi.addAdmin(route.params.eventId, email);
    await loadEventAdmins();
    closeAdminModal();
  } catch (error) {
    console.error('Failed to add admin:', error);
    alert(error.response?.data?.message || 'Failed to add administrator. Please try again.');
  }
};

const removeAdmin = async (userEmail) => {
  showConfirm('Remove Administrator', `Remove ${userEmail} as an administrator?`, async () => {
    try {
      await eventAdminsApi.removeAdmin(route.params.eventId, userEmail);
      await loadEventAdmins();
    } catch (error) {
      console.error('Failed to remove admin:', error);
      alert(error.response?.data?.message || 'Failed to remove administrator. Please try again.');
    }
  });
};

const closeAdminModal = () => {
  showAddAdmin.value = false;
  formDirty.value = false;
};

const handleUploadRouteSubmit = async (file) => {
  if (!file) {
    uploadError.value = 'Please select a GPX file';
    return;
  }

  uploading.value = true;
  uploadError.value = null;

  try {
    await eventsApi.uploadGpx(route.params.eventId, file);
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
  uploadError.value = null;
};

const handleImportLocationsSubmit = async ({ file, deleteExisting }) => {
  if (!file) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importing.value = true;
  importError.value = null;
  importResult.value = null;

  try {
    const response = await locationsApi.importCsv(
      route.params.eventId,
      file,
      deleteExisting
    );
    importResult.value = response.data;
    await loadEventData();

    closeImportModal();

    const result = response.data;
    let message = `<p>Created <strong>${result.locationsCreated}</strong> location(s) and <strong>${result.assignmentsCreated}</strong> assignment(s)</p>`;

    if (result.errors && result.errors.length > 0) {
      message += '<br><p><strong>Errors:</strong></p><ul style="margin: 0; padding-left: 1.5rem;">';
      result.errors.forEach(err => {
        message += `<li>${err}</li>`;
      });
      message += '</ul>';
    }

    infoModalTitle.value = 'Import Complete';
    infoModalMessage.value = message;
    showInfoModal.value = true;
  } catch (error) {
    console.error('Failed to import locations:', error);
    importError.value = error.response?.data?.message || 'Failed to import locations. Please try again.';
  } finally {
    importing.value = false;
  }
};

const closeImportModal = () => {
  showImportLocations.value = false;
  importError.value = null;
  importResult.value = null;
};

const handleMapClick = (coords) => {
  if (showAddLocation.value) {
    locationForm.value.latitude = coords.lat;
    locationForm.value.longitude = coords.lng;
    markFormDirty();
  } else if (isMovingLocation.value) {
    editLocationForm.value.latitude = coords.lat;
    editLocationForm.value.longitude = coords.lng;
    isMovingLocation.value = false;
    showEditLocation.value = true;
    markFormDirty();
  } else {
    const nearbyCheckpoint = locationStatuses.value.find(location => {
      const distance = calculateDistance(
        coords.lat,
        coords.lng,
        location.latitude,
        location.longitude
      );
      return distance <= CHECK_IN_RADIUS_METERS;
    });

    if (!nearbyCheckpoint) {
      locationForm.value.latitude = coords.lat;
      locationForm.value.longitude = coords.lng;
      showAddLocation.value = true;
      markFormDirty();
    }
  }
};

const handleLocationClick = (location) => {
  selectLocation(location);
};

const selectLocation = (location) => {
  selectedLocation.value = location;
  editLocationForm.value = {
    id: location.id,
    name: location.name,
    description: location.description || '',
    latitude: location.latitude,
    longitude: location.longitude,
    requiredMarshals: location.requiredMarshals,
    what3Words: location.what3Words || '',
  };
  pendingAssignments.value = [];
  pendingDeleteAssignments.value = [];
  formDirty.value = false;
  showEditLocation.value = true;
};

const handleSaveLocation = async () => {
  if (!isValidWhat3Words(locationForm.value.what3Words)) {
    alert('Invalid What3Words format. Please use word.word.word or word/word/word (lowercase letters only, 1-20 characters each)');
    return;
  }

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
    what3Words: '',
  };
  formDirty.value = false;
};

const closeEditLocationModal = () => {
  showEditLocation.value = false;
  selectedLocation.value = null;
  isMovingLocation.value = false;
  showAssignMarshalModal.value = false;
  editLocationForm.value = {
    id: '',
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };
  pendingAssignments.value = [];
  pendingDeleteAssignments.value = [];
  formDirty.value = false;
};

const closeAssignMarshalModal = () => {
  showAssignMarshalModal.value = false;
};

const startMoveLocation = () => {
  isMovingLocation.value = true;
  showEditLocation.value = false;
  switchTab('course');
};

const cancelMoveLocation = () => {
  isMovingLocation.value = false;
  showEditLocation.value = true;
};

const handleUpdateLocation = async (formData) => {
  if (!isValidWhat3Words(formData.what3Words)) {
    alert('Invalid What3Words format.');
    return;
  }

  try {
    await locationsApi.update(
      route.params.eventId,
      selectedLocation.value.id,
      {
        eventId: route.params.eventId,
        name: formData.name,
        description: formData.description,
        latitude: formData.latitude,
        longitude: formData.longitude,
        requiredMarshals: formData.requiredMarshals,
        what3Words: formData.what3Words || null,
        startTime: formData.startTime,
        endTime: formData.endTime,
      }
    );

    for (const assignmentId of pendingDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    for (const pending of pendingAssignments.value) {
      await eventsStore.createAssignment({
        eventId: route.params.eventId,
        locationId: selectedLocation.value.id,
        marshalId: pending.marshalId,
      });
    }

    // Process check-in status changes
    if (formData.checkInChanges && formData.checkInChanges.length > 0) {
      for (const change of formData.checkInChanges) {
        // Find the current assignment to check its status
        const assignment = selectedLocation.value.assignments.find(a => a.id === change.assignmentId);
        if (assignment) {
          // Only call API if the status needs to change
          if (assignment.isCheckedIn !== change.shouldBeCheckedIn) {
            await checkInApi.adminCheckIn(route.params.eventId, change.assignmentId);
          }
        }
      }
    }

    await loadEventData();
    closeEditLocationModal();
  } catch (error) {
    console.error('Failed to update location:', error);
    alert('Failed to update location. Please try again.');
  }
};

const handleDeleteLocation = async (locationId) => {
  showConfirm('Delete Location', 'Are you sure you want to delete this location?', async () => {
    try {
      await locationsApi.delete(route.params.eventId, locationId);
      await loadEventData();
      closeEditLocationModal();
    } catch (error) {
      console.error('Failed to delete location:', error);
      alert('Failed to delete location. Please try again.');
    }
  });
};

const toggleCheckIn = async (assignment) => {
  try {
    await checkInApi.adminCheckIn(route.params.eventId, assignment.id);
    await loadEventData();

    // Update selectedLocation if the modal is open
    if (selectedLocation.value) {
      const updatedLocation = locationStatuses.value.find(l => l.id === selectedLocation.value.id);
      if (updatedLocation) {
        selectedLocation.value = updatedLocation;
      }
    }
  } catch (error) {
    console.error('Failed to toggle check-in:', error);
    alert('Failed to toggle check-in. Please try again.');
  }
};

const handleRemoveLocationAssignment = (assignmentId) => {
  // Check if this is a pending assignment (has temp ID)
  if (assignmentId.toString().startsWith('temp-')) {
    // Remove from pending assignments list
    const assignment = selectedLocation.value.assignments.find(a => a.id === assignmentId);
    if (assignment) {
      pendingAssignments.value = pendingAssignments.value.filter(
        p => p.marshalId !== assignment.marshalId
      );
    }
    // Optimistically remove from UI
    selectedLocation.value.assignments = selectedLocation.value.assignments.filter(
      a => a.id !== assignmentId
    );
    markFormDirty();
  } else {
    // For existing assignments, add to pending deletions
    if (!pendingDeleteAssignments.value.includes(assignmentId)) {
      pendingDeleteAssignments.value.push(assignmentId);
      // Optimistically remove from UI
      selectedLocation.value.assignments = selectedLocation.value.assignments.filter(
        a => a.id !== assignmentId
      );
      markFormDirty();
    }
  }
};

const handleAssignExistingMarshal = async (marshalId) => {
  const marshal = marshals.value.find(m => m.id === marshalId);
  if (!marshal) return;

  // Check if marshal is already assigned to other locations
  const assignedLocations = locationStatuses.value.filter(loc =>
    loc.assignments?.some(a => a.marshalId === marshalId && loc.id !== selectedLocation.value.id)
  );

  if (assignedLocations.length > 0) {
    // Show conflict modal and wait for user choice
    assignmentConflictData.value = {
      marshalName: marshal.name,
      locations: assignedLocations.map(loc => loc.name),
      marshal: marshal,
    };

    const choice = await new Promise((resolve) => {
      conflictResolveCallback.value = resolve;
      showAssignmentConflict.value = true;
    });

    if (choice === 'cancel' || choice === 'choose-other') {
      // Keep the assign marshal modal open for 'choose-other'
      return;
    }

    if (choice === 'move') {
      // Mark existing assignments for removal (will be deleted on save)
      for (const loc of assignedLocations) {
        const assignment = loc.assignments.find(a => a.marshalId === marshalId);
        if (assignment) {
          pendingDeleteAssignments.value.push(assignment.id);
          // Optimistically remove from UI
          const locationIndex = locationStatuses.value.findIndex(l => l.id === loc.id);
          if (locationIndex !== -1) {
            locationStatuses.value[locationIndex].assignments =
              locationStatuses.value[locationIndex].assignments.filter(a => a.id !== assignment.id);
          }
        }
      }
    }
    // If choice is 'both', just continue to add the pending assignment
  }

  // Add to pending assignments
  pendingAssignments.value.push({
    marshalId: marshal.id,
    marshalName: marshal.name,
  });

  // Optimistically update UI - add temporary assignment to selectedLocation
  if (!selectedLocation.value.assignments) {
    selectedLocation.value.assignments = [];
  }
  selectedLocation.value.assignments.push({
    id: `temp-${Date.now()}`, // Temporary ID
    marshalId: marshal.id,
    marshalName: marshal.name,
    locationId: selectedLocation.value.id,
    isCheckedIn: false,
    isPending: true, // Flag to identify pending assignments
  });

  markFormDirty();
  closeAssignMarshalModal();
};

const handleAddNewMarshalInline = async (marshalData) => {
  try {
    const response = await marshalsApi.create({
      eventId: route.params.eventId,
      name: marshalData.name,
      email: marshalData.email,
      phoneNumber: marshalData.phoneNumber,
    });
    const newMarshal = response.data;

    await loadMarshals();

    pendingAssignments.value.push({
      marshalId: newMarshal.id,
      marshalName: newMarshal.name,
    });

    markFormDirty();
    closeAssignMarshalModal();
  } catch (error) {
    console.error('Failed to create marshal:', error);
    alert('Failed to create marshal. Please try again.');
  }
};

const addNewMarshal = () => {
  selectedMarshal.value = null;
  editMarshalForm.value = {
    id: '',
    name: '',
    email: '',
    phoneNumber: '',
    notes: '',
  };
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  formDirty.value = false;
  showEditMarshal.value = true;
};

const selectMarshal = (marshal) => {
  selectedMarshal.value = marshal;
  editMarshalForm.value = {
    id: marshal.id,
    name: marshal.name,
    email: marshal.email || '',
    phoneNumber: marshal.phoneNumber || '',
    notes: marshal.notes || '',
  };
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  formDirty.value = false;
  showEditMarshal.value = true;
};

const getMarshalAssignmentsForEdit = () => {
  if (!selectedMarshal.value) return [];

  const assignments = [];
  locationStatuses.value.forEach(location => {
    const marshalAssignments = location.assignments.filter(
      a => a.marshalName === selectedMarshal.value.name
    );
    marshalAssignments.forEach(assignment => {
      assignments.push({
        ...assignment,
        locationName: location.name,
      });
    });
  });

  return assignments;
};

const handleSaveMarshal = async (formData) => {
  try {
    if (selectedMarshal.value) {
      await marshalsApi.update(
        route.params.eventId,
        selectedMarshal.value.id,
        {
          name: formData.name,
          email: formData.email,
          phoneNumber: formData.phoneNumber,
          notes: formData.notes,
        }
      );
    } else {
      await marshalsApi.create({
        eventId: route.params.eventId,
        name: formData.name,
        email: formData.email,
        phoneNumber: formData.phoneNumber,
        notes: formData.notes,
      });
    }

    for (const assignmentId of pendingMarshalDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    for (const pending of pendingMarshalAssignments.value) {
      await eventsStore.createAssignment({
        eventId: route.params.eventId,
        locationId: pending.locationId,
        marshalId: selectedMarshal.value.id,
      });
    }

    // Process check-in status changes
    if (formData.checkInChanges && formData.checkInChanges.length > 0) {
      const marshalAssignments = getMarshalAssignmentsForEdit();
      for (const change of formData.checkInChanges) {
        // Find the current assignment to check its status
        const assignment = marshalAssignments.find(a => a.id === change.assignmentId);
        if (assignment) {
          // Only call API if the status needs to change
          if (assignment.isCheckedIn !== change.shouldBeCheckedIn) {
            await checkInApi.adminCheckIn(route.params.eventId, change.assignmentId);
          }
        }
      }
    }

    await loadEventData();
    closeEditMarshalModal();
  } catch (error) {
    console.error('Failed to save marshal:', error);
    alert('Failed to save marshal. Please try again.');
  }
};

const handleDeleteMarshal = async (marshalId) => {
  showConfirm('Delete Marshal', 'Are you sure you want to delete this marshal?', async () => {
    try {
      await marshalsApi.delete(route.params.eventId, marshalId);
      await loadEventData();
      closeEditMarshalModal();
    } catch (error) {
      console.error('Failed to delete marshal:', error);
      alert('Failed to delete marshal. Please try again.');
    }
  });
};

const handleDeleteMarshalFromGrid = (marshal) => {
  handleDeleteMarshal(marshal.id);
};

const toggleMarshalCheckIn = async (assignment) => {
  await toggleCheckIn(assignment);
};

const handleRemoveMarshalAssignment = (assignmentId) => {
  if (!pendingMarshalDeleteAssignments.value.includes(assignmentId)) {
    pendingMarshalDeleteAssignments.value.push(assignmentId);
    markFormDirty();
  }
};

const assignMarshalToLocation = (locationId) => {
  pendingMarshalAssignments.value.push({
    locationId,
  });
  markFormDirty();
};

const closeEditMarshalModal = () => {
  showEditMarshal.value = false;
  selectedMarshal.value = null;
  editMarshalForm.value = {
    id: '',
    name: '',
    email: '',
    phoneNumber: '',
    notes: '',
  };
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  formDirty.value = false;
};

const handleImportMarshalsSubmit = async ({ file }) => {
  if (!file) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importingMarshals.value = true;
  importError.value = null;
  importMarshalsResult.value = null;

  try {
    const response = await marshalsApi.importCsv(route.params.eventId, file);
    importMarshalsResult.value = response.data;
    await loadEventData();

    closeImportMarshalsModal();

    const result = response.data;
    let message = `<p>Created <strong>${result.marshalsCreated}</strong> marshal(s) and <strong>${result.assignmentsCreated}</strong> assignment(s)</p>`;

    if (result.errors && result.errors.length > 0) {
      message += '<br><p><strong>Errors:</strong></p><ul style="margin: 0; padding-left: 1.5rem;">';
      result.errors.forEach(err => {
        message += `<li>${err}</li>`;
      });
      message += '</ul>';
    }

    infoModalTitle.value = 'Import Complete';
    infoModalMessage.value = message;
    showInfoModal.value = true;
  } catch (error) {
    console.error('Failed to import marshals:', error);
    importError.value = error.response?.data?.message || 'Failed to import marshals. Please try again.';
  } finally {
    importingMarshals.value = false;
  }
};

const closeImportMarshalsModal = () => {
  showImportMarshals.value = false;
  importError.value = null;
  importMarshalsResult.value = null;
};

const handleConflictChoice = (choice) => {
  if (conflictResolveCallback.value) {
    conflictResolveCallback.value(choice);
    conflictResolveCallback.value = null;
  }
  showAssignmentConflict.value = false;
};

const changeAssignmentStatus = async ({ assignment, status }) => {
  try {
    if (status === 'not-checked-in') {
      if (assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(route.params.eventId, assignment.id);
      }
    } else {
      if (!assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(route.params.eventId, assignment.id);
      }
    }

    await loadEventData();

    // Update selectedLocation if the modal is open
    if (selectedLocation.value) {
      const updatedLocation = locationStatuses.value.find(l => l.id === selectedLocation.value.id);
      if (updatedLocation) {
        selectedLocation.value = updatedLocation;
      }
    }
  } catch (error) {
    console.error('Failed to change assignment status:', error);
    alert('Failed to change status. Please try again.');
  }
};

const getLocationName = (locationId) => {
  const location = locationStatuses.value.find(l => l.id === locationId);
  return location ? location.name : 'Unknown';
};

// Lifecycle
onMounted(async () => {
  await loadEventData();

  try {
    await startSignalRConnection(route.params.eventId, async () => {
      await loadEventData();
    });
  } catch (error) {
    console.error('Failed to start SignalR connection:', error);
  }
});

onUnmounted(() => {
  stopSignalRConnection();
});
</script>

<style scoped>
/* Preserved styles from original file */
.admin-event-manage {
  min-height: 100vh;
  background: #f5f7fa;
}

.header {
  background: white;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #dee2e6;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.btn-back {
  background: none;
  border: none;
  color: #007bff;
  cursor: pointer;
  font-size: 1rem;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.btn-back:hover {
  background: #e7f3ff;
}

.header h1 {
  margin: 0;
  font-size: 1.75rem;
  color: #333;
}

.event-date {
  margin: 0.25rem 0 0 0;
  color: #666;
  font-size: 0.9rem;
}

.header-actions {
  display: flex;
  gap: 1rem;
}

.container {
  padding: 2rem;
  max-width: 1600px;
  margin: 0 auto;
}

.tabs-nav {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid #dee2e6;
}

.tab-button {
  padding: 1rem 1.5rem;
  border: none;
  background: transparent;
  color: #666;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  border-bottom: 3px solid transparent;
  transition: all 0.2s;
  position: relative;
  bottom: -2px;
}

.tab-button:hover {
  color: #333;
  background: #f8f9fa;
}

.tab-button.active {
  color: #007bff;
  border-bottom-color: #007bff;
}

.tab-content-wrapper {
  animation: fadeIn 0.3s ease-in;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Modal Sidebar */
.modal-sidebar {
  position: fixed;
  top: 0;
  right: 0;
  width: 450px;
  height: 100vh;
  background: white;
  box-shadow: -2px 0 8px rgba(0, 0, 0, 0.15);
  z-index: 1001;
  overflow-y: auto;
  animation: slideIn 0.3s ease-out;
}

@keyframes slideIn {
  from {
    transform: translateX(100%);
  }
  to {
    transform: translateX(0);
  }
}

.modal-sidebar-content {
  padding: 2rem;
}

.modal-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  background: none;
  border: none;
  font-size: 1.5rem;
  color: #666;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.modal-close-btn:hover {
  background: #f8f9fa;
}

.modal-sidebar h2 {
  margin: 0 0 1.5rem 0;
  font-size: 1.5rem;
  color: #333;
}

.instruction {
  background: #e7f3ff;
  padding: 1rem;
  border-radius: 8px;
  margin-bottom: 1.5rem;
  color: #0056b3;
  font-size: 0.9rem;
}

.location-set-notice {
  background: #d4edda;
  color: #155724;
  padding: 0.75rem;
  border-radius: 4px;
  margin-bottom: 1rem;
  font-size: 0.9rem;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  margin-top: 2rem;
  padding-top: 2rem;
  border-top: 1px solid #dee2e6;
}

.modal-actions button {
  flex: 1;
}

/* Move Checkpoint Overlay */
.move-checkpoint-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 1000;
  display: flex;
  align-items: center;
  justify-content: center;
}

.move-checkpoint-instructions {
  background: white;
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  max-width: 500px;
  text-align: center;
}

.move-checkpoint-instructions h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.move-checkpoint-instructions p {
  margin: 0 0 1.5rem 0;
  color: #666;
}

.move-checkpoint-actions {
  display: flex;
  justify-content: center;
  gap: 1rem;
}

@media (max-width: 768px) {
  .header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .header-actions {
    width: 100%;
    flex-direction: column;
  }

  .header-actions button {
    width: 100%;
  }

  .container {
    padding: 1rem;
  }

  .tabs-nav {
    overflow-x: auto;
  }

  .modal-sidebar {
    width: 100%;
  }
}
</style>
