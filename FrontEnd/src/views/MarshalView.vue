<template>
  <div class="marshal-view" :style="pageBackgroundStyle">
    <!-- Offline Indicator -->
    <OfflineIndicator />

    <header class="header" :class="`logo-${brandingLogoPosition}`" :style="headerStyle">
      <!-- Cover logo background -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'cover'" class="header-logo-cover">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-cover-img" />
      </div>

      <!-- Logout button on left (when logo is on right) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition === 'right'"
        @click="confirmLogout"
        class="btn-logout-icon btn-logout-left"
        :style="{ color: headerTextColor }"
        :title="`Logout ${currentPerson?.name || ''}`"
      >
        <span v-html="getIcon('logout')"></span>
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
          </div>
        </div>
        <div v-if="isAuthenticated" class="header-actions">
          <button @click="showReportIncident = true" class="btn-header-action btn-report-incident">
            Report Incident
          </button>
          <button @click="showEmergency = true" class="btn-header-action btn-emergency">
            Emergency Info
          </button>
        </div>
      </div>

      <!-- Right logo -->
      <div v-if="brandingLogoUrl && brandingLogoPosition === 'right'" class="header-logo header-logo-right">
        <img :src="brandingLogoUrl" alt="Event logo" class="header-logo-img" />
      </div>

      <!-- Logout button on right (when logo is on left, cover, or none) -->
      <button
        v-if="isAuthenticated && brandingLogoPosition !== 'right'"
        @click="confirmLogout"
        class="btn-logout-icon btn-logout-right"
        :style="{ color: headerTextColor }"
        :title="`Logout ${currentPerson?.name || ''}`"
      >
        <span v-html="getIcon('logout')"></span>
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
            :header-style="headerStyle"
            :header-text-color="headerTextColor"
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
            :checkpoint-term="termsLower.checkpoint"
            :checkpoint-term-plural="termsLower.checkpoints"
            :people-term="terms.people"
            :area-term="terms.area"
            :get-toolbar-actions="getCheckpointMapActions"
            :is-area-lead-for-areas="isAreaLeadForAreas"
            :get-notes-for-checkpoint="getNotesForCheckpoint"
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
            :checklist-term="termsLower.checklists"
            :area-term="termsLower.area"
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
            :area-term="termsLower.area"
            :area-term-plural="termsLower.areas"
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
            :person-term="termsLower.person"
            :people-term="termsLower.people"
            :checklist-term="terms.checklists"
            :checklist-term-lower="termsLower.checklists"
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
            :course-term="terms.course"
            :user-location="userLocation"
            :selecting-location="selectingLocationOnMap"
            :selecting-location-name="updatingLocationFor?.location?.name || ''"
            :locations="allLocations"
            :route="eventRoute"
            :center="mapCenter"
            :highlight-location-ids="assignmentLocationIds"
            :clickable="selectingLocationOnMap || hasDynamicAssignment"
            :header-style="headerStyle"
            :header-text-color="headerTextColor"
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
      :checkpoint-term="terms.checkpoint"
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
      :checkpoint-term="updatingLocationFor?.location?.resolvedCheckpointTerm || terms.checkpoint"
      :checkpoint-term-plural="termsLower.checkpoints"
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
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { authApi, checkInApi, checklistApi, eventsApi, assignmentsApi, locationsApi, areasApi, notesApi, contactsApi, incidentsApi, queueOfflineAction, getOfflineMode } from '../services/api';
import CommonMap from '../components/common/CommonMap.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import BaseModal from '../components/BaseModal.vue';
import EmergencyContactModal from '../components/event-manage/modals/EmergencyContactModal.vue';
import ReportIncidentModal from '../components/ReportIncidentModal.vue';
import IncidentCard from '../components/IncidentCard.vue';
import IncidentDetailModal from '../components/IncidentDetailModal.vue';
import AreaLeadSection from '../components/AreaLeadSection.vue';
import GroupedTasksList from '../components/event-manage/GroupedTasksList.vue';
import NotesView from '../components/NotesView.vue';
import OfflineIndicator from '../components/OfflineIndicator.vue';
import CheckInToggleButton from '../components/common/CheckInToggleButton.vue';
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
import CheckpointCard from '../components/marshal/CheckpointCard.vue';
import NoteCard from '../components/marshal/NoteCard.vue';
import ContactCard from '../components/marshal/ContactCard.vue';
import { setTerminology, useTerminology } from '../composables/useTerminology';
import { useMarshalBranding } from '../composables/useMarshalBranding';
import { useLocationTracking } from '../composables/useLocationTracking';
import { getIcon } from '../utils/icons';
import { generateCheckpointSvg } from '../constants/checkpointIcons';
import { useOffline } from '../composables/useOffline';
import { cacheEventData, getCachedEventData, updateCachedField } from '../services/offlineDb';

const { terms, termsLower } = useTerminology();

// Offline support
const {
  isFullyOnline,
  pendingActionsCount,
  updatePendingCount
} = useOffline();

const route = useRoute();
const router = useRouter();

// Event ID from route params (reactive for template usage)
const eventId = computed(() => route.params.eventId);

// Auth state
const isAuthenticated = ref(false);
const authenticating = ref(false);
const loginError = ref(null);
const currentPerson = ref(null);
const currentMarshalId = ref(null);
const userClaims = ref(null);

// Current marshal name for check-in display
const currentMarshalName = computed(() => currentPerson.value?.name || null);

// Area lead state
const areaLeadAreaIds = computed(() => {
  if (!userClaims.value?.eventRoles) return [];
  const areaLeadRoles = userClaims.value.eventRoles.filter(r => r.role === 'EventAreaLead');
  return areaLeadRoles.flatMap(r => r.areaIds || []);
});

const isAreaLead = computed(() => areaLeadAreaIds.value.length > 0);

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

// All checkpoints available for incident reporting
// Marshals: see their assigned checkpoints
// Area leads: see all checkpoints in their areas plus their own assignments
const incidentCheckpoints = computed(() => {
  const checkpointMap = new Map();

  // Add marshal's assigned checkpoints
  for (const assign of assignmentsWithDetails.value) {
    if (assign.location) {
      checkpointMap.set(assign.location.id, {
        id: assign.location.id,
        name: assign.location.name,
        description: assign.location.description,
        order: assign.location.order ?? 999,
      });
    }
  }

  // For area leads, also add all checkpoints in their areas
  if (isAreaLead.value) {
    for (const loc of allLocations.value) {
      if (checkpointMap.has(loc.id)) continue;
      const locAreaIds = loc.areaIds || loc.AreaIds || [];
      if (locAreaIds.some(areaId => areaLeadAreaIds.value.includes(areaId))) {
        checkpointMap.set(loc.id, {
          id: loc.id,
          name: loc.name,
          description: loc.description,
          order: loc.order ?? 999,
        });
      }
    }
  }

  // Sort by order, then by name (natural sort so "2 A" comes before "10 - B")
  return Array.from(checkpointMap.values()).sort((a, b) => {
    if (a.order !== b.order) return a.order - b.order;
    return (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

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

// Check if user is an event admin
const isEventAdmin = computed(() => {
  if (!userClaims.value?.eventRoles) return false;
  return userClaims.value.eventRoles.some(r => r.role === 'EventAdmin');
});

// Check if user can switch to admin mode (logged in via SecureEmailLink AND is admin)
const canSwitchToAdmin = computed(() => {
  return userClaims.value?.authMethod === 'SecureEmailLink' && isEventAdmin.value;
});

// Check if user is admin but logged in via magic code (needs re-auth for admin)
const isAdminButNeedsReauth = computed(() => {
  return userClaims.value?.authMethod === 'MarshalMagicCode' && isEventAdmin.value;
});

// Switch to admin mode
const switchToAdminMode = () => {
  router.push(`/admin/events/${route.params.eventId}`);
};

// Event data
const event = ref(null);
const allLocations = ref([]);
const assignments = ref([]);
const areas = ref([]);
const loading = ref(true);

// Branding styles from composable
const {
  pageBackgroundStyle,
  headerStyle,
  headerTextColor,
  accentColor,
  accentTextColor,
  accentButtonStyle,
  brandingLogoUrl,
  brandingLogoPosition,
} = useMarshalBranding(event);

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

// Check-in state
const checkingIn = ref(null); // Now stores the assignment ID being checked in
const checkingInAssignment = ref(null);
const checkInError = ref(null);

// Checklist state
const checklistItems = ref([]);
const checklistLoading = ref(false);
const checklistError = ref(null);
const savingChecklist = ref(false);
const checklistGroupBy = ref('person'); // 'none', 'checkpoint', 'person', 'job'
const expandedChecklistGroup = ref(null);

// Area lead checkpoints data (for marshal name lookup in checklists)
const areaLeadCheckpoints = ref([]);

// Notes state
const notes = ref([]);

// Incidents state (my incidents and area lead incidents)
const myIncidents = ref([]);
const incidentsLoading = ref(false);
const selectedIncident = ref(null);
const showIncidentDetail = ref(false);
const showAddIncidentNoteModal = ref(false);
const incidentNoteText = ref('');
const submittingIncidentNote = ref(false);

// Count of open incidents for badge
const openIncidentsCount = computed(() => {
  return myIncidents.value.filter(i =>
    i.status === 'open' || i.status === 'acknowledged' || i.status === 'in_progress'
  ).length;
});

// Dynamic checkpoint update state
const showLocationUpdateModal = ref(false);
const updatingLocationFor = ref(null); // Holds the assignment being updated
const locationUpdateError = ref(null);
const locationUpdateSuccess = ref(null);
const updatingLocation = ref(false);
const autoUpdateEnabled = ref(false);
let autoUpdateInterval = null;
let dynamicCheckpointPollInterval = null;

// Area Lead section ref (accesses areaLeadRef via areaLeadSectionRef.value?.areaLeadRef?.value)
const areaLeadSectionRef = ref(null);
const areaLeadRef = computed(() => areaLeadSectionRef.value?.areaLeadRef?.value);

// Area Lead Marshals section state
const expandedAreaLeadMarshal = ref(null);
const savingAreaLeadMarshalTask = ref(false);
const areaLeadMarshalDataVersion = ref(0); // Trigger for recomputing marshals

// Toggle area lead marshal expansion
const toggleAreaLeadMarshalExpansion = (marshalId) => {
  expandedAreaLeadMarshal.value = expandedAreaLeadMarshal.value === marshalId ? null : marshalId;
};

// Aggregate all marshals from area lead checkpoints (deduplicated, sorted by name)
const allAreaLeadMarshals = computed(() => {
  // Access the version trigger to force recomputation when data changes
  // eslint-disable-next-line no-unused-vars
  const _version = areaLeadMarshalDataVersion.value;
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  const marshalMap = new Map();

  for (const checkpoint of checkpoints) {
    for (const marshal of (checkpoint.marshals || [])) {
      if (!marshalMap.has(marshal.marshalId)) {
        marshalMap.set(marshal.marshalId, {
          ...marshal,
          checkpoints: [],
          allTasks: [],
          totalTaskCount: 0,
          completedTaskCount: 0,
        });
      }
      const m = marshalMap.get(marshal.marshalId);
      m.checkpoints.push({
        checkpointId: checkpoint.checkpointId,
        name: checkpoint.name,
        description: checkpoint.description,
      });
      // Add marshal's outstanding tasks
      if (marshal.outstandingTasks) {
        for (const task of marshal.outstandingTasks) {
          m.allTasks.push({ ...task, isCompleted: false, checkpointName: checkpoint.name });
          m.totalTaskCount++;
        }
      }
      // Add marshal's completed tasks if available
      if (marshal.completedTasks) {
        for (const task of marshal.completedTasks) {
          m.allTasks.push({ ...task, isCompleted: true, checkpointName: checkpoint.name });
          m.totalTaskCount++;
          m.completedTaskCount++;
        }
      }
    }
  }

  return Array.from(marshalMap.values()).sort((a, b) =>
    (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
  );
});

// Handle check-in for area lead marshals section (uses unified handleCheckInToggle)
const handleAreaLeadMarshalCheckIn = async (marshal) => {
  // Find a checkpoint that has this marshal to get the assignment ID
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  const checkpoint = checkpoints.find(c =>
    c.marshals?.some(m => m.marshalId === marshal.marshalId)
  );
  if (!checkpoint) {
    console.error('Could not find checkpoint for marshal:', marshal);
    return;
  }

  // Find the full marshal object from the checkpoint
  const checkpointMarshal = checkpoint.marshals.find(m => m.marshalId === marshal.marshalId);
  if (!checkpointMarshal) return;

  // Build an assignment-like object for handleCheckInToggle
  const assignmentId = checkpointMarshal.assignmentId || checkpointMarshal.id;
  if (!assignmentId) {
    console.error('No assignment ID found for marshal:', checkpointMarshal);
    return;
  }

  const assignmentLike = {
    id: assignmentId,
    marshalId: marshal.marshalId,
    isCheckedIn: marshal.isCheckedIn,
    effectiveIsCheckedIn: marshal.isCheckedIn,
  };

  // Use unified toggle (no GPS for checking in others)
  await handleCheckInToggle(assignmentLike, false, checkpoint.locationId);
};

// Toggle task completion from the area lead marshals section
const toggleAreaLeadMarshalTask = async (task, marshal) => {
  if (savingAreaLeadMarshalTask.value) return;
  savingAreaLeadMarshalTask.value = true;

  const actionData = {
    marshalId: marshal.marshalId,
    contextType: task.contextType,
    contextId: task.contextId,
    actorMarshalId: currentMarshalId.value,
  };

  try {
    if (task.isCompleted) {
      // Uncomplete
      await checklistApi.uncomplete(eventId.value, task.itemId, actionData);
    } else {
      // Complete
      await checklistApi.complete(eventId.value, task.itemId, actionData);
    }
    // Reload the area lead dashboard data
    if (areaLeadRef.value?.loadDashboard) {
      await areaLeadRef.value.loadDashboard();
    }
    // Force recomputation of allAreaLeadMarshals
    areaLeadMarshalDataVersion.value++;
    // Also reload the main checklist
    await loadChecklist(true);
  } catch (err) {
    console.error('Failed to toggle task:', err);
  } finally {
    savingAreaLeadMarshalTask.value = false;
  }
};

// UI state
const showEmergency = ref(false);
const showReportIncident = ref(false);
const reportingIncident = ref(false);
const showConfirmModal = ref(false);
const confirmModalTitle = ref('');
const confirmModalMessage = ref('');
const confirmModalCallback = ref(null);

// Message modal state (for success/error messages)
const showMessageModal = ref(false);
const messageModalTitle = ref('');
const messageModalMessage = ref('');

// Check-in reminder modal state
const showCheckInReminderModal = ref(false);
const checkInReminderCheckpoint = ref(null);
const dismissedCheckInReminders = ref(new Set());

// Route section ref (accesses courseMapRef via routeSectionRef.value?.mapRef?.value)
const routeSectionRef = ref(null);
const courseMapRef = computed(() => routeSectionRef.value?.mapRef?.value);

// Course map visibility tracking (default to false so buttons show initially)
const courseMapVisibility = ref({
  userLocationInView: false,
  highlightedLocationInView: false,
});

// Checkpoint map visibility tracking (keyed by assignment ID)
const checkpointMapVisibility = ref({});

// Refs for checkpoint mini-maps (plain object, not reactive)
const checkpointMapRefs = {};

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

// Format check-in time for display
const formatCheckInTime = (checkInTime) => {
  if (!checkInTime) return '';
  const date = new Date(checkInTime);
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
};

// Format check-in method for display
const formatCheckInMethod = (method) => {
  if (!method) return '';
  switch (method) {
    case 'GPS': return 'GPS';
    case 'Manual': return 'Manual';
    case 'AreaLead': return 'By area lead';
    default: return method;
  }
};

// Format marshal checkpoints with descriptions for display
const formatMarshalCheckpoints = (checkpoints) => {
  if (!checkpoints || checkpoints.length === 0) return '';
  return checkpoints.map(c => {
    if (c.description) {
      // Truncate description if too long
      const maxDescLength = 40;
      const desc = c.description.length > maxDescLength
        ? c.description.substring(0, maxDescLength).trim() + '...'
        : c.description;
      return `${c.name} - ${desc}`;
    }
    return c.name;
  }).join(', ');
};

// Function to set checkpoint map ref
const setCheckpointMapRef = (assignId, el) => {
  if (el) {
    checkpointMapRefs[assignId] = el;
  }
};

// Helper to check if a check-in is stale (more than 24 hours old)
const isCheckInStale = (checkInTime) => {
  if (!checkInTime) return true;
  const checkInDate = new Date(checkInTime);
  const now = new Date();
  const hoursDiff = (now - checkInDate) / (1000 * 60 * 60);
  return hoursDiff > 24;
};

// GPS tracking from composable
const {
  userLocation,
  locationLastUpdated,
  startLocationTracking,
  stopLocationTracking,
} = useLocationTracking();

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

// Primary assignment (first one) for map highlighting
const primaryAssignment = computed(() => {
  return assignments.value.length > 0 ? assignments.value[0] : null;
});

// All assignment location IDs for multi-checkpoint highlighting
const assignmentLocationIds = computed(() => {
  return assignments.value.map(a => a.locationId);
});

// Check if user has any dynamic checkpoint assignments
const hasDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.some(a => a.location?.isDynamic || a.location?.IsDynamic);
});

// Get the user's first dynamic assignment (for quick updates)
const firstDynamicAssignment = computed(() => {
  return assignmentsWithDetails.value.find(a => a.location?.isDynamic || a.location?.IsDynamic);
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

// Dismiss check-in reminder for a checkpoint
const dismissCheckInReminder = (assignmentId) => {
  dismissedCheckInReminders.value.add(assignmentId);
  showCheckInReminderModal.value = false;
  checkInReminderCheckpoint.value = null;
};

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

// Filter out completed shared tasks that the marshal doesn't need to see
// Keep: incomplete tasks, personal tasks (completed or not), tasks completed by this marshal
const visibleChecklistItems = computed(() => {
  return checklistItems.value.filter(item => {
    // Always show incomplete tasks
    if (!item.isCompleted) return true;

    // Hide completed shared tasks (OnePerCheckpoint, OnePerArea, OneLeadPerArea)
    // unless this marshal completed it (so they can uncomplete it)
    const sharedScopes = ['OnePerCheckpoint', 'OnePerArea', 'OneLeadPerArea'];
    if (sharedScopes.includes(item.matchedScope)) {
      // Show if this marshal completed it
      return item.completedByActorId === currentMarshalId.value;
    }

    // Show all other completed tasks (personal tasks, everyone tasks, etc.)
    return true;
  });
});

// Checklist completion count (from visible items only)
const completedChecklistCount = computed(() => {
  return visibleChecklistItems.value.filter(item => item.isCompleted).length;
});

// Separate checklist items into "your jobs" vs "your area's jobs"
// "Your jobs" = tasks assigned specifically to you or applicable to everyone
// "Your area's jobs" = tasks for other people in your areas (area leads only)
const myChecklistItems = computed(() => {
  const myAssignmentIds = assignments.value.map(a => a.locationId);

  return visibleChecklistItems.value.filter(item => {
    // Personal tasks assigned to current marshal
    if (item.completionContextType === 'Personal') {
      return item.contextOwnerMarshalId === currentMarshalId.value;
    }
    // Checkpoint tasks for checkpoints the marshal is assigned to
    if (item.completionContextType === 'Checkpoint') {
      return myAssignmentIds.includes(item.completionContextId);
    }
    // Area-scoped tasks where you're in that area
    if (item.completionContextType === 'Area') {
      return areaLeadAreaIds.value.includes(item.completionContextId);
    }
    // Everyone tasks are yours
    return true;
  });
});

const areaChecklistItems = computed(() => {
  if (!isAreaLead.value) return [];
  const myAssignmentIds = assignments.value.map(a => a.locationId);

  return visibleChecklistItems.value.filter(item => {
    // Personal tasks for OTHER marshals
    if (item.completionContextType === 'Personal') {
      return item.contextOwnerMarshalId !== currentMarshalId.value;
    }
    // Checkpoint tasks for checkpoints the marshal is NOT assigned to
    if (item.completionContextType === 'Checkpoint') {
      return !myAssignmentIds.includes(item.completionContextId);
    }
    // Area tasks not in your personal areas (shouldn't happen but filter anyway)
    if (item.completionContextType === 'Area') {
      return !areaLeadAreaIds.value.includes(item.completionContextId);
    }
    return false;
  });
});

// Convert items to format needed by GroupedTasksList
const myChecklistItemsWithLocalState = computed(() => {
  return myChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false,
  }));
});

const areaChecklistItemsWithLocalState = computed(() => {
  return areaChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false,
  }));
});

// Keep for backwards compatibility (combines both)
// Checklist items with localIsCompleted for GroupedTasksList component
const checklistItemsWithLocalState = computed(() => {
  return visibleChecklistItems.value.map(item => ({
    ...item,
    localIsCompleted: item.isCompleted,
    isModified: false, // We don't track unsaved changes in marshal view
  }));
});

// Collect all marshals from area lead's checkpoints for name lookup
// Uses AreaLeadSection data if available, falls back to preloaded data
const areaMarshalsForChecklist = computed(() => {
  // Try to get checkpoints from AreaLeadSection (most up-to-date)
  // or fall back to preloaded area lead checkpoints
  const checkpoints = areaLeadRef.value?.checkpoints || areaLeadCheckpoints.value || [];
  if (checkpoints.length === 0) return [];

  const marshals = [];
  const seenIds = new Set();
  for (const checkpoint of checkpoints) {
    for (const marshal of (checkpoint.marshals || [])) {
      if (!seenIds.has(marshal.marshalId)) {
        seenIds.add(marshal.marshalId);
        marshals.push(marshal);
      }
    }
  }
  return marshals;
});

// Check if there are multiple checkpoints or jobs (to show grouping selector)
const hasMultipleChecklistContexts = computed(() => {
  const items = visibleChecklistItems.value;
  if (items.length <= 1) return false;

  // Check for multiple checkpoints
  const checkpointIds = new Set();
  const jobTexts = new Set();

  for (const item of items) {
    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      checkpointIds.add(item.completionContextId);
    }
    jobTexts.add(item.text);
  }

  return checkpointIds.size > 1 || jobTexts.size > 1;
});

// Effective grouping mode for non-leads (checkpoint grouping if multiple checkpoints exist)
const effectiveChecklistGroupBy = computed(() => {
  // Area leads use GroupedTasksList component instead
  if (isAreaLead.value) {
    return 'none'; // Not used for leads
  }

  // For non-leads, check if there are multiple checkpoints
  const items = visibleChecklistItems.value;
  const checkpointIds = new Set();
  for (const item of items) {
    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      checkpointIds.add(item.completionContextId);
    }
  }

  // If multiple checkpoints, group by checkpoint; otherwise show flat list
  return checkpointIds.size > 1 ? 'checkpoint' : 'none';
});

// Group checklist items by checkpoint (for non-leads with multiple checkpoints)
const groupedChecklistItems = computed(() => {
  const items = visibleChecklistItems.value;
  const groups = {};

  for (const item of items) {
    let key, name;

    if (item.completionContextType === 'Checkpoint' && item.completionContextId) {
      key = `checkpoint_${item.completionContextId}`;
      const location = allLocations.value.find(l => l.id === item.completionContextId);
      name = location?.name || 'Unknown ' + terms.value.checkpoint;
    } else if (item.completionContextType === 'Area' && item.completionContextId) {
      key = `area_${item.completionContextId}`;
      const area = areas.value.find(a => a.id === item.completionContextId);
      name = area?.name || 'Unknown ' + terms.value.area;
    } else {
      key = 'personal';
      name = 'Personal';
    }

    if (!groups[key]) {
      groups[key] = { key, name, items: [], completedCount: 0 };
    }
    groups[key].items.push(item);
    if (item.isCompleted) {
      groups[key].completedCount++;
    }
  }

  // Sort groups by name using natural sort
  return Object.values(groups).sort((a, b) =>
    a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })
  );
});

// Toggle checklist group expansion
const toggleChecklistGroup = (key) => {
  expandedChecklistGroup.value = expandedChecklistGroup.value === key ? null : key;
};

// Get short context name for grouped view (without "At checkpoint" prefix)
const getContextNameShort = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return 'Personal';
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = allLocations.value.find(l => l.id === item.completionContextId);
    return location?.name || 'Unknown';
  }

  if (item.completionContextType === 'Area') {
    const area = areas.value.find(a => a.id === item.completionContextId);
    return area?.name || 'Unknown';
  }

  return 'Personal';
};

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

  // Sort by name alphabetically
  return deduplicated.sort((a, b) =>
    (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
  );
});

// Emergency contacts - filter for specific roles that should be shown in emergency modal
const emergencyContacts = computed(() => {
  const emergencyRoles = ['EmergencyContact', 'EventDirector', 'MedicalLead', 'SafetyOfficer'];
  // Use eventContacts which already filters out self and deduplicates
  return eventContacts.value.filter(contact => emergencyRoles.includes(contact.role));
});

// Emergency notes - filter for Emergency or Urgent priority notes
const emergencyNotes = computed(() => {
  return notes.value.filter(note => {
    const priority = note.priority || note.Priority;
    return priority === 'Emergency' || priority === 'Urgent';
  });
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

// Area contacts - loaded from API, grouped by area
const areaContactsRaw = ref([]);

// Area contacts grouped by area
const areaContactsByArea = computed(() => {
  if (areaContactsRaw.value.length === 0) return [];

  // Filter out contacts that are linked to the current marshal (don't show yourself)
  const filtered = areaContactsRaw.value.filter(contact => {
    return !contact.marshalId || contact.marshalId !== currentMarshalId.value;
  });

  // Deduplicate contacts with same name, marshalId, role, phone, email, and notes
  const seen = new Set();
  const deduped = filtered.filter(contact => {
    const key = JSON.stringify({
      name: contact.name || '',
      marshalId: contact.marshalId || '',
      role: contact.role || '',
      phone: contact.phone || '',
      email: contact.email || '',
      notes: contact.notes || '',
      areaId: contact.areaId || '',
    });
    if (seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });

  // Group by area
  const grouped = {};
  for (const contact of deduped) {
    const key = contact.areaId || 'unknown';
    if (!grouped[key]) {
      grouped[key] = {
        areaId: contact.areaId,
        areaName: contact.areaName || 'Unknown Area',
        contacts: [],
      };
    }
    grouped[key].contacts.push(contact);
  }

  // Sort contacts within each group by name
  const groups = Object.values(grouped);
  for (const group of groups) {
    group.contacts.sort((a, b) =>
      (a.marshalName || a.name || '').localeCompare(b.marshalName || b.name || '', undefined, { sensitivity: 'base' })
    );
  }

  return groups;
});

// Total area contacts count (after filtering and deduplication)
const totalAreaContacts = computed(() => {
  return areaContactsByArea.value.reduce((sum, group) => sum + group.contacts.length, 0);
});

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

// SVG icons for course map toolbar actions (defined as constants to avoid reactivity issues)
const ICON_MY_LOCATION = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><circle cx="12" cy="12" r="4"/><path d="M13 4.069V2h-2v2.069A8.01 8.01 0 0 0 4.069 11H2v2h2.069A8.008 8.008 0 0 0 11 19.931V22h2v-2.069A8.007 8.007 0 0 0 19.931 13H22v-2h-2.069A8.008 8.008 0 0 0 13 4.069zM12 18c-3.309 0-6-2.691-6-6s2.691-6 6-6 6 2.691 6 6-2.691 6-6 6z"/></svg>';
const ICON_CHECKPOINT = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" width="18" height="18"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>';

// First assignment with a valid location (for recentering)
const firstAssignmentWithLocation = computed(() => {
  return assignmentsWithDetails.value.find(a =>
    a.location?.latitude && a.location?.longitude &&
    !(a.location.latitude === 0 && a.location.longitude === 0)
  );
});

// Course map toolbar actions for recentering (only show when location is off-screen)
const courseMapActions = computed(() => {
  const actions = [];

  // Recenter on my location (if GPS is available AND location is off-screen)
  if (userLocation.value && !courseMapVisibility.value.userLocationInView) {
    actions.push({
      id: 'recenter-user',
      label: 'My location',
      icon: 'custom',
      customIcon: ICON_MY_LOCATION,
    });
  }

  // Recenter on my checkpoint (if I have one with a location AND it's off-screen)
  // Use highlightedLocationInView since we highlight our assignments on the course map
  if (firstAssignmentWithLocation.value && !courseMapVisibility.value.highlightedLocationInView) {
    actions.push({
      id: 'recenter-checkpoint',
      label: 'My ' + termsLower.value.checkpoint,
      icon: 'custom',
      customIcon: ICON_CHECKPOINT,
    });
  }

  return actions;
});

// Handle course map visibility changes (only update if changed to avoid infinite loops)
const handleCourseMapVisibilityChange = (visibility) => {
  const current = courseMapVisibility.value;
  if (current.userLocationInView !== visibility.userLocationInView ||
      current.highlightedLocationInView !== visibility.highlightedLocationInView) {
    courseMapVisibility.value = visibility;
  }
};

// Handle course map toolbar actions
const handleCourseMapAction = ({ actionId }) => {
  if (actionId === 'recenter-user' && userLocation.value) {
    courseMapRef.value?.recenterOnUserLocation();
  } else if (actionId === 'recenter-checkpoint' && firstAssignmentWithLocation.value) {
    const loc = firstAssignmentWithLocation.value.location;
    courseMapRef.value?.recenterOnLocation(loc.latitude, loc.longitude);
  }
};

// Get toolbar actions for a checkpoint mini-map
const getCheckpointMapActions = (assignId) => {
  const actions = [];
  const visibility = checkpointMapVisibility.value[assignId] || { userLocationInView: false, highlightedLocationInView: false };

  // Recenter on my location (if GPS is available AND location is off-screen)
  if (userLocation.value && !visibility.userLocationInView) {
    actions.push({
      id: 'recenter-user',
      label: 'My location',
      icon: 'custom',
      customIcon: ICON_MY_LOCATION,
    });
  }

  // Recenter on this checkpoint (if it's off-screen)
  if (!visibility.highlightedLocationInView) {
    actions.push({
      id: 'recenter-checkpoint',
      label: termsLower.value.checkpoint.charAt(0).toUpperCase() + termsLower.value.checkpoint.slice(1),
      icon: 'custom',
      customIcon: ICON_CHECKPOINT,
    });
  }

  return actions;
};

// Handle checkpoint map visibility changes (only update if changed to avoid infinite loops)
const handleCheckpointMapVisibilityChange = (assignId, visibility) => {
  const current = checkpointMapVisibility.value[assignId];
  if (!current ||
      current.userLocationInView !== visibility.userLocationInView ||
      current.highlightedLocationInView !== visibility.highlightedLocationInView) {
    checkpointMapVisibility.value = {
      ...checkpointMapVisibility.value,
      [assignId]: visibility,
    };
  }
};

// Handle checkpoint map toolbar actions
const handleCheckpointMapAction = (assign, { actionId }) => {
  const mapRef = checkpointMapRefs[assign.id];
  if (actionId === 'recenter-user' && userLocation.value) {
    mapRef?.recenterOnUserLocation();
  } else if (actionId === 'recenter-checkpoint' && assign.location) {
    mapRef?.recenterOnLocation(assign.location.latitude, assign.location.longitude);
  }
};

const authenticateWithMagicCode = async (eventId, code, isReauth = false) => {
  authenticating.value = true;
  loginError.value = null;
  console.log('Authenticating with magic code:', { eventId, code, isReauth });

  try {
    const response = await authApi.marshalLogin(eventId, code);
    console.log('Auth response:', response.data);

    if (response.data.success) {
      // Store session token and magic code for future re-authentication
      localStorage.setItem('sessionToken', response.data.sessionToken);
      localStorage.setItem(`marshal_${eventId}`, response.data.marshalId);
      localStorage.setItem(`marshalCode_${eventId}`, code); // Store for re-auth

      currentPerson.value = response.data.person;
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;
      console.log('Authenticated as marshal:', currentMarshalId.value, currentPerson.value?.name);

      // Fetch full claims to get role information
      try {
        const claimsResponse = await authApi.getMe(eventId);
        userClaims.value = claimsResponse.data;
        console.log('User claims:', userClaims.value);
      } catch (claimsError) {
        console.warn('Failed to fetch claims:', claimsError);
      }

      // Clear the code from URL to prevent re-authentication attempts
      router.replace({ path: route.path, query: {} });

      // Start heartbeat to keep LastAccessedDate updated
      startHeartbeat();

      // Load data after authentication
      await loadEventData();
    } else {
      loginError.value = response.data.message || 'Authentication failed';
    }
  } catch (error) {
    console.error('Authentication failed:', error);
    loginError.value = error.response?.data?.message || 'Invalid or expired login link';
  } finally {
    authenticating.value = false;
  }
};

const checkExistingSession = async (eventId) => {
  const sessionToken = localStorage.getItem('sessionToken');
  const marshalId = localStorage.getItem(`marshal_${eventId}`);
  const storedCode = localStorage.getItem(`marshalCode_${eventId}`);

  if (!sessionToken || !marshalId) {
    // No session, but check if we have a stored code to re-authenticate
    if (storedCode) {
      console.log('No session but have stored code, attempting re-authentication...');
      await authenticateWithMagicCode(eventId, storedCode, true);
      return isAuthenticated.value;
    }
    return false;
  }

  try {
    const response = await authApi.getMe(eventId);

    if (response.data && response.data.marshalId) {
      userClaims.value = response.data;
      currentPerson.value = {
        personId: response.data.personId,
        name: response.data.personName,
        email: response.data.personEmail,
      };
      currentMarshalId.value = response.data.marshalId;
      isAuthenticated.value = true;

      // Start heartbeat to keep LastAccessedDate updated
      startHeartbeat();

      return true;
    }
  } catch (error) {
    console.log('Session validation failed, attempting re-authentication...', error);

    // Session invalid - try to re-authenticate with stored magic code
    if (storedCode) {
      try {
        await authenticateWithMagicCode(eventId, storedCode, true);
        if (isAuthenticated.value) {
          console.log('Re-authentication successful');
          return true;
        }
      } catch (reAuthError) {
        console.error('Re-authentication failed:', reAuthError);
      }
    }

    // Re-auth failed or no stored code, clear session
    localStorage.removeItem('sessionToken');
    localStorage.removeItem(`marshal_${eventId}`);
    localStorage.removeItem(`marshalCode_${eventId}`);
  }

  return false;
};

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

  // Phase 2: Supplementary data (4 calls in parallel)
  await Promise.allSettled([
    loadChecklist(),
    loadContacts(),
    loadNotes(),
    loadMyIncidents()
  ]);

  // Cache all data for offline access
  // Use JSON.parse(JSON.stringify()) to convert Vue reactive proxies to plain objects
  try {
    const plainData = JSON.parse(JSON.stringify({
      event: event.value,
      areas: areas.value,
      locations: allLocations.value,
      checklist: checklistItems.value,
      contacts: myContacts.value,
      notes: notes.value,
      marshalId: currentMarshalId.value
    }));
    await cacheEventData(eventId, plainData);
    console.log('Event data cached for offline access');
  } catch (error) {
    console.warn('Failed to cache event data:', error);
  }

  // Start polling for dynamic checkpoint positions if any exist
  startDynamicCheckpointPolling();
};

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

// Load incidents for "Your incidents" section
// Shows: incidents I reported + incidents in areas I'm a lead for
const loadMyIncidents = async () => {
  const eventId = route.params.eventId;
  incidentsLoading.value = true;

  try {
    const incidentMap = new Map();

    // For area leads, load incidents from their areas
    if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
      for (const areaId of areaLeadAreaIds.value) {
        try {
          const response = await incidentsApi.getForArea(eventId, areaId);
          const areaIncidents = response.data.incidents || [];
          for (const incident of areaIncidents) {
            incidentMap.set(incident.incidentId, incident);
          }
        } catch (err) {
          console.warn(`Failed to load incidents for area ${areaId}:`, err);
        }
      }
    }

    // Also try to get all incidents and filter to ones I reported
    // (in case I reported an incident outside my area)
    try {
      const response = await incidentsApi.getAll(eventId);
      const allIncidents = response.data.incidents || [];
      for (const incident of allIncidents) {
        if (incident.reportedBy?.marshalId === currentMarshalId.value) {
          incidentMap.set(incident.incidentId, incident);
        }
      }
    } catch (err) {
      // Non-leads may not have access to getAll, which is fine
      console.debug('Could not load all incidents:', err);
    }

    myIncidents.value = Array.from(incidentMap.values()).sort((a, b) => {
      // Sort by date, most recent first
      return new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt);
    });

    sectionLastLoadedAt.value.incidents = Date.now();
    console.log('My incidents loaded:', myIncidents.value.length);
  } catch (error) {
    console.error('Failed to load incidents:', error);
  } finally {
    incidentsLoading.value = false;
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

// Legacy function - kept for backwards compatibility but now just loads new contacts
const loadAreaContacts = async () => {
  // Area contacts from areas are now handled by the new contacts system
  // This function is kept for any remaining code that calls it
  areaContactsRaw.value = [];
};

const loadChecklist = async (silent = false) => {
  if (!currentMarshalId.value) {
    console.warn('No marshal ID, skipping checklist load');
    return;
  }

  // Only show loading state for initial load, not refreshes
  if (!silent) {
    checklistLoading.value = true;
  }
  checklistError.value = null;

  try {
    const eventId = route.params.eventId;
    console.log('Fetching checklist for marshal:', currentMarshalId.value);

    // Fetch personal checklist items
    const marshalResponse = await checklistApi.getMarshalChecklist(eventId, currentMarshalId.value);
    let allItems = marshalResponse.data || [];

    // For area leads, also fetch checklist items for their areas
    if (isAreaLead.value && areaLeadAreaIds.value.length > 0) {
      console.log('Fetching area checklists for areas:', areaLeadAreaIds.value);
      const areaPromises = areaLeadAreaIds.value.map(areaId =>
        checklistApi.getAreaChecklist(eventId, areaId).catch(err => {
          console.warn(`Failed to fetch checklist for area ${areaId}:`, err);
          return { data: [] };
        })
      );
      const areaResponses = await Promise.all(areaPromises);

      // Merge and deduplicate items
      const itemMap = new Map();
      for (const item of allItems) {
        const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
        itemMap.set(key, item);
      }
      for (const response of areaResponses) {
        for (const item of (response.data || [])) {
          const key = `${item.itemId}_${item.completionContextType}_${item.completionContextId}`;
          if (!itemMap.has(key)) {
            itemMap.set(key, item);
          }
        }
      }
      allItems = Array.from(itemMap.values());
      console.log('Merged checklist items:', allItems.length);
    }

    checklistItems.value = allItems;
    sectionLastLoadedAt.value.checklist = Date.now();
    console.log('Checklist loaded:', checklistItems.value.length, 'items');
  } catch (error) {
    console.error('Failed to load checklist:', error.response?.status, error.response?.data);
    checklistError.value = error.response?.data?.message || 'Failed to load checklist';
  } finally {
    if (!silent) {
      checklistLoading.value = false;
    }
  }
};

const handleToggleChecklist = async (item) => {
  if (savingChecklist.value) return;

  savingChecklist.value = true;
  const eventId = route.params.eventId;

  // For Personal items, use the owner's marshal ID; otherwise use current marshal
  // Also send actorMarshalId to identify who is actually completing the task
  const actionData = {
    marshalId: item.contextOwnerMarshalId || currentMarshalId.value,
    contextType: item.completionContextType,
    contextId: item.completionContextId,
    actorMarshalId: currentMarshalId.value,
  };

  try {
    if (item.isCompleted) {
      // Uncomplete
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checklist_uncomplete', {
          eventId,
          itemId: item.itemId,
          data: actionData
        });
        // Optimistic update
        item.isCompleted = false;
        item.completedAt = null;
        item.completedByActorName = null;
        await updatePendingCount();
      } else {
        await checklistApi.uncomplete(eventId, item.itemId, actionData);
        await loadChecklist(true); // Silent refresh to preserve UI state
      }
    } else {
      // Complete
      if (getOfflineMode()) {
        // Queue for offline sync
        await queueOfflineAction('checklist_complete', {
          eventId,
          itemId: item.itemId,
          data: actionData
        });
        // Optimistic update
        item.isCompleted = true;
        item.completedAt = new Date().toISOString();
        item.completedByActorName = currentPerson.value?.name || 'You';
        await updatePendingCount();
      } else {
        await checklistApi.complete(eventId, item.itemId, actionData);
        await loadChecklist(true); // Silent refresh to preserve UI state
      }
    }

    // Update cached checklist (convert to plain objects for IndexedDB)
    await updateCachedField(eventId, 'checklist', JSON.parse(JSON.stringify(checklistItems.value)));
  } catch (error) {
    console.error('Failed to toggle checklist item:', error);

    // If offline, queue the action
    if (getOfflineMode() || !error.response) {
      const actionType = item.isCompleted ? 'checklist_uncomplete' : 'checklist_complete';
      await queueOfflineAction(actionType, {
        eventId,
        itemId: item.itemId,
        data: actionData
      });
      // Optimistic update
      item.isCompleted = !item.isCompleted;
      await updatePendingCount();
    } else {
      // Show error temporarily
      checklistError.value = error.response?.data?.message || 'Failed to update checklist';
      setTimeout(() => {
        checklistError.value = null;
      }, 3000);
    }
  } finally {
    savingChecklist.value = false;
  }
};

const getContextName = (item) => {
  if (!item.completionContextType || !item.completionContextId) {
    return null;
  }

  if (item.completionContextType === 'Checkpoint') {
    const location = allLocations.value.find(l => l.id === item.completionContextId);
    if (!location) return null;
    const desc = location.description ? ` - ${location.description}` : '';
    return `At ${termsLower.value.checkpoint} ${location.name}${desc}`;
  }

  if (item.completionContextType === 'Area') {
    const area = areas.value.find(a => a.id === item.completionContextId);
    if (!area) return null;
    return `At ${termsLower.value.area} ${area.name}`;
  }

  if (item.completionContextType === 'Personal') {
    return 'Personal item';
  }

  return null;
};


/**
 * Unified check-in/check-out toggle handler
 * @param {Object} assign - The assignment object
 * @param {boolean} tryGps - Whether to attempt GPS (true for self, false for checking in others)
 * @param {string} locationId - Optional location ID for updating location assignments
 */
const handleCheckInToggle = async (assign, tryGps = true, locationId = null) => {
  checkingIn.value = assign.id;
  checkingInAssignment.value = assign.id;
  checkInError.value = null;

  const eventId = route.params.eventId;
  const isCurrentlyCheckedIn = assign.effectiveIsCheckedIn || assign.isCheckedIn;
  const action = isCurrentlyCheckedIn ? 'check-out' : 'check-in';
  const newIsCheckedIn = !isCurrentlyCheckedIn;

  // Try to get GPS coordinates if requested (for self check-in, not check-out)
  let latitude = null;
  let longitude = null;
  let method = 'Manual';

  if (tryGps && !isCurrentlyCheckedIn && 'geolocation' in navigator) {
    try {
      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 5000, // Shorter timeout - fall back to manual quickly
        });
      });
      latitude = position.coords.latitude;
      longitude = position.coords.longitude;
      method = 'GPS';
    } catch (gpsError) {
      // GPS failed, fall back to manual silently
      console.log('GPS unavailable, using manual check-in:', gpsError.message);
    }
  }

  const gpsData = { latitude, longitude, action };

  try {
    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checkin_toggle', {
        eventId,
        assignmentId: assign.id,
        gpsData,
      });

      // Optimistic update for own assignments
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: newIsCheckedIn,
          checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
          checkInMethod: newIsCheckedIn ? `${method} (pending)` : null,
          checkInLatitude: latitude,
          checkInLongitude: longitude,
        };
      }

      // Also update in location assignments if checking in another marshal
      if (locationId) {
        const location = allLocations.value.find(l => l.id === locationId);
        if (location?.assignments) {
          const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
          if (locAssign) {
            locAssign.isCheckedIn = newIsCheckedIn;
            locAssign.checkInTime = newIsCheckedIn ? new Date().toISOString() : null;
          }
        }
      }

      await updatePendingCount();
      await updateCachedField(eventId, 'locations', allLocations.value);
    } else {
      const response = await checkInApi.toggleCheckIn(eventId, assign.id, gpsData);

      // Update the assignment in the list
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = response.data;
      }

      // Also update in location assignments if checking in another marshal
      if (locationId) {
        const location = allLocations.value.find(l => l.id === locationId);
        if (location?.assignments) {
          const locAssign = location.assignments.find(a => a.id === assign.id || a.marshalId === assign.marshalId);
          if (locAssign) {
            Object.assign(locAssign, response.data);
          }
        }
      }
    }

    // Refresh area lead dashboard if applicable
    if (locationId && areaLeadRef.value?.loadDashboard) {
      await areaLeadRef.value.loadDashboard();
    }
    areaLeadMarshalDataVersion.value++;
  } catch (error) {
    // Check if it's a network error - queue for offline
    if (getOfflineMode() || !error.response) {
      await queueOfflineAction('checkin_toggle', {
        eventId,
        assignmentId: assign.id,
        gpsData,
      });

      // Optimistic update
      const index = assignments.value.findIndex(a => a.id === assign.id);
      if (index !== -1) {
        assignments.value[index] = {
          ...assignments.value[index],
          isCheckedIn: newIsCheckedIn,
          checkInTime: newIsCheckedIn ? new Date().toISOString() : null,
          checkInMethod: newIsCheckedIn ? 'Manual (pending)' : null,
        };
      }
      await updatePendingCount();
    } else if (error.response?.data?.message) {
      checkInError.value = error.response.data.message;
    } else if (error.message) {
      checkInError.value = error.message;
    } else {
      checkInError.value = `Failed to ${action}. Please try again.`;
    }
  } finally {
    checkingIn.value = null;
    checkingInAssignment.value = null;
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

// Message modal helper
const showMessage = (title, message) => {
  messageModalTitle.value = title;
  messageModalMessage.value = message;
  showMessageModal.value = true;
};

const handleMessageModalClose = () => {
  showMessageModal.value = false;
};

// Incident reporting
const handleReportIncident = async (incidentData) => {
  reportingIncident.value = true;
  try {
    await incidentsApi.create(eventId.value, incidentData);
    showReportIncident.value = false;
    // Reload incidents to show the new one
    loadMyIncidents();
    showMessage('Incident Reported', 'An administrator will review it shortly.');
  } catch (error) {
    console.error('Failed to report incident:', error);
    showMessage('Error', error.response?.data?.message || 'Failed to report incident. Please try again.');
  } finally {
    reportingIncident.value = false;
  }
};

// Incident detail handlers
const openIncidentDetail = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
};

const handleIncidentStatusChange = async ({ incidentId, status }) => {
  try {
    await incidentsApi.updateStatus(eventId.value, incidentId, { status });
    // Update local state
    const incident = myIncidents.value.find(i => i.incidentId === incidentId);
    if (incident) {
      incident.status = status;
    }
    if (selectedIncident.value?.incidentId === incidentId) {
      selectedIncident.value.status = status;
    }
  } catch (error) {
    console.error('Failed to update incident status:', error);
    showMessage('Error', 'Failed to update status. Please try again.');
  }
};

const openAddIncidentNoteModal = () => {
  incidentNoteText.value = '';
  showAddIncidentNoteModal.value = true;
};

const closeAddIncidentNoteModal = () => {
  showAddIncidentNoteModal.value = false;
  incidentNoteText.value = '';
};

const submitIncidentNote = async () => {
  if (!incidentNoteText.value.trim() || !selectedIncident.value) return;

  submittingIncidentNote.value = true;
  try {
    await incidentsApi.addNote(eventId.value, selectedIncident.value.incidentId, incidentNoteText.value.trim());
    // Close the modal
    closeAddIncidentNoteModal();
    // Reload to get the updated incident with new note
    loadMyIncidents();
    // Also reload the selected incident
    const response = await incidentsApi.get(eventId.value, selectedIncident.value.incidentId);
    selectedIncident.value = response.data;
  } catch (error) {
    console.error('Failed to add note:', error);
    showMessage('Error', 'Failed to add note. Please try again.');
  } finally {
    submittingIncidentNote.value = false;
  }
};

// Dynamic checkpoint location update methods
const openLocationUpdateModal = (assign) => {
  updatingLocationFor.value = assign;
  locationUpdateError.value = null;
  locationUpdateSuccess.value = null;
  showLocationUpdateModal.value = true;
};

const closeLocationUpdateModal = () => {
  showLocationUpdateModal.value = false;
  updatingLocationFor.value = null;
  locationUpdateError.value = null;
  locationUpdateSuccess.value = null;
  selectingLocationOnMap.value = false;
};

// State for selecting location on map
const selectingLocationOnMap = ref(false);

const startMapLocationSelect = () => {
  selectingLocationOnMap.value = true;
  showLocationUpdateModal.value = false;
  // Expand the map section so user can see it
  expandedSection.value = 'map';
};

const handleMapClick = (coords) => {
  // If in explicit selection mode, update immediately
  if (selectingLocationOnMap.value && updatingLocationFor.value) {
    updateDynamicCheckpointLocation(
      updatingLocationFor.value.locationId,
      coords.lat,
      coords.lng,
      'manual'
    );
    selectingLocationOnMap.value = false;
    updatingLocationFor.value = null;
    return;
  }

  // If user has a dynamic assignment and clicks on the map, offer to update
  if (hasDynamicAssignment.value && firstDynamicAssignment.value) {
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
    };
    showConfirmModal.value = true;
  }
};

const handleLocationClick = (location) => {
  // If in selection mode, don't do anything special - map click will handle it
  if (selectingLocationOnMap.value) return;

  // If user has a dynamic assignment, offer to copy this location
  if (hasDynamicAssignment.value && firstDynamicAssignment.value) {
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
    };
    showConfirmModal.value = true;
  }
};

// Unified function to update dynamic checkpoint location
const updateDynamicCheckpointLocation = async (locationId, lat, lng, sourceType, sourceCheckpointId = null) => {
  updatingLocation.value = true;

  try {
    const eventId = route.params.eventId;

    const payload = {
      latitude: lat,
      longitude: lng,
      sourceType,
    };
    if (sourceCheckpointId) {
      payload.sourceCheckpointId = sourceCheckpointId;
    }

    const response = await locationsApi.updatePosition(eventId, locationId, payload);

    if (response.data.success) {
      // Stop auto-update when manually setting location
      if (autoUpdateEnabled.value) {
        stopAutoUpdate();
      }

      // Update local state immediately
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
    }
  } catch (error) {
    console.error('Failed to update location:', error);
    if (error.response?.status === 403) {
      alert('You do not have permission to update this location.');
    } else {
      alert('Failed to update location. Please try again.');
    }
  } finally {
    updatingLocation.value = false;
  }
};

const cancelMapLocationSelect = () => {
  selectingLocationOnMap.value = false;
  showLocationUpdateModal.value = true;
};

const updateLocationWithGps = async () => {
  if (!updatingLocationFor.value) return;

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    let latitude, longitude;

    // Use cached location if available and recent (within last 30 seconds)
    if (userLocation.value && locationLastUpdated.value) {
      const ageMs = Date.now() - locationLastUpdated.value;
      if (ageMs < 30000) {
        latitude = userLocation.value.lat;
        longitude = userLocation.value.lng;
      }
    }

    // Fall back to getCurrentPosition if no cached location
    if (!latitude || !longitude) {
      if (!('geolocation' in navigator)) {
        throw new Error('Geolocation is not supported by your browser');
      }

      const position = await new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(resolve, reject, {
          enableHighAccuracy: true,
          timeout: 15000,
          maximumAge: 30000, // Accept cached position up to 30 seconds old
        });
      });

      latitude = position.coords.latitude;
      longitude = position.coords.longitude;
    }

    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude,
      longitude,
      sourceType: 'gps',
    });

    if (response.data.success) {
      // Note: GPS updates don't disable auto-update (only manual selections do)
      locationUpdateSuccess.value = 'Location updated successfully!';
      // Update local state immediately
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to update location with GPS:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else if (error.code === 1) {
      locationUpdateError.value = 'Location access denied. Please enable location permissions.';
    } else if (error.code === 2) {
      locationUpdateError.value = 'Unable to determine your location. Please try again.';
    } else if (error.code === 3 || error.message?.includes('Timeout')) {
      locationUpdateError.value = 'GPS timed out. Please ensure location services are enabled and try again.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocationFromCheckpoint = async (sourceCheckpointId) => {
  if (!updatingLocationFor.value) return;

  const sourceLocation = allLocations.value.find(l => l.id === sourceCheckpointId);
  if (!sourceLocation) {
    locationUpdateError.value = 'Source checkpoint not found.';
    return;
  }

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude: sourceLocation.latitude,
      longitude: sourceLocation.longitude,
      sourceType: 'checkpoint',
      sourceCheckpointId: sourceCheckpointId,
    });

    if (response.data.success) {
      // Stop auto-update when manually copying from another checkpoint
      if (autoUpdateEnabled.value) {
        stopAutoUpdate();
      }
      locationUpdateSuccess.value = `Location copied from ${sourceLocation.name}!`;
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to copy location from checkpoint:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocationManually = async (lat, lng) => {
  if (!updatingLocationFor.value) return;

  updatingLocation.value = true;
  locationUpdateError.value = null;

  try {
    const eventId = route.params.eventId;
    const locationId = updatingLocationFor.value.locationId;

    const response = await locationsApi.updatePosition(eventId, locationId, {
      latitude: lat,
      longitude: lng,
      sourceType: 'manual',
    });

    if (response.data.success) {
      locationUpdateSuccess.value = 'Location updated successfully!';
      updateLocalCheckpointPosition(locationId, response.data.latitude, response.data.longitude, response.data.lastLocationUpdate);
      setTimeout(() => closeLocationUpdateModal(), 1500);
    }
  } catch (error) {
    console.error('Failed to update location manually:', error);
    if (error.response?.status === 403) {
      locationUpdateError.value = 'You do not have permission to update this location.';
    } else {
      locationUpdateError.value = 'Failed to update location. Please try again.';
    }
  } finally {
    updatingLocation.value = false;
  }
};

const updateLocalCheckpointPosition = (locationId, lat, lng, lastUpdate) => {
  const location = allLocations.value.find(l => l.id === locationId);
  if (location) {
    location.latitude = lat;
    location.longitude = lng;
    location.lastLocationUpdate = lastUpdate;
  }
};

// Auto-update location every 60 seconds
const toggleAutoUpdate = (assign) => {
  if (autoUpdateEnabled.value) {
    stopAutoUpdate();
  } else {
    startAutoUpdate(assign);
  }
};

const startAutoUpdate = (assign) => {
  autoUpdateEnabled.value = true;
  // Immediately update once
  performAutoUpdate(assign);
  // Then every 60 seconds
  autoUpdateInterval = setInterval(() => {
    performAutoUpdate(assign);
  }, 60000);
};

const stopAutoUpdate = () => {
  autoUpdateEnabled.value = false;
  if (autoUpdateInterval) {
    clearInterval(autoUpdateInterval);
    autoUpdateInterval = null;
  }
};

const performAutoUpdate = async (assign) => {
  if (!assign || !('geolocation' in navigator)) return;

  try {
    const position = await new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(resolve, reject, {
        enableHighAccuracy: true,
        timeout: 15000,
      });
    });

    const eventId = route.params.eventId;
    const locationId = assign.locationId;

    await locationsApi.updatePosition(eventId, locationId, {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      sourceType: 'gps',
    });

    updateLocalCheckpointPosition(locationId, position.coords.latitude, position.coords.longitude, new Date().toISOString());
  } catch (error) {
    console.warn('Auto-update location failed:', error);
    // Don't stop auto-update on error, just log it
  }
};

// Poll for dynamic checkpoint position updates
const startDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) return;

  // Check if there are any dynamic checkpoints to poll
  const hasDynamicCheckpoints = allLocations.value.some(l => l.isDynamic || l.IsDynamic);
  if (!hasDynamicCheckpoints) return;

  dynamicCheckpointPollInterval = setInterval(async () => {
    try {
      const eventId = route.params.eventId;
      const response = await locationsApi.getDynamicCheckpoints(eventId);
      if (response.data && Array.isArray(response.data)) {
        // Update local positions for dynamic checkpoints
        for (const dynamicCp of response.data) {
          const location = allLocations.value.find(l => l.id === dynamicCp.checkpointId);
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
  }, 60000); // Poll every 60 seconds
};

const stopDynamicCheckpointPolling = () => {
  if (dynamicCheckpointPollInterval) {
    clearInterval(dynamicCheckpointPollInterval);
    dynamicCheckpointPollInterval = null;
  }
};

// Check if a location is a dynamic checkpoint
const isDynamicCheckpoint = (location) => {
  return location?.isDynamic === true || location?.IsDynamic === true;
};

const handleLogout = async () => {
  try {
    await authApi.logout();
  } catch (error) {
    // Ignore logout errors
  }

  const eventId = route.params.eventId;
  localStorage.removeItem('sessionToken');
  localStorage.removeItem(`marshal_${eventId}`);
  localStorage.removeItem(`marshalCode_${eventId}`);

  isAuthenticated.value = false;
  currentPerson.value = null;
  currentMarshalId.value = null;
  assignments.value = [];
  checklistItems.value = [];
  areaContactsRaw.value = [];

  // Redirect to home
  router.push('/');
};

const confirmLogout = () => {
  confirmModalTitle.value = 'Logout';
  confirmModalMessage.value = `Are you sure you want to logout${currentPerson.value?.name ? `, ${currentPerson.value.name}` : ''}?`;
  confirmModalCallback.value = handleLogout;
  showConfirmModal.value = true;
};

const formatTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
};

const formatTimeRange = (startTime, endTime) => {
  if (!startTime && !endTime) return '';
  const start = startTime ? formatTime(startTime) : '?';
  const end = endTime ? formatTime(endTime) : '?';
  return `${start} - ${end}`;
};

const formatEventDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString('en-US', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
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

const formatRoleName = (role) => {
  if (!role) return '';
  // Convert PascalCase/camelCase to words
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

// Check if a role is an emergency contact (for red styling)
const isEmergencyRole = (role) => {
  return role === 'EmergencyContact';
};

// Generate checkpoint icon SVG based on resolved style
const getCheckpointIconSvg = (location) => {
  if (!location) return '';

  const resolvedType = location.resolvedStyleType || location.ResolvedStyleType;
  const resolvedBgColor = location.resolvedStyleBackgroundColor || location.ResolvedStyleBackgroundColor || location.resolvedStyleColor || location.ResolvedStyleColor;
  const resolvedBorderColor = location.resolvedStyleBorderColor || location.ResolvedStyleBorderColor;
  const resolvedIconColor = location.resolvedStyleIconColor || location.ResolvedStyleIconColor;
  const resolvedShape = location.resolvedStyleBackgroundShape || location.ResolvedStyleBackgroundShape;

  // Check if there's any resolved styling - type, colors, or shape
  const hasResolvedStyle = (resolvedType && resolvedType !== 'default')
    || resolvedBgColor
    || resolvedBorderColor
    || resolvedIconColor
    || (resolvedShape && resolvedShape !== 'circle');

  if (hasResolvedStyle) {
    // Use resolved style from hierarchy
    return generateCheckpointSvg({
      type: resolvedType || 'circle',
      backgroundShape: resolvedShape || 'circle',
      backgroundColor: resolvedBgColor || '#667eea',
      borderColor: resolvedBorderColor || '#ffffff',
      iconColor: resolvedIconColor || '#ffffff',
      size: '75',
      outputSize: 20,
    });
  }

  // Default: use neutral colored circle (not status-based)
  return `<svg width="20" height="20" viewBox="0 0 20 20" xmlns="http://www.w3.org/2000/svg">
    <circle cx="10" cy="10" r="8" fill="#667eea" stroke="#fff" stroke-width="1.5"/>
  </svg>`;
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

// Heartbeat to update LastAccessedDate every 5 minutes
let heartbeatInterval = null;

const startHeartbeat = () => {
  if (heartbeatInterval) return;

  heartbeatInterval = setInterval(async () => {
    if (userClaims.value?.marshalId) {
      try {
        await authApi.getMe(route.params.eventId);
      } catch (err) {
        // Silently ignore heartbeat errors
        console.debug('Heartbeat failed:', err);
      }
    }
  }, 5 * 60 * 1000); // 5 minutes
};

const stopHeartbeat = () => {
  if (heartbeatInterval) {
    clearInterval(heartbeatInterval);
    heartbeatInterval = null;
  }
};

onUnmounted(() => {
  // Note: stopLocationTracking is called automatically by useLocationTracking composable
  stopHeartbeat();
  stopAutoUpdate();
  stopDynamicCheckpointPolling();
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

.btn-logout-icon {
  position: absolute;
  top: 35%;
  transform: translateY(-50%);
  background: var(--glass-bg);
  border: 1px solid var(--glass-border);
  border-radius: 50%;
  width: 54px;
  height: 54px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  z-index: 2;
}

.btn-logout-icon:hover {
  background: var(--glass-bg-strong);
}

.btn-logout-icon svg {
  width: 26px;
  height: 26px;
}

.btn-logout-left {
  left: 1rem;
}

.btn-logout-right {
  right: 1rem;
}

.container {
  padding: 2rem;
  max-width: 800px;
  margin: 0 auto;
}

.selection-card,
.welcome-card,
.assignment-card,
.checklist-card,
.contacts-card,
.map-card {
  background: var(--card-bg);
  border-radius: 12px;
  padding: 2rem;
  box-shadow: var(--shadow-lg);
  margin-bottom: 2rem;
}

.error-card {
  border: 2px solid var(--emergency-bg);
}

.selection-card h2,
.welcome-card h2,
.assignment-card h3,
.checklist-card h3,
.contacts-card h3,
.map-card h3 {
  margin: 0 0 1rem 0;
  color: var(--text-dark);
}

/* Contact list styles */
.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

/* Incidents list styles */
.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

/* Contact link styles (used in area lead marshal section) */
.contact-link {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.5rem 0.75rem;
  background: var(--brand-primary);
  color: white;
  text-decoration: none;
  border-radius: 6px;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background 0.2s;
}

.contact-link:hover {
  background: var(--brand-primary-hover);
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

.welcome-name {
  font-weight: 600;
  color: var(--text-dark);
  font-size: 1rem;
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

.location-info {
  margin-bottom: 1.5rem;
  padding: 1rem;
  background: var(--bg-muted);
  border-radius: 8px;
}

.location-info strong {
  font-size: 1.125rem;
  color: var(--text-dark);
}

.location-info p {
  margin: 0.5rem 0 0 0;
  color: var(--text-secondary);
}

.time-range {
  font-size: 0.9rem;
  color: var(--brand-primary);
  font-weight: 500;
}

.no-assignment {
  color: var(--text-secondary);
  font-style: italic;
}

.checked-in-status {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1.5rem;
  background: var(--checked-in-bg);
  border: 2px solid var(--success-light);
  border-radius: 8px;
}

.check-icon {
  font-size: 2rem;
  color: var(--checked-in-text);
}

.checked-in-status strong {
  color: var(--checked-in-text);
  font-size: 1.125rem;
}

.check-time,
.check-method {
  margin: 0.25rem 0 0 0;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.check-in-actions {
  display: flex;
  flex-direction: row;
  gap: 0.75rem;
  margin-top: 0.75rem;
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

.empty-state {
  text-align: center;
  padding: 1.5rem;
  color: var(--text-secondary);
  font-style: italic;
}

/* Map card */
.map-card {
  height: auto;
}

.map-card h3 {
  display: flex;
  align-items: center;
  gap: 1rem;
}


/* Accordion styles */
.accordion {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.accordion-section {
  background: var(--card-bg);
  border-radius: 12px;
  box-shadow: var(--shadow-sm);
  overflow: hidden;
  margin-bottom: 0.5rem;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
  overflow: hidden;
}

.accordion-header:hover {
  background: var(--bg-secondary);
}

.accordion-header.active {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.section-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--brand-primary);
}

.section-icon :deep(svg) {
  width: 18px;
  height: 18px;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.accordion-content {
  padding: 1rem 1.5rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* Nested checkpoint accordion styles */
.assignments-accordion-content {
  padding: 0.5rem;
}

/* Checkpoint marshals list - kept for any remaining inline uses */
.marshals-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
  font-weight: 500;
}

.btn-sm {
  padding: 0.35rem 0.6rem;
  font-size: 0.8rem;
}

/* Modal styles */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: var(--modal-overlay);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: var(--z-modal, 3000);
  padding: 1rem;
}

.modal-content {
  background: var(--modal-bg);
  border-radius: 12px;
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: var(--shadow-xl);
}

/* Check-in Reminder Modal */
.check-in-reminder-modal .modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.check-in-reminder-modal .modal-header.warning {
  background: var(--warning-bg);
  border-bottom: 1px solid var(--warning);
}

.check-in-reminder-modal .modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--warning-text);
}

.check-in-reminder-modal .modal-body {
  text-align: center;
  padding: 2rem 1.5rem;
}

.check-in-reminder-modal .reminder-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.check-in-reminder-modal .reminder-message {
  font-size: 1.1rem;
  color: var(--text-dark);
  margin-bottom: 0.75rem;
}

.check-in-reminder-modal .reminder-hint {
  font-size: 0.9rem;
  color: var(--text-secondary);
  margin: 0;
}

.check-in-reminder-modal .modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

.location-update-modal .modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.location-update-modal .modal-header h3 {
  margin: 0;
  font-size: 1.1rem;
  color: var(--text-dark);
}

.modal-close {
  background: none;
  border: none;
  font-size: 1.5rem;
  cursor: pointer;
  color: var(--text-secondary);
  padding: 0;
  line-height: 1;
}

.modal-close:hover {
  color: var(--text-dark);
}

.modal-body {
  padding: 1.5rem;
}

.modal-description {
  margin: 0 0 1.5rem 0;
  color: var(--text-darker);
}

.update-option {
  margin-bottom: 1.5rem;
}

.option-label {
  display: block;
  font-size: 0.9rem;
  color: var(--text-darker);
  margin-bottom: 0.5rem;
}

.btn-full {
  width: 100%;
  justify-content: center;
}

.checkpoint-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.btn-checkpoint-source {
  text-align: left;
  padding: 0.75rem 1rem;
}

.no-checkpoints {
  font-size: 0.85rem;
  color: var(--text-light);
  font-style: italic;
}

.success-message {
  padding: 1rem;
  background: var(--success-bg-light);
  color: var(--success-dark);
  border-radius: 8px;
  text-align: center;
  font-weight: 500;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--border-light);
}

/* New modal elements */
.last-update-info {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
  padding: 0.5rem;
  background: var(--bg-muted);
  border-radius: 4px;
}

.gps-status {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-bottom: 1rem;
}

.gps-status.active {
  color: var(--success-dark);
}

.gps-status .status-icon {
  width: 16px;
  height: 16px;
}

.auto-update-option {
  background: var(--bg-secondary);
  padding: 1rem;
  border-radius: 8px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: 500;
}

.checkbox-label input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.option-hint {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin: 0.5rem 0 0 0;
  padding-left: 27px;
}

/* Wide screen layout - multi-column checkpoints */
@media (min-width: 1200px) {
  .container {
    max-width: 1400px;
  }

  .checkpoint-accordion {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 0.75rem;
  }

  /* Expanded checkpoint spans full width */
  .checkpoint-accordion-section:has(.checkpoint-accordion-header.active) {
    grid-column: 1 / -1;
  }
}

/* Extra wide screens - 3 columns */
@media (min-width: 1600px) {
  .container {
    max-width: 1800px;
  }

  .checkpoint-accordion {
    grid-template-columns: repeat(3, 1fr);
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

  .selection-card,
  .welcome-card,
  .assignment-card,
  .checklist-card,
  .contacts-card,
  .map-card {
    padding: 1.5rem;
  }

  .map-card :deep(.map-container) {
    height: 300px;
  }

  .contact-link {
    flex: 1;
    justify-content: center;
  }

  /* Accordion mobile adjustments */
  .accordion-header {
    padding: 1rem;
  }

  .accordion-content {
    padding: 1rem;
  }

  .assignments-accordion-content {
    padding: 0.25rem;
  }

  .checkpoint-accordion-header {
    padding: 0.75rem 1rem;
  }

  .checkpoint-accordion-content {
    padding: 0.75rem 1rem;
  }

  .checkpoint-area-contacts {
    padding: 0.5rem;
  }

  .assignment-item {
    padding: 0.75rem;
  }

  .check-in-actions {
    flex-direction: row;
    gap: 0.5rem;
  }

  .check-in-actions .btn {
    padding: 0.75rem 1rem;
    font-size: 0.9rem;
    flex: 1;
  }

  /* Dynamic location mobile adjustments */
  .dynamic-location-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .dynamic-location-actions {
    width: 100%;
  }

  .btn-update-location {
    flex: 1;
  }
}

</style>
