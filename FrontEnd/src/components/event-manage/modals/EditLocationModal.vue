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
      :area-term-singular="effectiveAreaTermSingular"
      :area-term-plural="effectiveAreaTermPlural"
      :checkpoint-term-singular="effectiveCheckpointTermSingular"
      :people-term-plural="effectivePeopleTerm"
      @update:form="form = $event"
      @input="handleInput"
      @save="handleSave"
    />

    <!-- Location Tab -->
    <LocationCoordinatesTab
      v-if="activeTab === 'location'"
      :form="form"
      :is-moving="isMoving"
      :checkpoint-term-singular="effectiveCheckpointTermSingular"
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
      :people-term="effectivePeopleTerm"
      :person-term="effectivePeopleTermSingular"
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
      :checkpoint-term-singular="effectiveCheckpointTermSingular"
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
      :checkpoint-term-singular="effectiveCheckpointTermSingular"
      @change="handleInput"
    />

    <!-- Appearance Tab -->
    <div v-if="activeTab === 'appearance'" class="tab-content appearance-tab">
      <!-- Icon style accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.iconStyle }"
          @click="toggleSection('iconStyle')"
        >
          <div class="accordion-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z" fill="#10B981"/>
              <circle cx="12" cy="9" r="2.5" fill="white"/>
            </svg>
          </div>
          <span class="accordion-title">{{ effectiveCheckpointTermSingular }} icon style</span>
          <span class="accordion-arrow">{{ expandedSections.iconStyle ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.iconStyle" class="accordion-content">
          <p class="section-description">
            Choose a custom icon for this {{ effectiveCheckpointTermSingularLower }}, or leave as Default to inherit from {{ effectiveAreaTermSingularLower }} or event settings.
          </p>
          <CheckpointStylePicker
            :style-type="form.styleType || 'default'"
            :style-color="form.styleColor || ''"
            :style-background-shape="form.styleBackgroundShape || ''"
            :style-background-color="form.styleBackgroundColor || ''"
            :style-border-color="form.styleBorderColor || ''"
            :style-icon-color="form.styleIconColor || ''"
            :style-size="form.styleSize || ''"
            :inherited-style-type="inheritedStyle.type"
            :inherited-style-color="inheritedStyle.color"
            :inherited-background-shape="inheritedStyle.backgroundShape || ''"
            :inherited-background-color="inheritedStyle.backgroundColor || ''"
            :inherited-border-color="inheritedStyle.borderColor || ''"
            :inherited-icon-color="inheritedStyle.iconColor || ''"
            :inherited-size="inheritedStyle.size || ''"
            :default-label="defaultStyleLabel"
            icon-label="Icon style"
            level="checkpoint"
            :show-preview="true"
            @update:style-type="handleStyleInput('styleType', $event)"
            @update:style-color="handleStyleInput('styleColor', $event)"
            @update:style-background-shape="handleStyleInput('styleBackgroundShape', $event)"
            @update:style-background-color="handleStyleInput('styleBackgroundColor', $event)"
            @update:style-border-color="handleStyleInput('styleBorderColor', $event)"
            @update:style-icon-color="handleStyleInput('styleIconColor', $event)"
            @update:style-size="handleStyleInput('styleSize', $event)"
          />
        </div>
      </div>

      <!-- People terminology accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.peopleTerm }"
          @click="toggleSection('peopleTerm')"
        >
          <div class="accordion-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
              <circle cx="12" cy="8" r="4" fill="#6366F1"/>
              <path d="M4 20c0-4.4 3.6-8 8-8s8 3.6 8 8" fill="#6366F1" opacity="0.6"/>
            </svg>
          </div>
          <span class="accordion-title">{{ effectivePeopleTerm }} terminology</span>
          <span class="accordion-preview">{{ form.peopleTerm || `${inheritedPeopleTerm} (${effectiveAreaTermSingularLower} default)` }}</span>
          <span class="accordion-arrow">{{ expandedSections.peopleTerm ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.peopleTerm" class="accordion-content">
          <p class="section-description">
            Choose what {{ effectivePeopleTerm.toLowerCase() }} are called at this {{ effectiveCheckpointTermSingularLower }}.
          </p>
          <div class="form-group">
            <label>What are {{ effectivePeopleTerm.toLowerCase() }} called?</label>
            <select
              v-model="form.peopleTerm"
              class="form-input"
              @change="handleInput"
            >
              <option value="">Use {{ effectiveAreaTermSingularLower }} default ({{ inheritedPeopleTerm }})</option>
              <option
                v-for="option in terminologyOptions.people"
                :key="option"
                :value="option"
              >
                {{ option }}
              </option>
            </select>
          </div>
        </div>
      </div>
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
          Delete {{ effectiveCheckpointTermSingularLower }}
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
import { useTerminology, terminologyOptions, getSingularTerm, getPluralTerm } from '../../../composables/useTerminology';

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
  eventDefaultStyleBackgroundShape: {
    type: String,
    default: '',
  },
  eventDefaultStyleBackgroundColor: {
    type: String,
    default: '',
  },
  eventDefaultStyleBorderColor: {
    type: String,
    default: '',
  },
  eventDefaultStyleIconColor: {
    type: String,
    default: '',
  },
  eventDefaultStyleSize: {
    type: String,
    default: '',
  },
  eventPeopleTerm: {
    type: String,
    default: 'Marshals',
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
  styleBackgroundShape: '',
  styleBackgroundColor: '',
  styleBorderColor: '',
  styleIconColor: '',
  styleSize: '',
  peopleTerm: '',
});

// Accordion expanded state for appearance tab - with 2+ sections, all start collapsed
const expandedSections = ref({
  iconStyle: false,
  peopleTerm: false,
});

const toggleSection = (section) => {
  expandedSections.value[section] = !expandedSections.value[section];
};

// Get inherited people term from area hierarchy
const inheritedPeopleTerm = computed(() => {
  const areaIds = form.value.areaIds || [];
  if (areaIds.length > 0) {
    // Find areas with custom people term, prefer the one with fewest checkpoints
    const termedAreas = props.areas
      .filter(a => areaIds.includes(a.id) && a.peopleTerm)
      .sort((a, b) => (a.checkpointCount || 0) - (b.checkpointCount || 0));

    if (termedAreas.length > 0) {
      return termedAreas[0].peopleTerm;
    }
  }
  return props.eventPeopleTerm || 'Marshals';
});

// Get effective people term (checkpoint's own term or inherited)
const effectivePeopleTerm = computed(() => {
  // Use checkpoint's own term if set
  if (form.value.peopleTerm) {
    return form.value.peopleTerm;
  }
  // Otherwise use inherited term
  return inheritedPeopleTerm.value;
});

// Get effective checkpoint term from area hierarchy
const effectiveCheckpointTerm = computed(() => {
  const areaIds = form.value.areaIds || [];
  if (areaIds.length > 0) {
    // Find areas with custom checkpoint term, prefer the one with fewest checkpoints
    const termedAreas = props.areas
      .filter(a => areaIds.includes(a.id) && a.checkpointTerm)
      .sort((a, b) => (a.checkpointCount || 0) - (b.checkpointCount || 0));

    if (termedAreas.length > 0) {
      return termedAreas[0].checkpointTerm;
    }
  }
  // Fall back to event's checkpoint term via global terminology
  return terms.value.checkpoint;
});

// Lowercase versions of effective terms
const effectiveCheckpointTermLower = computed(() => effectiveCheckpointTerm.value.toLowerCase());

// Singular form of checkpoint term
const effectiveCheckpointTermSingular = computed(() => {
  // Get the stored checkpoint term value to look up in mappings
  const areaIds = form.value.areaIds || [];
  let storedTerm = null;
  if (areaIds.length > 0) {
    const termedAreas = props.areas
      .filter(a => areaIds.includes(a.id) && a.checkpointTerm)
      .sort((a, b) => (a.checkpointCount || 0) - (b.checkpointCount || 0));
    if (termedAreas.length > 0) {
      storedTerm = termedAreas[0].checkpointTerm;
    }
  }
  // If no area term, the effectiveCheckpointTerm will be from global terms
  if (!storedTerm) {
    return terms.value.checkpoint; // This is already singular from useTerminology
  }
  return getSingularTerm('checkpoint', storedTerm, effectivePeopleTerm.value);
});

const effectiveCheckpointTermSingularLower = computed(() => effectiveCheckpointTermSingular.value.toLowerCase());

// Singular form of people term
const effectivePeopleTermSingular = computed(() => {
  const term = effectivePeopleTerm.value;
  return getSingularTerm('people', term);
});

const effectivePeopleTermSingularLower = computed(() => effectivePeopleTermSingular.value.toLowerCase());

// Get effective area term from area hierarchy (for display purposes)
const effectiveAreaTerm = computed(() => {
  // For now, use global area term since areas don't have their own area terminology
  return terms.value.area;
});

const effectiveAreaTermLower = computed(() => effectiveAreaTerm.value.toLowerCase());
const effectiveAreaTermSingular = computed(() => terms.value.area); // singular from useTerminology
const effectiveAreaTermSingularLower = computed(() => effectiveAreaTermSingular.value.toLowerCase());
const effectiveAreaTermPlural = computed(() => terms.value.areas); // plural from useTerminology

// Computed properties for add vs edit mode
const isExistingLocation = computed(() => {
  return props.location && props.location.id;
});

const modalTitle = computed(() => {
  if (isExistingLocation.value) {
    return `Edit ${effectiveCheckpointTermSingularLower.value}: ${props.location?.name || ''}`;
  }
  return `Add ${effectiveCheckpointTermSingularLower.value}`;
});

const availableTabs = computed(() => [
  { value: 'details', label: 'Details', icon: 'details' },
  { value: 'location', label: 'Location', icon: 'location' },
  { value: 'marshals', label: effectivePeopleTerm.value, icon: 'marshal' },
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
      const area = styledAreas[0];
      return {
        type: area.checkpointStyleType || area.CheckpointStyleType,
        color: area.checkpointStyleColor || area.CheckpointStyleColor || '',
        backgroundShape: area.checkpointStyleBackgroundShape || area.CheckpointStyleBackgroundShape || '',
        backgroundColor: area.checkpointStyleBackgroundColor || area.CheckpointStyleBackgroundColor || '',
        borderColor: area.checkpointStyleBorderColor || area.CheckpointStyleBorderColor || '',
        iconColor: area.checkpointStyleIconColor || area.CheckpointStyleIconColor || '',
        size: area.checkpointStyleSize || area.CheckpointStyleSize || '',
        source: 'area',
      };
    }
  }

  // Fall back to event default
  return {
    type: props.eventDefaultStyleType || 'default',
    color: props.eventDefaultStyleColor || '',
    backgroundShape: props.eventDefaultStyleBackgroundShape || '',
    backgroundColor: props.eventDefaultStyleBackgroundColor || '',
    borderColor: props.eventDefaultStyleBorderColor || '',
    iconColor: props.eventDefaultStyleIconColor || '',
    size: props.eventDefaultStyleSize || '',
    source: 'event',
  };
});

// Label for the default option - depends on whether area has a custom style
const defaultStyleLabel = computed(() => {
  if (inheritedStyle.value.source === 'area') {
    return `${effectiveAreaTermSingular.value} default`;
  }
  return 'Event default';
});

const nextTab = computed(() => {
  if (isLastTab.value) return null;
  return availableTabs.value[currentTabIndex.value + 1];
});

// Button text for create mode - shows "Add [next tab]..." or "Create [checkpoint]"
const createButtonText = computed(() => {
  if (isLastTab.value) {
    return `Create ${effectiveCheckpointTermSingularLower.value}`;
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
      styleType: newVal.styleType || newVal.StyleType || 'default',
      styleColor: newVal.styleColor || newVal.StyleColor || '',
      styleBackgroundShape: newVal.styleBackgroundShape || newVal.StyleBackgroundShape || '',
      styleBackgroundColor: newVal.styleBackgroundColor || newVal.StyleBackgroundColor || '',
      styleBorderColor: newVal.styleBorderColor || newVal.StyleBorderColor || '',
      styleIconColor: newVal.styleIconColor || newVal.StyleIconColor || '',
      styleSize: newVal.styleSize || newVal.StyleSize || '',
      peopleTerm: newVal.peopleTerm || '',
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
        styleBackgroundShape: '',
        styleBackgroundColor: '',
        styleBorderColor: '',
        styleIconColor: '',
        styleSize: '',
        peopleTerm: '',
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
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.section-description {
  color: #666;
  font-size: 0.9rem;
  margin: 0 0 1rem 0;
}

.form-group {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: #333;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

/* Accordion styles */
.accordion-item {
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  overflow: hidden;
}

.accordion-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.875rem 1rem;
  background: #f8f9fa;
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background-color 0.2s;
}

.accordion-header:hover {
  background: #e9ecef;
}

.accordion-header.expanded {
  background: #e7f3ff;
  border-bottom: 1px solid #e2e8f0;
}

.accordion-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.accordion-title {
  font-weight: 500;
  color: #333;
  font-size: 0.95rem;
}

.accordion-preview {
  flex: 1;
  text-align: right;
  color: #666;
  font-size: 0.85rem;
  font-weight: normal;
  margin-right: 0.5rem;
}

.accordion-arrow {
  color: #666;
  font-size: 0.75rem;
  flex-shrink: 0;
}

.accordion-content {
  padding: 1rem;
  background: white;
}
</style>
