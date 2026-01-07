<template>
  <BaseModal
    :show="show"
    :title="modalTitle"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header -->
    <template #tab-header>
      <TabHeader
        v-model="activeTab"
        :tabs="availableTabs"
      />
    </template>

    <!-- Details Tab -->
    <MarshalDetailsTab
      v-if="activeTab === 'details'"
      :form="form"
      :event-id="eventId"
      :marshal-id="isEditing ? marshal?.id : null"
      @update:form="updateForm"
      @input="handleInput"
    />

    <!-- Checkpoints Tab -->
    <MarshalCheckpointsTab
      v-if="activeTab === 'checkpoints'"
      ref="checkpointsTabRef"
      :assignments="pendingAssignmentsForDisplay"
      :available-locations="availableLocationsFiltered"
      :is-new-marshal="!isEditing"
      @input="handleInput"
      @remove-assignment="handleRemoveAssignment"
      @assign-to-location="handleAssignToLocation"
    />

    <!-- Checklists Tab (only when editing - checklists require saved marshal) -->
    <MarshalChecklistView
      v-if="activeTab === 'checklists' && isEditing"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :marshal-id="marshal.id"
      :locations="allLocations"
      :areas="areas"
      @change="handleChecklistChange"
    />

    <!-- Checklists preview - shown in both create and edit modes to add new scoped items -->
    <ChecklistPreview
      v-if="activeTab === 'checklists'"
      v-model="pendingNewChecklistItems"
      :all-checklist-items="allChecklistItems"
      :pending-location-ids="pendingLocationIds"
      :all-locations="allLocations"
      :areas="areas"
      :is-editing="isEditing"
      @change="handleInput"
    />

    <!-- Notes Tab (only when editing - notes require saved marshal) -->
    <NotesView
      v-if="activeTab === 'notes' && isEditing"
      :event-id="eventId"
      :marshal-id="marshal?.id"
      :all-notes="eventNotes"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
    />

    <!-- Notes preview - shown in both create and edit modes to add new scoped items -->
    <NotesPreview
      v-if="activeTab === 'notes'"
      v-model="pendingNewNotes"
      :all-notes="eventNotes"
      :pending-location-ids="pendingLocationIds"
      :all-locations="allLocations"
      :areas="areas"
      :is-editing="isEditing"
      @change="handleInput"
    />

    <!-- Incidents Tab (only when there are incidents) -->
    <div v-if="activeTab === 'incidents'" class="tab-content incidents-tab">
      <div v-if="incidents && incidents.length > 0" class="incidents-list">
        <IncidentCard
          v-for="incident in incidents"
          :key="incident.incidentId"
          :incident="incident"
          @click="$emit('select-incident', incident)"
        />
      </div>
      <div v-else class="empty-state">
        No incidents for this {{ termsLower.person }}.
      </div>
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isEditing"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete {{ termsLower.person }}
        </button>
        <div v-else></div>
        <button type="button" @click="handlePrimaryAction" class="btn btn-primary">
          {{ isEditing ? 'Save changes' : createButtonText }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import MarshalDetailsTab from '../tabs/MarshalDetailsTab.vue';
import MarshalCheckpointsTab from '../tabs/MarshalCheckpointsTab.vue';
import MarshalChecklistView from '../../MarshalChecklistView.vue';
import ChecklistPreview from '../../ChecklistPreview.vue';
import NotesView from '../../NotesView.vue';
import NotesPreview from '../../NotesPreview.vue';
import IncidentCard from '../../IncidentCard.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  marshal: {
    type: Object,
    default: null,
  },
  eventId: {
    type: String,
    required: true,
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  availableLocations: {
    type: Array,
    default: () => [],
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  isEditing: {
    type: Boolean,
    default: false,
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
  eventNotes: {
    type: Array,
    default: () => [],
  },
  allChecklistItems: {
    type: Array,
    default: () => [],
  },
  incidents: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'toggle-check-in',
  'remove-assignment',
  'assign-to-location',
  'update:isDirty',
  'select-incident',
]);

const activeTab = ref('details');
const checkpointsTabRef = ref(null);
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const pendingNewAssignments = ref([]); // For tracking assignments when creating new marshal
const pendingNewChecklistItems = ref([]); // For creating new checklist items scoped to this marshal
const pendingNewNotes = ref([]); // For creating new notes scoped to this marshal
const form = ref({
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});

const modalTitle = computed(() => {
  const baseTitle = props.isEditing ? `Edit ${termsLower.value.person}` : `Create ${termsLower.value.person}`;
  const name = form.value.name?.trim();
  return name ? `${baseTitle} - ${name}` : baseTitle;
});

const availableTabs = computed(() => {
  const tabs = [
    { value: 'details', label: 'Details', icon: 'details' },
    { value: 'checkpoints', label: terms.value.checkpoints, icon: 'checkpoint' },
    { value: 'checklists', label: terms.value.checklists, icon: 'checklist' },
    { value: 'notes', label: 'Notes', icon: 'notes' },
  ];
  // Only show incidents tab if there are incidents for this marshal or their checkpoints
  if (props.incidents && props.incidents.length > 0) {
    tabs.push({ value: 'incidents', label: 'Incidents', icon: 'incidents' });
  }
  return tabs;
});

// Get current tab index and check if on last tab
const currentTabIndex = computed(() => {
  return availableTabs.value.findIndex(tab => tab.value === activeTab.value);
});

const isLastTab = computed(() => {
  return currentTabIndex.value === availableTabs.value.length - 1;
});

const nextTab = computed(() => {
  if (isLastTab.value) return null;
  return availableTabs.value[currentTabIndex.value + 1];
});

// Button text for create mode - shows "Add [next tab]..." or "Create [person]"
const createButtonText = computed(() => {
  if (isLastTab.value) {
    return `Create ${termsLower.value.person}`;
  }
  return `Add ${nextTab.value.label.toLowerCase()}...`;
});

// Pending location IDs for checklist preview (when creating new marshal)
const pendingLocationIds = computed(() => {
  return pendingNewAssignments.value.map(pa => pa.locationId);
});

// For new marshals, show pending assignments; for existing, show actual assignments
const pendingAssignmentsForDisplay = computed(() => {
  if (props.isEditing) {
    return props.assignments;
  }
  // Convert pending assignments to display format
  return pendingNewAssignments.value.map(pa => ({
    id: `pending-${pa.locationId}`,
    locationId: pa.locationId,
    locationName: props.allLocations.find(l => l.id === pa.locationId)?.name || 'Unknown',
    isPending: true,
  }));
});

// Filter out already assigned locations
const availableLocationsFiltered = computed(() => {
  if (props.isEditing) {
    return props.availableLocations;
  }
  // For new marshals, filter out pending assignments
  const pendingLocationIds = pendingNewAssignments.value.map(pa => pa.locationId);
  return props.allLocations.filter(loc => !pendingLocationIds.includes(loc.id));
});

watch(() => props.marshal, (newVal) => {
  if (newVal) {
    form.value = {
      name: newVal.name || '',
      email: newVal.email || '',
      phoneNumber: newVal.phoneNumber || '',
      notes: newVal.notes || '',
    };
  } else {
    form.value = {
      name: '',
      email: '',
      phoneNumber: '',
      notes: '',
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    checklistChanges.value = [];
    pendingNewAssignments.value = []; // Clear pending assignments for new marshals
    pendingNewChecklistItems.value = []; // Clear pending new checklist items
    pendingNewNotes.value = []; // Clear pending new notes
    // Reset form when opening for a new marshal
    if (!props.isEditing) {
      form.value = {
        name: '',
        email: '',
        phoneNumber: '',
        notes: '',
      };
    }
    // Clear pending changes in checkpoints tab if it exists
    if (checkpointsTabRef.value?.clearPendingChanges) {
      checkpointsTabRef.value.clearPendingChanges();
    }
    // Clear pending changes in checklist tab if it exists
    if (checklistTabRef.value?.resetLocalState) {
      checklistTabRef.value.resetLocalState();
    }
  }
});

const updateForm = (newFormData) => {
  form.value = newFormData;
};

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleChecklistChange = () => {
  emit('update:isDirty', true);
};

const handlePrimaryAction = () => {
  if (props.isEditing) {
    // When editing, always save
    handleSave();
  } else if (isLastTab.value) {
    // When creating and on the last tab, save
    handleSave();
  } else {
    // When creating and not on the last tab, advance to next tab
    activeTab.value = nextTab.value.value;
  }
};

const handleSave = () => {
  const formData = {
    ...form.value,
    // Include pending check-in changes from checkpoints tab
    checkInChanges: checkpointsTabRef.value?.getPendingChanges?.() || [],
    // Include pending checklist changes
    checklistChanges: checklistChanges.value || [],
    // Include pending assignments for new marshals
    pendingAssignments: props.isEditing ? [] : pendingNewAssignments.value,
    // Include pending new checklist items and notes (for both create and edit modes)
    pendingNewChecklistItems: pendingNewChecklistItems.value,
    pendingNewNotes: pendingNewNotes.value,
  };
  emit('save', formData);
};

const handleDelete = () => {
  emit('delete');
};

const handleRemoveAssignment = (assignment) => {
  if (!props.isEditing && assignment.isPending) {
    // For new marshals, remove from pending assignments locally
    pendingNewAssignments.value = pendingNewAssignments.value.filter(
      pa => pa.locationId !== assignment.locationId
    );
  } else {
    emit('remove-assignment', assignment);
  }
};

const handleAssignToLocation = (locationId) => {
  if (!props.isEditing) {
    // For new marshals, add to pending assignments locally
    pendingNewAssignments.value.push({ locationId });
    emit('update:isDirty', true);
  } else {
    emit('assign-to-location', locationId);
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-primary {
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-danger {
  background: #dc3545;
  color: white;
}

.btn-danger:hover {
  background: #c82333;
}

.placeholder-message {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 150px;
  color: #666;
  font-style: italic;
  text-align: center;
  padding: 2rem;
}

.placeholder-message p {
  margin: 0;
}

/* Incidents tab */
.incidents-tab {
  padding-top: 0.5rem;
}

.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 150px;
  color: var(--text-muted, #666);
  font-style: italic;
  text-align: center;
  padding: 2rem;
}
</style>
