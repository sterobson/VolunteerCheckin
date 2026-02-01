<template>
  <div class="admin-event-manage" :style="{ colorScheme }">
    <DemoBanner v-if="event?.isSampleEvent" :expires-at="event.expiresAt" />
    <header class="header">
      <div class="header-left">
        <button @click="goBack" class="btn-back" title="Back to sessions">
          <AppLogo :size="48" animate />
        </button>
        <div v-if="event">
          <h1>{{ event.name }}</h1>
          <p class="event-date">{{ formatEventDate(event.eventDate) }}</p>
        </div>
      </div>
      <div class="header-actions">
        <button v-if="canSwitchToMarshal" @click="switchToMarshalMode" class="btn btn-marshal-mode">
          Switch to Marshal
        </button>
      </div>
      <ThemeToggle class="theme-toggle-fixed" />
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
          :layers="layerManagement.layers.value"
          :route="event?.route || []"
          :route-color="event?.routeColor || ''"
          :route-style="event?.routeStyle || ''"
          :route-weight="event?.routeWeight"
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
          @fullscreen="handleCourseFullscreen"
          @update:route-settings="handleRouteSettingsChange"
          @add-layer="handleAddLayer"
          @select-layer="handleSelectLayer"
        />

        <CheckpointsList
          v-if="activeTab === 'checkpoints'"
          :locations="locationStatuses"
          :areas="areas"
          :layers="layerManagement.layers.value"
          @add-checkpoint-manually="handleAddCheckpointManually"
          @import-checkpoints="showImportLocations = true"
          @select-location="selectLocation"
        />

        <AreasList
          v-if="activeTab === 'areas'"
          :areas="areas"
          :checkpoints="locationStatuses"
          :contacts="contacts"
          :event-people-term="event?.peopleTerm || 'Marshals'"
          :event-checkpoint-term="event?.checkpointTerm || 'Checkpoints'"
          @add-area="handleAddArea"
          @select-area="handleSelectArea"
        />

        <MarshalsTab
          v-if="activeTab === 'marshals'"
          ref="marshalsTabRef"
          :marshals="marshals"
          :assignments="allAssignments"
          :locations="locationStatuses"
          @add-marshal="addNewMarshal"
          @import-marshals="showImportMarshals = true"
          @select-marshal="selectMarshal"
          @delete-marshal="handleDeleteMarshalFromGrid"
          @toggle-check-in="toggleCheckIn"
        />

        <ChecklistsTab
          v-if="activeTab === 'checklists'"
          :checklist-items="checklistItems"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          :completion-report="checklistCompletionReport"
          :detailed-report="checklistDetailedReport"
          :is-loading-detailed-report="isLoadingDetailedReport"
          @add-checklist-item="handleAddChecklistItem"
          @select-checklist-item="handleSelectChecklistItem"
          @reorder="handleReorderChecklistItems"
          @load-detailed-report="loadDetailedReport"
        />

        <NotesTab
          v-if="activeTab === 'notes'"
          :notes="notes"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          @add-note="handleAddNote"
          @select-note="handleSelectNote"
          @reorder="handleReorderNotes"
        />

        <IncidentsTab
          v-if="activeTab === 'incidents'"
          :incidents="incidents"
          :areas="areas"
          :loading="incidentsLoading"
          @select-incident="handleSelectIncident"
          @report-incident="handleReportIncident"
        />

        <ContactsTab
          v-if="activeTab === 'contacts'"
          :contacts="contacts"
          :areas="areas"
          :locations="locationStatuses"
          :marshals="marshals"
          :available-roles="contactRoles"
          :role-definitions="roleDefinitions"
          @add-contact="handleAddContact"
          @select-contact="handleSelectContact"
          @reorder="handleReorderContacts"
        />

        <RolesTab
          v-if="activeTab === 'roles'"
          :role-definitions="roleDefinitions"
          @add-role="handleAddRole"
          @select-role="handleSelectRole"
          @reorder="handleReorderRoles"
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
      :layers="layerManagement.layers.value"
      :event-id="route.params.eventId"
      :all-locations="locationStatuses"
      :notes="notes"
      :available-marshals="availableMarshalsForAssignment"
      :all-marshals="marshals"
      :all-checklist-items="checklistItems"
      :incidents="selectedLocationIncidents"
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
      :z-index="showEditArea ? 1200 : (lockedFromMarshal ? 1100 : 1000)"
      :locked-from-marshal="lockedFromMarshal"
      @close="closeEditLocationModal"
      @save="handleUpdateLocation"
      @delete="handleDeleteLocation"
      @move-location="startMoveLocation"
      @remove-assignment="handleRemoveLocationAssignment"
      @open-assign-modal="openCreateMarshalModal"
      @update:isDirty="markFormDirty"
      @select-incident="handleSelectLocationIncident"
      @select-marshal="handleSelectMarshalFromCheckpoint"
      @reorder-notes="handleReorderNotes"
      @reorder-checklists="handleReorderChecklists"
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
      :event-name="event?.name || ''"
      :marshal-id="newlyCreatedMarshalId"
      :marshal-name="newlyCreatedMarshalName"
      :marshal-email="newlyCreatedMarshalEmail"
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
      ref="editMarshalModalRef"
      :show="showEditMarshal"
      :marshal="selectedMarshal"
      :event-id="route.params.eventId"
      :event-name="event?.name"
      :assignments="marshalAssignmentsForEdit"
      :available-locations="availableMarshalLocations"
      :all-locations="locationStatuses"
      :areas="areas"
      :is-editing="!!selectedMarshal"
      :event-notes="notes"
      :all-checklist-items="checklistItems"
      :incidents="selectedMarshalIncidents"
      :all-marshals="marshals"
      :is-dirty="formDirty"
      :validation-errors="marshalValidationErrors"
      :locked-checkpoint-id="lockedCheckpointId"
      :z-index="lockedCheckpointId ? 1100 : 1000"
      :role-definitions="roleDefinitions"
      @close="closeEditMarshalModal"
      @save="handleSaveMarshal"
      @delete="handleDeleteMarshal"
      @remove-assignment="handleRemoveMarshalAssignment"
      @assign-to-location="assignMarshalToLocation"
      @update:isDirty="markFormDirty"
      @select-incident="handleSelectMarshalIncident"
      @select-checkpoint="handleSelectCheckpointFromMarshal"
      @reorder-notes="handleReorderNotes"
      @reorder-checklists="handleReorderChecklists"
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
      ref="editAreaModalRef"
      :show="showEditArea"
      :area="selectedArea"
      :initial-tab="areaInitialTab"
      :checkpoints="locationStatuses"
      :marshals="marshals"
      :areas="areas"
      :route="event?.route || []"
      :route-color="event?.routeColor || ''"
      :route-style="event?.routeStyle || ''"
      :route-weight="event?.routeWeight"
      :is-dirty="formDirty"
      :event-id="route.params.eventId"
      :all-locations="locationStatuses"
      :assignments="allAssignments"
      :notes="notes"
      :contacts="contacts"
      :incidents="selectedAreaIncidents"
      :role-definitions="roleDefinitions"
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
      @create-contact-from-marshal="handleCreateContactFromMarshal"
      @create-new-contact="handleCreateNewContact"
      @edit-contact="handleEditContactFromArea"
      @select-incident="handleSelectAreaIncident"
      @reorder-notes="handleReorderNotes"
      @reorder-checklists="handleReorderChecklists"
      @reorder-contacts="handleReorderContacts"
    />

    <EditLayerModal
      ref="editLayerModalRef"
      :show="layerManagement.showEditLayer.value"
      :layer="layerManagement.selectedLayer.value"
      :is-dirty="layerFormDirty"
      @close="handleCloseLayerModal"
      @save="handleSaveLayer"
      @delete="handleDeleteLayer"
      @update:isDirty="layerFormDirty = $event"
      @upload-gpx="handleLayerGpxUpload"
      @edit-route="handleEditLayerRoute"
    />

    <RouteEditorModal
      :show="showRouteEditor"
      :route="routeEditorRoute"
      :route-color="routeEditorColor"
      :route-weight="routeEditorWeight"
      :locations="routeEditorLocations"
      :highlight-location-ids="routeEditorHighlightIds"
      :areas="areas"
      :layers="routeEditorLayers"
      :focus-on-route="routeEditorFocusOnRoute"
      :center="routeEditorCenter"
      :zoom="routeEditorZoom"
      @save="handleRouteEditorSave"
      @cancel="handleRouteEditorCancel"
    />

    <BatchLayerSelectionModal
      :show="showBatchLayerSelection"
      :z-index="2100"
      :layers="layerManagement.layers.value"
      :checkpoint-count="tempCheckpoints.length"
      @confirm="handleBatchLayerConfirm"
      @cancel="handleBatchLayerCancel"
    />

    <EditChecklistItemModal
      :show="showEditChecklistItem"
      :checklist-item="selectedChecklistItem"
      :initial-tab="checklistItemInitialTab"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :is-dirty="formDirty"
      :next-display-order="nextChecklistDisplayOrder"
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

    <IncidentDetailModal
      :show="showIncidentDetail"
      :incident="selectedIncident"
      :route="event?.route || []"
      :route-color="event?.routeColor || ''"
      :route-style="event?.routeStyle || ''"
      :route-weight="event?.routeWeight"
      :checkpoints="locationStatuses"
      :can-manage="true"
      @close="closeIncidentDetailModal"
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
        <div class="modal-footer-actions">
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

    <ReportIncidentModal
      :show="showReportIncident"
      :event-id="route.params.eventId"
      :checkpoint="null"
      :initial-location="null"
      :checkpoints="locationStatuses"
      :route="event?.route || []"
      :route-color="event?.routeColor || ''"
      :route-style="event?.routeStyle || ''"
      :route-weight="event?.routeWeight"
      @close="showReportIncident = false"
      @submit="handleSubmitIncident"
    />

    <EditContactModal
      :show="showEditContact"
      :contact="selectedContact"
      :initial-tab="contactInitialTab"
      :areas="areas"
      :locations="locationStatuses"
      :marshals="marshals"
      :available-roles="contactRoles"
      :role-definitions="roleDefinitions"
      :is-dirty="formDirty"
      :prefilled-name="prefilledContactName"
      :prefilled-marshal-id="prefilledContactMarshalId"
      :create-from-area-context="!!pendingAreaForContact"
      @close="closeEditContactModal"
      @save="handleSaveContact"
      @delete="handleDeleteContact"
      @update:isDirty="markFormDirty"
    />

    <EditRoleDefinitionModal
      :show="showEditRole"
      :role="selectedRole"
      :initial-tab="roleInitialTab"
      :people="rolePeople"
      :loading-people="loadingRolePeople"
      :is-dirty="formDirty"
      @close="closeEditRoleModal"
      @save="handleSaveRole"
      @delete="handleDeleteRole"
      @update:isDirty="markFormDirty"
      @load-people="handleLoadPeopleForRole"
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
      :layers="layerManagement.layers.value"
      :route="event?.route || []"
      :route-color="event?.routeColor || ''"
      :route-style="event?.routeStyle || ''"
      :route-weight="event?.routeWeight"
      :selected-area-id="selectedAreaId"
      :center="savedMapCenter"
      :zoom="savedMapZoom"
      :highlight-location-id="fullscreenHighlightLocationId"
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
      :is-danger="confirmModalIsDanger"
      :confirm-text="confirmModalConfirmText"
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
import { authApi, checkInApi, eventAdminsApi, eventsApi, locationsApi, marshalsApi, areasApi, checklistApi, notesApi, contactsApi, incidentsApi, roleDefinitionsApi, layersApi, sampleEventsApi, setAuthContext } from '../services/api';
import { setSkipLoadingOverlayForGets } from '../services/loadingOverlay';

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
import { useAdminLayerManagement } from '../composables/useAdminLayerManagement';

// Utilities
import { formatDate as formatEventDate, formatDateForInput } from '../utils/dateFormatters';
import { isValidWhat3Words } from '../utils/validators';
import { calculateDistance, findLayersNearPoint, roundCoordinate, roundRoutePoints, roundPolygonPoints } from '../utils/coordinateUtils';
import { getCheckpointsInPolygon } from '../utils/geometryUtils';
import { cleanOrphanedScopeConfigurations } from '../utils/scopeUtils';
import { denormalizeIncidentsList } from '../utils/denormalize';
import { CHECK_IN_RADIUS_METERS } from '../constants/app';

// Tab Components
import CourseAreasTab from './AdminEventManage/CourseAreasTab.vue';
import MarshalsTab from './AdminEventManage/MarshalsTab.vue';
import ChecklistsTab from './AdminEventManage/ChecklistsTab.vue';
import NotesTab from './AdminEventManage/NotesTab.vue';
import ContactsTab from './AdminEventManage/ContactsTab.vue';
import RolesTab from './AdminEventManage/RolesTab.vue';
import EventDetailsTab from './AdminEventManage/EventDetailsTab.vue';
import SettingsTab from './AdminEventManage/SettingsTab.vue';
import IncidentsTab from './AdminEventManage/IncidentsTab.vue';
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
import IncidentDetailModal from '../components/IncidentDetailModal.vue';
import ReportIncidentModal from '../components/ReportIncidentModal.vue';
import EditContactModal from '../components/event-manage/modals/EditContactModal.vue';
import EditRoleDefinitionModal from '../components/event-manage/modals/EditRoleDefinitionModal.vue';
import InfoModal from '../components/InfoModal.vue';
import ConfirmModal from '../components/ConfirmModal.vue';
import BaseModal from '../components/BaseModal.vue';
import EditLayerModal from '../components/event-manage/modals/EditLayerModal.vue';
import RouteEditorModal from '../components/event-manage/modals/RouteEditorModal.vue';
import BatchLayerSelectionModal from '../components/event-manage/modals/BatchLayerSelectionModal.vue';
import FullscreenMapOverlay from '../components/FullscreenMapOverlay.vue';
import AddLocationModal from '../components/event-manage/modals/AddLocationModal.vue';
import ThemeToggle from '../components/ThemeToggle.vue';
import AppLogo from '../components/AppLogo.vue';
import DemoBanner from '../components/DemoBanner.vue';

const route = useRoute();
const router = useRouter();
const eventsStore = useEventsStore();

// Reactive event ID for composables
const eventId = computed(() => route.params.eventId);

// Use composables
const { activeTab, switchTab } = useTabs('details', ['details', 'course', 'areas', 'checkpoints', 'marshals', 'notes', 'checklists', 'contacts', 'roles', 'settings']);
const {
  showConfirmModal,
  confirmModalTitle,
  confirmModalMessage,
  confirmModalIsDanger,
  confirmModalConfirmText,
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

// Layer management composable
const layerManagement = useAdminLayerManagement(eventId);

// Layers to show in route editor (excludes the layer being edited, shows others in gray)
const routeEditorLayers = computed(() => {
  const selectedLayerId = layerManagement.selectedLayer.value?.id;
  if (!selectedLayerId) return layerManagement.layers.value;
  // Show other layers in gray
  return layerManagement.layers.value
    .filter(layer => layer.id !== selectedLayerId)
    .map(layer => ({
      ...layer,
      routeColor: '#999999', // Gray out non-editing layers
    }));
});

// Locations to show in route editor (all locations - non-associated ones will be grayed out)
const routeEditorLocations = computed(() => {
  return locationStatuses.value;
});

// IDs of checkpoints associated with the layer being edited (for highlighting)
const routeEditorHighlightIds = computed(() => {
  const selectedLayerId = layerManagement.selectedLayer.value?.id;
  if (!selectedLayerId) return [];
  // Get IDs of checkpoints that are on this layer (or all layers if layerIds is null)
  return locationStatuses.value
    .filter(location => {
      const layerIds = location.layerIds || location.LayerIds;
      // null/undefined means all layers, so include it
      if (!layerIds) return true;
      // Otherwise, check if this layer is in the list
      return layerIds.includes(selectedLayerId);
    })
    .map(location => location.id);
});

// Computed tabs with dynamic labels from terminology
// Order: Details, Course, Zones, Locations, Volunteers, Notes, Incidents, Tasks, Contacts
const mainTabs = computed(() => [
  { value: 'details', label: 'Event details', icon: 'details' },
  { value: 'course', label: tabLabels.value.course, icon: 'course' },
  { value: 'areas', label: tabLabels.value.areas, icon: 'area' },
  { value: 'checkpoints', label: tabLabels.value.checkpoints, icon: 'checkpoint' },
  { value: 'marshals', label: tabLabels.value.marshals, icon: 'marshal' },
  { value: 'notes', label: 'Notes', icon: 'notes' },
  { value: 'incidents', label: 'Incidents', icon: 'incidents' },
  { value: 'checklists', label: tabLabels.value.checklists, icon: 'checklist' },
  { value: 'contacts', label: 'Contacts', icon: 'contacts' },
  { value: 'roles', label: 'Roles', icon: 'roles' },
  { value: 'settings', label: 'Settings', icon: 'settings' },
]);

// State
const event = ref(null);
const locations = ref([]);
const locationStatuses = ref([]);

// Track when each tab's data was last loaded (for stale data refresh)
const STALE_DATA_THRESHOLD_MS = 60000; // 60 seconds
const dataLastLoadedAt = ref({
  marshals: 0,
  areas: 0,
  checkpoints: 0,
  checklists: 0,
  notes: 0,
  incidents: 0,
  contacts: 0,
  roles: 0,
});
const selectedLocation = ref(null);
const lockedFromMarshal = ref(false);
const showAddLocation = ref(false);
const courseAreasTab = ref(null);
const marshalsTabRef = ref(null);
const showShareLink = ref(false);
const showMarshalCreated = ref(false);
const newlyCreatedMarshalId = ref(null);
const newlyCreatedMarshalName = ref('');
const newlyCreatedMarshalEmail = ref('');

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
const lockedCheckpointId = ref(null);
const editMarshalModalRef = ref(null);
const marshalValidationErrors = ref({});
const editMarshalForm = ref({
  id: '',
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});
const formDirty = ref(false);
const layerFormDirty = ref(false);
const editLayerModalRef = ref(null);
const showRouteEditor = ref(false);
const routeEditorRoute = ref([]);
const routeEditorColor = ref('#3388ff');
const routeEditorWeight = ref(4);
const routeEditorCenter = ref(null);
const routeEditorZoom = ref(null);
const routeEditorFocusOnRoute = ref(false);
const showBatchLayerSelection = ref(false);
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
const areaInitialTab = ref('details');
const editAreaModalRef = ref(null);
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
// Calculate the next display order for new checklist items (add at the end)
const nextChecklistDisplayOrder = computed(() => {
  if (!checklistItems.value || checklistItems.value.length === 0) return 0;
  const maxOrder = Math.max(...checklistItems.value.map(item => item.displayOrder || 0));
  return maxOrder + 1;
});
const checklistCompletionReport = ref(null);
const checklistDetailedReport = ref(null);
const isLoadingDetailedReport = ref(false);
const notes = ref([]);
const selectedNote = ref(null);
const showEditNote = ref(false);
const noteInitialTab = ref('details');
const incidents = ref([]);
const incidentsLoading = ref(false);
const selectedIncident = ref(null);
const showIncidentDetail = ref(false);
const showReportIncident = ref(false);
const showAddIncidentNoteModal = ref(false);
const incidentNoteText = ref('');
const submittingIncidentNote = ref(false);
const contacts = ref([]);
const contactRoles = ref({ builtInRoles: [], customRoles: [] });
const selectedContact = ref(null);
const showEditContact = ref(false);
// Role definitions state
const roleDefinitions = ref([]);
const selectedRole = ref(null);
const showEditRole = ref(false);
const roleInitialTab = ref('details');
const rolePeople = ref([]);
const loadingRolePeople = ref(false);
const contactInitialTab = ref('details');
const pendingAreaForContact = ref(null);
const prefilledContactName = ref('');
const prefilledContactMarshalId = ref(null);
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

// Highlight the location being moved in fullscreen mode
const fullscreenHighlightLocationId = computed(() => {
  if (fullscreenMode.value === 'move-checkpoint' && selectedLocation.value?.id) {
    return selectedLocation.value.id;
  }
  return null;
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
  let areasList = [...areas.value];

  // If in draw mode, exclude the area being redrawn (so old boundary doesn't show)
  if (fullscreenMode.value === 'draw-area' && selectedArea.value?.id) {
    areasList = areasList.filter(a => a.id !== selectedArea.value.id);
  }

  // If drawing or editing an area with a polygon, include it in display
  if (fullscreenMode.value === 'draw-area' && selectedArea.value?.polygon?.length > 0) {
    // Add the area being drawn with its new polygon
    areasList.push({
      ...selectedArea.value,
      id: selectedArea.value.id || 'temp-area',
    });
  }

  return areasList;
});

// Incidents for selected marshal - includes incidents they reported OR at their assigned checkpoints
const selectedMarshalIncidents = computed(() => {
  if (!selectedMarshal.value || !incidents.value.length) return [];

  const marshalId = selectedMarshal.value.id;
  const assignedLocationIds = selectedMarshal.value.assignedLocationIds || [];

  return incidents.value.filter(incident => {
    // Reported by this marshal
    if (incident.reportedBy?.marshalId === marshalId) return true;
    // Or at a checkpoint this marshal is assigned to
    const checkpointId = incident.context?.checkpoint?.checkpointId;
    if (checkpointId && assignedLocationIds.includes(checkpointId)) return true;
    return false;
  }).sort((a, b) => new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt));
});

// Incidents for selected location/checkpoint
const selectedLocationIncidents = computed(() => {
  if (!selectedLocation.value || !incidents.value.length) return [];

  const locationId = selectedLocation.value.id;

  return incidents.value.filter(incident => {
    const checkpointId = incident.context?.checkpoint?.checkpointId;
    return checkpointId === locationId;
  }).sort((a, b) => new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt));
});

// Incidents for selected area
const selectedAreaIncidents = computed(() => {
  if (!selectedArea.value || !incidents.value.length) return [];

  const areaId = selectedArea.value.id;

  // Get checkpoint IDs in this area
  const areaCheckpointIds = locationStatuses.value
    .filter(loc => loc.areaIds && loc.areaIds.includes(areaId))
    .map(loc => loc.id);

  return incidents.value.filter(incident => {
    const checkpointId = incident.context?.checkpoint?.checkpointId;
    return checkpointId && areaCheckpointIds.includes(checkpointId);
  }).sort((a, b) => new Date(b.incidentTime || b.createdAt) - new Date(a.incidentTime || a.createdAt));
});

// Handlers for selecting incidents from modals
const handleSelectMarshalIncident = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
};

const handleSelectLocationIncident = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
};

const handleSelectAreaIncident = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
};

// Methods
const goBack = () => {
  router.push('/myevents');
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

// Handle route settings change - save all at once to avoid race conditions
const handleRouteSettingsChange = async ({ routeColor, routeStyle, routeWeight }) => {
  if (!event.value) return;
  try {
    await eventsStore.updateEvent(route.params.eventId, {
      name: event.value.name,
      description: event.value.description,
      eventDate: event.value.eventDate,
      timeZoneId: event.value.timeZoneId,
      routeColor,
      routeStyle,
      routeWeight,
    });
    event.value = { ...event.value, routeColor, routeStyle, routeWeight };
  } catch (error) {
    console.error('Failed to update route settings:', error);
  }
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

    // Phase 2: Load all other data in parallel (11 independent calls)
    const results = await Promise.allSettled([
      eventsStore.fetchLocations(route.params.eventId),
      eventsStore.fetchEventStatus(route.params.eventId),
      loadEventAdmins(),
      loadMarshals(),
      loadAreas(),
      loadChecklists(),
      loadNotes(),
      loadIncidents(),
      loadContacts(),
      loadRoleDefinitions(),
      layerManagement.loadLayers(),
    ]);

    // Update state from store after parallel fetches
    locations.value = eventsStore.locations;
    locationStatuses.value = eventsStore.eventStatus.locations;

    // Log any failures for debugging
    results.forEach((result, index) => {
      if (result.status === 'rejected') {
        const names = ['locations', 'eventStatus', 'admins', 'marshals', 'areas', 'checklists', 'notes', 'contacts', 'roles', 'layers'];
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
    dataLastLoadedAt.value.marshals = Date.now();
  } catch (error) {
    console.error('Failed to load marshals:', error);
  }
};

const loadAreas = async () => {
  try {
    const response = await areasApi.getByEvent(route.params.eventId);
    areas.value = response.data;
    dataLastLoadedAt.value.areas = Date.now();
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
    dataLastLoadedAt.value.checklists = Date.now();
  } catch (error) {
    console.error('Failed to load checklists:', error);
  }
};

const loadDetailedReport = async () => {
  try {
    isLoadingDetailedReport.value = true;
    const response = await checklistApi.getDetailedReport(route.params.eventId);
    checklistDetailedReport.value = response.data;
  } catch (error) {
    console.error('Failed to load detailed report:', error);
  } finally {
    isLoadingDetailedReport.value = false;
  }
};

const loadNotes = async () => {
  try {
    const response = await notesApi.getByEvent(route.params.eventId);
    notes.value = response.data;
    dataLastLoadedAt.value.notes = Date.now();
  } catch (error) {
    console.error('Failed to load notes:', error);
  }
};

const loadIncidents = async () => {
  incidentsLoading.value = true;
  try {
    const response = await incidentsApi.getAll(route.params.eventId);
    const denormalized = denormalizeIncidentsList(response.data);
    incidents.value = denormalized.incidents || [];
    dataLastLoadedAt.value.incidents = Date.now();
  } catch (error) {
    console.error('Failed to load incidents:', error);
  } finally {
    incidentsLoading.value = false;
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
    dataLastLoadedAt.value.contacts = Date.now();
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
  dataLastLoadedAt.value.checkpoints = Date.now();
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
  areaInitialTab.value = 'details';
  formDirty.value = false;
  showEditArea.value = true;
};

const handleSelectArea = (area) => {
  selectedArea.value = area;
  selectedAreaId.value = area.id;
  areaInitialTab.value = 'details';
  formDirty.value = false;
  showEditArea.value = true;
};

const handleSaveArea = async (formData) => {
  try {
    // Round polygon coordinates to 6 decimal places before saving
    const dataToSave = { ...formData };
    if (dataToSave.polygon) {
      dataToSave.polygon = roundPolygonPoints(dataToSave.polygon);
    }

    let areaId;

    if (selectedArea.value && selectedArea.value.id) {
      // Update existing area (checkpoints will be automatically recalculated on backend)
      areaId = selectedArea.value.id;
      await areasApi.update(route.params.eventId, areaId, dataToSave);

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
      const response = await areasApi.create({
        eventId: route.params.eventId,
        ...dataToSave,
      });
      areaId = response.data.id;
    }

    // Process pending contacts - add the area to each contact's scope
    if (formData.pendingContacts && formData.pendingContacts.length > 0 && areaId) {
      for (const contact of formData.pendingContacts) {
        try {
          // Clone existing scope configurations
          const scopeConfigurations = contact.scopeConfigurations
            ? JSON.parse(JSON.stringify(contact.scopeConfigurations))
            : [];

          // Check if there's already an EveryoneInAreas scope for Area type
          const existingAreaScope = scopeConfigurations.find(
            (config) => config.scope === 'EveryoneInAreas' && config.itemType === 'Area'
          );

          if (existingAreaScope) {
            // Add the area ID if not already present
            if (!existingAreaScope.ids) {
              existingAreaScope.ids = [];
            }
            if (!existingAreaScope.ids.includes(areaId)) {
              existingAreaScope.ids.push(areaId);
            }
          } else {
            // Create new scope configuration for this area
            scopeConfigurations.push({
              scope: 'EveryoneInAreas',
              itemType: 'Area',
              ids: [areaId],
            });
          }

          // Update the contact with the new scope configurations
          await contactsApi.update(route.params.eventId, contact.contactId, {
            ...contact,
            scopeConfigurations,
          });
        } catch (error) {
          console.error('Failed to add area to contact:', contact, error);
          // Continue with other contacts even if one fails
        }
      }
    }

    // Process contacts to remove - remove the area from each contact's scope
    if (formData.contactsToRemove && formData.contactsToRemove.length > 0 && areaId) {
      for (const contactId of formData.contactsToRemove) {
        try {
          // Find the contact to get current scope configurations
          const contact = contacts.value.find(c => c.contactId === contactId);
          if (!contact) continue;

          // Clone existing scope configurations
          const scopeConfigurations = contact.scopeConfigurations
            ? JSON.parse(JSON.stringify(contact.scopeConfigurations))
            : [];

          // Find EveryoneInAreas scope for Area type
          const areaScope = scopeConfigurations.find(
            (config) => config.scope === 'EveryoneInAreas' && config.itemType === 'Area'
          );

          if (areaScope && areaScope.ids) {
            // Remove this area ID from the list
            areaScope.ids = areaScope.ids.filter(id => id !== areaId);

            // If no areas left, remove the entire scope configuration
            const updatedScopes = areaScope.ids.length > 0
              ? scopeConfigurations
              : scopeConfigurations.filter(config =>
                  !(config.scope === 'EveryoneInAreas' && config.itemType === 'Area')
                );

            // Update the contact with the new scope configurations
            await contactsApi.update(route.params.eventId, contactId, {
              ...contact,
              scopeConfigurations: updatedScopes,
            });
          }
        } catch (error) {
          console.error('Failed to remove area from contact:', contactId, error);
          // Continue with other contacts even if one fails
        }
      }
    }

    // Reload contacts to reflect any scope changes
    if ((formData.pendingContacts && formData.pendingContacts.length > 0) ||
        (formData.contactsToRemove && formData.contactsToRemove.length > 0)) {
      await loadContacts();
    }

    // Create pending new checklist items scoped to this area
    if (formData.pendingNewChecklistItems && formData.pendingNewChecklistItems.length > 0 && areaId) {
      for (const item of formData.pendingNewChecklistItems) {
        if (!item.text?.trim()) continue;
        try {
          await checklistApi.create(route.params.eventId, {
            text: item.text,
            scopeConfigurations: [
              { scope: 'EveryoneInAreas', itemType: 'Area', ids: [areaId] }
            ],
            displayOrder: 0,
            isRequired: false,
          });
        } catch (error) {
          console.error('Failed to create checklist item:', item, error);
        }
      }
      await loadChecklists();
    }

    // Create pending new notes scoped to this area
    if (formData.pendingNewNotes && formData.pendingNewNotes.length > 0 && areaId) {
      for (const note of formData.pendingNewNotes) {
        if (!note.title?.trim() && !note.content?.trim()) continue;
        try {
          await notesApi.create(route.params.eventId, {
            title: note.title || 'Untitled',
            content: note.content || '',
            scopeConfigurations: [
              { scope: 'EveryoneInAreas', itemType: 'Area', ids: [areaId] }
            ],
            displayOrder: 0,
            priority: 'Normal',
            isPinned: false,
          });
        } catch (error) {
          console.error('Failed to create note:', note, error);
        }
      }
      await loadNotes();
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
  showConfirm(`Delete ${termsLower.value.area}`, `Are you sure you want to delete this ${termsLower.value.area}? ${terms.value.checkpoints} will be automatically reassigned.`, async () => {
    try {
      // Delete the area (checkpoints will be automatically reassigned to default area on backend)
      await areasApi.delete(route.params.eventId, areaId);

      // Trigger recalculation to reassign checkpoints that were in this area
      await areasApi.recalculate(route.params.eventId);

      await loadAreas();
      await loadEventData();
      closeEditAreaModal();
    } catch (error) {
      console.error(`Failed to delete ${termsLower.value.area}:`, error);
      alert(error.response?.data?.message || `Failed to delete ${termsLower.value.area}. Please try again.`);
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const closeEditAreaModal = () => {
  showEditArea.value = false;
  selectedArea.value = null;
  selectedAreaId.value = null;
  formDirty.value = false;
  isDrawingAreaBoundary.value = false;
  pendingAreaFormData.value = null;
};

// Layer handlers
const handleAddLayer = () => {
  layerManagement.selectedLayer.value = null;
  layerFormDirty.value = false;
  layerManagement.showEditLayer.value = true;
};

const handleSelectLayer = (layer) => {
  layerManagement.selectedLayer.value = layer;
  layerFormDirty.value = false;
  layerManagement.showEditLayer.value = true;
};

const handleSaveLayer = async (formData) => {
  try {
    // Round route coordinates to 6 decimal places before saving
    const dataToSave = { ...formData };
    if (dataToSave.route) {
      dataToSave.route = roundRoutePoints(dataToSave.route);
    }

    if (layerManagement.selectedLayer.value?.id) {
      // Update existing layer
      await layersApi.update(
        route.params.eventId,
        layerManagement.selectedLayer.value.id,
        dataToSave
      );
    } else {
      // Create new layer
      await layersApi.create(route.params.eventId, dataToSave);
    }
    // Reload layers and checkpoints - checkpoints may have auto-assigned layerIds based on route proximity
    await Promise.all([
      layerManagement.loadLayers(),
      reloadLocationsAndStatus(),
    ]);
    handleCloseLayerModal();
  } catch (error) {
    console.error('Failed to save layer:', error);
    alert(error.response?.data?.message || 'Failed to save layer. Please try again.');
  }
};

const handleDeleteLayer = async (layerId) => {
  showConfirm('Delete layer', 'Are you sure you want to delete this layer?', async () => {
    try {
      await layersApi.delete(route.params.eventId, layerId);
      // Reload layers and checkpoints - checkpoints may have layerIds updated when layer is deleted
      await Promise.all([
        layerManagement.loadLayers(),
        reloadLocationsAndStatus(),
      ]);
      handleCloseLayerModal();
    } catch (error) {
      console.error('Failed to delete layer:', error);
      alert(error.response?.data?.message || 'Failed to delete layer. Please try again.');
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const handleCloseLayerModal = () => {
  layerManagement.showEditLayer.value = false;
  layerManagement.selectedLayer.value = null;
  layerFormDirty.value = false;
};

const handleReorderLayers = async (reorderedLayers) => {
  try {
    const items = reorderedLayers.map((layer, index) => ({
      id: layer.id,
      displayOrder: index,
    }));
    await layersApi.reorder(route.params.eventId, { items });
    await layerManagement.loadLayers();
  } catch (error) {
    console.error('Failed to reorder layers:', error);
  }
};

const handleLayerGpxUpload = async (file) => {
  try {
    const layerId = layerManagement.selectedLayer.value?.id;
    if (!layerId) {
      // For new layers, we need to create first then upload
      // For now, parse GPX client-side and set route
      const reader = new FileReader();
      reader.onload = async (e) => {
        const gpxContent = e.target.result;
        const parser = new DOMParser();
        const gpxDoc = parser.parseFromString(gpxContent, 'text/xml');

        const points = [];
        const trackPoints = gpxDoc.querySelectorAll('trkpt');
        trackPoints.forEach((pt) => {
          points.push({
            lat: parseFloat(pt.getAttribute('lat')),
            lng: parseFloat(pt.getAttribute('lon')),
          });
        });

        if (points.length > 0 && editLayerModalRef.value) {
          editLayerModalRef.value.setRoute(points);
        } else if (points.length === 0 && editLayerModalRef.value) {
          editLayerModalRef.value.setUploadError('No track points found in GPX file');
        }
      };
      reader.readAsText(file);
    } else {
      // Upload to existing layer
      await layersApi.uploadGpx(route.params.eventId, layerId, file);
      await layerManagement.loadLayers();

      // Update the selected layer with new data
      const updatedLayer = layerManagement.layers.value.find((l) => l.id === layerId);
      if (updatedLayer) {
        layerManagement.selectedLayer.value = updatedLayer;
        if (editLayerModalRef.value) {
          editLayerModalRef.value.setRoute(updatedLayer.route);
        }
      }
    }
  } catch (error) {
    console.error('Failed to upload GPX:', error);
    if (editLayerModalRef.value) {
      editLayerModalRef.value.setUploadError('Failed to upload GPX file');
    }
  }
};

const handleEditLayerRoute = ({ route, routeColor, routeWeight }) => {
  routeEditorRoute.value = route || [];
  routeEditorColor.value = routeColor || '#3388ff';
  routeEditorWeight.value = routeWeight || 4;

  // Capture current map state to preserve view when drawing a new route
  if (courseAreasTab.value) {
    routeEditorCenter.value = courseAreasTab.value.getMapCenter();
    routeEditorZoom.value = courseAreasTab.value.getMapZoom();
  } else {
    routeEditorCenter.value = null;
    routeEditorZoom.value = null;
  }

  // Only focus on route bounds if editing an existing route
  routeEditorFocusOnRoute.value = route && route.length > 0;

  showRouteEditor.value = true;
};

const handleRouteEditorSave = (points) => {
  // Update the route in the layer modal
  if (editLayerModalRef.value) {
    editLayerModalRef.value.setRoute(points);
  }
  showRouteEditor.value = false;
  layerFormDirty.value = true;
};

const handleRouteEditorCancel = () => {
  showRouteEditor.value = false;
};

const handleCreateContactFromMarshal = ({ marshal, area }) => {
  // Open contact modal with marshal pre-selected
  selectedContact.value = null;
  contactInitialTab.value = 'details';
  formDirty.value = false;
  pendingAreaForContact.value = area;
  prefilledContactName.value = '';
  prefilledContactMarshalId.value = marshal.id;
  showEditContact.value = true;
};

const handleCreateNewContact = ({ name, area }) => {
  // Open contact modal with name pre-filled
  selectedContact.value = null;
  contactInitialTab.value = 'details';
  formDirty.value = false;
  pendingAreaForContact.value = area;
  prefilledContactName.value = name;
  prefilledContactMarshalId.value = null;
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
    // Clean up any orphaned scope configurations before saving
    const cleanedFormData = {
      ...formData,
      scopeConfigurations: cleanOrphanedScopeConfigurations(
        formData.scopeConfigurations,
        { areas: areas.value, locations: locationStatuses.value, marshals: marshals.value }
      ),
    };

    if (selectedChecklistItem.value && selectedChecklistItem.value.itemId) {
      await checklistApi.update(route.params.eventId, selectedChecklistItem.value.itemId, cleanedFormData);
    } else {
      const response = await checklistApi.create(route.params.eventId, cleanedFormData);

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
  showConfirm(`Delete ${termsLower.value.checklist}`, `Are you sure you want to delete this ${termsLower.value.checklist}?`, async () => {
    try {
      await checklistApi.delete(route.params.eventId, itemId);
      await loadChecklists();
      closeEditChecklistItemModal();
    } catch (error) {
      console.error(`Failed to delete ${termsLower.value.checklist}:`, error);
      alert(error.response?.data?.message || `Failed to delete ${termsLower.value.checklist}. Please try again.`);
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const closeEditChecklistItemModal = () => {
  showEditChecklistItem.value = false;
  selectedChecklistItem.value = null;
  formDirty.value = false;
};

const handleReorderChecklistItems = async (changes) => {
  // Store original order for rollback
  const originalItems = [...checklistItems.value];

  // Apply optimistic update
  changes.forEach(change => {
    const item = checklistItems.value.find(i => i.itemId === change.id);
    if (item) {
      item.displayOrder = change.displayOrder;
    }
  });

  try {
    await checklistApi.reorder(route.params.eventId, changes);
  } catch (error) {
    // Rollback on failure
    checklistItems.value = originalItems;
    console.error('Failed to reorder checklist items:', error);
    alert('Failed to save order. Changes have been reverted.');
  }
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
    // Clean up any orphaned scope configurations before saving
    const cleanedFormData = {
      ...formData,
      scopeConfigurations: cleanOrphanedScopeConfigurations(
        formData.scopeConfigurations,
        { areas: areas.value, locations: locationStatuses.value, marshals: marshals.value }
      ),
    };

    if (selectedNote.value && selectedNote.value.noteId) {
      await notesApi.update(route.params.eventId, selectedNote.value.noteId, cleanedFormData);
    } else {
      await notesApi.create(route.params.eventId, cleanedFormData);
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
  }, { isDanger: true, confirmText: 'Delete' });
};

const closeEditNoteModal = () => {
  showEditNote.value = false;
  selectedNote.value = null;
  formDirty.value = false;
};

const handleReorderNotes = async (changes) => {
  // Store original order for rollback
  const originalNotes = [...notes.value];

  // Apply optimistic update
  changes.forEach(change => {
    const note = notes.value.find(n => n.noteId === change.id);
    if (note) {
      note.displayOrder = change.displayOrder;
    }
  });

  try {
    await notesApi.reorder(route.params.eventId, changes);
  } catch (error) {
    // Rollback on failure
    notes.value = originalNotes;
    console.error('Failed to reorder notes:', error);
    alert('Failed to save order. Changes have been reverted.');
  }
};

const handleReorderChecklists = async (changes) => {
  // Store original order for rollback
  const originalItems = [...checklistItems.value];

  // Apply optimistic update
  changes.forEach(change => {
    const item = checklistItems.value.find(i => i.itemId === change.id);
    if (item) {
      item.displayOrder = change.displayOrder;
    }
  });

  try {
    await checklistApi.reorder(route.params.eventId, changes);
  } catch (error) {
    // Rollback on failure
    checklistItems.value = originalItems;
    console.error('Failed to reorder checklists:', error);
    alert('Failed to save order. Changes have been reverted.');
  }
};

// Incident handlers
const handleReportIncident = () => {
  showReportIncident.value = true;
};

const handleSubmitIncident = async (incidentData) => {
  try {
    await incidentsApi.create(route.params.eventId, incidentData);
    showReportIncident.value = false;
    await loadIncidents();
  } catch (error) {
    console.error('Failed to report incident:', error);
    alert(error.response?.data?.message || 'Failed to report incident. Please try again.');
  }
};

const handleSelectIncident = (incident) => {
  selectedIncident.value = incident;
  showIncidentDetail.value = true;
};

const closeIncidentDetailModal = () => {
  showIncidentDetail.value = false;
  selectedIncident.value = null;
};

const handleIncidentStatusChange = async ({ incidentId, status }) => {
  try {
    await incidentsApi.updateStatus(route.params.eventId, incidentId, { status });
    await loadIncidents();
    // Update the selected incident if still viewing
    if (selectedIncident.value?.incidentId === incidentId) {
      const updated = incidents.value.find(i => i.incidentId === incidentId);
      if (updated) {
        selectedIncident.value = updated;
      }
    }
  } catch (error) {
    console.error('Failed to update incident status:', error);
    alert(error.response?.data?.message || 'Failed to update status. Please try again.');
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
    await incidentsApi.addNote(route.params.eventId, selectedIncident.value.incidentId, incidentNoteText.value.trim());
    // Close the modal
    closeAddIncidentNoteModal();
    await loadIncidents();
    // Update the selected incident if still viewing
    const updated = incidents.value.find(i => i.incidentId === selectedIncident.value.incidentId);
    if (updated) {
      selectedIncident.value = updated;
    }
  } catch (error) {
    console.error('Failed to add note to incident:', error);
    alert(error.response?.data?.message || 'Failed to add note. Please try again.');
  } finally {
    submittingIncidentNote.value = false;
  }
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
    // Clean up any orphaned scope configurations before saving
    const cleanedFormData = {
      ...formData,
      scopeConfigurations: cleanOrphanedScopeConfigurations(
        formData.scopeConfigurations,
        { areas: areas.value, locations: locationStatuses.value, marshals: marshals.value }
      ),
    };

    let newContactId = null;
    const isCreatingNew = !selectedContact.value || !selectedContact.value.contactId;
    const hasAreaContext = !!pendingAreaForContact.value;

    if (selectedContact.value && selectedContact.value.contactId) {
      await contactsApi.update(route.params.eventId, selectedContact.value.contactId, cleanedFormData);
    } else {
      const response = await contactsApi.create(route.params.eventId, cleanedFormData);
      newContactId = response.data?.contactId || response.data?.id;
    }

    await loadContacts();

    // If creating a new contact from the area modal context, add it to the pending list
    if (isCreatingNew && hasAreaContext && newContactId && editAreaModalRef.value) {
      const newContact = contacts.value.find(c => c.contactId === newContactId);
      if (newContact) {
        editAreaModalRef.value.addPendingContact(newContact);
      }
    }

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

      // If area modal is open, clean up any references to this deleted contact
      if (editAreaModalRef.value) {
        editAreaModalRef.value.removeContactById(contactId);
      }

      closeEditContactModal();
    } catch (error) {
      console.error('Failed to delete contact:', error);
      alert(error.response?.data?.message || 'Failed to delete contact. Please try again.');
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const closeEditContactModal = () => {
  showEditContact.value = false;
  selectedContact.value = null;
  pendingAreaForContact.value = null;
  prefilledContactName.value = '';
  prefilledContactMarshalId.value = null;
  formDirty.value = false;
};

const handleReorderContacts = async (changes) => {
  // Store original order for rollback
  const originalContacts = [...contacts.value];

  // Apply optimistic update
  changes.forEach(change => {
    const contact = contacts.value.find(c => c.contactId === change.id);
    if (contact) {
      contact.displayOrder = change.displayOrder;
    }
  });

  try {
    await contactsApi.reorder(route.params.eventId, changes);
  } catch (error) {
    // Rollback on failure
    contacts.value = originalContacts;
    console.error('Failed to reorder contacts:', error);
    alert('Failed to save order. Changes have been reverted.');
  }
};

// Role Definition Handlers
const loadRoleDefinitions = async () => {
  try {
    const response = await roleDefinitionsApi.getAll(route.params.eventId);
    roleDefinitions.value = response.data;
    dataLastLoadedAt.value.roles = Date.now();
  } catch (error) {
    console.error('Failed to load role definitions:', error);
  }
};

const handleAddRole = () => {
  selectedRole.value = null;
  roleInitialTab.value = 'details';
  rolePeople.value = [];
  formDirty.value = false;
  showEditRole.value = true;
};

const handleSelectRole = (role) => {
  selectedRole.value = role;
  roleInitialTab.value = 'details';
  rolePeople.value = [];
  formDirty.value = false;
  showEditRole.value = true;
};

const handleSaveRole = async (formData) => {
  try {
    const roleId = selectedRole.value?.roleId;
    const { assignmentChanges, ...roleData } = formData;

    if (roleId) {
      // Update existing role
      await roleDefinitionsApi.update(route.params.eventId, roleId, roleData);

      // Save assignment changes if any
      const hasAssignmentChanges = assignmentChanges &&
        (assignmentChanges.marshalIdsToAdd.length > 0 ||
         assignmentChanges.marshalIdsToRemove.length > 0 ||
         assignmentChanges.contactIdsToAdd.length > 0 ||
         assignmentChanges.contactIdsToRemove.length > 0);

      if (hasAssignmentChanges) {
        await roleDefinitionsApi.updatePeople(route.params.eventId, roleId, assignmentChanges);
      }
    } else {
      // Create new role
      await roleDefinitionsApi.create(route.params.eventId, roleData);
    }
    await loadRoleDefinitions();
    closeEditRoleModal();
  } catch (error) {
    console.error('Failed to save role:', error);
    const errorMessage = error.response?.data?.message || 'Failed to save role. Please try again.';
    alert(errorMessage);
  }
};

const handleDeleteRole = async (roleId) => {
  const usageCount = selectedRole.value?.usageCount || 0;

  let message = 'Are you sure you want to delete this role?';
  if (usageCount > 0) {
    const peopleWord = usageCount === 1 ? termsLower.value.person : termsLower.value.people;
    message = `This role is assigned to ${usageCount} ${peopleWord}. They will all be unassigned from this role. Are you sure you want to delete it?`;
  }

  showConfirm('Delete role', message, async () => {
    try {
      await roleDefinitionsApi.delete(route.params.eventId, roleId);
      await loadRoleDefinitions();
      closeEditRoleModal();
    } catch (error) {
      console.error('Failed to delete role:', error);
      alert(error.response?.data?.message || 'Failed to delete role. Please try again.');
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const closeEditRoleModal = () => {
  showEditRole.value = false;
  selectedRole.value = null;
  rolePeople.value = [];
  formDirty.value = false;
};

const handleLoadPeopleForRole = async (roleId) => {
  loadingRolePeople.value = true;
  try {
    const response = await roleDefinitionsApi.getPeople(route.params.eventId, roleId);
    rolePeople.value = response.data;
  } catch (error) {
    console.error('Failed to load people for role:', error);
  } finally {
    loadingRolePeople.value = false;
  }
};

const handleReorderRoles = async (changes) => {
  // Store original order for rollback
  const originalRoles = [...roleDefinitions.value];

  // Apply optimistic update
  changes.forEach(change => {
    const role = roleDefinitions.value.find(r => r.roleId === change.id);
    if (role) {
      role.displayOrder = change.displayOrder;
    }
  });

  try {
    await roleDefinitionsApi.reorder(route.params.eventId, changes);
  } catch (error) {
    // Rollback on failure
    roleDefinitions.value = originalRoles;
    console.error('Failed to reorder roles:', error);
    alert('Failed to save order. Changes have been reverted.');
  }
};

const handleDrawBoundary = (formData) => {
  // Store ORIGINAL form data (with polygon AND id) for restoration if canceled
  // formData from modal doesn't include id, so preserve it from selectedArea
  pendingAreaFormData.value = { ...formData, id: selectedArea.value?.id };
  // Clear polygon from selectedArea so old boundary doesn't show while redrawing
  const formWithoutPolygon = { ...formData, id: selectedArea.value?.id, polygon: [] };
  selectedArea.value = formWithoutPolygon;
  // Keep modal open - fullscreen map will appear over top of it

  // Try to zoom to the area's extent
  let foundBounds = false;

  // First, try to use the existing polygon bounds
  if (formData.polygon && formData.polygon.length > 0) {
    const lats = formData.polygon.map(p => p.lat || p[0]);
    const lngs = formData.polygon.map(p => p.lng || p[1]);
    const minLat = Math.min(...lats);
    const maxLat = Math.max(...lats);
    const minLng = Math.min(...lngs);
    const maxLng = Math.max(...lngs);
    savedMapCenter.value = { lat: (minLat + maxLat) / 2, lng: (minLng + maxLng) / 2 };
    // Calculate zoom based on extent (rough approximation)
    const latDiff = maxLat - minLat;
    const lngDiff = maxLng - minLng;
    const maxDiff = Math.max(latDiff, lngDiff);
    // Approximate zoom: smaller diff = higher zoom
    savedMapZoom.value = maxDiff < 0.005 ? 17 : maxDiff < 0.01 ? 16 : maxDiff < 0.02 ? 15 : maxDiff < 0.05 ? 14 : 13;
    foundBounds = true;
  }

  // If no polygon, try to use the area's checkpoints
  if (!foundBounds && formData.checkpointIds && formData.checkpointIds.length > 0) {
    const areaCheckpoints = locationStatuses.value.filter(loc => formData.checkpointIds.includes(loc.id));
    if (areaCheckpoints.length > 0) {
      const lats = areaCheckpoints.map(c => c.latitude).filter(Boolean);
      const lngs = areaCheckpoints.map(c => c.longitude).filter(Boolean);
      if (lats.length > 0 && lngs.length > 0) {
        const minLat = Math.min(...lats);
        const maxLat = Math.max(...lats);
        const minLng = Math.min(...lngs);
        const maxLng = Math.max(...lngs);
        savedMapCenter.value = { lat: (minLat + maxLat) / 2, lng: (minLng + maxLng) / 2 };
        const latDiff = maxLat - minLat;
        const lngDiff = maxLng - minLng;
        const maxDiff = Math.max(latDiff, lngDiff);
        savedMapZoom.value = maxDiff < 0.005 ? 16 : maxDiff < 0.01 ? 15 : maxDiff < 0.02 ? 14 : maxDiff < 0.05 ? 13 : 12;
        foundBounds = true;
      }
    }
  }

  // Fallback to current map state if no area bounds found
  if (!foundBounds && courseAreasTab.value) {
    const center = courseAreasTab.value.getMapCenter();
    const zoom = courseAreasTab.value.getMapZoom();
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
  showConfirm('Remove administrator', `Remove ${userEmail} as an administrator?`, async () => {
    try {
      await eventAdminsComposable.removeAdmin(userEmail);
      await loadEventAdmins();
    } catch (error) {
      console.error('Failed to remove admin:', error);
      alert(error.response?.data?.message || 'Failed to remove administrator. Please try again.');
    }
  }, { isDanger: true, confirmText: 'Remove' });
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
        latitude: roundCoordinate(coords.lat),
        longitude: roundCoordinate(coords.lng),
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

const handleCourseFullscreen = () => {
  // Capture current map state before entering fullscreen
  fullscreenMap.saveMapState(courseAreasTab.value);

  // Enter fullscreen view mode
  isFullscreenMapActive.value = true;
  fullscreenMode.value = 'view';
  fullscreenContext.value = {
    title: 'Course map',
    description: '',
  };
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
    // Complete the polygon and exit - modal stays open behind fullscreen
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

    pendingAreaFormData.value = null;
    exitFullscreen();
    showEditArea.value = true;

  } else if (fullscreenMode.value === 'add-multiple') {
    // Show layer selection modal if there are multiple layers
    if (layerManagement.layers.value.length > 1) {
      showBatchLayerSelection.value = true;
    } else {
      // Only one layer or no layers - save with default (all layers)
      saveTempCheckpoints({ mode: 'all' });
    }
  }
};

const saveTempCheckpoints = async (layerSelection = { mode: 'all' }) => {
  const checkpointsToSave = fullscreenMap.getTempCheckpoints();

  try {
    // Save all checkpoints
    for (const checkpoint of checkpointsToSave) {
      // Map old selection mode to new layerAssignmentMode
      let layerAssignmentMode = 'auto'; // default
      let layerIds = null;

      if (layerSelection.mode === 'auto-detect') {
        // Auto mode - backend will calculate layer IDs
        layerAssignmentMode = 'auto';
        layerIds = null;
      } else if (layerSelection.mode === 'specific' && layerSelection.selectedLayerIds) {
        layerAssignmentMode = 'specific';
        layerIds = layerSelection.selectedLayerIds;
      } else if (layerSelection.mode === 'all') {
        layerAssignmentMode = 'all';
        layerIds = null;
      }

      await eventsStore.createLocation({
        eventId: route.params.eventId,
        name: checkpoint.name,
        description: checkpoint.description,
        latitude: roundCoordinate(checkpoint.latitude),
        longitude: roundCoordinate(checkpoint.longitude),
        requiredMarshals: checkpoint.requiredMarshals,
        what3Words: checkpoint.what3Words,
        layerAssignmentMode,
        layerIds,
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

const handleBatchLayerConfirm = (layerSelection) => {
  showBatchLayerSelection.value = false;
  saveTempCheckpoints(layerSelection);
};

const handleBatchLayerCancel = () => {
  showBatchLayerSelection.value = false;
  // Don't exit fullscreen - user can continue adding or try again
};

const handleFullscreenCancel = () => {
  if (fullscreenMode.value === 'draw-area') {
    // Restore original data - modal is still open behind fullscreen
    if (pendingAreaFormData.value) {
      selectedArea.value = pendingAreaFormData.value;
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
    alert('Invalid What3Words format. Please use word.word.word (lowercase letters only)');
    return;
  }

  try {
    await eventsStore.createLocation({
      eventId: route.params.eventId,
      ...formData,
      latitude: roundCoordinate(formData.latitude),
      longitude: roundCoordinate(formData.longitude),
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
  lockedFromMarshal.value = false;
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

// Handle selecting an incident from the location modal

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

  // Center on the location being moved (use form data or selectedLocation)
  const lat = preservedLocationFormData.value?.latitude ?? selectedLocation.value?.latitude;
  const lng = preservedLocationFormData.value?.longitude ?? selectedLocation.value?.longitude;
  if (lat && lng) {
    savedMapCenter.value = { lat, lng };
    savedMapZoom.value = 16; // Zoom in on the checkpoint
  } else if (courseAreasTab.value) {
    // Fallback to current map state if no coordinates
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
        latitude: roundCoordinate(formData.latitude),
        longitude: roundCoordinate(formData.longitude),
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
        layerAssignmentMode: formData.layerAssignmentMode || 'auto',
        layerIds: formData.layerIds,
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
          latitude: roundCoordinate(formData.latitude),
          longitude: roundCoordinate(formData.longitude),
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
          layerAssignmentMode: formData.layerAssignmentMode || 'auto',
          layerIds: formData.layerIds,
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

      // Process assignments marked for removal in the modal
      if (formData.assignmentsToRemove && formData.assignmentsToRemove.length > 0) {
        for (const assignmentId of formData.assignmentsToRemove) {
          await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
        }
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
  showConfirm(`Delete ${termsLower.value.checkpoint}`, `Are you sure you want to delete this ${termsLower.value.checkpoint}?`, async () => {
    try {
      await locationsApi.delete(route.params.eventId, locationId);
      await reloadLocationsAreasAndStatus();
      closeEditLocationModal();
    } catch (error) {
      console.error(`Failed to delete ${termsLower.value.checkpoint}:`, error);
      alert(`Failed to delete ${termsLower.value.checkpoint}. Please try again.`);
    }
  }, { isDanger: true, confirmText: 'Delete' });
};

const toggleCheckIn = async (assignment) => {
  try {
    // Set loading state for marshals tab
    if (marshalsTabRef.value) {
      marshalsTabRef.value.setCheckingIn(assignment.id);
    }

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
  } finally {
    // Clear loading state
    if (marshalsTabRef.value) {
      marshalsTabRef.value.setCheckingIn(null);
    }
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
  marshalValidationErrors.value = {};
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
  marshalValidationErrors.value = {};
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

const marshalAssignmentsForEdit = computed(() => {
  if (!selectedMarshal.value) return [];

  const assignments = [];

  // Get existing assignments, filtering out those marked for deletion
  locationStatuses.value.forEach(location => {
    const marshalAssignments = location.assignments.filter(
      a => a.marshalName === selectedMarshal.value.name &&
           !pendingMarshalDeleteAssignments.value.includes(a.id)
    );
    marshalAssignments.forEach(assignment => {
      assignments.push({
        ...assignment,
        locationName: location.name,
        locationDescription: location.description || '',
      });
    });
  });

  // Add pending new assignments
  pendingMarshalAssignments.value.forEach((pending, index) => {
    const location = locationStatuses.value.find(l => l.id === pending.locationId);
    if (location) {
      assignments.push({
        id: `pending-${index}`,
        locationId: pending.locationId,
        locationName: location.name,
        locationDescription: location.description || '',
        marshalId: selectedMarshal.value.id,
        marshalName: selectedMarshal.value.name,
        isCheckedIn: false,
        isPending: true,
      });
    }
  });

  return assignments;
});

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
          roles: formData.roles || [],
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
        roles: formData.roles || [],
        pendingNewChecklistItems: formData.pendingNewChecklistItems || [],
        pendingNewNotes: formData.pendingNewNotes || [],
      });
      marshalId = response.data.id;
      isNewMarshal = true;
    }

    for (const assignmentId of pendingMarshalDeleteAssignments.value) {
      await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
    }

    // Process assignments marked for removal in the modal
    if (formData.assignmentsToRemove && formData.assignmentsToRemove.length > 0) {
      for (const assignmentId of formData.assignmentsToRemove) {
        await eventsStore.deleteAssignment(route.params.eventId, assignmentId);
      }
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
      const marshalAssignments = marshalAssignmentsForEdit.value;
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

    // Update selectedLocation if the checkpoint modal is still open (when editing marshal from checkpoint)
    if (selectedLocation.value) {
      const updatedLocation = locationStatuses.value.find(l => l.id === selectedLocation.value.id);
      if (updatedLocation) {
        selectedLocation.value = updatedLocation;
      }
    }

    closeEditMarshalModal();

    // Show confirmation modal with login link for newly created marshals
    if (isNewMarshal) {
      newlyCreatedMarshalId.value = marshalId;
      newlyCreatedMarshalName.value = formData.name;
      newlyCreatedMarshalEmail.value = formData.email || '';
      showMarshalCreated.value = true;
    }
  } catch (error) {
    console.error('Failed to save marshal:', error);

    // Extract error message from API response
    const errorMessage = error.response?.data?.message || 'Failed to save marshal. Please try again.';

    // Check if it's a validation error (name required, etc.)
    if (error.response?.status === 400) {
      // Parse the error message to determine which field has the error
      const lowerMessage = errorMessage.toLowerCase();

      if (lowerMessage.includes('name')) {
        marshalValidationErrors.value = { name: errorMessage };
        // Switch to details tab and focus the name field
        if (editMarshalModalRef.value) {
          editMarshalModalRef.value.switchToTab('details');
          editMarshalModalRef.value.focusField('name');
        }
      } else if (lowerMessage.includes('email')) {
        marshalValidationErrors.value = { email: errorMessage };
        if (editMarshalModalRef.value) {
          editMarshalModalRef.value.switchToTab('details');
          editMarshalModalRef.value.focusField('email');
        }
      } else {
        // General validation error
        marshalValidationErrors.value = { general: errorMessage };
        if (editMarshalModalRef.value) {
          editMarshalModalRef.value.switchToTab('details');
        }
      }
    } else {
      // Other errors (network, server, etc.)
      marshalValidationErrors.value = { general: errorMessage };
      if (editMarshalModalRef.value) {
        editMarshalModalRef.value.switchToTab('details');
      }
    }
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
  }, { isDanger: true, confirmText: 'Delete' });
};

const handleDeleteMarshalFromGrid = (marshal) => {
  handleDeleteMarshal(marshal.id);
};

const toggleMarshalCheckIn = async (assignment) => {
  await toggleCheckIn(assignment);
};

const handleRemoveMarshalAssignment = (assignmentOrId) => {
  const assignmentId = assignmentOrId?.id ?? assignmentOrId;

  // Check if this is a pending assignment (not yet saved)
  if (assignmentId?.toString().startsWith('pending-')) {
    // Remove from pending assignments list using the locationId
    const locationId = assignmentOrId?.locationId;
    if (locationId) {
      pendingMarshalAssignments.value = pendingMarshalAssignments.value.filter(
        p => p.locationId !== locationId
      );
    }
    markFormDirty();
  } else {
    // For existing assignments, add to pending deletions
    if (!pendingMarshalDeleteAssignments.value.includes(assignmentId)) {
      pendingMarshalDeleteAssignments.value.push(assignmentId);
      markFormDirty();
    }
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
  lockedCheckpointId.value = null;
  marshalValidationErrors.value = {};
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

// Handle selecting a marshal from within the checkpoint/location modal
const handleSelectMarshalFromCheckpoint = async (assignment) => {
  // Get the full marshal data
  const marshal = marshals.value.find(m => m.id === assignment.marshalId);
  if (!marshal) {
    console.error('Marshal not found:', assignment.marshalId);
    return;
  }

  // Set the locked checkpoint ID to the current location
  lockedCheckpointId.value = selectedLocation.value?.id || null;

  // Reset dirty state before opening
  formDirty.value = false;

  // Open the marshal modal
  selectedMarshal.value = marshal;
  showEditMarshal.value = true;
};

// Handle selecting a checkpoint from within the marshal modal
const handleSelectCheckpointFromMarshal = (assignment) => {
  // Find the full location data
  const location = locationStatuses.value.find(l => l.id === assignment.locationId);
  if (!location) {
    console.error('Location not found:', assignment.locationId);
    return;
  }

  // Set locked from marshal flag
  lockedFromMarshal.value = true;

  // Reset dirty state before opening
  formDirty.value = false;

  // Open the location modal
  selectedLocation.value = location;
  showEditLocation.value = true;
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

// Watch for tab changes to start/stop polling and refresh stale data
watch(activeTab, (newTab) => {
  if (newTab === 'course') {
    startDynamicCheckpointPolling();
  } else {
    stopDynamicCheckpointPolling();
  }

  // Check if data for this tab is stale (>60 seconds) and refresh in background
  const now = Date.now();
  const isStale = (key) => now - dataLastLoadedAt.value[key] > STALE_DATA_THRESHOLD_MS;

  switch (newTab) {
    case 'marshals':
      if (isStale('marshals')) {
        loadMarshals();
      }
      // Also load role definitions for displaying roles in marshal edit modal
      if (isStale('roles')) {
        loadRoleDefinitions();
      }
      break;
    case 'areas':
      if (isStale('areas')) {
        loadAreas();
      }
      // Also load role definitions for displaying role names in area contacts
      if (isStale('roles')) {
        loadRoleDefinitions();
      }
      break;
    case 'checkpoints':
      if (isStale('checkpoints')) {
        reloadLocationsAndStatus();
      }
      break;
    case 'checklists':
      if (isStale('checklists')) {
        loadChecklists();
      }
      break;
    case 'notes':
      if (isStale('notes')) {
        loadNotes();
      }
      break;
    case 'incidents':
      if (isStale('incidents')) {
        loadIncidents();
      }
      break;
    case 'contacts':
      if (isStale('contacts')) {
        loadContacts();
      }
      // Also load role definitions for displaying role names
      if (isStale('roles')) {
        loadRoleDefinitions();
      }
      break;
    case 'roles':
      if (isStale('roles')) {
        loadRoleDefinitions();
      }
      break;
  }
});

// Lifecycle
onMounted(async () => {
  // In admin mode, never show the loading overlay for GET requests
  setSkipLoadingOverlayForGets(true);

  // Check for sample event access
  const sampleCode = route.query.sample;
  if (sampleCode) {
    try {
      // Validate the sample code
      const response = await sampleEventsApi.validate(sampleCode);
      if (response.data.eventId !== route.params.eventId) {
        console.error('Sample code does not match event ID');
        router.push('/');
        return;
      }
      // Set sample auth context for API calls
      setAuthContext('sample', route.params.eventId, sampleCode);
    } catch (error) {
      console.error('Invalid or expired sample code:', error);
      router.push('/');
      return;
    }
  }

  await loadEventData();

  // Fetch user claims to check if user has marshal access (skip for sample events)
  if (!sampleCode) {
    try {
      const claimsResponse = await authApi.getMe(route.params.eventId);
      userClaims.value = claimsResponse.data;
    } catch (error) {
      console.warn('Failed to fetch user claims:', error);
    }
  }

  // Start polling if already on course tab
  if (activeTab.value === 'course') {
    startDynamicCheckpointPolling();
  }
});

// Update document title when event loads - always "Admin" for admin view
watch(event, (newEvent) => {
  if (newEvent) {
    document.title = 'OnTheDay App - Admin';
  }
}, { immediate: true });

onUnmounted(() => {
  stopDynamicCheckpointPolling();
  setSkipLoadingOverlayForGets(false);
});
</script>

<style scoped>
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
  border-color: var(--accent-primary);
}

.modal-footer-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
}

/* Preserved styles from original file */
.admin-event-manage {
  min-height: 100vh;
  background: var(--bg-secondary);
  color: var(--text-primary);
  width: 100%;
  max-width: 100%;
  overflow-x: hidden;
}

.header {
  background: var(--bg-primary);
  padding: 1.5rem 2rem;
  border-bottom: 1px solid var(--border-color);
  display: flex;
  justify-content: space-between;
  align-items: center;
  position: relative;
}

.theme-toggle-fixed {
  position: absolute;
  top: 1rem;
  right: 1rem;
  z-index: 100;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.btn-back {
  background: none;
  border: none;
  color: var(--text-primary);
  cursor: pointer;
  padding: 0.25rem;
  border-radius: 8px;
  transition: transform 0.2s, opacity 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
}

.btn-back:hover {
  transform: scale(1.05);
  opacity: 0.9;
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
  background: var(--brand-gradient);
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-size: 0.9rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
}

.btn-marshal-mode:hover {
  transform: translateY(-1px);
  box-shadow: 0 3px 10px var(--brand-shadow-lg);
}

.container {
  padding: 2rem;
  max-width: 1600px;
  margin: 0 auto;
}

.tabs-nav {
  flex-wrap: nowrap;
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
  .admin-event-manage {
    overflow-x: hidden;
    width: 100%;
  }

  .header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
    padding: 1rem;
  }

  .header-left {
    width: 100%;
    gap: 0.75rem;
  }

  .header-left h1 {
    font-size: 1.25rem;
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
    width: 100%;
    max-width: 100%;
  }

  .tabs-nav {
  flex-wrap: nowrap;
    overflow-x: visible;
    overflow-y: visible;
  }
}
</style>
