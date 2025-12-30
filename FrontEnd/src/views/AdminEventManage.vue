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
          :class="{ active: activeTab === 'checklists' }"
          @click="switchTab('checklists')"
        >
          Checklists
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
        <CourseAreasTab
          v-if="activeTab === 'course'"
          ref="courseAreasTab"
          :checkpoints="locationStatuses"
          :areas="areas"
          :route="event?.route || []"
          :selectedAreaId="selectedAreaId"
          :drawingMode="isDrawingAreaBoundary"
          @map-click="handleMapClick"
          @location-click="handleLocationClick"
          @area-click="handleSelectArea"
          @add-checkpoint="handleAddLocationClick"
          @add-multiple-checkpoints="handleAddMultipleCheckpointsClick"
          @import-checkpoints="showImportLocations = true"
          @select-location="selectLocation"
          @add-area="handleAddArea"
          @select-area="handleSelectArea"
          @polygon-complete="handlePolygonComplete"
          @cancel-drawing="handleCancelDrawing"
          @add-checkpoint-from-map="handleAddCheckpointFromMap"
          @add-many-checkpoints-from-map="handleAddMultipleCheckpointsClick"
          @add-area-from-map="handleAddAreaFromMap"
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

        <ChecklistsTab
          v-if="activeTab === 'checklists'"
          :checklist-items="checklistItems"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          :completion-report="checklistCompletionReport"
          @add-checklist-item="handleAddChecklistItem"
          @select-checklist-item="handleSelectChecklistItem"
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
    <!-- All Modals -->
    <AddLocationModal
      :show="showAddLocation"
      :location="locationForm"
      :isDirty="formDirty"
      @close="closeLocationModal"
      @save="handleSaveLocation"
      @set-on-map="handleSetLocationOnMap"
      @update:isDirty="markFormDirty"
      @update:location="handleLocationFormUpdate"
    />
    <EditLocationModal
      :show="showEditLocation"
      :location="selectedLocation"
      :assignments="selectedLocation?.assignments || []"
      :event-date="event?.eventDate || ''"
      :areas="areas"
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

    <EditAreaModal
      :show="showEditArea"
      :area="selectedArea"
      :checkpoints="locationStatuses"
      :marshals="marshals"
      :areas="areas"
      :isDirty="formDirty"
      @close="closeEditAreaModal"
      @save="handleSaveArea"
      @delete="handleDeleteArea"
      @draw-boundary="handleDrawBoundary"
      @update:isDirty="markFormDirty"
    />

    <EditChecklistItemModal
      :show="showEditChecklistItem"
      :checklist-item="selectedChecklistItem"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :isDirty="formDirty"
      @close="closeEditChecklistItemModal"
      @save="handleSaveChecklistItem"
      @delete="handleDeleteChecklistItem"
      @update:isDirty="markFormDirty"
    />

    <!-- Fullscreen Map Overlay -->
    <FullscreenMapOverlay
      :show="isFullscreenMapActive"
      :mode="fullscreenMode"
      :context-title="fullscreenContext.title"
      :context-description="fullscreenContext.description"
      :can-complete="canCompleteFullscreen"
      :clickable="fullscreenMode !== 'draw-area'"
      :drawing-mode="fullscreenMode === 'draw-area' && isDrawingAreaBoundary"
      :locations="displayCheckpoints"
      :areas="displayAreas"
      :route="event?.route || []"
      :selected-area-id="selectedAreaId"
      :center="savedMapCenter"
      :zoom="savedMapZoom"
      @map-click="handleFullscreenMapClick"
      @polygon-complete="handlePolygonComplete"
      @polygon-drawing="handlePolygonDrawing"
      @done="handleFullscreenDone"
      @cancel="handleFullscreenCancel"
    />

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
import { checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi, areasApi, checklistApi } from '../services/api';
import { startSignalRConnection, stopSignalRConnection } from '../services/signalr';

// New composables and utilities
import { useTabs } from '../composables/useTabs';
import { useConfirmDialog } from '../composables/useConfirmDialog';
import { formatDate as formatEventDate, formatDateForInput } from '../utils/dateFormatters';
import { isValidWhat3Words } from '../utils/validators';
import { calculateDistance } from '../utils/coordinateUtils';
import { getCheckpointsInPolygon } from '../utils/geometryUtils';
import { CHECK_IN_RADIUS_METERS } from '../constants/app';

// Tab Components
import CourseAreasTab from './AdminEventManage/CourseAreasTab.vue';
import MarshalsTab from './AdminEventManage/MarshalsTab.vue';
import ChecklistsTab from './AdminEventManage/ChecklistsTab.vue';
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
import EditAreaModal from '../components/event-manage/modals/EditAreaModal.vue';
import EditChecklistItemModal from '../components/event-manage/modals/EditChecklistItemModal.vue';
import InfoModal from '../components/InfoModal.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import FullscreenMapOverlay from '../components/FullscreenMapOverlay.vue';
import AddLocationModal from '../components/event-manage/modals/AddLocationModal.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

// Use composables
const { activeTab, switchTab } = useTabs('course', ['course', 'marshals', 'checklists', 'areas', 'details']);
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
const courseAreasTab = ref(null);
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
const areas = ref([]);
const selectedArea = ref(null);
const showEditArea = ref(false);
const selectedAreaId = ref(null);
const isDrawingAreaBoundary = ref(false);
const pendingAreaFormData = ref(null);
const isAddingMultipleCheckpoints = ref(false);
const multiCheckpointCounter = ref(1);
// Fullscreen map mode
const isFullscreenMapActive = ref(false);
const fullscreenMode = ref(null);
const fullscreenContext = ref({});
const tempLocationCoords = ref(null); // For previewing checkpoint placement
const currentDrawingPolygon = ref(null); // For area drawing with Done button
const tempCheckpoints = ref([]); // For storing multiple checkpoints before saving
const savedMapCenter = ref(null); // Store map center when entering fullscreen
const savedMapZoom = ref(null); // Store map zoom when entering fullscreen
const checklistItems = ref([]);
const selectedChecklistItem = ref(null);
const showEditChecklistItem = ref(false);
const checklistCompletionReport = ref(null);

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

const canCompleteFullscreen = computed(() => {
  if (fullscreenMode.value === 'place-checkpoint') {
    return tempLocationCoords.value !== null;
  } else if (fullscreenMode.value === 'move-checkpoint') {
    return tempLocationCoords.value !== null;
  } else if (fullscreenMode.value === 'draw-area') {
    // Enable Done button if there's a completed polygon or at least 3 points drawn
    return selectedArea.value?.polygon?.length > 0 ||
           (currentDrawingPolygon.value && currentDrawingPolygon.value.length >= 3);
  } else if (fullscreenMode.value === 'add-multiple') {
    return true; // Always enabled for multiple mode
  }
  return false;
});

// Combine real checkpoints with temporary ones for display
const displayCheckpoints = computed(() => {
  const checkpoints = [...locationStatuses.value];

  // Add temporary preview for single checkpoint placement
  if (fullscreenMode.value === 'place-checkpoint' && tempLocationCoords.value) {
    checkpoints.push({
      id: 'temp-single',
      name: locationForm.value.name || 'New Checkpoint',
      latitude: tempLocationCoords.value.lat,
      longitude: tempLocationCoords.value.lng,
      requiredMarshals: locationForm.value.requiredMarshals || 1,
      assignments: [],
      isTemporary: true,
    });
  }

  // Add temporary preview for move checkpoint
  if (fullscreenMode.value === 'move-checkpoint' && tempLocationCoords.value && selectedLocation.value) {
    checkpoints.push({
      id: 'temp-move',
      name: selectedLocation.value.name + ' (new position)',
      latitude: tempLocationCoords.value.lat,
      longitude: tempLocationCoords.value.lng,
      requiredMarshals: selectedLocation.value.requiredMarshals,
      assignments: [],
      isTemporary: true,
    });
  }

  // Add temporary checkpoints for multiple placement mode
  if (fullscreenMode.value === 'add-multiple') {
    checkpoints.push(...tempCheckpoints.value);
  }

  return checkpoints;
});

// Combine real areas with the area being drawn/edited
const displayAreas = computed(() => {
  const areasList = [...areas.value];

  // If drawing or editing an area with a polygon, include it in display
  if (fullscreenMode.value === 'draw-area' && selectedArea.value?.polygon?.length > 0) {
    // Check if this area already exists in the list
    const existingIndex = areasList.findIndex(a => a.id === selectedArea.value.id);
    if (existingIndex >= 0) {
      // Replace existing area with updated version
      areasList[existingIndex] = { ...selectedArea.value };
    } else {
      // Add new area being drawn
      areasList.push({
        ...selectedArea.value,
        id: selectedArea.value.id || 'temp-area',
      });
    }
  }

  return areasList;
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
    await loadAreas();
    await loadChecklists();
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

const loadAreas = async () => {
  try {
    const response = await areasApi.getByEvent(route.params.eventId);
    areas.value = response.data;
  } catch (error) {
    console.error('Failed to load areas:', error);
  }
};

const loadChecklists = async () => {
  try {
    const [itemsResponse, reportResponse] = await Promise.all([
      checklistApi.getByEvent(route.params.eventId),
      checklistApi.getReport(route.params.eventId),
    ]);
    checklistItems.value = itemsResponse.data;
    checklistCompletionReport.value = reportResponse.data;
  } catch (error) {
    console.error('Failed to load checklists:', error);
  }
};

const handleAddArea = () => {
  selectedArea.value = null;
  formDirty.value = false;
  showEditArea.value = true;
};

const handleSelectArea = (area) => {
  selectedArea.value = area;
  selectedAreaId.value = area.id;
  formDirty.value = false;
  showEditArea.value = true;
};

const handleSaveArea = async (formData) => {
  try {
    if (selectedArea.value && selectedArea.value.id) {
      // Update existing area (checkpoints will be automatically recalculated on backend)
      await areasApi.update(route.params.eventId, selectedArea.value.id, formData);
    } else {
      // Create new area (checkpoints will be automatically assigned on backend)
      await areasApi.create({
        eventId: route.params.eventId,
        ...formData,
      });
    }

    await loadAreas();
    await loadEventData(); // Reload to get updated checkpoint area assignments
    closeEditAreaModal();
  } catch (error) {
    console.error('Failed to save area:', error);
    alert(error.response?.data?.message || 'Failed to save area. Please try again.');
  }
};

const handleDeleteArea = async (areaId) => {
  showConfirm('Delete Area', 'Are you sure you want to delete this area? Checkpoints will be automatically reassigned.', async () => {
    try {
      // Delete the area (checkpoints will be automatically reassigned to default area on backend)
      await areasApi.delete(route.params.eventId, areaId);

      // Trigger recalculation to reassign checkpoints that were in this area
      await areasApi.recalculate(route.params.eventId);

      await loadAreas();
      await loadEventData();
      closeEditAreaModal();
    } catch (error) {
      console.error('Failed to delete area:', error);
      alert(error.response?.data?.message || 'Failed to delete area. Please try again.');
    }
  });
};

const closeEditAreaModal = () => {
  showEditArea.value = false;
  selectedArea.value = null;
  selectedAreaId.value = null;
  formDirty.value = false;
  isDrawingAreaBoundary.value = false;
  pendingAreaFormData.value = null;
};

const handleAddChecklistItem = () => {
  selectedChecklistItem.value = null;
  formDirty.value = false;
  showEditChecklistItem.value = true;
};

const handleSelectChecklistItem = (item) => {
  selectedChecklistItem.value = item;
  formDirty.value = false;
  showEditChecklistItem.value = true;
};

const handleSaveChecklistItem = async (formData) => {
  try {
    if (selectedChecklistItem.value && selectedChecklistItem.value.itemId) {
      await checklistApi.update(route.params.eventId, selectedChecklistItem.value.itemId, formData);
    } else {
      const response = await checklistApi.create(route.params.eventId, formData);

      // Show success message if multiple items were created
      if (response.data.count && response.data.count > 1) {
        infoModalTitle.value = 'Checklist Items Created';
        infoModalMessage.value = `Successfully created ${response.data.count} checklist items.`;
        showInfoModal.value = true;
      }
    }

    await loadChecklists();
    closeEditChecklistItemModal();
  } catch (error) {
    console.error('Failed to save checklist item:', error);
    alert(error.response?.data?.message || 'Failed to save checklist item. Please try again.');
  }
};

const handleDeleteChecklistItem = async (itemId) => {
  showConfirm('Delete Checklist Item', 'Are you sure you want to delete this checklist item?', async () => {
    try {
      await checklistApi.delete(route.params.eventId, itemId);
      await loadChecklists();
      closeEditChecklistItemModal();
    } catch (error) {
      console.error('Failed to delete checklist item:', error);
      alert(error.response?.data?.message || 'Failed to delete checklist item. Please try again.');
    }
  });
};

const closeEditChecklistItemModal = () => {
  showEditChecklistItem.value = false;
  selectedChecklistItem.value = null;
  formDirty.value = false;
};

const handleDrawBoundary = (formData) => {
  // Store form data
  pendingAreaFormData.value = formData;
  // Close modal
  showEditArea.value = false;

  // Capture current map state before entering fullscreen
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();

    // Only use captured values if they're valid, otherwise keep null for auto-fit
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Enter fullscreen with drawing mode
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'draw-area';
  isDrawingAreaBoundary.value = true;
  fullscreenContext.value = {
    title: 'Draw area boundary',
    description: 'Click to add points. Click Done when finished to complete the area.',
  };
};

const handlePolygonComplete = (coordinates) => {
  // Store polygon coordinates, but don't exit fullscreen yet - wait for Done button
  if (pendingAreaFormData.value) {
    selectedArea.value = {
      ...(selectedArea.value || {}),
      ...pendingAreaFormData.value,
      polygon: coordinates,
    };
  } else {
    selectedArea.value = {
      ...selectedArea.value,
      polygon: coordinates,
    };
  }
  // Don't exit fullscreen - user needs to click Done
};

const handlePolygonDrawing = (points) => {
  // Store intermediate polygon points as user draws
  currentDrawingPolygon.value = points;
};

const handleCancelDrawing = () => {
  // Disable drawing mode
  isDrawingAreaBoundary.value = false;

  // Restore the area modal if there was pending data
  if (pendingAreaFormData.value) {
    selectedArea.value = pendingAreaFormData.value;
    showEditArea.value = true;
    pendingAreaFormData.value = null;
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

const handleMapClick = async (coords) => {
  if (showAddLocation.value) {
    // User has add location modal open, set coordinates
    locationForm.value.latitude = coords.lat;
    locationForm.value.longitude = coords.lng;
    // Don't mark dirty for map click - only mark dirty when user types in form fields
  } else if (isMovingLocation.value) {
    // User is moving an existing location
    editLocationForm.value.latitude = coords.lat;
    editLocationForm.value.longitude = coords.lng;
    isMovingLocation.value = false;
    showEditLocation.value = true;
    markFormDirty();
  } else if (isAddingMultipleCheckpoints.value) {
    // User is adding multiple checkpoints
    try {
      await eventsStore.createLocation({
        eventId: route.params.eventId,
        name: `Checkpoint ${multiCheckpointCounter.value}`,
        description: '',
        latitude: coords.lat,
        longitude: coords.lng,
        requiredMarshals: 1,
        what3Words: '',
      });
      multiCheckpointCounter.value++;
      await loadEventData();
    } catch (error) {
      console.error('Failed to create checkpoint:', error);
      alert('Failed to create checkpoint. Please try again.');
    }
  }
  // Removed auto-opening add location modal - user must explicitly click "Add checkpoint"
};

const handleFullscreenMapClick = (coords) => {
  if (fullscreenMode.value === 'place-checkpoint') {
    // Allow repositioning - last click wins
    tempLocationCoords.value = coords;
  } else if (fullscreenMode.value === 'move-checkpoint') {
    // Preview new location
    tempLocationCoords.value = coords;
  } else if (fullscreenMode.value === 'add-multiple') {
    // Instant creation (existing behavior)
    createMultipleCheckpoint(coords);
  }
};

const handleAddLocationClick = () => {
  // Capture current map state before opening modal
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Reset the form
  locationForm.value = {
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };

  // Open the modal
  showAddLocation.value = true;
};

const handleLocationFormUpdate = (updatedLocation) => {
  // Sync form data from modal back to locationForm
  locationForm.value = { ...updatedLocation };
};

const handleSetLocationOnMap = () => {
  // Close the modal temporarily
  showAddLocation.value = false;

  // Reset temp coordinates
  tempLocationCoords.value = null;

  // Map state already captured in handleAddLocationClick

  // Enter fullscreen
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'place-checkpoint';
  fullscreenContext.value = {
    title: 'Select checkpoint location',
    description: 'Click on the map to set the location. Click again to reposition.',
  };
};

const handleAddCheckpointFromMap = () => {
  // Capture current map state before entering fullscreen
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Reset the form
  locationForm.value = {
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };

  // Reset temp coordinates
  tempLocationCoords.value = null;

  // Enter fullscreen directly
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'place-checkpoint';
  fullscreenContext.value = {
    title: 'Select checkpoint location',
    description: 'Click on the map to set the location. Click again to reposition.',
  };
};

const handleAddAreaFromMap = () => {
  // Capture current map state before entering fullscreen
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Create new area with empty data
  selectedArea.value = {
    id: null,
    name: '',
    description: '',
    color: '#667eea',
    polygon: [],
    checkpointIds: [],
    marshalIds: [],
    displayOrder: areas.value.length,
  };

  // Enter fullscreen with drawing mode
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'draw-area';
  isDrawingAreaBoundary.value = true;
  fullscreenContext.value = {
    title: 'Draw area boundary',
    description: 'Click to add points. Click Done when finished to complete the area.',
  };
};

const handleAddMultipleCheckpointsClick = () => {
  // Find the highest checkpoint number to continue from
  const checkpointNumbers = locationStatuses.value
    .map(loc => {
      const match = loc.name.match(/^Checkpoint (\d+)$/);
      return match ? parseInt(match[1]) : 0;
    })
    .filter(n => n > 0);

  multiCheckpointCounter.value = checkpointNumbers.length > 0
    ? Math.max(...checkpointNumbers) + 1
    : 1;

  // Capture current map state before entering fullscreen
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Enter fullscreen
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'add-multiple';
  isAddingMultipleCheckpoints.value = true;
  fullscreenContext.value = {
    title: 'Add multiple checkpoints',
    description: `Click on map to add checkpoints. Next: Checkpoint ${multiCheckpointCounter.value}`,
  };
};

const handleCancelMultipleCheckpoints = () => {
  isAddingMultipleCheckpoints.value = false;
};

const createMultipleCheckpoint = (coords) => {
  // Add to temporary array instead of saving immediately
  tempCheckpoints.value.push({
    id: `temp-${multiCheckpointCounter.value}`,
    name: `Checkpoint ${multiCheckpointCounter.value}`,
    description: '',
    latitude: coords.lat,
    longitude: coords.lng,
    requiredMarshals: 1,
    what3Words: '',
    assignments: [],
    isTemporary: true,
  });

  multiCheckpointCounter.value++;
  // Update context description with next number (reassign whole object for reactivity)
  fullscreenContext.value = {
    ...fullscreenContext.value,
    description: `Click on map to add checkpoints. Next: Checkpoint ${multiCheckpointCounter.value}`,
  };
};

const handleFullscreenDone = () => {
  if (fullscreenMode.value === 'place-checkpoint') {
    // Confirm placement
    if (tempLocationCoords.value) {
      locationForm.value.latitude = tempLocationCoords.value.lat;
      locationForm.value.longitude = tempLocationCoords.value.lng;
      markFormDirty();
    }
    // Exit fullscreen and reopen modal
    exitFullscreen();
    showAddLocation.value = true;

  } else if (fullscreenMode.value === 'move-checkpoint') {
    // Confirm move
    if (tempLocationCoords.value) {
      editLocationForm.value.latitude = tempLocationCoords.value.lat;
      editLocationForm.value.longitude = tempLocationCoords.value.lng;
      markFormDirty();
    }
    // Exit fullscreen and reopen modal
    exitFullscreen();
    showEditLocation.value = true;

  } else if (fullscreenMode.value === 'draw-area') {
    // Complete the polygon and exit
    // If user hasn't finished drawing with double-click, use current polygon points
    if (currentDrawingPolygon.value && currentDrawingPolygon.value.length >= 3) {
      const coordinates = currentDrawingPolygon.value.map(p => ({
        lat: p.lat,
        lng: p.lng
      }));

      // If there's pending form data, merge it; otherwise just update polygon
      if (pendingAreaFormData.value) {
        selectedArea.value = {
          ...(selectedArea.value || {}),
          ...pendingAreaFormData.value,
          polygon: coordinates,
        };
      } else {
        selectedArea.value = {
          ...(selectedArea.value || {}),
          polygon: coordinates,
        };
      }
    }

    isDrawingAreaBoundary.value = false;
    exitFullscreen();
    showEditArea.value = true;
    pendingAreaFormData.value = null;
    currentDrawingPolygon.value = null;

  } else if (fullscreenMode.value === 'add-multiple') {
    // Save all temporary checkpoints
    saveTempCheckpoints();
  }
};

const saveTempCheckpoints = async () => {
  const checkpointsToSave = [...tempCheckpoints.value];

  try {
    // Save all checkpoints
    for (const checkpoint of checkpointsToSave) {
      await eventsStore.createLocation({
        eventId: route.params.eventId,
        name: checkpoint.name,
        description: checkpoint.description,
        latitude: checkpoint.latitude,
        longitude: checkpoint.longitude,
        requiredMarshals: checkpoint.requiredMarshals,
        what3Words: checkpoint.what3Words,
      });
    }

    // Clear temp checkpoints and exit
    tempCheckpoints.value = [];
    isAddingMultipleCheckpoints.value = false;
    exitFullscreen();

    // Reload to show saved checkpoints
    await loadEventData();
  } catch (error) {
    console.error('Failed to create checkpoints:', error);
    alert('Failed to create checkpoints. Please try again.');
  }
};

const handleFullscreenCancel = () => {
  if (fullscreenMode.value === 'draw-area') {
    // Restore modal if there was pending data
    if (pendingAreaFormData.value) {
      selectedArea.value = pendingAreaFormData.value;
      showEditArea.value = true;
      pendingAreaFormData.value = null;
    }
    isDrawingAreaBoundary.value = false;
    currentDrawingPolygon.value = null;
  } else if (fullscreenMode.value === 'add-multiple') {
    // Clear temp checkpoints without saving
    tempCheckpoints.value = [];
    isAddingMultipleCheckpoints.value = false;
  }
  exitFullscreen();
};

const exitFullscreen = () => {
  isFullscreenMapActive.value = false;
  fullscreenMode.value = null;
  fullscreenContext.value = {};
  tempLocationCoords.value = null;
  savedMapCenter.value = null;
  savedMapZoom.value = null;
  // Always ensure drawing mode is disabled when exiting fullscreen
  if (isDrawingAreaBoundary.value) {
    isDrawingAreaBoundary.value = false;
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
  // Scroll map into view
  setTimeout(() => {
    courseAreasTab.value?.scrollMapIntoView();
  }, 100);
};

const handleSaveLocation = async (formData) => {
  if (!isValidWhat3Words(formData.what3Words)) {
    alert('Invalid What3Words format. Please use word.word.word or word/word/word (lowercase letters only, 1-20 characters each)');
    return;
  }

  try {
    await eventsStore.createLocation({
      eventId: route.params.eventId,
      ...formData,
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
  // Close edit modal
  showEditLocation.value = false;
  // Reset temp coordinates
  tempLocationCoords.value = null;

  // Capture current map state before entering fullscreen
  if (courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
    if (center && zoom !== null) {
      savedMapCenter.value = center;
      savedMapZoom.value = zoom;
    }
  }

  // Enter fullscreen
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'move-checkpoint';
  fullscreenContext.value = {
    title: `Move checkpoint: ${selectedLocation.value?.name}`,
    description: 'Click on the map to set the new location',
  };
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
        // areaId removed - areas are auto-assigned by backend based on polygon boundaries
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
  overflow-y: hidden;
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
}
</style>
