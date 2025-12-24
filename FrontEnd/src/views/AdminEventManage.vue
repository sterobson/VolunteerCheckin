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
          @click="activeTab = 'course'"
        >
          Course
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'marshals' }"
          @click="activeTab = 'marshals'"
        >
          Marshals
        </button>
        <button
          class="tab-button"
          :class="{ active: activeTab === 'details' }"
          @click="activeTab = 'details'"
        >
          Event details
        </button>
      </div>

      <!-- Details Tab -->
      <div v-if="activeTab === 'details'" class="tab-content-wrapper">
        <div class="details-container">
          <div class="details-section">
            <h2>Event Information</h2>
            <form @submit.prevent="handleUpdateEvent" class="event-details-form">
              <div class="form-group">
                <label>Event Name</label>
                <input
                  v-model="eventDetailsForm.name"
                  type="text"
                  required
                  class="form-input"
                  @input="markEventDetailsFormDirty"
                />
              </div>

              <div class="form-group">
                <label>Description</label>
                <textarea
                  v-model="eventDetailsForm.description"
                  rows="3"
                  class="form-input"
                  @input="markEventDetailsFormDirty"
                ></textarea>
              </div>

              <div class="form-group">
                <label>Event Date & Time</label>
                <input
                  v-model="eventDetailsForm.eventDate"
                  type="datetime-local"
                  required
                  class="form-input"
                  @input="markEventDetailsFormDirty"
                />
              </div>

              <div class="form-group">
                <label>Time Zone</label>
                <select
                  v-model="eventDetailsForm.timeZoneId"
                  required
                  class="form-input"
                  @change="markEventDetailsFormDirty"
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

              <div class="form-actions">
                <button type="submit" class="btn btn-primary" :disabled="!eventDetailsFormDirty">
                  Save Changes
                </button>
              </div>
            </form>
          </div>

          <div class="details-section">
            <AdminsList
              :admins="eventAdmins"
              @add-admin="showAddAdmin = true"
              @remove-admin="removeAdmin"
            />
          </div>
        </div>
      </div>

      <!-- Course Tab -->
      <div v-if="activeTab === 'course'" class="tab-content-wrapper">
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
              <CheckpointsList
                :locations="locationStatuses"
                @add-checkpoint="showAddLocation = true"
                @import-checkpoints="showImportLocations = true"
                @select-location="selectLocation"
              />
            </div>
          </div>
        </div>
      </div>

      <!-- Marshals Tab -->
      <div v-if="activeTab === 'marshals'" class="tab-content-wrapper">
        <div class="marshals-tab-header">
          <h2>Marshals management</h2>
          <div class="button-group">
            <button @click="addNewMarshal" class="btn btn-primary">
              Add marshal
            </button>
            <button @click="showImportMarshals = true" class="btn btn-secondary">
              Import CSV
            </button>
          </div>
        </div>

        <div class="marshals-grid">
          <table class="marshals-table">
            <thead>
              <tr>
                <th @click="changeMarshalSort('name')" class="sortable">
                  Name
                  <span class="sort-indicator" v-if="marshalSortBy === 'name'">
                    {{ marshalSortOrder === 'asc' ? '▲' : '▼' }}
                  </span>
                </th>
                <th @click="changeMarshalSort('email')" class="sortable hide-on-mobile">
                  Email
                  <span class="sort-indicator" v-if="marshalSortBy === 'email'">
                    {{ marshalSortOrder === 'asc' ? '▲' : '▼' }}
                  </span>
                </th>
                <th @click="changeMarshalSort('phone')" class="sortable hide-on-mobile">
                  Phone
                  <span class="sort-indicator" v-if="marshalSortBy === 'phone'">
                    {{ marshalSortOrder === 'asc' ? '▲' : '▼' }}
                  </span>
                </th>
                <th @click="changeMarshalSort('checkpoint')" class="sortable">
                  Checkpoint
                  <span class="sort-indicator" v-if="marshalSortBy === 'checkpoint'">
                    {{ marshalSortOrder === 'asc' ? '▲' : '▼' }}
                  </span>
                </th>
                <th @click="changeMarshalSort('status')" class="sortable">
                  Status
                  <span class="sort-indicator" v-if="marshalSortBy === 'status'">
                    {{ marshalSortOrder === 'asc' ? '▲' : '▼' }}
                  </span>
                </th>
                <th class="hide-on-mobile">Actions</th>
              </tr>
            </thead>
            <tbody>
              <template v-for="marshal in sortedMarshals" :key="marshal.id">
                <template v-if="getMarshalAssignments(marshal.id).length > 0">
                  <tr
                    v-for="(assignment, index) in getMarshalAssignments(marshal.id)"
                    :key="`${marshal.id}-${assignment.id}`"
                    class="marshal-row"
                    @click="selectMarshal(marshal)"
                  >
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length">
                      {{ marshal.name }}
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      {{ marshal.email || '-' }}
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      {{ marshal.phoneNumber || '-' }}
                    </td>
                    <td>{{ getLocationName(assignment.locationId) }}</td>
                    <td @click.stop>
                      <select
                        :value="getAssignmentStatusValue(assignment)"
                        @change="changeAssignmentStatus(assignment, $event.target.value)"
                        class="status-select hide-on-mobile"
                        :class="getStatusClass(assignment)"
                      >
                        <option value="not-checked-in">Not checked-in</option>
                        <option value="checked-in">Checked-in</option>
                        <option value="admin-checked-in">Admin checked-in</option>
                        <option value="wrong-location">Checked-in but not at correct location</option>
                      </select>
                      <span class="status-icon show-on-mobile" :class="getStatusClass(assignment)">
                        {{ getStatusIcon(assignment) }}
                      </span>
                    </td>
                    <td v-if="index === 0" :rowspan="getMarshalAssignments(marshal.id).length" class="hide-on-mobile">
                      <div class="action-buttons" @click.stop>
                        <button @click="selectMarshal(marshal)" class="btn btn-small btn-secondary">
                          Edit
                        </button>
                        <button @click="handleDeleteMarshalFromGrid(marshal)" class="btn btn-small btn-danger">
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                </template>
                <tr v-else class="marshal-row" @click="selectMarshal(marshal)">
                  <td>{{ marshal.name }}</td>
                  <td class="hide-on-mobile">{{ marshal.email || '-' }}</td>
                  <td class="hide-on-mobile">{{ marshal.phoneNumber || '-' }}</td>
                  <td style="color: #999; font-style: italic;">No checkpoint assigned</td>
                  <td>
                    <span class="hide-on-mobile">-</span>
                    <span class="status-icon show-on-mobile" style="color: #999;">-</span>
                  </td>
                  <td class="hide-on-mobile">
                    <div class="action-buttons" @click.stop>
                      <button @click="selectMarshal(marshal)" class="btn btn-small btn-secondary">
                        Edit
                      </button>
                      <button @click="handleDeleteMarshalFromGrid(marshal)" class="btn btn-small btn-danger">
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
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

    <EditLocationModal
      :show="showEditLocation"
      :location="selectedLocation"
      :assignments="selectedLocation?.assignments || []"
      :isDirty="formDirty"
      @close="closeEditLocationModal"
      @save="handleUpdateLocation"
      @delete="handleDeleteLocation"
      @move-location="startMoveLocation"
      @toggle-check-in="toggleCheckIn"
      @remove-assignment="handleRemoveLocationAssignment"
      @assign-marshal="showAssignMarshalModal = true"
      @update:isDirty="markFormDirty"
    />

    <!-- Share Link Modal -->
    <ShareLinkModal
      :show="showShareLink"
      :link="marshalLink"
      :isDirty="false"
      @close="showShareLink = false"
    />

    <!-- Add Admin Modal -->
    <AddAdminModal
      :show="showAddAdmin"
      :isDirty="formDirty"
      @close="closeAdminModal"
      @submit="handleAddAdminSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Upload Route Modal -->
    <UploadRouteModal
      :show="showUploadRoute"
      :uploading="uploading"
      :error="uploadError"
      :isDirty="formDirty"
      @close="closeRouteModal"
      @submit="handleUploadRouteSubmit"
      @update:isDirty="markFormDirty"
    />

    <!-- Import Locations Modal -->
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
      @toggle-check-in="toggleMarshalCheckIn"
      @remove-assignment="handleRemoveMarshalAssignment"
      @assign-to-location="assignMarshalToLocation"
      @update:isDirty="markFormDirty"
    />

    <!-- Import Marshals Modal -->
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

    <!-- Info Modal -->
    <InfoModal
      :show="showInfoModal"
      :title="infoModalTitle"
      :message="infoModalMessage"
      @close="showInfoModal = false"
    />

    <!-- Confirm Modal -->
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
import { ref, computed, onMounted, onUnmounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi } from '../services/api';
import { startSignalRConnection, stopSignalRConnection } from '../services/signalr';
import MapView from '../components/MapView.vue';
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
import CheckpointsList from '../components/event-manage/lists/CheckpointsList.vue';
import AdminsList from '../components/event-manage/lists/AdminsList.vue';
import InfoModal from '../components/InfoModal.vue';
import ConfirmModal from '../components/ConfirmModal.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

const activeTab = ref('course');
const editLocationTab = ref('course');
const editMarshalTab = ref('course');
const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);
const selectedLocation = ref(null);
const showAddLocation = ref(false);
const showShareLink = ref(false);
const eventAdmins = ref([]);
const showAddAdmin = ref(false);
const showUploadRoute = ref(false);
const selectedGpxFile = ref(null);
const uploading = ref(false);
const uploadError = ref(null);
const showImportLocations = ref(false);
const showImportMarshals = ref(false);
const selectedCsvFile = ref(null);
const selectedMarshalsCsvFile = ref(null);
const deleteExistingLocations = ref(false);
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
const selectedMarshalToAssign = ref('');
const showAddNewMarshalInline = ref(false);
const showAssignMarshalModal = ref(false);
const newMarshalInlineForm = ref({
  name: '',
  email: '',
  phoneNumber: '',
});
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
const marshalSortBy = ref('name');
const marshalSortOrder = ref('asc');
const newAssignmentLocationId = ref('');
const formDirty = ref(false);
const pendingAssignments = ref([]);
const pendingDeleteAssignments = ref([]);
const pendingMarshalAssignments = ref([]);
const pendingMarshalDeleteAssignments = ref([]);
const showInfoModal = ref(false);
const infoModalTitle = ref('');
const infoModalMessage = ref('');
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);
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

const adminForm = ref({
  email: '',
});

const marshalLink = computed(() => {
  return `${window.location.origin}/event/${route.params.eventId}`;
});

const sortedMarshals = computed(() => {
  const sorted = [...marshals.value];

  sorted.sort((a, b) => {
    let compareValue = 0;

    if (marshalSortBy.value === 'name') {
      compareValue = a.name.localeCompare(b.name);
    } else if (marshalSortBy.value === 'email') {
      const aEmail = a.email || '';
      const bEmail = b.email || '';
      compareValue = aEmail.localeCompare(bEmail);
    } else if (marshalSortBy.value === 'phone') {
      const aPhone = a.phoneNumber || '';
      const bPhone = b.phoneNumber || '';
      compareValue = aPhone.localeCompare(bPhone);
    } else if (marshalSortBy.value === 'checkpoint') {
      // Sort by first assigned checkpoint name (using same rules as course page)
      const aAssignments = getMarshalAssignments(a.id);
      const bAssignments = getMarshalAssignments(b.id);
      const aCheckpoint = aAssignments.length > 0 ? getLocationName(aAssignments[0].locationId) : '';
      const bCheckpoint = bAssignments.length > 0 ? getLocationName(bAssignments[0].locationId) : '';

      // Check if both names are purely numeric
      const aNum = parseFloat(aCheckpoint);
      const bNum = parseFloat(bCheckpoint);
      const aIsNum = !isNaN(aNum) && String(aNum) === aCheckpoint.trim();
      const bIsNum = !isNaN(bNum) && String(bNum) === bCheckpoint.trim();

      // If both are numbers, sort numerically
      if (aIsNum && bIsNum) {
        compareValue = aNum - bNum;
      } else {
        // Otherwise, sort alphabetically (case-insensitive)
        compareValue = aCheckpoint.localeCompare(bCheckpoint, undefined, { numeric: true, sensitivity: 'base' });
      }
    } else if (marshalSortBy.value === 'status') {
      // Sort by checked-in status of first assignment
      const aAssignments = getMarshalAssignments(a.id);
      const bAssignments = getMarshalAssignments(b.id);
      const aCheckedIn = aAssignments.length > 0 && aAssignments[0].isCheckedIn ? 1 : 0;
      const bCheckedIn = bAssignments.length > 0 && bAssignments[0].isCheckedIn ? 1 : 0;
      compareValue = bCheckedIn - aCheckedIn;
    }

    return marshalSortOrder.value === 'asc' ? compareValue : -compareValue;
  });

  return sorted;
});

const availableLocations = computed(() => {
  if (!selectedMarshal.value) return locationStatuses.value;

  return locationStatuses.value.filter(
    loc => !selectedMarshal.value.assignedLocationIds.includes(loc.id)
  );
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

  // Sort alphabetically by name
  return marshalsList.slice().sort((a, b) => a.name.localeCompare(b.name));
});

const showConfirm = (title, message, callback) => {
  confirmModalTitle.value = title;
  confirmModalMessage.value = message;
  confirmModalCallback.value = callback;
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

const markFormDirty = () => {
  formDirty.value = true;
};

const markEventDetailsFormDirty = () => {
  eventDetailsFormDirty.value = true;
};

const handleUpdateEvent = async () => {
  try {
    // Check if event date changed and if any checkpoints have custom times
    const oldEventDate = event.value.eventDate;
    const newEventDate = eventDetailsForm.value.eventDate;

    // Check if we need to show the shift modal
    if (oldEventDate !== newEventDate) {
      // Count checkpoints with custom times
      const checkpointsWithTimes = locations.value.filter(
        loc => loc.startTime || loc.endTime
      );

      if (checkpointsWithTimes.length > 0) {
        // Show shift modal
        oldEventDateForShift.value = oldEventDate;
        newEventDateForShift.value = newEventDate;
        checkpointsWithCustomTimes.value = checkpointsWithTimes.length;
        showShiftCheckpointTimes.value = true;
        return; // Don't update yet, wait for modal response
      }
    }

    // No checkpoints with custom times, proceed with update
    await performEventUpdate(false, null);
  } catch (error) {
    console.error('Failed to update event:', error);
    alert('Failed to update event. Please try again.');
  }
};

const performEventUpdate = async (shouldShiftCheckpoints, timeDeltaMs) => {
  try {
    // Update the event
    await eventsStore.updateEvent(route.params.eventId, {
      ...eventDetailsForm.value,
    });

    // If we should shift checkpoint times, do it now
    if (shouldShiftCheckpoints && timeDeltaMs !== null) {
      // Convert milliseconds to TimeSpan format for C# (HH:mm:ss)
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
  // Reset the form to original values
  loadEventData();
};

const tryCloseModal = (closeFunction) => {
  if (formDirty.value || pendingAssignments.value.length > 0 || pendingDeleteAssignments.value.length > 0 ||
      pendingMarshalAssignments.value.length > 0 || pendingMarshalDeleteAssignments.value.length > 0) {
    showConfirm('Unsaved Changes', 'You have unsaved changes. Are you sure you want to close without saving?', () => {
      formDirty.value = false;
      pendingAssignments.value = [];
      pendingDeleteAssignments.value = [];
      pendingMarshalAssignments.value = [];
      pendingMarshalDeleteAssignments.value = [];
      closeFunction();
    });
  } else {
    closeFunction();
  }
};

const isValidWhat3Words = (what3Words) => {
  if (!what3Words || what3Words.trim() === '') {
    return true;
  }
  const pattern = /^[a-z]{1,20}[./][a-z]{1,20}[./][a-z]{1,20}$/;
  if (!pattern.test(what3Words)) {
    return false;
  }
  const hasDots = what3Words.includes('.');
  const hasSlashes = what3Words.includes('/');
  return hasDots !== hasSlashes;
};

const loadEventData = async () => {
  try {
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    // Populate event details form
    if (event.value) {
      // Convert eventDate to datetime-local format (YYYY-MM-DDTHH:mm)
      let formattedDate = '';
      if (event.value.eventDate) {
        const date = new Date(event.value.eventDate);
        // Format as YYYY-MM-DDTHH:mm (local time)
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hours = String(date.getHours()).padStart(2, '0');
        const minutes = String(date.getMinutes()).padStart(2, '0');
        formattedDate = `${year}-${month}-${day}T${hours}:${minutes}`;
      }

      eventDetailsForm.value = {
        name: event.value.name || '',
        description: event.value.description || '',
        eventDate: formattedDate,
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
  adminForm.value = { email: '' };
  formDirty.value = false;
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

    // Close import modal and show success message
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
  selectedCsvFile.value = null;
  deleteExistingLocations.value = false;
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
    // Check if click is within 25m of any existing checkpoint
    const nearbyCheckpoint = locationStatuses.value.find(location => {
      const distance = calculateDistance(
        coords.lat,
        coords.lng,
        location.latitude,
        location.longitude
      );
      return distance <= 25;
    });

    // If not near any checkpoint, open Add Checkpoint modal
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
  editLocationTab.value = 'details';
  showAssignMarshalModal.value = false;
  selectedMarshalToAssign.value = '';
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
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
  selectedMarshalToAssign.value = '';
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const startMoveLocation = () => {
  isMovingLocation.value = true;
  showEditLocation.value = false;
  activeTab.value = 'course';
};

const cancelMoveLocation = () => {
  isMovingLocation.value = false;
  showEditLocation.value = true;
};

const handleUpdateLocation = async (formData) => {
  if (!isValidWhat3Words(formData.what3Words)) {
    alert('Invalid What3Words format. Please use word.word.word or word/word/word (lowercase letters only, 1-20 characters each)');
    return;
  }

  try {
    // Update location
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
      }
    );

    // Process pending deletions
    for (const assignmentId of pendingDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    // Process pending additions
    for (const pending of pendingAssignments.value) {
      // Check if marshal is already assigned to other checkpoints
      const marshal = marshals.value.find(m => m.id === pending.marshalId);
      if (marshal && marshal.assignedLocationIds.length > 0) {
        const otherLocations = marshal.assignedLocationIds
          .filter(id => id !== selectedLocation.value.id)
          .map(id => getLocationName(id));

        if (otherLocations.length > 0) {
          const choice = await new Promise((resolve) => {
            assignmentConflictData.value = {
              marshalName: marshal.name,
              locations: otherLocations,
              marshal: marshal,
            };
            conflictResolveCallback.value = resolve;
            showAssignmentConflict.value = true;
          });

          if (choice === 'cancel') {
            continue;
          } else if (choice === 'move') {
            for (const locationId of marshal.assignedLocationIds) {
              if (locationId !== selectedLocation.value.id) {
                const location = locationStatuses.value.find(loc => loc.id === locationId);
                if (location) {
                  const assignment = location.assignments.find(
                    a => a.marshalName === marshal.name
                  );
                  if (assignment) {
                    await eventsStore.deleteAssignment(route.params.eventId, assignment.id);
                  }
                }
              }
            }
          }
        }
      }

      await eventsStore.createAssignment({
        eventId: route.params.eventId,
        locationId: selectedLocation.value.id,
        marshalId: pending.marshalId,
        marshalName: pending.marshalName,
      });
    }

    await loadEventData();
    closeEditLocationModal();
  } catch (error) {
    console.error('Failed to update checkpoint:', error);
    alert('Failed to update checkpoint. Please try again.');
  }
};

const handleDeleteLocation = async () => {
  showConfirm('Delete Checkpoint', `Are you sure you want to delete "${editLocationForm.value.name}"? This will remove all marshal assignments for this checkpoint.`, async () => {
    try {
      await locationsApi.delete(route.params.eventId, editLocationForm.value.id);
      await loadEventData();
      closeEditLocationModal();
    } catch (error) {
      console.error('Failed to delete checkpoint:', error);
      alert('Failed to delete checkpoint. Please try again.');
    }
  });
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

const markAssignmentForDelete = (assignment) => {
  const index = pendingDeleteAssignments.value.indexOf(assignment.id);
  if (index > -1) {
    pendingDeleteAssignments.value.splice(index, 1);
  } else {
    pendingDeleteAssignments.value.push(assignment.id);
  }
  markFormDirty();
};

const handleRemoveLocationAssignment = (assignment) => {
  markAssignmentForDelete(assignment);
};

const isPendingDelete = (assignmentId) => {
  return pendingDeleteAssignments.value.includes(assignmentId);
};

const shareEvent = () => {
  showShareLink.value = true;
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

const calculateDistance = (lat1, lon1, lat2, lon2) => {
  const R = 6371000;
  const φ1 = lat1 * Math.PI / 180;
  const φ2 = lat2 * Math.PI / 180;
  const Δφ = (lat2 - lat1) * Math.PI / 180;
  const Δλ = (lon2 - lon1) * Math.PI / 180;

  const a = Math.sin(Δφ/2) * Math.sin(Δφ/2) +
          Math.cos(φ1) * Math.cos(φ2) *
          Math.sin(Δλ/2) * Math.sin(Δλ/2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1-a));

  return R * c;
};

const getAssignmentStatus = (assignment) => {
  if (!assignment.isCheckedIn) {
    return { icon: '✗', color: 'red', text: 'Not checked in' };
  }

  if (!assignment.checkInLatitude || !assignment.checkInLongitude ||
      !selectedLocation.value.latitude || !selectedLocation.value.longitude) {
    return { icon: '✓', color: 'green', text: 'Checked in' };
  }

  const distance = calculateDistance(
    selectedLocation.value.latitude,
    selectedLocation.value.longitude,
    assignment.checkInLatitude,
    assignment.checkInLongitude
  );

  if (distance <= 25) {
    return { icon: '✓', color: 'green', text: `Checked in (${Math.round(distance)}m away)` };
  } else {
    return { icon: '⚠', color: 'orange', text: `Checked in (${Math.round(distance)}m away - outside 25m range)` };
  }
};

const handleCheckinUpdate = (data) => {
  if (data.eventId === route.params.eventId) {
    eventsStore.updateAssignmentCheckIn(data.assignment);
    loadEventData();
  }
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
  editMarshalTab.value = 'details';
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  formDirty.value = false;
  showEditMarshal.value = true;
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
  formDirty.value = false;
  showEditMarshal.value = true;
};

const toggleSortOrder = () => {
  marshalSortOrder.value = marshalSortOrder.value === 'asc' ? 'desc' : 'asc';
};

const changeMarshalSort = (column) => {
  if (marshalSortBy.value === column) {
    // Toggle sort order if clicking the same column
    marshalSortOrder.value = marshalSortOrder.value === 'asc' ? 'desc' : 'asc';
  } else {
    // Change to new column with ascending order
    marshalSortBy.value = column;
    marshalSortOrder.value = 'asc';
  }
};

const closeEditMarshalModal = () => {
  showEditMarshal.value = false;
  selectedMarshal.value = null;
  newAssignmentLocationId.value = '';
  editMarshalTab.value = 'details';
  pendingMarshalAssignments.value = [];
  pendingMarshalDeleteAssignments.value = [];
  editMarshalForm.value = {
    id: '',
    name: '',
    email: '',
    phoneNumber: '',
    notes: '',
  };
  formDirty.value = false;
};

const handleSaveMarshal = async (formData) => {
  try {
    if (selectedMarshal.value) {
      // Update marshal details
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

      // Process pending deletions
      for (const assignmentId of pendingMarshalDeleteAssignments.value) {
        await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
      }

      // Process pending additions
      for (const pending of pendingMarshalAssignments.value) {
        await eventsStore.createAssignment({
          eventId: route.params.eventId,
          locationId: pending.locationId,
          marshalId: selectedMarshal.value.id,
          marshalName: selectedMarshal.value.name,
        });
      }
    } else {
      await marshalsApi.create({
        eventId: route.params.eventId,
        name: formData.name,
        email: formData.email,
        phoneNumber: formData.phoneNumber,
        notes: formData.notes,
      });
    }

    await loadEventData();
    closeEditMarshalModal();
  } catch (error) {
    console.error('Failed to save marshal:', error);
    alert('Failed to save marshal. Please try again.');
  }
};

const handleDeleteMarshal = async () => {
  showConfirm('Delete Marshal', `Are you sure you want to delete "${editMarshalForm.value.name}"? This will remove all assignments for this marshal.`, async () => {
    try {
      await marshalsApi.delete(route.params.eventId, editMarshalForm.value.id);
      await loadEventData();
      closeEditMarshalModal();
    } catch (error) {
      console.error('Failed to delete marshal:', error);
      alert('Failed to delete marshal. Please try again.');
    }
  });
};

const assignToLocation = async () => {
  if (!newAssignmentLocationId.value || !selectedMarshal.value) return;

  try {
    await eventsStore.createAssignment({
      eventId: route.params.eventId,
      locationId: newAssignmentLocationId.value,
      marshalId: selectedMarshal.value.id,
      marshalName: selectedMarshal.value.name,
    });

    await loadEventData();
    const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
    selectedMarshal.value = response.data;
    newAssignmentLocationId.value = '';
  } catch (error) {
    console.error('Failed to assign marshal:', error);
    alert('Failed to assign marshal. Please try again.');
  }
};

const removeAssignment = async (locationId) => {
  if (!selectedMarshal.value) return;

  const location = locationStatuses.value.find(loc => loc.id === locationId);
  if (!location) return;

  const assignment = location.assignments.find(
    a => a.marshalName === selectedMarshal.value.name
  );

  if (!assignment) return;

  showConfirm('Remove Assignment', `Remove ${selectedMarshal.value.name} from ${location.name}?`, async () => {
    try {
      await eventsStore.deleteAssignment(route.params.eventId, assignment.id);
      await loadEventData();
      const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
      selectedMarshal.value = response.data;
    } catch (error) {
      console.error('Failed to remove assignment:', error);
      alert('Failed to remove assignment. Please try again.');
    }
  });
};

const getLocationName = (locationId) => {
  const location = locationStatuses.value.find(loc => loc.id === locationId);
  return location ? location.name : 'Unknown';
};

const formatCoordinate = (value, decimals) => {
  if (!value && value !== 0) return '';
  return parseFloat(value).toFixed(decimals);
};

// Marshal edit modal helper functions
const getMarshalAssignmentsForEdit = () => {
  if (!selectedMarshal.value) return [];

  const allAssignments = [];
  locationStatuses.value.forEach(location => {
    location.assignments.forEach(assignment => {
      const marshal = marshals.value.find(m => m.name === assignment.marshalName);
      if (marshal && marshal.id === selectedMarshal.value.id) {
        allAssignments.push({
          ...assignment,
          locationId: location.id,
          locationLatitude: location.latitude,
          locationLongitude: location.longitude,
        });
      }
    });
  });
  return allAssignments;
};

const getMarshalAssignmentCount = () => {
  if (!selectedMarshal.value) return 0;
  const currentCount = getMarshalAssignmentsForEdit().length;
  const pendingAddCount = pendingMarshalAssignments.value.length;
  const pendingDeleteCount = pendingMarshalDeleteAssignments.value.length;
  return currentCount + pendingAddCount - pendingDeleteCount;
};

const getMarshalAssignmentStatus = (assignment) => {
  if (!assignment.isCheckedIn) {
    return { icon: '✗', color: 'red', text: 'Not checked in' };
  }

  if (!assignment.checkInLatitude || !assignment.checkInLongitude ||
      !assignment.locationLatitude || !assignment.locationLongitude) {
    return { icon: '✓', color: 'green', text: 'Checked in' };
  }

  const distance = calculateDistance(
    assignment.locationLatitude,
    assignment.locationLongitude,
    assignment.checkInLatitude,
    assignment.checkInLongitude
  );

  if (distance <= 25) {
    return { icon: '✓', color: 'green', text: `Checked in (${Math.round(distance)}m away)` };
  } else {
    return { icon: '⚠', color: 'orange', text: `Checked in (${Math.round(distance)}m away - outside 25m range)` };
  }
};

const toggleMarshalCheckIn = async (assignment) => {
  try {
    await checkInApi.adminCheckIn(assignment.id);
    await loadEventData();
    // Refresh selected marshal
    if (selectedMarshal.value) {
      const response = await marshalsApi.getById(route.params.eventId, selectedMarshal.value.id);
      selectedMarshal.value = response.data;
    }
  } catch (error) {
    console.error('Failed to toggle check-in:', error);
    alert('Failed to toggle check-in. Please try again.');
  }
};

const markMarshalAssignmentForDelete = (assignment) => {
  const index = pendingMarshalDeleteAssignments.value.indexOf(assignment.id);
  if (index > -1) {
    pendingMarshalDeleteAssignments.value.splice(index, 1);
  } else {
    pendingMarshalDeleteAssignments.value.push(assignment.id);
  }
  markFormDirty();
};

const handleRemoveMarshalAssignment = (assignment) => {
  markMarshalAssignmentForDelete(assignment);
};

const isPendingMarshalDelete = (assignmentId) => {
  return pendingMarshalDeleteAssignments.value.includes(assignmentId);
};

const assignMarshalToLocation = () => {
  if (!newAssignmentLocationId.value) return;

  const alreadyAssigned = selectedMarshal.value?.assignedLocationIds?.includes(newAssignmentLocationId.value);
  const alreadyPending = pendingMarshalAssignments.value.some(
    p => p.locationId === newAssignmentLocationId.value
  );

  if (alreadyAssigned || alreadyPending) {
    alert('Marshal is already assigned to this checkpoint.');
    return;
  }

  pendingMarshalAssignments.value.push({
    locationId: newAssignmentLocationId.value,
  });
  markFormDirty();
  newAssignmentLocationId.value = '';
};

const removePendingMarshalAssignment = (locationId) => {
  const index = pendingMarshalAssignments.value.findIndex(p => p.locationId === locationId);
  if (index > -1) {
    pendingMarshalAssignments.value.splice(index, 1);
  }
};

const handleConflictChoice = async (choice) => {
  showAssignmentConflict.value = false;

  if (conflictResolveCallback.value) {
    await conflictResolveCallback.value(choice);
    conflictResolveCallback.value = null;
  }
};

const handleAssignExistingMarshal = async (marshalId) => {
  if (!marshalId || !selectedLocation.value) return;

  const marshal = marshals.value.find(m => m.id === marshalId);
  if (!marshal) return;

  const alreadyAssignedHere = selectedLocation.value.assignments.some(
    a => a.marshalName === marshal.name
  );
  const alreadyPending = pendingAssignments.value.some(
    p => p.marshalId === marshal.id
  );

  if (alreadyAssignedHere || alreadyPending) {
    alert(`${marshal.name} is already assigned to this checkpoint.`);
    return;
  }

  pendingAssignments.value.push({
    marshalId: marshal.id,
    marshalName: marshal.name,
  });
  markFormDirty();
  closeAssignMarshalModal();
};

const removePendingAssignment = (marshalId) => {
  const index = pendingAssignments.value.findIndex(p => p.marshalId === marshalId);
  if (index > -1) {
    pendingAssignments.value.splice(index, 1);
  }
};

const handleAddNewMarshalInline = async (formData) => {
  if (!formData.name) {
    alert('Please enter a name for the marshal.');
    return;
  }

  try {
    const response = await marshalsApi.create({
      eventId: route.params.eventId,
      name: formData.name,
      email: formData.email,
      phoneNumber: formData.phoneNumber,
      notes: '',
    });

    const newMarshal = response.data;

    pendingAssignments.value.push({
      marshalId: newMarshal.id,
      marshalName: newMarshal.name,
    });
    markFormDirty();

    // Add to local marshals array to avoid map refresh issues
    marshals.value.push(newMarshal);
    closeAssignMarshalModal();
  } catch (error) {
    console.error('Failed to add new marshal:', error);
    alert('Failed to add new marshal. Please try again.');
  }
};

const cancelAddNewMarshalInline = () => {
  showAddNewMarshalInline.value = false;
  newMarshalInlineForm.value = {
    name: '',
    email: '',
    phoneNumber: '',
  };
};

const getMarshalAssignments = (marshalId) => {
  const allAssignments = [];
  locationStatuses.value.forEach(location => {
    location.assignments.forEach(assignment => {
      const marshal = marshals.value.find(m => m.name === assignment.marshalName);
      if (marshal && marshal.id === marshalId) {
        allAssignments.push({
          ...assignment,
          locationId: location.id,
          locationLatitude: location.latitude,
          locationLongitude: location.longitude,
        });
      }
    });
  });
  return allAssignments;
};

const getAssignmentStatusValue = (assignment) => {
  if (!assignment.isCheckedIn) {
    return 'not-checked-in';
  }

  if (assignment.checkInMethod === 'Admin') {
    return 'admin-checked-in';
  }

  if (assignment.checkInLatitude && assignment.checkInLongitude &&
      assignment.locationLatitude && assignment.locationLongitude) {
    const distance = calculateDistance(
      assignment.locationLatitude,
      assignment.locationLongitude,
      assignment.checkInLatitude,
      assignment.checkInLongitude
    );

    if (distance > 25) {
      return 'wrong-location';
    }
  }

  return 'checked-in';
};

const getStatusClass = (assignment) => {
  const status = getAssignmentStatusValue(assignment);
  return `status-${status}`;
};

const getStatusIcon = (assignment) => {
  if (!assignment.isCheckedIn) {
    return '✗';
  }

  if (assignment.checkInMethod === 'Admin') {
    return '✓';
  }

  if (assignment.checkInLatitude && assignment.checkInLongitude &&
      assignment.locationLatitude && assignment.locationLongitude) {
    const distance = calculateDistance(
      assignment.locationLatitude,
      assignment.locationLongitude,
      assignment.checkInLatitude,
      assignment.checkInLongitude
    );

    if (distance > 25) {
      return '⚠';
    }
  }

  return '✓';
};

const changeAssignmentStatus = async (assignment, newStatus) => {
  try {
    if (newStatus === 'not-checked-in') {
      if (assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(assignment.id);
      }
    } else {
      if (!assignment.isCheckedIn) {
        await checkInApi.adminCheckIn(assignment.id);
      }
    }
    await loadEventData();
  } catch (error) {
    console.error('Failed to change assignment status:', error);
    alert('Failed to change status. Please try again.');
  }
};

const handleDeleteMarshalFromGrid = async (marshal) => {
  showConfirm('Delete Marshal', `Are you sure you want to delete "${marshal.name}"? This will remove all assignments for this marshal.`, async () => {
    try {
      await marshalsApi.delete(route.params.eventId, marshal.id);
      await loadEventData();
    } catch (error) {
      console.error('Failed to delete marshal:', error);
      alert('Failed to delete marshal. Please try again.');
    }
  });
};

const handleMarshalsCsvFileChange = (event) => {
  selectedMarshalsCsvFile.value = event.target.files[0];
  importError.value = null;
  importMarshalsResult.value = null;
};

const handleImportMarshals = async () => {
  if (!selectedMarshalsCsvFile.value) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importingMarshals.value = true;
  importError.value = null;
  importMarshalsResult.value = null;

  try {
    const response = await marshalsApi.importCsv(
      route.params.eventId,
      selectedMarshalsCsvFile.value
    );
    importMarshalsResult.value = response.data;
    await loadEventData();
  } catch (error) {
    console.error('Failed to import marshals:', error);
    importError.value = error.response?.data?.message || 'Failed to import marshals. Please try again.';
  } finally {
    importingMarshals.value = false;
  }
};

const handleImportMarshalsSubmit = async (file) => {
  if (!file) {
    importError.value = 'Please select a CSV file';
    return;
  }

  importingMarshals.value = true;
  importError.value = null;
  importMarshalsResult.value = null;

  try {
    const response = await marshalsApi.importCsv(
      route.params.eventId,
      file
    );
    importMarshalsResult.value = response.data;
    await loadEventData();

    // Close import modal and show success message
    closeImportMarshalsModal();

    const result = response.data;
    let message = `<p>Imported <strong>${result.marshalsCreated}</strong> marshal(s) and <strong>${result.assignmentsCreated}</strong> assignment(s)</p>`;

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
  selectedMarshalsCsvFile.value = null;
  importError.value = null;
  importMarshalsResult.value = null;
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

.assignment-item.pending-delete {
  border-color: #ff4444;
  background: #fff1f1;
  opacity: 0.6;
}

.assignment-item.pending-add {
  border-color: #2196f3;
  background: #e3f2fd;
}

.pending-badge {
  font-size: 0.75rem;
  padding: 0.25rem 0.5rem;
  background: #ff9800;
  color: white;
  border-radius: 4px;
  font-weight: 600;
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
  position: relative;
}

.modal-content h2 {
  margin: 0 0 1rem 0;
  color: #333;
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
  padding: 0.25rem 0.5rem;
  line-height: 1;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s;
}

.modal-close-btn:hover {
  background: #f0f0f0;
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

.form-error {
  display: block;
  color: #ff4444;
  font-size: 0.875rem;
  margin-top: 0.25rem;
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
  padding-top: 1.5rem;
  background: white;
  position: sticky;
  bottom: 0;
  border-top: 1px solid #e0e0e0;
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
  position: relative;
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

.status-indicator {
  font-size: 1.2rem;
  font-weight: bold;
}

.distance-info {
  font-size: 0.75rem;
  color: #666;
}

.modal-content-large {
  max-width: 800px;
  max-height: 90vh;
  overflow-y: auto;
}

.tab-content {
  margin-top: 1rem;
}

.marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
}

.marshal-item {
  padding: 1rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.3s;
}

.marshal-item:hover {
  border-color: #667eea;
}

.marshal-item.checked-in {
  border-color: #4caf50;
  background: #f1f8f4;
}

.marshal-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.marshal-assignments {
  margin-top: 0.25rem;
}

.marshal-notes {
  font-size: 0.75rem;
  color: #666;
  font-style: italic;
  margin-top: 0.5rem;
  padding: 0.5rem;
  background: #f5f7fa;
  border-radius: 4px;
}

.sort-controls {
  display: flex;
  align-items: center;
  margin-top: 1rem;
  margin-bottom: 0.5rem;
  font-size: 0.875rem;
}

.marshal-assignments-section {
  margin-top: 2rem;
  padding-top: 2rem;
  border-top: 2px solid #e0e0e0;
}

.no-assignments {
  color: #999;
  font-style: italic;
  padding: 1rem;
  text-align: center;
  background: #f5f7fa;
  border-radius: 6px;
}

.assigned-checkpoints {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.assigned-checkpoint-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  background: #f5f7fa;
  border-radius: 6px;
}

.tabs-nav {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 2rem;
  border-bottom: 2px solid #e0e0e0;
}

.tab-button {
  padding: 1rem 2rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 1rem;
  font-weight: 600;
  color: #666;
  transition: all 0.3s;
  margin-bottom: -2px;
}

.tab-button:hover {
  color: #667eea;
}

.tab-button.active {
  color: #667eea;
  border-bottom-color: #667eea;
}

.tab-content-wrapper {
  animation: fadeIn 0.3s;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.details-container {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 2rem;
}

.details-section {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.details-section h2 {
  margin: 0 0 1.5rem 0;
  color: #333;
  font-size: 1.5rem;
}

.event-details-form .form-group {
  margin-bottom: 1.5rem;
}

.event-details-form label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 600;
  color: #333;
}

.event-details-form .form-input {
  width: 100%;
  padding: 0.75rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 1rem;
  transition: border-color 0.3s;
  box-sizing: border-box;
}

.event-details-form .form-input:focus {
  outline: none;
  border-color: #667eea;
}

.event-details-form textarea.form-input {
  resize: vertical;
  font-family: inherit;
}

.form-actions {
  margin-top: 2rem;
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 1024px) {
  .details-container {
    grid-template-columns: 1fr;
  }
}

.marshals-tab-header {
  background: white;
  padding: 1.5rem;
  border-radius: 12px;
  margin-bottom: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.marshals-tab-header h2 {
  margin: 0;
  color: #333;
}

.marshals-grid {
  background: white;
  padding: 1.5rem;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  overflow-x: auto;
}

.marshals-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.marshals-table thead {
  background: #f5f7fa;
  border-bottom: 2px solid #e0e0e0;
}

.marshals-table th {
  padding: 1rem;
  text-align: left;
  font-weight: 600;
  color: #333;
}

.marshals-table th.sortable {
  cursor: pointer;
  user-select: none;
  transition: background-color 0.2s;
}

.marshals-table th.sortable:hover {
  background: #e8ecf2;
}

.sort-indicator {
  font-size: 0.7rem;
  margin-left: 0.3rem;
  color: #667eea;
}

.marshals-table td {
  padding: 1rem;
  border-bottom: 1px solid #e0e0e0;
  vertical-align: middle;
}

.marshal-row:hover {
  background: #f9fafb;
}

.status-select {
  padding: 0.5rem;
  border: 2px solid #e0e0e0;
  border-radius: 6px;
  font-size: 0.875rem;
  cursor: pointer;
  transition: border-color 0.3s;
  min-width: 200px;
}

.status-select:focus {
  outline: none;
  border-color: #667eea;
}

.status-select.status-not-checked-in {
  background: #fff1f1;
  border-color: #ff4444;
  color: #cc0000;
}

.status-select.status-checked-in {
  background: #f1f8f4;
  border-color: #4caf50;
  color: #2e7d32;
}

.status-select.status-admin-checked-in {
  background: #e3f2fd;
  border-color: #2196f3;
  color: #1565c0;
}

.status-select.status-wrong-location {
  background: #fff3e0;
  border-color: #ff9800;
  color: #e65100;
}

.action-buttons {
  display: flex;
  gap: 0.5rem;
}

.edit-modal-tabs-nav {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
  border-bottom: 2px solid #e0e0e0;
}

.edit-modal-tab-button {
  padding: 0.75rem 1.5rem;
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  cursor: pointer;
  font-size: 0.95rem;
  font-weight: 600;
  color: #666;
  transition: all 0.3s;
  margin-bottom: -2px;
}

.edit-modal-tab-button:hover {
  color: #667eea;
}

.edit-modal-tab-button.active {
  color: #667eea;
  border-bottom-color: #667eea;
}

.edit-modal-tab-content {
  animation: fadeIn 0.3s;
}

.assign-marshal-section {
  padding: 1rem;
  background: #f9fafb;
  border-radius: 8px;
  margin-bottom: 1.5rem;
}

.inline-add-marshal {
  border: 2px solid #e0e0e0;
}

.inline-add-marshal h4 {
  margin: 0 0 1rem 0;
  color: #333;
}

.assign-marshal-modal-section {
  margin-bottom: 1.5rem;
}

.assign-marshal-modal-section h3 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1rem;
}

.move-checkpoint-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  z-index: 900;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding-top: 2rem;
  pointer-events: none;
}

.move-checkpoint-instructions {
  background: white;
  padding: 2rem;
  border-radius: 12px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
  max-width: 500px;
  text-align: center;
  pointer-events: all;
}

.move-checkpoint-instructions h3 {
  margin: 0 0 1rem 0;
  color: #333;
}

.move-checkpoint-instructions p {
  margin: 0 0 1.5rem 0;
  color: #666;
  font-size: 1rem;
}

.move-checkpoint-actions {
  display: flex;
  justify-content: center;
  gap: 1rem;
}

.error {
  color: #ff4444;
  background: #fff1f1;
  padding: 0.75rem;
  border-radius: 6px;
  margin-top: 1rem;
  font-size: 0.875rem;
}

/* Fixed modal actions */
.modal-actions-fixed {
  position: sticky;
  bottom: 0;
  background: white;
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding: 1.5rem 0 0 0;
  border-top: 2px solid #e0e0e0;
}

.modal-content-large .modal-actions-fixed {
  margin-left: -2rem;
  margin-right: -2rem;
  padding-left: 2rem;
  padding-right: 2rem;
  padding-bottom: 1rem;
}

/* Mobile status icon */
.status-icon {
  display: none;
  font-size: 1.5rem;
  font-weight: bold;
}

.status-icon.status-not-checked-in {
  color: #cc0000;
}

.status-icon.status-checked-in {
  color: #2e7d32;
}

.status-icon.status-admin-checked-in {
  color: #1565c0;
}

.status-icon.status-wrong-location {
  color: #e65100;
}

.hide-on-mobile {
  display: table-cell;
}

.show-on-mobile {
  display: none;
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }

  .map-section {
    height: 400px;
  }

  .marshals-tab-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }

  .marshals-table {
    font-size: 0.8rem;
  }

  .marshals-table th,
  .marshals-table td {
    padding: 0.75rem 0.5rem;
  }
}

@media (max-width: 768px) {
  .hide-on-mobile {
    display: none !important;
  }

  .show-on-mobile {
    display: inline-block !important;
  }

  .marshal-row {
    cursor: pointer;
  }

  .marshal-row:hover {
    background: #f0f0f0;
  }
}
</style>
