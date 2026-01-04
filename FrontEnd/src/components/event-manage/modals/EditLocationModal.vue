<template>
  <BaseModal
    :show="show"
    :title="modalTitle"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    :z-index="zIndex"
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
    <LocationDetailsTab
      v-if="activeTab === 'details'"
      :form="form"
      :areas="areas"
      :event-date="eventDate"
      @update:form="form = $event"
      @input="handleInput"
      @save="handleSave"
    />

    <!-- Location Tab -->
    <LocationCoordinatesTab
      v-if="activeTab === 'location'"
      :form="form"
      :is-moving="isMoving"
      @update:form="form = $event"
      @input="handleInput"
      @save="handleSave"
      @move-location="handleMoveLocation"
    />

    <!-- Marshals Tab -->
    <LocationAssignmentsTab
      v-if="activeTab === 'marshals'"
      ref="assignmentsTabRef"
      :form="form"
      :assignments="assignments"
      :available-marshals="availableMarshals"
      @update:form="form = $event"
      @input="handleInput"
      @remove-assignment="handleRemoveAssignment"
      @assign-marshal="handleAssignMarshal"
    />

    <!-- Checklists Tab (only when editing - checklists require saved location) -->
    <CheckpointChecklistView
      v-if="activeTab === 'checklists' && isExistingLocation"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :location-id="location.id"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
      @change="handleChecklistChange"
    />

    <!-- Checklists preview - shown in both create and edit modes to add new scoped items -->
    <ChecklistPreviewForLocation
      v-if="activeTab === 'checklists'"
      v-model="pendingNewChecklistItems"
      :all-checklist-items="allChecklistItems"
      :pending-area-ids="form.areaIds"
      :areas="areas"
      :is-editing="isExistingLocation"
      @change="handleInput"
    />

    <!-- Notes Tab (only when editing - notes require saved location) -->
    <NotesView
      v-if="activeTab === 'notes' && isExistingLocation"
      :event-id="eventId"
      :location-id="location?.id"
      :all-notes="notes"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
    />

    <!-- Notes preview - shown in both create and edit modes to add new scoped items -->
    <NotesPreviewForLocation
      v-if="activeTab === 'notes'"
      v-model="pendingNewNotes"
      :all-notes="notes"
      :pending-area-ids="form.areaIds"
      :areas="areas"
      :is-editing="isExistingLocation"
      @change="handleInput"
    />

    <!-- Appearance Tab -->
    <div v-if="activeTab === 'appearance'" class="tab-content appearance-tab">
      <p class="section-description">
        Choose a custom icon for this {{ termsLower.checkpoint }}, or leave as Default to inherit from {{ termsLower.area }} or event settings.
      </p>
      <CheckpointStylePicker
        :style-type="form.styleType || 'default'"
        :style-color="form.styleColor || ''"
        :inherited-style-type="inheritedStyle.type"
        :inherited-style-color="inheritedStyle.color"
        :default-label="`${terms.area} default`"
        icon-label="Icon style"
        color-label="Icon colour"
        :show-preview="true"
        @update:style-type="handleStyleInput('styleType', $event)"
        @update:style-color="handleStyleInput('styleColor', $event)"
      />
    </div>

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isExistingLocation"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete {{ termsLower.checkpoint }}
        </button>
        <div v-else></div>
        <button type="button" @click="handlePrimaryAction" class="btn btn-primary">
          {{ isExistingLocation ? 'Save changes' : createButtonText }}
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import LocationDetailsTab from '../tabs/LocationDetailsTab.vue';
import LocationCoordinatesTab from '../tabs/LocationCoordinatesTab.vue';
import LocationAssignmentsTab from '../tabs/LocationAssignmentsTab.vue';
import CheckpointChecklistView from '../../CheckpointChecklistView.vue';
import NotesView from '../../NotesView.vue';
import ChecklistPreviewForLocation from '../../ChecklistPreviewForLocation.vue';
import NotesPreviewForLocation from '../../NotesPreviewForLocation.vue';
import CheckpointStylePicker from '../../CheckpointStylePicker.vue';
import { formatDateForInput } from '../../../utils/dateFormatters';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  location: {
    type: Object,
    default: null,
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
  eventDate: {
    type: String,
    default: '',
  },
  areas: {
    type: Array,
    default: () => [],
  },
  eventId: {
    type: String,
    required: true,
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  notes: {
    type: Array,
    default: () => [],
  },
  availableMarshals: {
    type: Array,
    default: () => [],
  },
  allChecklistItems: {
    type: Array,
    default: () => [],
  },
  eventDefaultStyleType: {
    type: String,
    default: 'default',
  },
  eventDefaultStyleColor: {
    type: String,
    default: '',
  },
  zIndex: {
    type: Number,
    default: 1000,
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'move-location',
  'toggle-check-in',
  'remove-assignment',
  'assign-marshal',
  'update:isDirty',
]);

const activeTab = ref('details');
const isMoving = ref(false);
const assignmentsTabRef = ref(null);
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const pendingNewChecklistItems = ref([]); // For creating new checklist items scoped to this location
const pendingNewNotes = ref([]); // For creating new notes scoped to this location
const form = ref({
  name: '',
  description: '',
  what3Words: '',
  latitude: 0,
  longitude: 0,
  requiredMarshals: 1,
  useCustomTimes: false,
  startTime: '',
  endTime: '',
  areaIds: [],
  styleType: 'default',
  styleColor: '',
});

// Computed properties for add vs edit mode
const isExistingLocation = computed(() => {
  return props.location && props.location.id;
});

const modalTitle = computed(() => {
  if (isExistingLocation.value) {
    return `Edit ${termsLower.value.checkpoint}: ${props.location?.name || ''}`;
  }
  return `Add ${termsLower.value.checkpoint}`;
});

const availableTabs = computed(() => [
  { value: 'details', label: 'Details', icon: 'details' },
  { value: 'location', label: 'Location', icon: 'location' },
  { value: 'marshals', label: terms.value.people, icon: 'marshal' },
  { value: 'checklists', label: terms.value.checklists, icon: 'checklist' },
  { value: 'notes', label: 'Notes', icon: 'notes' },
  { value: 'appearance', label: 'Appearance', icon: 'appearance' },
]);

// Get current tab index and check if on last tab
const currentTabIndex = computed(() => {
  return availableTabs.value.findIndex(tab => tab.value === activeTab.value);
});

const isLastTab = computed(() => {
  return currentTabIndex.value === availableTabs.value.length - 1;
});

// Calculate inherited style from area or event
const inheritedStyle = computed(() => {
  // First, check if any of the checkpoint's areas have a custom style
  const areaIds = form.value.areaIds || [];
  if (areaIds.length > 0) {
    // Find areas with custom styles, prefer the one with fewest checkpoints (or first match)
    const styledAreas = props.areas
      .filter(a => areaIds.includes(a.id) && a.checkpointStyleType && a.checkpointStyleType !== 'default')
      .sort((a, b) => (a.checkpointCount || 0) - (b.checkpointCount || 0));

    if (styledAreas.length > 0) {
      return {
        type: styledAreas[0].checkpointStyleType,
        color: styledAreas[0].checkpointStyleColor || '',
      };
    }
  }

  // Fall back to event default
  return {
    type: props.eventDefaultStyleType || 'default',
    color: props.eventDefaultStyleColor || '',
  };
});

const nextTab = computed(() => {
  if (isLastTab.value) return null;
  return availableTabs.value[currentTabIndex.value + 1];
});

// Button text for create mode - shows "Add [next tab]..." or "Create [checkpoint]"
const createButtonText = computed(() => {
  if (isLastTab.value) {
    return `Create ${termsLower.value.checkpoint}`;
  }
  return `Add ${nextTab.value.label.toLowerCase()}...`;
});

watch(() => props.location, (newVal) => {
  if (newVal) {
    // Convert UTC times to local datetime strings for datetime-local inputs
    let startTimeLocal = '';
    let endTimeLocal = '';

    if (newVal.startTime) {
      startTimeLocal = formatDateForInput(newVal.startTime);
    }

    if (newVal.endTime) {
      endTimeLocal = formatDateForInput(newVal.endTime);
    }

    form.value = {
      name: newVal.name || '',
      description: newVal.description || '',
      what3Words: newVal.what3Words || '',
      latitude: newVal.latitude || 0,
      longitude: newVal.longitude || 0,
      requiredMarshals: newVal.requiredMarshals || 1,
      useCustomTimes: !!(newVal.startTime || newVal.endTime),
      startTime: startTimeLocal,
      endTime: endTimeLocal,
      areaIds: newVal.areaIds || newVal.AreaIds || [],
      styleType: newVal.styleType || 'default',
      styleColor: newVal.styleColor || '',
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    isMoving.value = false;
    checklistChanges.value = [];
    pendingNewChecklistItems.value = []; // Clear pending new checklist items
    pendingNewNotes.value = []; // Clear pending new notes
    // Reset form when opening for a new location (preserve coordinates from props.location)
    if (!isExistingLocation.value) {
      form.value = {
        name: props.location?.name || '',
        description: props.location?.description || '',
        what3Words: props.location?.what3Words || '',
        latitude: props.location?.latitude || 0,
        longitude: props.location?.longitude || 0,
        requiredMarshals: props.location?.requiredMarshals || 1,
        useCustomTimes: false,
        startTime: '',
        endTime: '',
        areaIds: props.location?.areaIds || [],
        styleType: 'default',
        styleColor: '',
      };
    }
    // Clear pending changes in assignments tab if it exists
    if (assignmentsTabRef.value?.clearPendingChanges) {
      assignmentsTabRef.value.clearPendingChanges();
    }
    // Clear pending changes in checklist tab if it exists
    if (checklistTabRef.value?.resetLocalState) {
      checklistTabRef.value.resetLocalState();
    }
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleMoveLocation = () => {
  isMoving.value = !isMoving.value;
  // Emit the current form data so it can be preserved during the move
  emit('move-location', { isMoving: isMoving.value, formData: { ...form.value } });
};

const handleChecklistChange = (changes) => {
  checklistChanges.value = changes;
  handleInput();
};

const handlePrimaryAction = () => {
  if (isExistingLocation.value) {
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
  // Convert local datetime strings back to UTC DateTime objects
  const formData = {
    ...form.value,
    startTime: form.value.useCustomTimes && form.value.startTime
      ? new Date(form.value.startTime).toISOString()
      : null,
    endTime: form.value.useCustomTimes && form.value.endTime
      ? new Date(form.value.endTime).toISOString()
      : null,
    // Include pending check-in changes from assignments tab
    checkInChanges: assignmentsTabRef.value?.getPendingChanges?.() || [],
    // Include pending checklist changes
    checklistChanges: checklistChanges.value || [],
    // Include pending assignments for new locations (filter from props.assignments where isPending)
    pendingAssignments: props.assignments
      .filter(a => a.isPending)
      .map(a => ({ marshalId: a.marshalId })),
    // Include pending new checklist items and notes (for both create and edit modes)
    pendingNewChecklistItems: pendingNewChecklistItems.value,
    pendingNewNotes: pendingNewNotes.value,
  };

  emit('save', formData);
};

const handleDelete = () => {
  if (props.location && props.location.id) {
    emit('delete', props.location.id);
  }
};

const handleRemoveAssignment = (assignment) => {
  emit('remove-assignment', assignment);
};

const handleAssignMarshal = (marshalId) => {
  emit('assign-marshal', marshalId);
};

const handleClose = () => {
  emit('close');
};

const handleStyleInput = (field, value) => {
  form.value[field] = value;
  handleInput();
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

.appearance-tab {
  padding-top: 0.5rem;
}

.section-description {
  color: #666;
  font-size: 0.9rem;
  margin: 0 0 1.5rem 0;
}
</style>
