<template>
  <div class="marshal-view" :style="pageBackgroundStyle">
    <!-- Offline Indicator -->
    <OfflineIndicator />

    <header class="header" :class="`logo-${brandingLogoPosition}`" :style="headerStyle">
      <!-- Cover logo background -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'cover'" class="header-logo-cover">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-cover-img" />
      </div>

      <!-- Close button on left (when logo is on right) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition === 'right'"
        @click="handleClose"
        class="btn-close-session btn-close-left"
        :style="{ color: headerTextColor }"
        title="Close session"
      >
        &times;
      </button>

      <!-- Left logo -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'left'" class="header-logo header-logo-left">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-img" />
      </div>

      <div class="header-center">
        <div class="header-title" :style="{ color: headerTextColor }">
          <h1 v-if="event">{{ event.name }}</h1>
          <div v-if="event?.eventDate" class="header-event-date">
            {{ formatEventDate(event.eventDate) }}
            <span v-if="shouldShowWeather && weather" class="header-weather" :title="weather.description">
              <WeatherIcon :icon="weather.icon" :size="18" :alt="weather.description" :color="headerTextColor" />
              <span class="weather-temp">{{ weather.tempMax }}°{{ weather.unit }}</span>
            </span>
          </div>
        </div>
        <div v-if="isAuthenticated" class="header-actions">
          <button @click="showReportIncident = true" class="btn-header-action btn-report-incident">
            Report Incident
          </button>
          <button v-if="hasEmergencyInfo" @click="showEmergency = true" class="btn-header-action btn-emergency">
            Emergency Info
          </button>
        </div>
      </div>

      <!-- Right logo -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'right'" class="header-logo header-logo-right">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-img" />
      </div>

      <!-- Close button on right (when logo is on left, cover, or none) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition !== 'right'"
        @click="handleClose"
        class="btn-close-session btn-close-right"
        :style="{ color: headerTextColor }"
        title="Close session"
      >
        &times;
      </button>
    </header>

    <div class="container">
      <!-- Loading State -->
      <div v-if="loading" class="selection-card">
        <div class="loading">Loading...</div>
      </div>

      <!-- Login with Magic Code -->
      <div v-else-if="!isAuthenticated && !loginError" class="selection-card">
        <h2>Welcome</h2>
        <p class="instruction">Please use the login link sent to you to access your marshal dashboard.</p>
        <p v-if="authenticating" class="loading">Authenticating...</p>
      </div>

      <!-- Login Error -->
      <div v-else-if="loginError" class="selection-card error-card">
        <h2>Login failed</h2>
        <p class="error">{{ loginError }}</p>
        <p class="instruction">Please contact the event organiser for a new login link.</p>
      </div>

      <!-- Marshal Dashboard -->
      <div v-else class="dashboard">
        <div v-if="canSwitchToAdmin || isAdminButNeedsReauth" class="welcome-bar">
          <div class="welcome-info">
            <span v-if="currentPerson?.email" class="welcome-email">{{ currentPerson.email }}</span>
          </div>
          <div v-if="canSwitchToAdmin" class="mode-switch">
            <button @click="switchToAdminMode" class="btn-mode-switch">
              Switch to Admin
            </button>
          </div>
          <div v-else-if="isAdminButNeedsReauth" class="mode-switch">
            <span class="reauth-hint" title="Login via admin flow to access admin features">
              Admin access requires re-login
            </span>
          </div>
        </div>

        <!-- Accordion Sections -->
        <div class="accordion">
          <!-- Assignments Section -->
          <MarshalAssignmentsSection
            ref="assignmentsSectionRef"
            :is-expanded="expandedSection === 'assignments'"
            :assignments="assignmentsWithDetails"
            :expanded-checkpoint-id="expandedCheckpoint"
            :all-locations="allLocations"
            :route="eventRoute"
            :user-location="userLocation"
            :has-dynamic-assignment="hasDynamicAssignment"
            :current-marshal-id="currentMarshalId"
            :current-marshal-name="currentMarshalName"
            :checking-in-id="checkingIn"
            :checking-in-assignment-id="checkingInAssignment"
            :check-in-error="checkInError"
            :checking-in-marshal-id="checkingIn"
            :expanded-marshal-id="expandedMarshalId"
            :updating-location="updatingLocation"
            :auto-update-enabled="autoUpdateEnabled"
            :get-toolbar-actions="getCheckpointMapActions"
            :is-area-lead-for-areas="isAreaLeadForAreas"
            :get-notes-for-checkpoint="getNotesForCheckpoint"
            :area-lead-area-ids="areaLeadAreaIds"
            @toggle="toggleSection('assignments')"
            @toggle-checkpoint="toggleCheckpoint"
            @map-click="handleMapClick"
            @location-click="handleLocationClick"
            @action-click="handleCheckpointMapAction"
            @visibility-change="handleCheckpointMapVisibilityChange"
            @update-location="openLocationUpdateModal"
            @check-in="(assign) => handleCheckInToggle(assign, true)"
            @toggle-marshal="toggleMarshalDetails"
            @marshal-check-in="(m, locId) => handleCheckInToggle(m, false, locId)"
            @toggle-auto-update="toggleAutoUpdate"
          />

          <!-- Checklist Section -->
          <MarshalChecklistSection
            :loading="checklistLoading"
            :error="checklistError"
            :is-expanded="expandedSection === 'checklist'"
            :is-area-lead="isAreaLead"
            :my-items="myChecklistItems"
            :area-items="areaChecklistItems"
            :area-items-with-local-state="areaChecklistItemsWithLocalState"
            :visible-items="visibleChecklistItems"
            :grouped-items="groupedChecklistItems"
            :completed-count="completedChecklistCount"
            :saving="savingChecklist"
            :effective-group-by="effectiveChecklistGroupBy"
            :expanded-group="expandedChecklistGroup"
            :locations="allLocations"
            :areas="areas"
            :marshals="areaMarshalsForChecklist"
            :get-context-name="getContextName"
            @toggle="toggleSection('checklist')"
            @toggle-item="handleToggleChecklist"
            @toggle-group="toggleChecklistGroup"
          />

          <!-- Notes Section -->
          <MarshalNotesSection
            :event-id="route.params.eventId"
            :notes="notes"
            :locations="allLocations"
            :areas="areas"
            :assignments="assignments"
            :is-expanded="expandedSection === 'notes'"
            @toggle="toggleSection('notes')"
            @notes-changed="loadNotes"
          />

          <!-- Contacts Section -->
          <MarshalContactsSection
            :contacts="eventContacts"
            :loading="contactsLoading"
            :is-expanded="expandedSection === 'eventContacts'"
            @toggle="toggleSection('eventContacts')"
          />

          <!-- Your Incidents Section -->
          <MarshalIncidentsSection
            :incidents="myIncidents"
            :loading="incidentsLoading"
            :is-expanded="expandedSection === 'incidents'"
            @toggle="toggleSection('incidents')"
            @select-incident="openIncidentDetail"
          />

          <!-- Area Lead Section (if user is an area lead) -->
          <MarshalAreaLeadSection
            ref="areaLeadSectionRef"
            :is-area-lead="isAreaLead"
            :is-expanded="expandedSection === 'areaLead'"
            :areas="areaLeadAreas"
            :selected-filters="selectedAreaFilters"
            :filtered-area-ids="filteredAreaLeadAreaIds"
            :event-id="route.params.eventId"
            :marshal-id="currentMarshalId"
            :route="eventRoute"
            @toggle="toggleSection('areaLead')"
            @toggle-filter="toggleAreaFilter"
            @checklist-updated="loadChecklist"
          />

          <!-- Your Marshals Section (for area leads with marshals at their checkpoints) -->
          <MarshalMarshalsList
            :is-area-lead="isAreaLead"
            :is-expanded="expandedSection === 'areaLeadMarshals'"
            :marshals="allAreaLeadMarshals"
            :expanded-marshal-id="expandedAreaLeadMarshal"
            :checking-in-marshal-id="checkingIn"
            :saving-task="savingAreaLeadMarshalTask"
            @toggle="toggleSection('areaLeadMarshals')"
            @toggle-marshal="toggleAreaLeadMarshalExpansion"
            @check-in="handleAreaLeadMarshalCheckIn"
            @toggle-task="toggleAreaLeadMarshalTask"
          />

          <!-- Route/Map Section -->
          <MarshalRouteSection
            ref="routeSectionRef"
            :is-expanded="expandedSection === 'map'"
            :user-location="userLocation"
            :selecting-location="selectingLocationOnMap"
            :selecting-location-name="updatingLocationFor?.location?.name || ''"
            :locations="courseMapLocations"
            :route="eventRoute"
            :center="mapCenter"
            :highlight-location-ids="assignmentLocationIds"
            :clickable="selectingLocationOnMap || canUpdateFirstDynamicAssignment"
            :toolbar-actions="courseMapActions"
            @toggle="toggleSection('map')"
            @cancel-select="cancelMapLocationSelect"
            @map-click="handleMapClick"
            @location-click="handleLocationClick"
            @action-click="handleCourseMapAction"
            @visibility-change="handleCourseMapVisibilityChange"
          />

          <!-- Event Details Section (at the end) -->
          <MarshalEventDetailsSection
            :event-date="event?.eventDate"
            :description="event?.description"
            :is-expanded="expandedSection === 'eventDetails'"
            @toggle="toggleSection('eventDetails')"
          />
        </div>
      </div>
    </div>

    <!-- Emergency Contact Modal -->
    <EmergencyContactModal
      :show="showEmergency"
      :contacts="emergencyContacts"
      :notes="emergencyNotes"
      @close="showEmergency = false"
    />

    <!-- Report Incident Modal -->
    <ReportIncidentModal
      :show="showReportIncident"
      :event-id="eventId || ''"
      :checkpoint="defaultIncidentCheckpoint"
      :initial-location="userLocation"
      :checkpoints="allLocations"
      :route="eventRoute"
      @close="showReportIncident = false"
      @submit="handleReportIncident"
    />

    <IncidentDetailModal
      :show="showIncidentDetail"
      :incident="selectedIncident"
      :route="eventRoute"
      :checkpoints="allLocations"
      :can-manage="isAreaLead"
      @close="showIncidentDetail = false"
      @status-change="handleIncidentStatusChange"
      @open-add-note="openAddIncidentNoteModal"
    />

    <!-- Add Incident Update Modal -->
    <BaseModal
      :show="showAddIncidentNoteModal"
      title="Add update"
      size="small"
      @close="closeAddIncidentNoteModal"
    >
      <div class="add-note-form">
        <textarea
          v-model="incidentNoteText"
          class="note-textarea"
          placeholder="Enter your update..."
          rows="4"
        ></textarea>
      </div>
      <template #footer>
        <div class="modal-actions">
          <button type="button" class="btn btn-secondary" @click="closeAddIncidentNoteModal">
            Cancel
          </button>
          <button
            type="button"
            class="btn btn-primary"
            @click="submitIncidentNote"
            :disabled="!incidentNoteText.trim() || submittingIncidentNote"
          >
            {{ submittingIncidentNote ? 'Adding...' : 'Add update' }}
          </button>
        </div>
      </template>
    </BaseModal>

    <ConfirmModal
      :show="showConfirmModal"
      :title="confirmModalTitle"
      :message="confirmModalMessage"
      @confirm="handleConfirmModalConfirm"
      @cancel="handleConfirmModalCancel"
    />

    <!-- Message Modal (for success/error messages) -->
    <ConfirmModal
      :show="showMessageModal"
      :title="messageModalTitle"
      :message="messageModalMessage"
      :show-cancel="false"
      @confirm="handleMessageModalClose"
    />

    <!-- Check-in Reminder Modal -->
    <CheckInReminderModal
      :show="showCheckInReminderModal"
      :checkpoint="checkInReminderCheckpoint"
      :accent-button-style="accentButtonStyle"
      @dismiss="dismissCheckInReminder(checkInReminderCheckpoint?.id)"
      @go-to-checkpoint="handleGoToCheckpoint"
    />

    <!-- Location Update Modal -->
    <LocationUpdateModal
      :show="showLocationUpdateModal"
      :assignment="updatingLocationFor"
      :user-location="userLocation"
      :auto-update-enabled="autoUpdateEnabled"
      :updating="updatingLocation"
      :error="locationUpdateError"
      :success="locationUpdateSuccess"
      :available-checkpoints="availableSourceCheckpoints"
      :accent-button-style="accentButtonStyle"
      @close="closeLocationUpdateModal"
      @update-gps="updateLocationWithGps"
      @toggle-auto-update="toggleAutoUpdate(updatingLocationFor)"
      @select-on-map="startMapLocationSelect"
      @copy-from-checkpoint="updateLocationFromCheckpoint"
    />

  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, watch, provide, nextTick } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { eventsApi, assignmentsApi, areasApi, notesApi, contactsApi, getOfflineMode } from '../services/api';
import ConfirmModal from '../components/ConfirmModal.vue';
import BaseModal from '../components/BaseModal.vue';
import EmergencyContactModal from '../components/event-manage/modals/EmergencyContactModal.vue';
import ReportIncidentModal from '../components/ReportIncidentModal.vue';
import IncidentDetailModal from '../components/IncidentDetailModal.vue';
import OfflineIndicator from '../components/OfflineIndicator.vue';
import CheckInReminderModal from '../components/marshal/modals/CheckInReminderModal.vue';
import LocationUpdateModal from '../components/marshal/modals/LocationUpdateModal.vue';
import MarshalContactsSection from '../components/marshal/MarshalContactsSection.vue';
import MarshalIncidentsSection from '../components/marshal/MarshalIncidentsSection.vue';
import MarshalEventDetailsSection from '../components/marshal/MarshalEventDetailsSection.vue';
import MarshalChecklistSection from '../components/marshal/MarshalChecklistSection.vue';
import MarshalNotesSection from '../components/marshal/MarshalNotesSection.vue';
import MarshalRouteSection from '../components/marshal/MarshalRouteSection.vue';
import MarshalAreaLeadSection from '../components/marshal/MarshalAreaLeadSection.vue';
import MarshalMarshalsList from '../components/marshal/MarshalMarshalsList.vue';
import MarshalAssignmentsSection from '../components/marshal/MarshalAssignmentsSection.vue';
import { setTerminology, useTerminology } from '../composables/useTerminology';
import { useMarshalBranding } from '../composables/useMarshalBranding';
import { useLocationTracking } from '../composables/useLocationTracking';
import { useMarshalChecklist } from '../composables/useMarshalChecklist';
import { useDynamicLocation } from '../composables/useDynamicLocation';
import { useMarshalCheckIn } from '../composables/useMarshalCheckIn';
import { useMarshalIncidents } from '../composables/useMarshalIncidents';
import { useMapActions } from '../composables/useMapActions';
import { useAreaLeadMarshals } from '../composables/useAreaLeadMarshals';
import { useMarshalAuth } from '../composables/useMarshalAuth';
import { useWeatherForecast } from '../composables/useWeatherForecast';
import WeatherIcon from '../components/common/WeatherIcon.vue';
import { getIcon } from '../utils/icons';
import { canMarshalUpdateDynamicLocation } from '../utils/scopeUtils';
import { useOffline } from '../composables/useOffline';
import { cacheEventData, getCachedEventData, updateCachedField } from '../services/offlineDb';

const { terms, termsLower } = useTerminology();

// Offline support
const { updatePendingCount } = useOffline();

const route = useRoute();
const router = useRouter();

// Event ID from route params (reactive for template usage)
const eventId = computed(() => route.params.eventId);

// Auth composable - callback will be set after loadEventData is defined
const authCallbacks = { onAuthSuccess: null };

const {
  isAuthenticated,
  authenticating,
  loginError,
  currentPerson,
  currentMarshalId,
  currentMarshalName,
  areaLeadAreaIds,
  isAreaLead,
  canSwitchToAdmin,
  isAdminButNeedsReauth,
  authenticateWithMagicCode,
  checkExistingSession,
  handleClose: baseHandleClose,
  updateSessionEventInfo,
  switchToAdminMode,
  stopHeartbeat,
} = useMarshalAuth({
  router,
  route,
  onAuthSuccess: () => authCallbacks.onAuthSuccess?.(),
});

// Area filter state for "Your areas" section
const selectedAreaFilters = ref(new Set());

// Get area objects for the filter pills
const areaLeadAreas = computed(() => {
  if (!areaLeadAreaIds.value.length) return [];
  return areas.value
    .filter(a => areaLeadAreaIds.value.includes(a.id))
    .map(a => ({ areaId: a.id, name: a.name, color: a.color }))
    .sort((a, b) => (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' }));
});

// Initialize selected filters when areas load
watch(areaLeadAreas, (newAreas) => {
  if (newAreas.length > 0 && selectedAreaFilters.value.size === 0) {
    // Initialize with all areas selected
    selectedAreaFilters.value = new Set(newAreas.map(a => a.areaId));
  }
}, { immediate: true });

// Filtered area IDs based on selection
const filteredAreaLeadAreaIds = computed(() => {
  // Return only the selected areas (empty array if none selected)
  return areaLeadAreaIds.value.filter(id => selectedAreaFilters.value.has(id));
});

// Toggle area filter selection
const toggleAreaFilter = (areaId) => {
  const newSet = new Set(selectedAreaFilters.value);
  if (newSet.has(areaId)) {
    newSet.delete(areaId);
  } else {
    newSet.add(areaId);
  }
  selectedAreaFilters.value = newSet;
};

// Default checkpoint for incident reporting - the checked-in checkpoint or first assignment
const defaultIncidentCheckpoint = computed(() => {
  // Find the first checked-in checkpoint
  const checkedIn = assignmentsWithDetails.value.find(a => {
    const effectiveIsCheckedIn = a.isCheckedIn && !isCheckInStale(a.checkInTime);
    return effectiveIsCheckedIn && a.location;
  });

  if (checkedIn?.location) {
    return {
      id: checkedIn.location.id,
      locationId: checkedIn.location.id,
      name: checkedIn.location.name,
      description: checkedIn.location.description,
    };
  }

  // Otherwise use expanded or first assignment
  if (expandedCheckpoint.value) {
    const expanded = assignmentsWithDetails.value.find(a => a.id === expandedCheckpoint.value);
    if (expanded?.location) {
      return {
        id: expanded.location.id,
        locationId: expanded.location.id,
        name: expanded.location.name,
        description: expanded.location.description,
      };
    }
  }

  // Fall back to first assignment
  const first = assignmentsWithDetails.value[0];
  if (first?.location) {
    return {
      id: first.location.id,
      locationId: first.location.id,
      name: first.location.name,
      description: first.location.description,
    };
  }

  return null;
});

// Event data
const event = ref(null);
const allLocations = ref([]);
const assignments = ref([]);
const areas = ref([]);
const loading = ref(true);

// Branding styles from composable (pass eventId for caching)
const {
  pageBackgroundStyle,
  headerStyle,
  headerTextColor,
  accentColor,
  accentTextColor,
  accentButtonStyle,
  brandingLogoUrl,
  brandingLogoPosition,
} = useMarshalBranding(event, route.params.eventId);

// Provide branding styles to child components via dependency injection
provide('marshalBranding', {
  headerStyle,
  headerTextColor,
  accentButtonStyle,
  accentColor,
  accentTextColor,
});

// Weather forecast - compute event location from checkpoint centroid
const eventLocationLat = computed(() => {
  if (!allLocations.value.length) return null;
  const validLocs = allLocations.value.filter(l => l.latitude && l.longitude);
  if (!validLocs.length) return null;
  return validLocs.reduce((sum, l) => sum + l.latitude, 0) / validLocs.length;
});

const eventLocationLng = computed(() => {
  if (!allLocations.value.length) return null;
  const validLocs = allLocations.value.filter(l => l.latitude && l.longitude);
  if (!validLocs.length) return null;
  return validLocs.reduce((sum, l) => sum + l.longitude, 0) / validLocs.length;
});

const eventDateRef = computed(() => event.value?.eventDate || null);

const {
  weather,
  shouldShowWeather,
} = useWeatherForecast({
  eventDate: eventDateRef,
  latitude: eventLocationLat,
  longitude: eventLocationLng,
});

// Accordion state
const expandedSection = ref('assignments');

// Track when each section's data was last loaded (for stale data refresh)
const STALE_DATA_THRESHOLD_MS = 60000; // 60 seconds
const sectionLastLoadedAt = ref({
  checklist: 0,
  notes: 0,
  incidents: 0,
  eventContacts: 0,
});

// Area lead checkpoints data (for marshal name lookup in checklists)
const areaLeadCheckpoints = ref([]);

// Notes state
const notes = ref([]);

// Area Lead section ref (accesses areaLeadRef via areaLeadSectionRef.value?.areaLeadRef?.value)
const areaLeadSectionRef = ref(null);
const areaLeadRef = computed(() => areaLeadSectionRef.value?.areaLeadRef?.value);

// Checklist composable
const {
  checklistItems,
  checklistLoading,
  checklistError,
  savingChecklist,
  expandedChecklistGroup,
  visibleChecklistItems,
  completedChecklistCount,
  myChecklistItems,
  areaChecklistItems,
  areaChecklistItemsWithLocalState,
  areaMarshalsForChecklist,
  effectiveChecklistGroupBy,
  groupedChecklistItems,
  loadChecklist,
  handleToggleChecklist,
  toggleChecklistGroup,
  getContextName,
} = useMarshalChecklist({
  eventId,
  currentMarshalId,
  currentPerson,
  assignments,
  areaLeadAreaIds,
  isAreaLead,
  allLocations,
  areas,
  terms,
  termsLower,
  areaLeadRef,
  areaLeadCheckpoints,
  sectionLastLoadedAt,
  updateCachedField,
  updatePendingCount,
});

// GPS tracking from composable - must be before useDynamicLocation which depends on userLocation
const {
  userLocation,
  locationLastUpdated,
  startLocationTracking,
  stopLocationTracking,
} = useLocationTracking();

// Dynamic location composable
const {
  showLocationUpdateModal,
  updatingLocationFor,
  locationUpdateError,
  locationUpdateSuccess,
  updatingLocation,
  autoUpdateEnabled,
  selectingLocationOnMap,
  openLocationUpdateModal,
  closeLocationUpdateModal,
  startMapLocationSelect: baseStartMapLocationSelect,
  cancelMapLocationSelect: baseCancelMapLocationSelect,
  updateDynamicCheckpointLocation,
  updateLocationWithGps,
  updateLocationFromCheckpoint,
  toggleAutoUpdate,
  stopAutoUpdate,
  startDynamicCheckpointPolling,
  stopDynamicCheckpointPolling,
  startAllCheckpointsPolling,
  stopAllCheckpointsPolling,
} = useDynamicLocation({
  eventId,
  allLocations,
  userLocation,
  locationLastUpdated,
});

// Track which section to return to after map location selection
let sectionBeforeMapSelect = null;

// Wrapper for startMapLocationSelect that also expands the map section and opens fullscreen
const startMapLocationSelect = async () => {
  baseStartMapLocationSelect();
  // Remember current section to return to after selection
  sectionBeforeMapSelect = expandedSection.value;
  expandedSection.value = 'map';
  // Wait for the section to expand, then open fullscreen
  await nextTick();
  routeSectionRef.value?.openFullscreen?.();
};

// Restore the section that was open before map location selection
const restoreSectionAfterMapSelect = () => {
  if (sectionBeforeMapSelect) {
    expandedSection.value = sectionBeforeMapSelect;
    sectionBeforeMapSelect = null;
  }
};

// Wrapper for cancelMapLocationSelect that also restores the previous section
const cancelMapLocationSelect = () => {
  baseCancelMapLocationSelect();
  routeSectionRef.value?.mapRef?.closeFullscreen?.();
  restoreSectionAfterMapSelect();
};

// Area lead marshals composable
const {
  expandedAreaLeadMarshal,
  savingAreaLeadMarshalTask,
  areaLeadMarshalDataVersion,
  allAreaLeadMarshals,
  toggleAreaLeadMarshalExpansion,
  toggleAreaLeadMarshalTask,
} = useAreaLeadMarshals({
  eventId,
  currentMarshalId,
  areaLeadRef,
  areaLeadCheckpoints,
  areaChecklistItems,
  areaLeadAreaIds,
  loadChecklist,
});

// Check-in composable
const {
  checkingIn,
  checkingInAssignment,
  checkInError,
  showCheckInReminderModal,
  checkInReminderCheckpoint,
  dismissedCheckInReminders,
  showDistanceWarning,
  distanceWarningMessage,
  isCheckInStale,
  handleCheckInToggle,
  handleAreaLeadMarshalCheckIn,
  dismissCheckInReminder,
  dismissDistanceWarning,
} = useMarshalCheckIn({
  eventId,
  assignments,
  allLocations,
  areaLeadRef,
  areaLeadCheckpoints,
  areaLeadMarshalDataVersion,
  updatePendingCount,
  updateCachedField,
});

// UI state
const showEmergency = ref(false);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

// Message modal state (for success/error messages)
const showMessageModal = ref(false);
const messageModalTitle = ref('');
const messageModalMessage = ref('');

// Message modal helper
const showMessage = (title, message) => {
  messageModalTitle.value = title;
  messageModalMessage.value = message;
  showMessageModal.value = true;
};

// Incidents composable
const {
  myIncidents,
  incidentsLoading,
  selectedIncident,
  showIncidentDetail,
  showAddIncidentNoteModal,
  incidentNoteText,
  submittingIncidentNote,
  showReportIncident,
  loadMyIncidents,
  handleReportIncident,
  openIncidentDetail,
  handleIncidentStatusChange,
  openAddIncidentNoteModal,
  closeAddIncidentNoteModal,
  submitIncidentNote,
} = useMarshalIncidents({
  eventId,
  currentMarshalId,
  isAreaLead,
  areaLeadAreaIds,
  sectionLastLoadedAt,
  showMessage,
});

// Route section ref - exposes recenterOnLocation and recenterOnUserLocation directly
const routeSectionRef = ref(null);
const courseMapRef = computed(() => routeSectionRef.value);

// Expanded marshal details (keyed by marshalId)
const expandedMarshalId = ref(null);

// Check if user is area lead for a given set of area IDs
const isAreaLeadForAreas = (areaIds) => {
  if (!areaIds || areaIds.length === 0) return false;
  return areaIds.some(areaId => areaLeadAreaIds.value.includes(areaId));
};

// Toggle marshal details expansion
const toggleMarshalDetails = (marshalId) => {
  expandedMarshalId.value = expandedMarshalId.value === marshalId ? null : marshalId;
};

// Toggle accordion section
const toggleSection = (section) => {
  const wasExpanded = expandedSection.value === section;
  expandedSection.value = wasExpanded ? null : section;

  // If expanding a section, check if data is stale and refresh in background
  if (!wasExpanded) {
    const now = Date.now();
    const isStale = (key) => now - sectionLastLoadedAt.value[key] > STALE_DATA_THRESHOLD_MS;

    switch (section) {
      case 'checklist':
        if (isStale('checklist')) {
          loadChecklist();
        }
        break;
      case 'notes':
        if (isStale('notes')) {
          loadNotes();
        }
        break;
      case 'incidents':
        if (isStale('incidents')) {
          loadMyIncidents();
        }
        break;
      case 'eventContacts':
        if (isStale('eventContacts')) {
          loadContacts();
        }
        break;
    }
  }
};

// Track which checkpoint accordion is expanded (by assignment ID)
const expandedCheckpoint = ref(null);

const toggleCheckpoint = (assignId) => {
  expandedCheckpoint.value = expandedCheckpoint.value === assignId ? null : assignId;
};

// Auto-expand if there's only one assignment
watch(() => assignments.value, (newAssignments) => {
  if (newAssignments.length === 1 && expandedCheckpoint.value === null) {
    expandedCheckpoint.value = newAssignments[0].id;
  }
}, { immediate: true });

// Track if any map is currently visible (course map or checkpoint card map)
const isAnyMapVisible = computed(() => {
  // Course map section is expanded
  if (expandedSection.value === 'map') return true;
  // Assignments section is expanded AND a checkpoint card is expanded (which shows its map)
  if (expandedSection.value === 'assignments' && expandedCheckpoint.value !== null) return true;
  return false;
});

// Poll checkpoint locations when a map is visible
watch(isAnyMapVisible, (visible) => {
  if (visible) {
    startAllCheckpointsPolling();
  } else {
    stopAllCheckpointsPolling();
  }
});

// Assignments with details (location info, area name, all marshals on checkpoint, area contacts)
const assignmentsWithDetails = computed(() => {
  return assignments.value.map(assign => {
    const location = allLocations.value.find(loc => loc.id === assign.locationId);

    // Get area IDs from the location
    const areaIds = location?.areaIds || location?.AreaIds || [];
    let areaName = null;
    if (areaIds.length > 0 && areas.value.length > 0) {
      const area = areas.value.find(a => areaIds.includes(a.id));
      areaName = area?.name;
    }

    // Get all marshals assigned to this checkpoint, with effective check-in status
    // Sort alphabetically by name per project guidelines
    const rawMarshals = location?.assignments || [];
    const allMarshals = rawMarshals
      .map(m => ({
        ...m,
        effectiveIsCheckedIn: m.isCheckedIn && !isCheckInStale(m.checkInTime),
      }))
      .sort((a, b) => (a.marshalName || '').localeCompare(b.marshalName || '', undefined, { sensitivity: 'base' }));

    // Get area contacts for this checkpoint's areas
    // Filter out self and deduplicate
    const rawAreaContacts = areaContactsRaw.value.filter(contact =>
      areaIds.includes(contact.areaId) &&
      (!contact.marshalId || contact.marshalId !== currentMarshalId.value)
    );
    const seenContacts = new Set();
    const areaContacts = rawAreaContacts
      .filter(contact => {
        const key = JSON.stringify({
          name: contact.name || '',
          marshalId: contact.marshalId || '',
          role: contact.role || '',
          phone: contact.phone || '',
          email: contact.email || '',
          notes: contact.notes || '',
        });
        if (seenContacts.has(key)) {
          return false;
        }
        seenContacts.add(key);
        return true;
      })
      .sort((a, b) => (a.marshalName || a.name || '').localeCompare(b.marshalName || b.name || '', undefined, { sensitivity: 'base' }));

    // Effective check-in status (reset if checked in more than 24 hours ago)
    const effectiveIsCheckedIn = assign.isCheckedIn && !isCheckInStale(assign.checkInTime);

    return {
      ...assign,
      location,
      areaName,
      areaIds,
      allMarshals,
      areaContacts,
      locationName: location?.name || 'Unknown Location',
      effectiveIsCheckedIn,
    };
  });
});

// Map actions composable (course map and checkpoint map toolbar)
const {
  courseMapActions,
  hideOtherCheckpoints,
  handleCourseMapVisibilityChange,
  handleCourseMapAction,
  getCheckpointMapActions,
  handleCheckpointMapVisibilityChange,
  handleCheckpointMapAction,
} = useMapActions({
  userLocation,
  assignmentsWithDetails,
  courseMapRef,
});

// Primary assignment (first one) for map highlighting
const primaryAssignment = computed(() => {
  return assignments.value.length > 0 ? assignments.value[0] : null;
});

// All assignment location IDs for multi-checkpoint highlighting
const assignmentLocationIds = computed(() => {
  return assignments.value.map(a => a.locationId);
});

// Filtered locations for course map (when hiding other checkpoints)
const courseMapLocations = computed(() => {
  if (!hideOtherCheckpoints.value) {
    return allLocations.value;
  }
  // Show only assigned checkpoints and dynamic checkpoints
  return allLocations.value.filter(loc => {
    const isAssigned = assignmentLocationIds.value.includes(loc.id);
    const isDynamic = loc.isDynamic || loc.IsDynamic;
    return isAssigned || isDynamic;
  });
});

// Check if user has any dynamic checkpoint assignments
const hasDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.some(a => a.location?.isDynamic || a.location?.IsDynamic);
});

// Get the user's first dynamic assignment (for quick updates)
const firstDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.find(a => a.location?.isDynamic || a.location?.IsDynamic);
});

// Check if user can update their first dynamic assignment based on scope
const canUpdateFirstDynamicAssignment = computed(() => {
  const dynAssign = firstDynamicAssignment.value;
  if (!dynAssign) return false;

  const location = dynAssign.location;
  const scopeConfigurations = location?.locationUpdateScopeConfigurations ||
    location?.LocationUpdateScopeConfigurations || [];

  return canMarshalUpdateDynamicLocation({
    scopeConfigurations,
    marshalId: currentMarshalId.value,
    marshalAssignmentLocationIds: assignmentLocationIds.value,
    areaLeadAreaIds: areaLeadAreaIds.value,
    locationId: dynAssign.locationId,
    locationAreaIds: dynAssign.areaIds || [],
  });
});

// Find checkpoints that are overdue for check-in and haven't been dismissed
const overdueCheckpoints = computed(() => {
  if (!event.value || !assignmentsWithDetails.value.length) return [];

  const now = new Date();
  const eventStartTime = event.value.eventDate ? new Date(event.value.eventDate) : null;

  return assignmentsWithDetails.value.filter(assign => {
    // Skip if already effectively checked in
    if (assign.effectiveIsCheckedIn) return false;

    // Skip if already dismissed
    if (dismissedCheckInReminders.value.has(assign.id)) return false;

    const location = assign.location;
    if (!location) return false;

    // Determine start time: checkpoint start time, or event start time
    const checkpointStartTime = location.startTime ? new Date(location.startTime) : eventStartTime;
    if (!checkpointStartTime) return false;

    // Determine end time: checkpoint end time, or event start + 2 hours
    let checkpointEndTime;
    if (location.endTime) {
      checkpointEndTime = new Date(location.endTime);
    } else if (eventStartTime) {
      checkpointEndTime = new Date(eventStartTime.getTime() + 2 * 60 * 60 * 1000);
    } else {
      // No end time can be determined, skip
      return false;
    }

    // Check if current time is within the reminder window
    return now >= checkpointStartTime && now < checkpointEndTime;
  });
});

// Watch for overdue checkpoints and show reminder modal
watch(overdueCheckpoints, (overdue) => {
  if (overdue.length > 0 && !showCheckInReminderModal.value) {
    checkInReminderCheckpoint.value = overdue[0];
    showCheckInReminderModal.value = true;
  }
}, { immediate: true });

// Watch for distance warning after check-in
watch(showDistanceWarning, (show) => {
  if (show) {
    showMessage('Distance notice', distanceWarningMessage.value);
    dismissDistanceWarning();
  }
});

// Handle "Go to checkpoint" from check-in reminder modal
const handleGoToCheckpoint = () => {
  const checkpoint = checkInReminderCheckpoint.value;
  if (checkpoint) {
    dismissCheckInReminder(checkpoint.id);
    expandedSection.value = 'assignments';
    expandedCheckpoint.value = checkpoint.id;
  }
};

// Event route for map display
const eventRoute = computed(() => {
  if (!event.value?.route) return [];
  // Route can be stored as JSON string or array
  const route = typeof event.value.route === 'string'
    ? JSON.parse(event.value.route)
    : event.value.route;
  return route || [];
});

const assignedLocation = computed(() => {
  if (!primaryAssignment.value || !allLocations.value.length) return null;
  return allLocations.value.find(loc => loc.id === primaryAssignment.value.locationId);
});

// Event contacts - loaded from new contacts API
const myContacts = ref([]);
const contactsLoading = ref(false);

// Legacy event contacts computed for backwards compatibility
const eventContacts = computed(() => {
  // Use new contacts API data, filtering to show only event-wide contacts
  // (those not specifically tied to areas/checkpoints in their scope)

  // Filter out contacts that are linked to the current marshal (don't show yourself)
  const filtered = myContacts.value.filter(contact => {
    return !contact.marshalId || contact.marshalId !== currentMarshalId.value;
  });

  // Deduplicate contacts with same name, marshalId, role, phone, email, and notes
  const seen = new Set();
  const deduplicated = filtered.filter(contact => {
    const key = JSON.stringify({
      name: contact.name || '',
      marshalId: contact.marshalId || '',
      role: contact.role || '',
      phone: contact.phone || '',
      email: contact.email || '',
      notes: contact.notes || '',
    });
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });

  // Sort by: pinned first, then display order, then name alphabetically
  return deduplicated.sort((a, b) => {
    // Pinned first
    if (a.isPinned !== b.isPinned) {
      return a.isPinned ? -1 : 1;
    }

    // Then by display order
    const orderA = a.displayOrder || 0;
    const orderB = b.displayOrder || 0;
    if (orderA !== orderB) {
      return orderA - orderB;
    }

    // Then by name
    return (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' });
  });
});

// Emergency contacts - filter by showInEmergencyInfo flag
const emergencyContacts = computed(() => {
  return eventContacts.value.filter(contact => contact.showInEmergencyInfo);
});

// Emergency notes - filter by showInEmergencyInfo flag
const emergencyNotes = computed(() => {
  return notes.value.filter(note => note.showInEmergencyInfo);
});

// Check if there's any emergency info to show
const hasEmergencyInfo = computed(() => {
  return emergencyContacts.value.length > 0 || emergencyNotes.value.length > 0;
});

// Get notes that are scoped to a specific checkpoint (area and checkpoint scopes only, not marshal scopes)
const getNotesForCheckpoint = (locationId, passedAreaIds = []) => {
  if (!notes.value || notes.value.length === 0) return [];

  // Get area IDs from the location directly as a fallback (matching NotesView behavior)
  const location = allLocations.value.find(l => l.id === locationId);
  const locationAreaIds = location?.areaIds || location?.AreaIds || [];
  // Use passed areaIds if available, otherwise use location's areaIds
  const areaIds = passedAreaIds.length > 0 ? passedAreaIds : locationAreaIds;

  const matchedNotes = [];
  const seenNoteIds = new Set();

  for (const note of notes.value) {
    const noteId = note.noteId || note.NoteId || note.id;
    if (seenNoteIds.has(noteId)) continue;

    // Handle both camelCase and PascalCase from backend
    const scopeConfigurations = note.scopeConfigurations || note.ScopeConfigurations || [];
    let matched = false;

    // First try scopeConfigurations if available
    if (scopeConfigurations.length > 0) {
      for (const config of scopeConfigurations) {
        const itemType = config.itemType || config.ItemType;
        const ids = config.ids || config.Ids || [];

        // Check checkpoint-specific scope
        if (itemType === 'Checkpoint') {
          if (ids.includes(locationId) || ids.includes('ALL_CHECKPOINTS')) {
            matched = true;
            break;
          }
        }

        // Check area-based scope (notes scoped to area apply to checkpoints in that area)
        if (itemType === 'Area') {
          const areaMatch = ids.includes('ALL_AREAS') || (areaIds.length > 0 && areaIds.some(areaId => ids.includes(areaId)));
          if (areaMatch) {
            matched = true;
            break;
          }
        }
      }
    } else {
      // Fallback: use matchedScope from my-notes API to determine if note applies to checkpoint/area
      // matchedScope indicates why the note was included for this marshal
      const matchedScope = note.matchedScope || note.MatchedScope || '';

      // Area and checkpoint scopes should show in checkpoint section
      // Marshal-specific scopes (SpecificPeople, AllMarshals) should NOT show in checkpoint section
      const areaOrCheckpointScopes = [
        'EveryoneInAreas',
        'EveryAreaLead',
        'EveryoneAtCheckpoints',
        'Area',
        'Checkpoint',
      ];

      if (areaOrCheckpointScopes.some(scope => matchedScope.includes(scope) || matchedScope === scope)) {
        matched = true;
      }
    }

    if (matched) {
      seenNoteIds.add(noteId);
      matchedNotes.push(note);
    }
  }

  // Sort by priority (Emergency > Urgent > High > Normal > Low), then pinned, then by date
  const priorityOrder = { 'Emergency': 0, 'Urgent': 1, 'High': 2, 'Normal': 3, 'Low': 4 };
  return matchedNotes.sort((a, b) => {
    // Pinned notes first
    const aPinned = a.isPinned || a.IsPinned;
    const bPinned = b.isPinned || b.IsPinned;
    if (aPinned && !bPinned) return -1;
    if (!aPinned && bPinned) return 1;

    // Then by priority
    const priorityA = priorityOrder[a.priority || a.Priority || 'Normal'] ?? 3;
    const priorityB = priorityOrder[b.priority || b.Priority || 'Normal'] ?? 3;
    if (priorityA !== priorityB) return priorityA - priorityB;

    // Then by date (newest first)
    const dateA = a.createdAt || a.CreatedAt;
    const dateB = b.createdAt || b.CreatedAt;
    return new Date(dateB) - new Date(dateA);
  });
};

// Area contacts - loaded from API (used in assignmentsWithDetails)
const areaContactsRaw = ref([]);

// Available source checkpoints for copying location (excludes the one being updated)
// Natural sort comparator for strings with numbers (e.g., "Checkpoint 2" before "Checkpoint 10")
const naturalSort = (a, b) => {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' });
};

const availableSourceCheckpoints = computed(() => {
  if (!updatingLocationFor.value) return [];
  const currentId = updatingLocationFor.value.locationId;
  // Show all checkpoints with valid coordinates (excluding current one and those at 0,0)
  return allLocations.value.filter(l =>
    l.id !== currentId &&
    l.latitude !== 0 &&
    l.longitude !== 0
  ).sort((a, b) => naturalSort(a.name, b.name));
});

const mapCenter = computed(() => {
  if (assignedLocation.value) {
    return {
      lat: assignedLocation.value.latitude,
      lng: assignedLocation.value.longitude,
    };
  }
  if (userLocation.value) {
    return userLocation.value;
  }
  if (allLocations.value.length > 0) {
    return {
      lat: allLocations.value[0].latitude,
      lng: allLocations.value[0].longitude,
    };
  }
  return { lat: 51.505, lng: -0.09 };
});

const loadEventData = async () => {
  const eventId = route.params.eventId;
  console.log('Loading event data for:', { eventId, marshalId: currentMarshalId.value });

  // Check if we're offline and have cached data
  if (getOfflineMode()) {
    console.log('Offline mode detected, attempting to load from cache...');
    const cachedData = await getCachedEventData(eventId);
    if (cachedData) {
      console.log('Loaded cached data from:', cachedData.cachedAt);
      event.value = cachedData.event;
      areas.value = cachedData.areas || [];
      allLocations.value = cachedData.locations || [];
      checklistItems.value = cachedData.checklist || [];
      myContacts.value = cachedData.contacts || [];
      notes.value = cachedData.notes || [];

      // Load area lead checkpoints for marshal name lookup
      if (cachedData.areaLeadDashboard?.checkpoints) {
        areaLeadCheckpoints.value = cachedData.areaLeadDashboard.checkpoints;
      }

      // Apply terminology settings
      if (event.value) {
        setTerminology(event.value);
      }

      // Extract assignments for current marshal
      if (currentMarshalId.value) {
        const myAssignments = [];
        for (const location of allLocations.value) {
          const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
          if (myAssignment) {
            myAssignments.push(myAssignment);
          }
        }
        assignments.value = myAssignments;
      }

      return;
    } else {
      console.warn('No cached data available while offline');
    }
  }

  // Load all data in parallel for better performance
  console.log('Fetching event data in parallel...');

  // Phase 1: Core event data (3 calls in parallel)
  const [eventResult, areasResult, statusResult] = await Promise.allSettled([
    eventsApi.getById(eventId),
    areasApi.getByEvent(eventId),
    assignmentsApi.getEventStatus(eventId)
  ]);

  // Process event result
  if (eventResult.status === 'fulfilled') {
    event.value = eventResult.value.data;
    console.log('Event loaded:', event.value?.name);
    if (event.value) {
      setTerminology(event.value);
      // Update session with event info for the marshal selector
      updateSessionEventInfo(eventId, event.value.name, event.value.eventDate);
    }
  } else {
    console.error('Failed to load event:', eventResult.reason);
  }

  // Process areas result
  if (areasResult.status === 'fulfilled') {
    areas.value = areasResult.value.data || [];
    console.log('Areas loaded:', areas.value.length);
  } else {
    console.error('Failed to load areas:', areasResult.reason);
  }

  // Process event status result
  if (statusResult.status === 'fulfilled') {
    allLocations.value = statusResult.value.data?.locations || [];
    console.log('Locations loaded:', allLocations.value.length);

    // Find ALL of current marshal's assignments from all locations
    if (currentMarshalId.value) {
      const myAssignments = [];
      for (const location of allLocations.value) {
        const myAssignment = location.assignments?.find(a => a.marshalId === currentMarshalId.value);
        if (myAssignment) {
          myAssignments.push(myAssignment);
        }
      }
      assignments.value = myAssignments;
      console.log('Total assignments found:', assignments.value.length);
    }
  } else {
    console.error('Failed to load event status:', statusResult.reason);
  }

  // Phase 2: Supplementary data in parallel
  // For area leads, also load the area lead dashboard to populate "Your marshals" section
  const phase2Promises = [
    loadChecklist(),
    loadContacts(),
    loadNotes(),
    loadMyIncidents()
  ];

  // Add area lead dashboard load if user is an area lead
  if (isAreaLead.value) {
    phase2Promises.push(loadAreaLeadDashboard());
  }

  await Promise.allSettled(phase2Promises);

  // Cache all data for offline access
  // Use JSON.parse(JSON.stringify()) to convert Vue reactive proxies to plain objects
  try {
    const cacheData = {
      event: event.value,
      areas: areas.value,
      locations: allLocations.value,
      checklist: checklistItems.value,
      contacts: myContacts.value,
      notes: notes.value,
      marshalId: currentMarshalId.value
    };

    // Include area lead dashboard if available
    if (areaLeadCheckpoints.value.length > 0) {
      cacheData.areaLeadDashboard = {
        checkpoints: areaLeadCheckpoints.value
      };
    }

    const plainData = JSON.parse(JSON.stringify(cacheData));
    await cacheEventData(eventId, plainData);
    console.log('Event data cached for offline access');
  } catch (error) {
    console.warn('Failed to cache event data:', error);
  }

  // Start polling for dynamic checkpoint positions if any exist
  startDynamicCheckpointPolling();
};

// Set up auth callback now that loadEventData is defined
authCallbacks.onAuthSuccess = loadEventData;

/**
 * Background preloader - fetches additional data when the page is idle
 * This ensures all data needed for offline access is cached
 */
const preloadOfflineData = async () => {
  const eventId = route.params.eventId;

  // Don't preload if offline
  if (getOfflineMode()) {
    console.log('Skipping background preload: offline');
    return;
  }

  console.log('Starting background preload for offline data...');

  // Preload area lead dashboard (even if not currently an area lead, in case roles change)
  // This data is useful for area leads to see their checkpoints offline
  try {
    const dashboardResponse = await areasApi.getAreaLeadDashboard(eventId);
    if (dashboardResponse.data) {
      // Store checkpoints for marshal name lookup in checklists
      areaLeadCheckpoints.value = dashboardResponse.data.checkpoints || [];

      // Convert to plain object to avoid IndexedDB serialization issues
      const areaLeadDashboard = JSON.parse(JSON.stringify({
        areas: dashboardResponse.data.areas || [],
        checkpoints: dashboardResponse.data.checkpoints || []
      }));

      // Try to update existing cache, or create new entry
      const existingCache = await getCachedEventData(eventId);
      if (existingCache) {
        await updateCachedField(eventId, 'areaLeadDashboard', areaLeadDashboard);
      } else {
        await cacheEventData(eventId, { areaLeadDashboard });
      }
      console.log('Area lead dashboard cached for offline access');
    }
  } catch (error) {
    // This might fail if user isn't an area lead - that's fine
    if (error.response?.status !== 403) {
      console.warn('Failed to preload area lead dashboard:', error);
    }
  }

  console.log('Background preload complete');
};

/**
 * Load area lead dashboard data - populates areaLeadCheckpoints for "Your marshals" section
 */
const loadAreaLeadDashboard = async () => {
  const eventIdVal = route.params.eventId;
  try {
    const response = await areasApi.getAreaLeadDashboard(eventIdVal);
    if (response.data?.checkpoints) {
      areaLeadCheckpoints.value = response.data.checkpoints;
      console.log('Area lead dashboard loaded:', areaLeadCheckpoints.value.length, 'checkpoints');
    }
  } catch (error) {
    // This might fail if user isn't an area lead - that's fine
    if (error.response?.status !== 403) {
      console.warn('Failed to load area lead dashboard:', error);
    }
  }
};

const loadNotes = async () => {
  const eventIdVal = route.params.eventId;
  try {
    // Use getMyNotes to get notes relevant to the current marshal
    const response = await notesApi.getMyNotes(eventIdVal);
    notes.value = response.data || [];
    sectionLastLoadedAt.value.notes = Date.now();
    console.log('Notes loaded:', notes.value.length);
  } catch (error) {
    console.error('Failed to load notes:', error);
  }
};

const loadContacts = async () => {
  const eventIdVal = route.params.eventId;
  contactsLoading.value = true;

  try {
    // Load contacts using the new API that returns scoped contacts for this user
    const response = await contactsApi.getMyContacts(eventIdVal);
    myContacts.value = response.data || [];
    sectionLastLoadedAt.value.eventContacts = Date.now();
    console.log('Contacts loaded:', myContacts.value.length);
  } catch (error) {
    console.warn('Failed to load contacts:', error);
    myContacts.value = [];
  } finally {
    contactsLoading.value = false;
  }
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

const handleMessageModalClose = () => {
  showMessageModal.value = false;
};

const handleMapClick = async (coords) => {
  // If in explicit selection mode, update immediately
  if (selectingLocationOnMap.value && updatingLocationFor.value) {
    await updateDynamicCheckpointLocation(
      updatingLocationFor.value.locationId,
      coords.lat,
      coords.lng,
      'manual'
    );
    selectingLocationOnMap.value = false;
    updatingLocationFor.value = null;
    // Close fullscreen and restore previous section
    routeSectionRef.value?.mapRef?.closeFullscreen?.();
    restoreSectionAfterMapSelect();
    return;
  }

  // If user has a dynamic assignment they can update and clicks on the map, offer to update
  if (canUpdateFirstDynamicAssignment.value && firstDynamicAssignment.value) {
    const dynAssign = firstDynamicAssignment.value;
    confirmModalTitle.value = 'Update location';
    confirmModalMessage.value = `Update ${dynAssign.location?.name || 'dynamic checkpoint'} to this location?`;
    confirmModalCallback.value = async () => {
      await updateDynamicCheckpointLocation(
        dynAssign.locationId,
        coords.lat,
        coords.lng,
        'manual'
      );
      // Close fullscreen after updating
      routeSectionRef.value?.mapRef?.closeFullscreen?.();
    };
    showConfirmModal.value = true;
  }
};

const handleLocationClick = async (location) => {
  // If in selection mode, copy this checkpoint's location
  if (selectingLocationOnMap.value && updatingLocationFor.value) {
    // Don't copy from itself
    if (location.id === updatingLocationFor.value.locationId) return;

    await updateDynamicCheckpointLocation(
      updatingLocationFor.value.locationId,
      location.latitude,
      location.longitude,
      'checkpoint',
      location.id
    );
    selectingLocationOnMap.value = false;
    // Close fullscreen and restore previous section
    routeSectionRef.value?.mapRef?.closeFullscreen?.();
    restoreSectionAfterMapSelect();
    return;
  }

  // If user has a dynamic assignment they can update, offer to copy this location
  if (canUpdateFirstDynamicAssignment.value && firstDynamicAssignment.value) {
    const dynAssign = firstDynamicAssignment.value;
    // Don't offer to copy from itself
    if (location.id === dynAssign.locationId) return;

    confirmModalTitle.value = 'Copy location';
    confirmModalMessage.value = `Copy location from "${location.name}" to ${dynAssign.location?.name || 'your dynamic checkpoint'}?`;
    confirmModalCallback.value = async () => {
      await updateDynamicCheckpointLocation(
        dynAssign.locationId,
        location.latitude,
        location.longitude,
        'checkpoint',
        location.id
      );
      // Close fullscreen after updating
      routeSectionRef.value?.mapRef?.closeFullscreen?.();
    };
    showConfirmModal.value = true;
  }
};

const handleClose = () => {
  baseHandleClose(() => {
    // Clear local state
    assignments.value = [];
    checklistItems.value = [];
    areaContactsRaw.value = [];
  });
};

const formatEventDate = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

onMounted(async () => {
  const eventId = route.params.eventId;
  const magicCode = route.query.code;

  loading.value = true;

  try {
    // Check for magic code in URL
    if (magicCode) {
      await authenticateWithMagicCode(eventId, magicCode);
    } else {
      // Check for existing session
      const hasSession = await checkExistingSession(eventId);

      if (hasSession) {
        await loadEventData();
      }
    }
  } finally {
    loading.value = false;
  }

  // Start location tracking if authenticated
  if (isAuthenticated.value) {
    startLocationTracking();

    // Schedule background preload when the page is idle
    // This fetches additional data for offline use without blocking the UI
    if ('requestIdleCallback' in window) {
      requestIdleCallback(() => {
        preloadOfflineData();
      }, { timeout: 5000 }); // Ensure it runs within 5 seconds
    } else {
      // Fallback for browsers without requestIdleCallback
      setTimeout(() => {
        preloadOfflineData();
      }, 2000);
    }
  }
});

// Watch for authentication changes to start/stop location tracking
watch(isAuthenticated, (authenticated) => {
  if (authenticated) {
    startLocationTracking();
  } else {
    stopLocationTracking();
  }
});

// Watch for URL query changes (handles pasting a new login link while already logged in)
watch(() => route.query.code, async (newCode) => {
  if (newCode && isAuthenticated.value) {
    // A new magic code was added to the URL while already authenticated
    // Re-authenticate with the new code (switching users)
    loading.value = true;
    try {
      await authenticateWithMagicCode(route.params.eventId, newCode);
    } finally {
      loading.value = false;
    }
  }
});

// Update document title when event loads - use configured person term
watch(event, (newEvent) => {
  if (newEvent) {
    document.title = `OnTheDay App - ${terms.value.person}`;
  }
}, { immediate: true });

onUnmounted(() => {
  // Note: stopLocationTracking is called automatically by useLocationTracking composable
  stopHeartbeat();
  stopAutoUpdate();
  stopDynamicCheckpointPolling();
  stopAllCheckpointsPolling();
});
</script>

<style scoped>
/* Z-index layering - modals should always be above fullscreen map */
.marshal-view {
  --z-fullscreen-map: 2000;
  --z-modal: calc(var(--z-fullscreen-map) + 1000);
  min-height: 100vh;
  background: var(--brand-gradient);
}

.header {
  backdrop-filter: blur(10px);
  padding: 1rem 2rem;
  display: flex;
  align-items: center;
  position: relative;
}

.header.logo-left,
.header.logo-right {
  flex-direction: row;
  padding-top: 0;
  padding-bottom: 0;
  align-items: stretch;
  height: 120px;
}

.header.logo-left {
  padding-left: 0;
}

.header.logo-right {
  padding-right: 0;
}

.header.logo-cover {
  flex-direction: column;
}

.header-center {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  padding: 1rem 0;
  position: relative;
  z-index: 1;
}

.header h1 {
  margin: 0;
}

.header-title {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
}

.header-event-date {
  font-size: 0.9rem;
  opacity: 0.9;
  font-weight: 400;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.header-weather {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  opacity: 0.85;
  cursor: help;
}

.header-weather .weather-temp {
  font-weight: 500;
}

.header-logo {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  align-self: stretch;
  aspect-ratio: 1 / 1;
  overflow: hidden;
}

.header-logo-left {
  margin-right: 0;
}

.header-logo-right {
  margin-left: 0;
}

.header-logo-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
}

/* Cover logo background */
.header-logo-cover {
  position: absolute;
  inset: 0;
  overflow: hidden;
  z-index: 0;
}

.header-logo-cover-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  object-position: center;
  opacity: 0.25;
}

.header-actions {
  display: flex;
  gap: 0.5rem;
  position: relative;
  z-index: 1;
}

.btn-header-action {
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  font-size: 0.85rem;
  transition: all 0.2s;
  min-width: 110px;
  text-align: center;
}

.btn-report-incident {
  background: var(--warning-orange);
  color: white;
}

.btn-report-incident:hover {
  background: var(--warning-dark);
}

.btn-emergency {
  background: var(--emergency-bg);
  color: white;
}

.btn-emergency:hover {
  background: var(--emergency-hover);
}

.btn-close-session {
  position: fixed;
  top: 0;
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: 0 0 8px 8px;
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  z-index: 100;
  font-size: 1.75rem;
  line-height: 1;
  font-weight: 300;
}

.btn-close-session:hover {
  background: var(--glass-bg-strong);
}

.btn-close-left {
  left: 0;
  border-radius: 0 0 8px 0;
  border-left: none;
  border-top: none;
}

.btn-close-right {
  right: 0;
  border-radius: 0 0 0 8px;
  border-right: none;
  border-top: none;
}

.container {
  padding: 2rem;
  max-width: 800px;
  margin: 0 auto;
}

.selection-card {
  background: var(--card-bg);
  border-radius: 12px;
  padding: 2rem;
  box-shadow: var(--shadow-lg);
  margin-bottom: 2rem;
}

.selection-card.error-card {
  border: 2px solid var(--emergency-bg);
}

.selection-card h2 {
  margin: 0 0 1rem 0;
  color: var(--text-dark);
}

.instruction {
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.loading {
  text-align: center;
  padding: 2rem;
  color: var(--text-secondary);
}

/* Welcome bar - compact horizontal layout */
.welcome-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--card-bg);
  border-radius: 10px;
  padding: 0.75rem 1.25rem;
  margin-bottom: 0.5rem;
  box-shadow: var(--shadow-sm);
}

.welcome-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.welcome-email {
  color: var(--text-secondary);
  font-size: 0.8rem;
}

.mode-switch {
  flex-shrink: 0;
}

.btn-mode-switch {
  background: var(--brand-gradient);
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-mode-switch:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px var(--brand-shadow-lg);
}

.reauth-hint {
  font-size: 0.75rem;
  color: var(--text-muted);
  font-style: italic;
  cursor: help;
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

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--brand-primary-hover);
  transform: translateY(-2px);
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--btn-cancel-hover);
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Add Incident Note Modal */
.add-note-form {
  padding: 0.5rem 0;
}

.note-textarea {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 6px;
  background: var(--input-bg);
  color: var(--text-primary);
  font-size: 0.9rem;
  font-family: inherit;
  resize: vertical;
  min-height: 80px;
}

.note-textarea:focus {
  outline: none;
  border-color: var(--brand-primary);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}

.modal-actions .btn {
  padding: 0.5rem 1rem;
  font-size: 0.875rem;
}

.error {
  padding: 1rem;
  background: var(--danger-bg-lighter);
  color: var(--danger);
  border-radius: 6px;
  font-size: 0.875rem;
}

/* Accordion styles */
.accordion {
  display: flex;
  flex-direction: column;
  gap: 0;
}

/* Wide screen layout */
@media (min-width: 1200px) {
  .container {
    max-width: 1400px;
  }
}

/* Extra wide screens */
@media (min-width: 1600px) {
  .container {
    max-width: 1800px;
  }
}

@media (max-width: 768px) {
  .header {
    flex-direction: column;
    gap: 1rem;
    text-align: center;
  }

  .header h1 {
    font-size: 1.25rem;
  }

  .header-event-date {
    font-size: 0.85rem;
  }

  .container {
    padding: 1rem;
  }

  .selection-card {
    padding: 1.5rem;
  }
}

</style>
