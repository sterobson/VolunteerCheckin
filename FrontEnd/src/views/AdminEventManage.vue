<template>
  <div class="admin-event-manage" :style="{ colorScheme }">
    <header class="header">
      <div class="header-left">
        <button @click="goBack" class="btn-back" title="Back to Dashboard">← Dashboard</button>
        <div v-if="event">
          <h1>{{ event.name }}</h1>
          <p class="event-date">{{ formatEventDate(event.eventDate) }}</p>
        </div>
      </div>
      <div class="header-actions">
        <ThemeToggle />
        <button v-if="canSwitchToMarshal" @click="switchToMarshalMode" class="btn btn-marshal-mode">
          Switch to Marshal
        </button>
        <button @click="goToProfile" class="btn btn-secondary">Profile</button>
      </div>
    </header>

    <div class="container">
      <!-- Tabs Navigation -->
      <div class="tabs-nav">
        <ResponsiveTabHeader
          :tabs="mainTabs"
          :model-value="activeTab"
          @update:model-value="switchTab"
        />
      </div>

      <!-- Tab Components -->
      <div class="tab-content-wrapper">
        <CourseAreasTab
          v-if="activeTab === 'course'"
          ref="courseAreasTab"
          :checkpoints="locationStatuses"
          :areas="areas"
          :route="event?.route || []"
          :selected-area-id="selectedAreaId"
          :drawing-mode="isDrawingAreaBoundary"
          @map-click="handleMapClick"
          @location-click="handleLocationClick"
          @area-click="handleSelectArea"
          @import-checkpoints="showImportLocations = true"
          @upload-route="showUploadRoute = true"
          @select-location="selectLocation"
          @add-area="handleAddArea"
          @select-area="handleSelectArea"
          @polygon-complete="handlePolygonComplete"
          @cancel-drawing="handleCancelDrawing"
          @add-checkpoint-from-map="handleAddCheckpointFromMap"
          @add-many-checkpoints-from-map="handleAddMultipleCheckpointsClick"
          @add-area-from-map="handleAddAreaFromMap"
        />

        <div v-if="activeTab === 'checkpoints'" class="tab-panel">
          <CheckpointsList
            :locations="locationStatuses"
            :areas="areas"
            @add-checkpoint-manually="handleAddCheckpointManually"
            @import-checkpoints="showImportLocations = true"
            @select-location="selectLocation"
          />
        </div>

        <div v-if="activeTab === 'areas'" class="tab-panel">
          <AreasList
            :areas="areas"
            :checkpoints="locationStatuses"
            :contacts="contacts"
            :event-people-term="event?.peopleTerm || 'Marshals'"
            :event-checkpoint-term="event?.checkpointTerm || 'Checkpoints'"
            @add-area="handleAddArea"
            @select-area="handleSelectArea"
          />
        </div>

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

        <NotesTab
          v-if="activeTab === 'notes'"
          :notes="notes"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          @add-note="handleAddNote"
          @select-note="handleSelectNote"
        />

        <ContactsTab
          v-if="activeTab === 'contacts'"
          :contacts="contacts"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          :available-roles="contactRoles"
          @add-contact="handleAddContact"
          @select-contact="handleSelectContact"
        />

        <EventDetailsTab
          v-if="activeTab === 'details'"
          :event-data="eventDetailsForm"
          :form-dirty="eventDetailsFormDirty"
          @submit="handleUpdateEvent"
          @update:form-dirty="markEventDetailsFormDirty"
        />

        <SettingsTab
          v-if="activeTab === 'settings'"
          :event-data="eventDetailsForm"
          :event-id="route.params.eventId"
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
      :is-dirty="formDirty"
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
      :event-id="route.params.eventId"
      :all-locations="locationStatuses"
      :notes="notes"
      :available-marshals="availableMarshalsForAssignment"
      :all-checklist-items="checklistItems"
      :is-dirty="formDirty"
      :event-default-style-type="event?.defaultCheckpointStyleType || event?.DefaultCheckpointStyleType || 'default'"
      :event-default-style-color="event?.defaultCheckpointStyleColor || event?.DefaultCheckpointStyleColor || ''"
      :event-default-style-background-shape="event?.defaultCheckpointStyleBackgroundShape || event?.DefaultCheckpointStyleBackgroundShape || ''"
      :event-default-style-background-color="event?.defaultCheckpointStyleBackgroundColor || event?.DefaultCheckpointStyleBackgroundColor || ''"
      :event-default-style-border-color="event?.defaultCheckpointStyleBorderColor || event?.DefaultCheckpointStyleBorderColor || ''"
      :event-default-style-icon-color="event?.defaultCheckpointStyleIconColor || event?.DefaultCheckpointStyleIconColor || ''"
      :event-default-style-size="event?.defaultCheckpointStyleSize || event?.DefaultCheckpointStyleSize || ''"
      :event-default-style-map-rotation="event?.defaultCheckpointStyleMapRotation ?? event?.DefaultCheckpointStyleMapRotation ?? ''"
      :event-people-term="event?.peopleTerm || 'Marshals'"
      :z-index="showEditArea ? 1100 : 1000"
      @close="closeEditLocationModal"
      @save="handleUpdateLocation"
      @delete="handleDeleteLocation"
      @move-location="startMoveLocation"
      @remove-assignment="handleRemoveLocationAssignment"
      @open-assign-modal="openCreateMarshalModal"
      @update:isDirty="markFormDirty"
    />

    <ShareLinkModal
      :show="showShareLink"
      :link="marshalLink"
      :is-dirty="false"
      @close="showShareLink = false"
    />

    <MarshalCreatedModal
      :show="showMarshalCreated"
      :event-id="route.params.eventId"
      :marshal-id="newlyCreatedMarshalId"
      :marshal-name="newlyCreatedMarshalName"
      @close="showMarshalCreated = false"
    />

    <AddAdminModal
      :show="showAddAdmin"
      :is-dirty="formDirty"
      @close="closeAdminModal"
      @submit="handleAddAdminSubmit"
      @update:isDirty="markFormDirty"
    />

    <UploadRouteModal
      :show="showUploadRoute"
      :uploading="uploading"
      :error="uploadError"
      :is-dirty="formDirty"
      @close="closeRouteModal"
      @submit="handleUploadRouteSubmit"
      @update:isDirty="markFormDirty"
    />

    <ImportLocationsModal
      :show="showImportLocations"
      :importing="importing"
      :error="importError"
      :result="importResult"
      :is-dirty="formDirty"
      @close="closeImportModal"
      @submit="handleImportLocationsSubmit"
      @update:isDirty="markFormDirty"
    />

    <EditMarshalModal
      :show="showEditMarshal"
      :marshal="selectedMarshal"
      :event-id="route.params.eventId"
      :assignments="getMarshalAssignmentsForEdit()"
      :available-locations="availableMarshalLocations"
      :all-locations="locationStatuses"
      :areas="areas"
      :is-editing="!!selectedMarshal"
      :event-notes="notes"
      :all-checklist-items="checklistItems"
      :is-dirty="formDirty"
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
      :is-dirty="formDirty"
      @close="closeImportMarshalsModal"
      @submit="handleImportMarshalsSubmit"
      @update:isDirty="markFormDirty"
    />

    <AssignmentConflictModal
      :show="showAssignmentConflict"
      :conflict-data="assignmentConflictData"
      @close="showAssignmentConflict = false"
      @choice="handleConflictChoice"
    />

    <AssignMarshalModal
      :show="showAssignMarshalModal"
      :location-name="selectedLocation?.name || ''"
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
      :is-dirty="formDirty"
      :event-id="route.params.eventId"
      :all-locations="locationStatuses"
      :assignments="allAssignments"
      :notes="notes"
      :contacts="contacts"
      :event-default-style-type="event?.defaultCheckpointStyleType || event?.DefaultCheckpointStyleType || 'default'"
      :event-default-style-color="event?.defaultCheckpointStyleColor || event?.DefaultCheckpointStyleColor || ''"
      :event-default-style-background-shape="event?.defaultCheckpointStyleBackgroundShape || event?.DefaultCheckpointStyleBackgroundShape || ''"
      :event-default-style-background-color="event?.defaultCheckpointStyleBackgroundColor || event?.DefaultCheckpointStyleBackgroundColor || ''"
      :event-default-style-border-color="event?.defaultCheckpointStyleBorderColor || event?.DefaultCheckpointStyleBorderColor || ''"
      :event-default-style-icon-color="event?.defaultCheckpointStyleIconColor || event?.DefaultCheckpointStyleIconColor || ''"
      :event-default-style-size="event?.defaultCheckpointStyleSize || event?.DefaultCheckpointStyleSize || ''"
      :event-default-style-map-rotation="event?.defaultCheckpointStyleMapRotation ?? event?.DefaultCheckpointStyleMapRotation ?? ''"
      :event-people-term="event?.peopleTerm || 'Marshals'"
      :event-checkpoint-term="event?.checkpointTerm || 'Checkpoints'"
      @close="closeEditAreaModal"
      @save="handleSaveArea"
      @delete="handleDeleteArea"
      @draw-boundary="handleDrawBoundary"
      @update:isDirty="markFormDirty"
      @select-checkpoint="selectLocation"
      @add-area-contact="handleAddAreaContact"
      @edit-contact="handleEditContactFromArea"
    />

    <EditChecklistItemModal
      :show="showEditChecklistItem"
      :checklist-item="selectedChecklistItem"
      :initial-tab="checklistItemInitialTab"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :is-dirty="formDirty"
      @close="closeEditChecklistItemModal"
      @save="handleSaveChecklistItem"
      @delete="handleDeleteChecklistItem"
      @update:isDirty="markFormDirty"
    />

    <EditNoteModal
      :show="showEditNote"
      :note="selectedNote"
      :initial-tab="noteInitialTab"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :is-dirty="formDirty"
      @close="closeEditNoteModal"
      @save="handleSaveNote"
      @delete="handleDeleteNote"
      @update:isDirty="markFormDirty"
    />

    <EditContactModal
      :show="showEditContact"
      :contact="selectedContact"
      :initial-tab="contactInitialTab"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :available-roles="contactRoles"
      :is-dirty="formDirty"
      :preselected-area="pendingAreaForContact"
      @close="closeEditContactModal"
      @save="handleSaveContact"
      @delete="handleDeleteContact"
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
// NOTE: This file has been refactored to use tab components and composables
// Original file: 2,981 lines → Current: ~2,900 lines
// Further reduction available by migrating to composables progressively

import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useEventsStore } from '../stores/events';
import { authApi, checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi, areasApi, checklistApi, notesApi, contactsApi } from '../services/api';

// Composables
import { useTabs } from '../composables/useTabs';
import { useConfirmDialog } from '../composables/useConfirmDialog';
import { setTerminology, useTerminology } from '../composables/useTerminology';
import { useFullscreenMap } from '../composables/useFullscreenMap';
import { useAdminAreaManagement } from '../composables/useAdminAreaManagement';
import { useAdminChecklistManagement } from '../composables/useAdminChecklistManagement';
import { useAdminNoteManagement } from '../composables/useAdminNoteManagement';
import { useAdminContactManagement } from '../composables/useAdminContactManagement';
import { useEventAdmins } from '../composables/useEventAdmins';
import { useImportExport } from '../composables/useImportExport';
import { useAdminTheme } from '../composables/useAdminTheme';

// Utilities
import { formatDate as formatEventDate, formatDateForInput } from '../utils/dateFormatters';
import { isValidWhat3Words } from '../utils/validators';
import { calculateDistance } from '../utils/coordinateUtils';
import { getCheckpointsInPolygon } from '../utils/geometryUtils';
import { CHECK_IN_RADIUS_METERS } from '../constants/app';

// Tab Components
import CourseAreasTab from './AdminEventManage/CourseAreasTab.vue';
import MarshalsTab from './AdminEventManage/MarshalsTab.vue';
import ChecklistsTab from './AdminEventManage/ChecklistsTab.vue';
import NotesTab from './AdminEventManage/NotesTab.vue';
import ContactsTab from './AdminEventManage/ContactsTab.vue';
import EventDetailsTab from './AdminEventManage/EventDetailsTab.vue';
import SettingsTab from './AdminEventManage/SettingsTab.vue';
import CheckpointsList from '../components/event-manage/lists/CheckpointsList.vue';
import AreasList from '../components/event-manage/lists/AreasList.vue';
import ResponsiveTabHeader from '../components/ResponsiveTabHeader.vue';

// Modal Components
import ShareLinkModal from '../components/event-manage/modals/ShareLinkModal.vue';
import MarshalCreatedModal from '../components/event-manage/modals/MarshalCreatedModal.vue';
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
import EditNoteModal from '../components/event-manage/modals/EditNoteModal.vue';
import EditContactModal from '../components/event-manage/modals/EditContactModal.vue';
import InfoModal from '../components/InfoModal.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import FullscreenMapOverlay from '../components/FullscreenMapOverlay.vue';
import AddLocationModal from '../components/event-manage/modals/AddLocationModal.vue';
import ThemeToggle from '../components/ThemeToggle.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

// Reactive event ID for composables
const eventId = computed(() => route.params.eventId);

// Use composables
const { activeTab, switchTab } = useTabs('details', ['details', 'course', 'areas', 'checkpoints', 'marshals', 'notes', 'checklists', 'contacts', 'settings']);
const {
  showConfirmModal,
  confirmModalTitle,
  confirmModalMessage,
  showConfirm,
  handleConfirmModalConfirm,
  handleConfirmModalCancel,
} = useConfirmDialog();

// Terminology
const { tabLabels, terms, termsLower, termsSentence } = useTerminology();

// Fullscreen map composable
const fullscreenMap = useFullscreenMap();

// Import/Export composable
const importExport = useImportExport(eventId);

// Event admins composable
const eventAdminsComposable = useEventAdmins(eventId);

// Theme composable
const { colorScheme } = useAdminTheme();

// Computed tabs with dynamic labels from terminology
// Order: Details, Course, Zones, Locations, Volunteers, Notes, Tasks, Contacts
const mainTabs = computed(() => [
  { value: 'details', label: 'Event details', icon: 'details' },
  { value: 'course', label: tabLabels.value.course, icon: 'course' },
  { value: 'areas', label: tabLabels.value.areas, icon: 'area' },
  { value: 'checkpoints', label: tabLabels.value.checkpoints, icon: 'checkpoint' },
  { value: 'marshals', label: tabLabels.value.marshals, icon: 'marshal' },
  { value: 'notes', label: 'Notes', icon: 'notes' },
  { value: 'checklists', label: tabLabels.value.checklists, icon: 'checklist' },
  { value: 'contacts', label: 'Contacts', icon: 'contacts' },
  { value: 'settings', label: 'Settings', icon: 'settings' },
]);

// State
const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);
const selectedLocation = ref(null);
const showAddLocation = ref(false);
const courseAreasTab = ref(null);
const showShareLink = ref(false);
const showMarshalCreated = ref(false);
const newlyCreatedMarshalId = ref(null);
const newlyCreatedMarshalName = ref('');

// Event admins - use composable state
const eventAdmins = eventAdminsComposable.eventAdmins;
const showAddAdmin = eventAdminsComposable.showAddAdmin;

// Import/Export - use composable state
const showUploadRoute = importExport.showUploadRoute;
const uploading = importExport.uploading;
const uploadError = importExport.uploadError;
const showImportLocations = importExport.showImportLocations;
const showImportMarshals = importExport.showImportMarshals;
const importing = importExport.importing;
const importingMarshals = importExport.importingMarshals;
const importError = importExport.importError;
const importResult = importExport.importResult;
const importMarshalsResult = importExport.importMarshalsResult;
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
  peopleTerm: 'Marshals',
  defaultCheckpointStyleBackgroundShape: '',
  defaultCheckpointStyleBackgroundColor: '',
  defaultCheckpointStyleBorderColor: '',
  defaultCheckpointStyleIconColor: '',
  defaultCheckpointStyleSize: '',
  defaultCheckpointStyleMapRotation: '',
  checkpointTerm: 'Checkpoints',
  areaTerm: 'Areas',
  checklistTerm: 'Checklists',
  courseTerm: 'Course',
  defaultCheckpointStyleType: 'default',
  defaultCheckpointStyleColor: '',
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
const pendingAreaFormData = ref(null);
// Fullscreen map - use composable state
const isAddingMultipleCheckpoints = fullscreenMap.isAddingMultipleCheckpoints;
const multiCheckpointCounter = fullscreenMap.multiCheckpointCounter;
const isFullscreenMapActive = fullscreenMap.isFullscreenMapActive;
const fullscreenMode = fullscreenMap.fullscreenMode;
const fullscreenContext = fullscreenMap.fullscreenContext;
const tempLocationCoords = fullscreenMap.tempLocationCoords;
const currentDrawingPolygon = fullscreenMap.currentDrawingPolygon;
const tempCheckpoints = fullscreenMap.tempCheckpoints;
const savedMapCenter = fullscreenMap.savedMapCenter;
const savedMapZoom = fullscreenMap.savedMapZoom;
const isDrawingAreaBoundary = fullscreenMap.isDrawingAreaBoundary;
const preservedLocationFormData = ref(null); // Keep this local - used for location modal state
const checklistItems = ref([]);
const selectedChecklistItem = ref(null);
const showEditChecklistItem = ref(false);
const checklistItemInitialTab = ref('details');
const checklistCompletionReport = ref(null);
const notes = ref([]);
const selectedNote = ref(null);
const showEditNote = ref(false);
const noteInitialTab = ref('details');
const contacts = ref([]);
const contactRoles = ref({ builtInRoles: [], customRoles: [] });
const selectedContact = ref(null);
const showEditContact = ref(false);
const contactInitialTab = ref('details');
const pendingAreaForContact = ref(null);
const userClaims = ref(null);

// Mode switching
const canSwitchToMarshal = computed(() => {
  return userClaims.value?.marshalId != null;
});

const switchToMarshalMode = () => {
  router.push(`/event/${route.params.eventId}`);
};

// Computed
const marshalLink = computed(() => {
  return `${window.location.origin}/#/event/${route.params.eventId}`;
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

// Use composable's canCompleteFullscreen but extend for selectedArea polygon check
const canCompleteFullscreen = computed(() => {
  if (fullscreenMode.value === 'draw-area') {
    return selectedArea.value?.polygon?.length > 0 || fullscreenMap.canCompleteFullscreen.value;
  }
  return fullscreenMap.canCompleteFullscreen.value;
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

const goToProfile = () => {
  router.push({ name: 'AdminProfile' });
};

const shareEvent = () => {
  showShareLink.value = true;
};

const markFormDirty = (isDirty = true) => {
  formDirty.value = isDirty;
};

const markEventDetailsFormDirty = () => {
  eventDetailsFormDirty.value = true;
};

const handleUpdateEvent = async (formData) => {
  try {
    // Update the parent's form with the child's data
    if (formData) {
      eventDetailsForm.value = { ...formData };
    }

    // Compare dates in the same format (both as input-formatted strings)
    const oldEventDate = formatDateForInput(event.value.eventDate);
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
      // Ensure map rotation is sent as string to match backend DTO
      defaultCheckpointStyleMapRotation: eventDetailsForm.value.defaultCheckpointStyleMapRotation != null
        ? String(eventDetailsForm.value.defaultCheckpointStyleMapRotation)
        : null,
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
    showConfirm('Unsaved changes', 'You have unsaved changes. Are you sure you want to close without saving?', async () => {
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
    // Phase 1: Fetch event first (needed for form setup)
    await eventsStore.fetchEvent(route.params.eventId);
    event.value = eventsStore.currentEvent;

    if (event.value) {
      eventDetailsForm.value = {
        name: event.value.name || '',
        description: event.value.description || '',
        eventDate: formatDateForInput(event.value.eventDate),
        timeZoneId: event.value.timeZoneId || 'UTC',
        peopleTerm: event.value.peopleTerm || 'Marshals',
        checkpointTerm: event.value.checkpointTerm || 'Checkpoints',
        areaTerm: event.value.areaTerm || 'Areas',
        checklistTerm: event.value.checklistTerm || 'Checklists',
        courseTerm: event.value.courseTerm || 'Course',
        defaultCheckpointStyleType: event.value.defaultCheckpointStyleType || 'default',
        defaultCheckpointStyleColor: event.value.defaultCheckpointStyleColor || '',
        defaultCheckpointStyleBackgroundShape: event.value.defaultCheckpointStyleBackgroundShape || '',
        defaultCheckpointStyleBackgroundColor: event.value.defaultCheckpointStyleBackgroundColor || '',
        defaultCheckpointStyleBorderColor: event.value.defaultCheckpointStyleBorderColor || '',
        defaultCheckpointStyleIconColor: event.value.defaultCheckpointStyleIconColor || '',
        defaultCheckpointStyleSize: event.value.defaultCheckpointStyleSize || '',
        defaultCheckpointStyleMapRotation: event.value.defaultCheckpointStyleMapRotation ?? '',
        // Branding fields
        brandingHeaderGradientStart: event.value.brandingHeaderGradientStart || '',
        brandingHeaderGradientEnd: event.value.brandingHeaderGradientEnd || '',
        brandingLogoUrl: event.value.brandingLogoUrl || '',
        brandingLogoPosition: event.value.brandingLogoPosition || '',
        brandingAccentColor: event.value.brandingAccentColor || '',
        brandingPageGradientStart: event.value.brandingPageGradientStart || '',
        brandingPageGradientEnd: event.value.brandingPageGradientEnd || '',
      };
      eventDetailsFormDirty.value = false;
      // Update global terminology settings
      setTerminology(event.value);
    }

    // Phase 2: Load all other data in parallel (8 independent calls)
    const results = await Promise.allSettled([
      eventsStore.fetchLocations(route.params.eventId),
      eventsStore.fetchEventStatus(route.params.eventId),
      loadEventAdmins(),
      loadMarshals(),
      loadAreas(),
      loadChecklists(),
      loadNotes(),
      loadContacts(),
    ]);

    // Update state from store after parallel fetches
    locations.value = eventsStore.locations;
    locationStatuses.value = eventsStore.eventStatus.locations;

    // Log any failures for debugging
    results.forEach((result, index) => {
      if (result.status === 'rejected') {
        const names = ['locations', 'eventStatus', 'admins', 'marshals', 'areas', 'checklists', 'notes', 'contacts'];
        console.error(`Failed to load ${names[index]}:`, result.reason);
      }
    });
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

const loadNotes = async () => {
  try {
    const response = await notesApi.getByEvent(route.params.eventId);
    notes.value = response.data;
  } catch (error) {
    console.error('Failed to load notes:', error);
  }
};

const loadContacts = async () => {
  try {
    const [contactsResponse, rolesResponse] = await Promise.all([
      contactsApi.getByEvent(route.params.eventId),
      contactsApi.getRoles(route.params.eventId),
    ]);
    contacts.value = contactsResponse.data;
    contactRoles.value = rolesResponse.data;
  } catch (error) {
    console.error('Failed to load contacts:', error);
  }
};

// Targeted reload functions for efficiency - avoid full reloads when only specific data changes
const reloadLocationsAndStatus = async () => {
  await Promise.all([
    eventsStore.fetchLocations(route.params.eventId),
    eventsStore.fetchEventStatus(route.params.eventId),
  ]);
  locations.value = eventsStore.locations;
  locationStatuses.value = eventsStore.eventStatus.locations;
};

const reloadMarshalsAndStatus = async () => {
  await Promise.all([
    loadMarshals(),
    eventsStore.fetchEventStatus(route.params.eventId),
  ]);
  locationStatuses.value = eventsStore.eventStatus.locations;
};

const reloadLocationsAreasAndStatus = async () => {
  await Promise.all([
    eventsStore.fetchLocations(route.params.eventId),
    eventsStore.fetchEventStatus(route.params.eventId),
    loadAreas(),
  ]);
  locations.value = eventsStore.locations;
  locationStatuses.value = eventsStore.eventStatus.locations;
};

const reloadStatusOnly = async () => {
  await eventsStore.fetchEventStatus(route.params.eventId);
  locationStatuses.value = eventsStore.eventStatus.locations;
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

      // Process checklist changes
      if (formData.checklistChanges && formData.checklistChanges.length > 0) {
        for (const change of formData.checklistChanges) {
          try {
            if (change.complete) {
              // Complete the item
              await checklistApi.complete(route.params.eventId, change.itemId, {
                marshalId: change.marshalId,
                contextType: change.contextType,
                contextId: change.contextId,
              });
            } else {
              // Uncomplete the item
              await checklistApi.uncomplete(route.params.eventId, change.itemId, {
                marshalId: change.marshalId,
                contextType: change.contextType,
                contextId: change.contextId,
              });
            }
          } catch (error) {
            console.error('Failed to update checklist item:', change, error);
            // Continue with other changes even if one fails
          }
        }
      }
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
  showConfirm('Delete area', 'Are you sure you want to delete this area? Checkpoints will be automatically reassigned.', async () => {
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

const handleAddAreaContact = (area) => {
  // Close the area modal and open contact modal with pre-configured area scope
  closeEditAreaModal();
  selectedContact.value = null;
  contactInitialTab.value = 'details';
  formDirty.value = false;
  // Store the area to pre-populate scope configuration
  pendingAreaForContact.value = area;
  showEditContact.value = true;
};

const handleEditContactFromArea = (contact) => {
  // Open contact modal without closing area modal - user can return to area modal after editing
  selectedContact.value = contact;
  contactInitialTab.value = 'details';
  formDirty.value = false;
  showEditContact.value = true;
};

const handleAddChecklistItem = () => {
  selectedChecklistItem.value = null;
  checklistItemInitialTab.value = 'details';
  formDirty.value = false;
  showEditChecklistItem.value = true;
};

const handleSelectChecklistItem = (item, tab = 'details') => {
  selectedChecklistItem.value = item;
  checklistItemInitialTab.value = tab;
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
    const errorMessage = error.response?.data?.message || 'Failed to save checklist item. Please try again.';
    alert(errorMessage);
  }
};

const handleDeleteChecklistItem = async (itemId) => {
  showConfirm('Delete checklist item', 'Are you sure you want to delete this checklist item?', async () => {
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

// Notes handlers
const handleAddNote = () => {
  selectedNote.value = null;
  noteInitialTab.value = 'details';
  formDirty.value = false;
  showEditNote.value = true;
};

const handleSelectNote = (note) => {
  selectedNote.value = note;
  noteInitialTab.value = 'details';
  formDirty.value = false;
  showEditNote.value = true;
};

const handleSaveNote = async (formData) => {
  try {
    if (selectedNote.value && selectedNote.value.noteId) {
      await notesApi.update(route.params.eventId, selectedNote.value.noteId, formData);
    } else {
      await notesApi.create(route.params.eventId, formData);
    }

    await loadNotes();
    closeEditNoteModal();
  } catch (error) {
    console.error('Failed to save note:', error);
    const errorMessage = error.response?.data?.message || 'Failed to save note. Please try again.';
    alert(errorMessage);
  }
};

const handleDeleteNote = async (noteId) => {
  showConfirm('Delete note', 'Are you sure you want to delete this note?', async () => {
    try {
      await notesApi.delete(route.params.eventId, noteId);
      await loadNotes();
      closeEditNoteModal();
    } catch (error) {
      console.error('Failed to delete note:', error);
      alert(error.response?.data?.message || 'Failed to delete note. Please try again.');
    }
  });
};

const closeEditNoteModal = () => {
  showEditNote.value = false;
  selectedNote.value = null;
  formDirty.value = false;
};

// Contacts handlers
const handleAddContact = () => {
  selectedContact.value = null;
  contactInitialTab.value = 'details';
  pendingAreaForContact.value = null;
  formDirty.value = false;
  showEditContact.value = true;
};

const handleSelectContact = (contact) => {
  selectedContact.value = contact;
  contactInitialTab.value = 'details';
  formDirty.value = false;
  showEditContact.value = true;
};

const handleSaveContact = async (formData) => {
  try {
    if (selectedContact.value && selectedContact.value.contactId) {
      await contactsApi.update(route.params.eventId, selectedContact.value.contactId, formData);
    } else {
      await contactsApi.create(route.params.eventId, formData);
    }

    await loadContacts();
    closeEditContactModal();
  } catch (error) {
    console.error('Failed to save contact:', error);
    const errorMessage = error.response?.data?.message || 'Failed to save contact. Please try again.';
    alert(errorMessage);
  }
};

const handleDeleteContact = async (contactId) => {
  showConfirm('Delete contact', 'Are you sure you want to delete this contact?', async () => {
    try {
      await contactsApi.delete(route.params.eventId, contactId);
      await loadContacts();
      closeEditContactModal();
    } catch (error) {
      console.error('Failed to delete contact:', error);
      alert(error.response?.data?.message || 'Failed to delete contact. Please try again.');
    }
  });
};

const closeEditContactModal = () => {
  showEditContact.value = false;
  selectedContact.value = null;
  pendingAreaForContact.value = null;
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
  fullscreenMap.handlePolygonDrawing(points);
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

// Event admins - use composable methods
const loadEventAdmins = () => eventAdminsComposable.loadEventAdmins();

const handleAddAdminSubmit = async (email) => {
  try {
    await eventAdminsComposable.addAdmin(email);
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
      await eventAdminsComposable.removeAdmin(userEmail);
      await loadEventAdmins();
    } catch (error) {
      console.error('Failed to remove admin:', error);
      alert(error.response?.data?.message || 'Failed to remove administrator. Please try again.');
    }
  });
};

const closeAdminModal = () => {
  eventAdminsComposable.closeAddAdminModal();
  formDirty.value = false;
};

const handleUploadRouteSubmit = async (file) => {
  const result = await importExport.uploadRoute(file);
  if (result) {
    await reloadLocationsAndStatus();
    closeRouteModal();
  }
};

const closeRouteModal = () => {
  importExport.closeUploadRouteModal();
};

const handleImportLocationsSubmit = async ({ file, deleteExisting }) => {
  const result = await importExport.importLocations(file, deleteExisting);
  if (result) {
    await reloadLocationsAreasAndStatus();
    closeImportModal();

    const message = importExport.formatImportResultMessage(result, 'location');
    infoModalTitle.value = 'Import Complete';
    infoModalMessage.value = message;
    showInfoModal.value = true;
  }
};

const closeImportModal = () => {
  importExport.closeImportLocationsModal();
};

const handleMapClick = async (coords) => {
  if (showAddLocation.value) {
    // User has add location modal open, set coordinates
    locationForm.value.latitude = coords.lat;
    locationForm.value.longitude = coords.lng;
    // Don't mark dirty for map click - only mark dirty when user types in form fields
  } else if (isMovingLocation.value) {
    // User is moving an existing location
    // Update both editLocationForm and selectedLocation so the modal sees the change
    editLocationForm.value.latitude = coords.lat;
    editLocationForm.value.longitude = coords.lng;
    if (selectedLocation.value) {
      selectedLocation.value = {
        ...selectedLocation.value,
        latitude: coords.lat,
        longitude: coords.lng,
      };
    }
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
  fullscreenMap.handleFullscreenMapClick(coords);
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

  // Map state already captured in handleAddLocationClick
  // Enter fullscreen
  fullscreenMap.enterPlaceCheckpointMode();
};

const handleAddCheckpointFromMap = () => {
  // Capture current map state before entering fullscreen
  fullscreenMap.saveMapState(courseAreasTab.value);

  // Reset the form
  locationForm.value = {
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
  };

  // Enter fullscreen directly
  fullscreenMap.enterPlaceCheckpointMode();
};

const handleAddAreaFromMap = () => {
  // Capture current map state before entering fullscreen
  fullscreenMap.saveMapState(courseAreasTab.value);

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
  fullscreenMap.enterDrawAreaMode();
};

const handleAddCheckpointManually = () => {
  // Create a new location object with default values
  selectedLocation.value = {
    id: null, // null ID indicates this is a new location
    name: '',
    description: '',
    latitude: 0,
    longitude: 0,
    requiredMarshals: 1,
    what3Words: '',
    areaIds: [],
    assignments: [],
  };
  editLocationForm.value = {
    id: null,
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
  showEditLocation.value = true;
};

const handleAddMultipleCheckpointsClick = () => {
  // Find the highest checkpoint number to continue from
  const startingNumber = fullscreenMap.calculateStartingNumber(locationStatuses);

  // Capture current map state before entering fullscreen
  fullscreenMap.saveMapState(courseAreasTab.value);

  // Enter fullscreen
  fullscreenMap.enterAddMultipleMode(startingNumber);
};

const handleFullscreenDone = () => {
  if (fullscreenMode.value === 'place-checkpoint') {
    // Confirm placement - use EditLocationModal for new location
    const coords = fullscreenMap.getPlacedCoords() || { lat: 0, lng: 0 };
    selectedLocation.value = {
      id: null, // null ID indicates this is a new location
      name: '',
      description: '',
      latitude: coords.lat,
      longitude: coords.lng,
      requiredMarshals: 1,
      what3Words: '',
      areaIds: [],
      assignments: [],
    };
    editLocationForm.value = {
      id: null,
      name: '',
      description: '',
      latitude: coords.lat,
      longitude: coords.lng,
      requiredMarshals: 1,
      what3Words: '',
    };
    pendingAssignments.value = [];
    pendingDeleteAssignments.value = [];
    formDirty.value = false;
    // Exit fullscreen and open EditLocationModal
    exitFullscreen();
    showEditLocation.value = true;

  } else if (fullscreenMode.value === 'move-checkpoint') {
    // Confirm move
    const coords = fullscreenMap.getPlacedCoords();
    if (coords) {
      editLocationForm.value.latitude = coords.lat;
      editLocationForm.value.longitude = coords.lng;
      // Also update selectedLocation so the modal sees the change, preserving form data
      if (selectedLocation.value) {
        selectedLocation.value = {
          ...selectedLocation.value,
          // Restore preserved form data (name, description, etc.) if available
          ...(preservedLocationFormData.value || {}),
          // Apply new coordinates on top
          latitude: coords.lat,
          longitude: coords.lng,
        };
      }
      markFormDirty();
    }
    // Clear preserved form data
    preservedLocationFormData.value = null;
    // Exit fullscreen and reopen modal
    exitFullscreen();
    showEditLocation.value = true;

  } else if (fullscreenMode.value === 'draw-area') {
    // Complete the polygon and exit
    const coordinates = fullscreenMap.getCompletedPolygon();
    if (coordinates) {
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

    exitFullscreen();
    showEditArea.value = true;
    pendingAreaFormData.value = null;

  } else if (fullscreenMode.value === 'add-multiple') {
    // Save all temporary checkpoints
    saveTempCheckpoints();
  }
};

const saveTempCheckpoints = async () => {
  const checkpointsToSave = fullscreenMap.getTempCheckpoints();

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
    fullscreenMap.clearTempCheckpoints();
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
  } else if (fullscreenMode.value === 'move-checkpoint') {
    // Restore the location modal with preserved form data
    if (preservedLocationFormData.value && selectedLocation.value) {
      // Merge preserved form data back into selectedLocation
      Object.assign(selectedLocation.value, preservedLocationFormData.value);
    }
    showEditLocation.value = true;
    isMovingLocation.value = false;
  }
  fullscreenMap.cancelFullscreen();
};

const exitFullscreen = () => {
  fullscreenMap.exitFullscreen();
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
  preservedLocationFormData.value = null;
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

const openCreateMarshalModal = () => {
  showAssignMarshalModal.value = true;
};

const startMoveLocation = (payload) => {
  // Preserve the form data from the modal so we can restore it after the move
  if (payload?.formData) {
    preservedLocationFormData.value = payload.formData;
  }

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

  // Use preserved name if available, otherwise fall back to selectedLocation name
  const locationName = preservedLocationFormData.value?.name || selectedLocation.value?.name;

  // Enter fullscreen
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'move-checkpoint';
  fullscreenContext.value = {
    title: `Move ${termsLower.value.checkpoint}: ${locationName}`,
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
    const isNewLocation = !selectedLocation.value.id;

    if (isNewLocation) {
      // Create new location
      const newLocation = await eventsStore.createLocation({
        eventId: route.params.eventId,
        name: formData.name,
        description: formData.description,
        latitude: formData.latitude,
        longitude: formData.longitude,
        requiredMarshals: formData.requiredMarshals,
        what3Words: formData.what3Words || null,
        startTime: formData.startTime,
        endTime: formData.endTime,
        styleType: formData.styleType,
        styleColor: formData.styleColor,
        styleBackgroundShape: formData.styleBackgroundShape,
        styleBackgroundColor: formData.styleBackgroundColor,
        styleBorderColor: formData.styleBorderColor,
        styleIconColor: formData.styleIconColor,
        styleSize: formData.styleSize,
        styleMapRotation: formData.styleMapRotation != null ? String(formData.styleMapRotation) : null,
        peopleTerm: formData.peopleTerm,
        checkpointTerm: formData.checkpointTerm,
        isDynamic: formData.isDynamic || false,
        locationUpdateScopeConfigurations: formData.locationUpdateScopeConfigurations || [],
        pendingNewChecklistItems: formData.pendingNewChecklistItems || [],
        pendingNewNotes: formData.pendingNewNotes || [],
      });

      // Process pending assignments for new locations (from the modal)
      if (formData.pendingAssignments && formData.pendingAssignments.length > 0) {
        for (const pending of formData.pendingAssignments) {
          await eventsStore.createAssignment({
            eventId: route.params.eventId,
            locationId: newLocation.id,
            marshalId: pending.marshalId,
          });
        }
      }
    } else {
      // Update existing location
      const locationId = selectedLocation.value.id;
      await locationsApi.update(
        route.params.eventId,
        locationId,
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
          styleType: formData.styleType,
          styleColor: formData.styleColor,
          styleBackgroundShape: formData.styleBackgroundShape,
          styleBackgroundColor: formData.styleBackgroundColor,
          styleBorderColor: formData.styleBorderColor,
          styleIconColor: formData.styleIconColor,
          styleSize: formData.styleSize,
          styleMapRotation: formData.styleMapRotation != null ? String(formData.styleMapRotation) : null,
          peopleTerm: formData.peopleTerm,
          checkpointTerm: formData.checkpointTerm,
          isDynamic: formData.isDynamic || false,
          locationUpdateScopeConfigurations: formData.locationUpdateScopeConfigurations || [],
          // areaId removed - areas are auto-assigned by backend based on polygon boundaries
        }
      );

      // Create pending new checklist items for this location (when editing)
      if (formData.pendingNewChecklistItems && formData.pendingNewChecklistItems.length > 0) {
        for (const item of formData.pendingNewChecklistItems) {
          if (!item.text?.trim()) continue;
          await checklistApi.create(route.params.eventId, {
            text: item.text,
            scopeConfigurations: [
              { scope: 'EveryoneAtCheckpoints', itemType: 'Checkpoint', ids: [locationId] }
            ],
            displayOrder: 0,
            isRequired: false,
          });
        }
      }

      // Create pending new notes for this location (when editing)
      if (formData.pendingNewNotes && formData.pendingNewNotes.length > 0) {
        for (const note of formData.pendingNewNotes) {
          if (!note.title?.trim() && !note.content?.trim()) continue;
          await notesApi.create(route.params.eventId, {
            title: note.title || 'Untitled',
            content: note.content || '',
            scopeConfigurations: [
              { scope: 'EveryoneAtCheckpoints', itemType: 'Checkpoint', ids: [locationId] }
            ],
            displayOrder: 0,
            priority: 'Normal',
            isPinned: false,
          });
        }
      }

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
    }

    // Process checklist changes (only for existing locations)
    if (!isNewLocation && formData.checklistChanges && formData.checklistChanges.length > 0) {
      for (const change of formData.checklistChanges) {
        try {
          if (change.complete) {
            // Complete the item
            await checklistApi.complete(route.params.eventId, change.itemId, {
              marshalId: change.marshalId,
              contextType: change.contextType,
              contextId: change.contextId,
            });
          } else {
            // Uncomplete the item
            await checklistApi.uncomplete(route.params.eventId, change.itemId, {
              marshalId: change.marshalId,
              contextType: change.contextType,
              contextId: change.contextId,
            });
          }
        } catch (error) {
          console.error('Failed to update checklist item:', change, error);
          // Continue with other changes even if one fails
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
  showConfirm('Delete location', 'Are you sure you want to delete this location?', async () => {
    try {
      await locationsApi.delete(route.params.eventId, locationId);
      await reloadLocationsAreasAndStatus();
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
    await reloadStatusOnly();

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

const handleRemoveLocationAssignment = (assignmentOrId) => {
  // Handle both assignment object or just ID being passed
  const assignmentId = assignmentOrId?.id ?? assignmentOrId;

  // Check if this is a pending assignment (has temp ID)
  if (assignmentId?.toString().startsWith('temp-')) {
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

    // Refresh marshals list so the new marshal appears in dropdowns
    await loadMarshals();

    // Add to pending assignments
    pendingAssignments.value.push({
      marshalId: newMarshal.id,
      marshalName: newMarshal.name,
    });

    // Optimistically update UI - add temporary assignment to selectedLocation
    if (selectedLocation.value) {
      if (!selectedLocation.value.assignments) {
        selectedLocation.value.assignments = [];
      }
      selectedLocation.value.assignments.push({
        id: `temp-${Date.now()}`, // Temporary ID
        marshalId: newMarshal.id,
        marshalName: newMarshal.name,
        locationId: selectedLocation.value.id,
        isCheckedIn: false,
        isPending: true, // Flag to identify pending assignments
      });
    }

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
    let marshalId = selectedMarshal.value?.id;
    let isNewMarshal = false;

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

      // Create pending new checklist items for this marshal (when editing)
      if (formData.pendingNewChecklistItems && formData.pendingNewChecklistItems.length > 0) {
        for (const item of formData.pendingNewChecklistItems) {
          if (!item.text?.trim()) continue;
          await checklistApi.create(route.params.eventId, {
            text: item.text,
            scopeConfigurations: [
              { scope: 'SpecificPeople', itemType: 'Marshal', ids: [marshalId] }
            ],
            displayOrder: 0,
            isRequired: false,
          });
        }
      }

      // Create pending new notes for this marshal (when editing)
      if (formData.pendingNewNotes && formData.pendingNewNotes.length > 0) {
        for (const note of formData.pendingNewNotes) {
          if (!note.title?.trim() && !note.content?.trim()) continue;
          await notesApi.create(route.params.eventId, {
            title: note.title || 'Untitled',
            content: note.content || '',
            scopeConfigurations: [
              { scope: 'SpecificPeople', itemType: 'Marshal', ids: [marshalId] }
            ],
            displayOrder: 0,
            priority: 'Normal',
            isPinned: false,
          });
        }
      }
    } else {
      const response = await marshalsApi.create({
        eventId: route.params.eventId,
        name: formData.name,
        email: formData.email,
        phoneNumber: formData.phoneNumber,
        notes: formData.notes,
        pendingNewChecklistItems: formData.pendingNewChecklistItems || [],
        pendingNewNotes: formData.pendingNewNotes || [],
      });
      marshalId = response.data.id;
      isNewMarshal = true;
    }

    for (const assignmentId of pendingMarshalDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    for (const pending of pendingMarshalAssignments.value) {
      await eventsStore.createAssignment({
        eventId: route.params.eventId,
        locationId: pending.locationId,
        marshalId: marshalId,
      });
    }

    // Process pending assignments for new marshals (from the modal)
    if (formData.pendingAssignments && formData.pendingAssignments.length > 0) {
      for (const pending of formData.pendingAssignments) {
        await eventsStore.createAssignment({
          eventId: route.params.eventId,
          locationId: pending.locationId,
          marshalId: marshalId,
        });
      }
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

    // Process checklist changes
    if (formData.checklistChanges && formData.checklistChanges.length > 0) {
      for (const change of formData.checklistChanges) {
        try {
          const requestBody = {
            marshalId: marshalId,
          };

          // Only include context if it's defined and not empty
          if (change.contextType && change.contextId) {
            requestBody.contextType = change.contextType;
            requestBody.contextId = change.contextId;
          }

          if (change.complete) {
            // Complete the item
            await checklistApi.complete(route.params.eventId, change.itemId, requestBody);
          } else {
            // Uncomplete the item
            await checklistApi.uncomplete(route.params.eventId, change.itemId, requestBody);
          }
        } catch (error) {
          console.error('Failed to process checklist change:', error);
          const errorMsg = error.response?.data?.message || 'Failed to update checklist item';
          alert(`Error: ${errorMsg}`);
          // Continue processing other changes
        }
      }
    }

    await loadEventData();
    closeEditMarshalModal();

    // Show confirmation modal with login link for newly created marshals
    if (isNewMarshal) {
      newlyCreatedMarshalId.value = marshalId;
      newlyCreatedMarshalName.value = formData.name;
      showMarshalCreated.value = true;
    }
  } catch (error) {
    console.error('Failed to save marshal:', error);
    alert('Failed to save marshal. Please try again.');
  }
};

const handleDeleteMarshal = async (marshalId) => {
  // Use selectedMarshal if no ID is provided (when called from modal)
  const idToDelete = marshalId || selectedMarshal.value?.id;
  if (!idToDelete) {
    console.error('No marshal ID to delete');
    return;
  }

  const personTerm = termsLower.value.person;
  showConfirm(`Delete ${personTerm}`, `Are you sure you want to delete this ${personTerm}?`, async () => {
    try {
      await marshalsApi.delete(route.params.eventId, idToDelete);
      await reloadMarshalsAndStatus();
      closeEditMarshalModal();
    } catch (error) {
      console.error(`Failed to delete ${personTerm}:`, error);
      alert(`Failed to delete ${personTerm}. Please try again.`);
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
  const result = await importExport.importMarshals(file);
  if (result) {
    await reloadMarshalsAndStatus();
    closeImportMarshalsModal();

    const message = importExport.formatImportResultMessage(result, 'marshal');
    infoModalTitle.value = 'Import Complete';
    infoModalMessage.value = message;
    showInfoModal.value = true;
  }
};

const closeImportMarshalsModal = () => {
  importExport.closeImportMarshalsModal();
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

    await reloadStatusOnly();

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

// Dynamic checkpoint location polling
let dynamicCheckpointPollInterval = null;

const pollDynamicCheckpoints = async () => {
  try {
    const eventId = route.params.eventId;
    const response = await locationsApi.getDynamicCheckpoints(eventId);
    if (response.data && Array.isArray(response.data)) {
      // Update local positions for dynamic checkpoints
      for (const dynamicCp of response.data) {
        const location = locationStatuses.value.find(l => l.id === dynamicCp.checkpointId);
        if (location) {
          location.latitude = dynamicCp.latitude;
          location.longitude = dynamicCp.longitude;
          location.lastLocationUpdate = dynamicCp.lastLocationUpdate;
        }
      }
    }
  } catch (error) {
    console.warn('Failed to poll dynamic checkpoints:', error);
  }
};

const startDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) return;

  // Check if there are any dynamic checkpoints to poll
  const hasDynamicCheckpoints = locationStatuses.value.some(l => l.isDynamic || l.IsDynamic);
  if (!hasDynamicCheckpoints) return;

  // Poll immediately, then every 60 seconds
  pollDynamicCheckpoints();
  dynamicCheckpointPollInterval = setInterval(pollDynamicCheckpoints, 60000);
};

const stopDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) {
    clearInterval(dynamicCheckpointPollInterval);
    dynamicCheckpointPollInterval = null;
  }
};

// Watch for tab changes to start/stop polling
watch(activeTab, (newTab) => {
  if (newTab === 'course') {
    startDynamicCheckpointPolling();
  } else {
    stopDynamicCheckpointPolling();
  }
});

// Lifecycle
onMounted(async () => {
  await loadEventData();

  // Fetch user claims to check if user has marshal access
  try {
    const claimsResponse = await authApi.getMe(route.params.eventId);
    userClaims.value = claimsResponse.data;
  } catch (error) {
    console.warn('Failed to fetch user claims:', error);
  }

  // Start polling if already on course tab
  if (activeTab.value === 'course') {
    startDynamicCheckpointPolling();
  }
});

onUnmounted(() => {
  stopDynamicCheckpointPolling();
});
</script>

<style scoped>
/* Preserved styles from original file */
.admin-event-manage {
  min-height: 100vh;
  background: var(--bg-secondary);
  color: var(--text-primary);
  overflow: visible;
}

.header {
  background: var(--bg-primary);
  padding: 1.5rem 2rem;
  border-bottom: 1px solid var(--border-color);
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
  color: var(--accent-primary);
  cursor: pointer;
  font-size: 1rem;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.btn-back:hover {
  background: var(--bg-active);
}

.header h1 {
  margin: 0;
  font-size: 1.75rem;
  color: var(--text-primary);
}

.event-date {
  margin: 0.25rem 0 0 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.header-actions {
  display: flex;
  gap: 1rem;
}

.btn-marshal-mode {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-marshal-mode:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px rgba(102, 126, 234, 0.4);
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
  border-bottom: 2px solid var(--border-color);
  overflow-y: visible;
  position: relative;
}

.tab-button {
  padding: 1rem 1.5rem;
  border: none;
  background: transparent;
  color: var(--text-secondary);
  cursor: pointer;
  font-size: 1rem;
  font-weight: 500;
  border-bottom: 3px solid transparent;
  transition: all 0.2s;
  position: relative;
  bottom: -2px;
}

.tab-button:hover {
  color: var(--text-primary);
  background: var(--bg-tertiary);
}

.tab-button.active {
  color: var(--accent-primary);
  border-bottom-color: var(--accent-primary);
}

.tab-content-wrapper {
  animation: fadeIn 0.3s ease-in;
}

.tab-panel {
  background: var(--card-bg);
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: var(--shadow-md);
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
    overflow: visible;
  }

  .tabs-nav {
    overflow-x: visible;
    overflow-y: visible;
  }
}
</style>
