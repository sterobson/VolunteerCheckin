<template>
  <BaseModal
    :show="show"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    :z-index="zIndex"
    @close="handleClose"
  >
    <!-- Custom header with icon -->
    <template #header>
      <h2 class="base-modal-title modal-title-with-icon">
        <span v-if="titleIconSvg" class="modal-title-icon" v-html="titleIconSvg"></span>
        {{ modalTitle }}
      </h2>
    </template>

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
      @update:form="updateForm"
      @input="handleInput"
      @save="handleSave"
    />

    <!-- Location Tab -->
    <LocationCoordinatesTab
      v-if="activeTab === 'location'"
      :form="form"
      :is-moving="isMoving"
      :checkpoint-term-singular="effectiveCheckpointTermSingular"
      :people-term-plural="effectivePeopleTerm"
      :areas="areas"
      :locations="allLocations"
      :marshals="availableMarshals"
      :is-editing="isExistingLocation"
      @update:form="updateForm"
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
      :people-term="effectivePeopleTerm"
      :person-term="effectivePeopleTermSingular"
      @update:form="updateForm"
      @input="handleInput"
      @remove-assignment="handleRemoveAssignment"
      @open-assign-modal="handleOpenAssignModal"
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
      <!-- Marker style accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.iconStyle }"
          @click="toggleSection('iconStyle')"
        >
          <div class="accordion-icon" v-html="accordionIconPreview"></div>
          <span class="accordion-title">{{ effectiveCheckpointTermSingular }} marker style</span>
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
            :style-map-rotation="form.styleMapRotation ?? ''"
            :inherited-style-type="inheritedStyle.type"
            :inherited-style-color="inheritedStyle.color"
            :inherited-background-shape="inheritedStyle.backgroundShape || ''"
            :inherited-background-color="inheritedStyle.backgroundColor || inheritedStyle.color || ''"
            :inherited-border-color="inheritedStyle.borderColor || ''"
            :inherited-icon-color="inheritedStyle.iconColor || ''"
            :inherited-size="inheritedStyle.size || ''"
            :inherited-map-rotation="inheritedStyle.mapRotation ?? ''"
            :default-label="defaultStyleLabel"
            icon-label="Marker style"
            level="checkpoint"
            :show-preview="true"
            @update:style-type="handleStyleInput('styleType', $event)"
            @update:style-color="handleStyleInput('styleColor', $event)"
            @update:style-background-shape="handleStyleInput('styleBackgroundShape', $event)"
            @update:style-background-color="handleStyleInput('styleBackgroundColor', $event)"
            @update:style-border-color="handleStyleInput('styleBorderColor', $event)"
            @update:style-icon-color="handleStyleInput('styleIconColor', $event)"
            @update:style-size="handleStyleInput('styleSize', $event)"
            @update:style-map-rotation="handleStyleInput('styleMapRotation', $event)"
          />
        </div>
      </div>

      <!-- Terminology accordion (combined people and checkpoint terms) -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.terminology }"
          @click="toggleSection('terminology')"
        >
          <div class="accordion-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
              <circle cx="9" cy="8" r="3" fill="#6366F1"/>
              <path d="M3 18c0-3.3 2.7-6 6-6s6 2.7 6 6" fill="#6366F1" opacity="0.5"/>
              <circle cx="17" cy="14" r="5" fill="#10B981" opacity="0.3"/>
              <circle cx="17" cy="14" r="3" fill="#10B981"/>
              <circle cx="17" cy="14" r="1" fill="white"/>
            </svg>
          </div>
          <span class="accordion-title">Terminology</span>
          <span class="accordion-preview">{{ terminologyPreview }}</span>
          <span class="accordion-arrow">{{ expandedSections.terminology ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.terminology" class="accordion-content">
          <p class="section-description">
            Customise what this {{ effectiveCheckpointTermSingularLower }} and its {{ effectivePeopleTerm.toLowerCase() }} are called.
          </p>

          <div class="terminology-row">
            <div class="terminology-field">
              <label>What is this {{ effectiveCheckpointTermSingularLower }} called?</label>
              <select
                :value="form.checkpointTerm"
                class="form-input"
                @change="updateForm({ ...form, checkpointTerm: $event.target.value }); handleInput()"
              >
                <option value="">Use {{ effectiveAreaTermSingularLower }} default ({{ effectiveCheckpointTermSingular }})</option>
                <option value="Vehicle">Vehicle</option>
                <option value="Car">Car</option>
                <option value="Bike">Bike</option>
                <option
                  v-for="option in terminologyOptions.checkpoint"
                  :key="option"
                  :value="option"
                >
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="terminology-field">
              <label>What are {{ effectivePeopleTerm.toLowerCase() }} called here?</label>
              <select
                :value="form.peopleTerm"
                class="form-input"
                @change="updateForm({ ...form, peopleTerm: $event.target.value }); handleInput()"
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

      </div>

    <!-- Incidents Tab (only shown if there are incidents) -->
    <div v-if="activeTab === 'incidents'" class="tab-content incidents-tab">
      <div class="incidents-list">
        <IncidentCard
          v-for="incident in incidents"
          :key="incident.incidentId"
          :incident="incident"
          @select="$emit('select-incident', $event)"
        />
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
import { ref, computed, defineProps, defineEmits, watch, onMounted } from 'vue';
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
import IncidentCard from '../../IncidentCard.vue';
import { generateCheckpointSvg } from '../../../constants/checkpointIcons';
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
  eventDefaultStyleMapRotation: {
    type: [String, Number],
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
  incidents: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'move-location',
  'toggle-check-in',
  'remove-assignment',
  'open-assign-modal',
  'update:isDirty',
  'select-incident',
]);

const activeTab = ref('details');
const isMoving = ref(false);
const assignmentsTabRef = ref(null);
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const pendingNewChecklistItems = ref([]); // For creating new checklist items scoped to this location
const pendingNewNotes = ref([]); // For creating new notes scoped to this location
const form = ref({
  id: null, // null for new checkpoints, set to actual ID when editing
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
  styleMapRotation: '',
  peopleTerm: '',
  checkpointTerm: '',
  isDynamic: false,
  locationUpdateScopeConfigurations: [],
});

// Accordion expanded state for appearance tab - with persistence
const ACCORDION_STORAGE_KEY = 'edit_location_accordion';
const getAccordionStorageKey = () => props.eventId ? `${ACCORDION_STORAGE_KEY}_${props.eventId}` : ACCORDION_STORAGE_KEY;

const expandedSections = ref({
  iconStyle: false,
  terminology: false,
});

// Load saved accordion state on mount
onMounted(() => {
  const saved = localStorage.getItem(getAccordionStorageKey());
  if (saved) {
    try {
      const parsed = JSON.parse(saved);
      expandedSections.value = { ...expandedSections.value, ...parsed };
    } catch (e) {
      // Ignore parse errors
    }
  }
});

const toggleSection = (section) => {
  expandedSections.value[section] = !expandedSections.value[section];
  // Persist accordion state
  localStorage.setItem(getAccordionStorageKey(), JSON.stringify(expandedSections.value));
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

// Singular form of checkpoint term - checks checkpoint's own term first, then area, then event
const effectiveCheckpointTermSingular = computed(() => {
  // First check if the checkpoint itself has a custom term
  const checkpointOwnTerm = form.value.checkpointTerm || props.location?.checkpointTerm || props.location?.CheckpointTerm;
  if (checkpointOwnTerm) {
    return getSingularTerm('checkpoint', checkpointOwnTerm, effectivePeopleTerm.value);
  }

  // Then check area terms
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
  return !!(props.location && props.location.id);
});

const modalTitle = computed(() => {
  if (isExistingLocation.value) {
    return `Edit ${effectiveCheckpointTermSingularLower.value}: ${props.location?.name || ''}`;
  }
  return `Add ${effectiveCheckpointTermSingularLower.value}`;
});

const availableTabs = computed(() => {
  const tabs = [
    { value: 'details', label: 'Details', icon: 'details' },
    { value: 'location', label: 'Location', icon: 'location' },
    { value: 'marshals', label: effectivePeopleTerm.value, icon: 'marshal' },
    { value: 'checklists', label: terms.value.checklists, icon: 'checklist' },
    { value: 'notes', label: 'Notes', icon: 'notes' },
    { value: 'appearance', label: 'Appearance', icon: 'appearance' },
  ];
  // Only show incidents tab if there are incidents
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

// Calculate inherited style from area or event - each property resolves independently
const inheritedStyle = computed(() => {
  const areaIds = form.value.areaIds || [];

  // Get matching areas sorted by checkpoint count (prefer smaller/more specific areas)
  const matchingAreas = props.areas
    .filter(a => areaIds.includes(a.id))
    .sort((a, b) => (a.checkpointCount || 0) - (b.checkpointCount || 0));

  // Helper to resolve a single property through the hierarchy: area → event
  const resolveProperty = (areaGetter, eventValue) => {
    // Check each matching area for this property
    for (const area of matchingAreas) {
      const areaValue = areaGetter(area);
      if (areaValue && areaValue !== 'default') {
        return { value: areaValue, source: 'area' };
      }
    }
    // Fall back to event default
    if (eventValue && eventValue !== 'default') {
      return { value: eventValue, source: 'event' };
    }
    return { value: '', source: 'none' };
  };

  // Resolve each property independently
  const type = resolveProperty(
    a => a.checkpointStyleType || a.CheckpointStyleType,
    props.eventDefaultStyleType
  );
  const color = resolveProperty(
    a => a.checkpointStyleColor || a.CheckpointStyleColor,
    props.eventDefaultStyleColor
  );
  const backgroundShape = resolveProperty(
    a => a.checkpointStyleBackgroundShape || a.CheckpointStyleBackgroundShape,
    props.eventDefaultStyleBackgroundShape
  );
  const backgroundColor = resolveProperty(
    a => a.checkpointStyleBackgroundColor || a.CheckpointStyleBackgroundColor || a.checkpointStyleColor || a.CheckpointStyleColor,
    props.eventDefaultStyleBackgroundColor || props.eventDefaultStyleColor
  );
  const borderColor = resolveProperty(
    a => a.checkpointStyleBorderColor || a.CheckpointStyleBorderColor,
    props.eventDefaultStyleBorderColor
  );
  const iconColor = resolveProperty(
    a => a.checkpointStyleIconColor || a.CheckpointStyleIconColor,
    props.eventDefaultStyleIconColor
  );
  const size = resolveProperty(
    a => a.checkpointStyleSize || a.CheckpointStyleSize,
    props.eventDefaultStyleSize
  );
  const mapRotation = resolveProperty(
    a => a.checkpointStyleMapRotation ?? a.CheckpointStyleMapRotation,
    props.eventDefaultStyleMapRotation
  );

  // Determine overall source (for label display)
  const sources = [type, backgroundShape, backgroundColor, borderColor, iconColor, size, mapRotation];
  const hasAreaSource = sources.some(s => s.source === 'area');

  return {
    type: type.value || 'default',
    color: color.value,
    backgroundShape: backgroundShape.value,
    backgroundColor: backgroundColor.value,
    borderColor: borderColor.value,
    iconColor: iconColor.value,
    size: size.value,
    mapRotation: mapRotation.value,
    source: hasAreaSource ? 'area' : (type.value ? 'event' : 'none'),
  };
});

// Label for the default option - depends on whether area has a custom style
const defaultStyleLabel = computed(() => {
  if (inheritedStyle.value.source === 'area') {
    return `${effectiveAreaTermSingular.value} default`;
  }
  return 'Event default';
});

// Generate preview SVG for the accordion header - shows current or inherited style
const accordionIconPreview = computed(() => {
  // Determine effective values - checkpoint's own value or inherited
  const styleType = form.value.styleType && form.value.styleType !== 'default' && form.value.styleType !== 'custom'
    ? form.value.styleType
    : inheritedStyle.value.type;

  const backgroundShape = form.value.styleBackgroundShape || inheritedStyle.value.backgroundShape || 'circle';
  const backgroundColor = form.value.styleBackgroundColor || inheritedStyle.value.backgroundColor || '#667eea';
  const borderColor = form.value.styleBorderColor || inheritedStyle.value.borderColor || '#ffffff';
  const iconColor = form.value.styleIconColor || inheritedStyle.value.iconColor || '#ffffff';

  return generateCheckpointSvg({
    type: styleType || 'circle',
    backgroundShape: backgroundShape,
    backgroundColor: backgroundColor,
    borderColor: borderColor,
    iconColor: iconColor,
    size: '100',
  });
});

// Generate icon SVG for the modal title
const titleIconSvg = computed(() => {
  // For existing checkpoints, use the resolved style from props
  // For new checkpoints, use form values with inherited fallbacks
  let styleType, backgroundShape, backgroundColor, borderColor, iconColor;

  if (isExistingLocation.value && props.location) {
    // Use resolved style from the checkpoint (from backend)
    styleType = props.location.resolvedStyleType || props.location.ResolvedStyleType;
    backgroundShape = props.location.resolvedStyleBackgroundShape || props.location.ResolvedStyleBackgroundShape || 'circle';
    backgroundColor = props.location.resolvedStyleBackgroundColor || props.location.ResolvedStyleBackgroundColor
      || props.location.resolvedStyleColor || props.location.ResolvedStyleColor || '#667eea';
    borderColor = props.location.resolvedStyleBorderColor || props.location.ResolvedStyleBorderColor || '#ffffff';
    iconColor = props.location.resolvedStyleIconColor || props.location.ResolvedStyleIconColor || '#ffffff';
  } else {
    // New checkpoint - use form values or inherited
    styleType = form.value.styleType && form.value.styleType !== 'default' && form.value.styleType !== 'custom'
      ? form.value.styleType
      : inheritedStyle.value.type;
    backgroundShape = form.value.styleBackgroundShape || inheritedStyle.value.backgroundShape || 'circle';
    backgroundColor = form.value.styleBackgroundColor || inheritedStyle.value.backgroundColor || '#667eea';
    borderColor = form.value.styleBorderColor || inheritedStyle.value.borderColor || '#ffffff';
    iconColor = form.value.styleIconColor || inheritedStyle.value.iconColor || '#ffffff';
  }

  return generateCheckpointSvg({
    type: styleType || 'circle',
    backgroundShape: backgroundShape,
    backgroundColor: backgroundColor,
    borderColor: borderColor,
    iconColor: iconColor,
    size: '100',
    outputSize: 28,
  });
});

// Preview text for terminology accordion
const terminologyPreview = computed(() => {
  const hasCheckpointTerm = !!form.value.checkpointTerm;
  const hasPeopleTerm = !!form.value.peopleTerm;

  if (hasCheckpointTerm && hasPeopleTerm) {
    // Show both custom terms
    const checkpointDisplay = getSingularTerm('checkpoint', form.value.checkpointTerm, form.value.peopleTerm);
    return `${checkpointDisplay}, ${form.value.peopleTerm}`;
  } else if (hasCheckpointTerm) {
    // Only checkpoint term is custom
    const checkpointDisplay = getSingularTerm('checkpoint', form.value.checkpointTerm, effectivePeopleTerm.value);
    return checkpointDisplay;
  } else if (hasPeopleTerm) {
    // Only people term is custom
    return form.value.peopleTerm;
  }
  // Both using defaults
  return 'Using defaults';
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

// Track whether form has been initialized for current location
const formInitialized = ref(false);
const currentLocationId = ref(null);

watch(() => props.location, (newVal) => {
  if (newVal) {
    // Only reset form if this is a different location than we've already initialized
    const locationId = newVal.id || 'new';
    if (currentLocationId.value !== locationId) {
      currentLocationId.value = locationId;
      formInitialized.value = true;

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
        id: newVal.id || null, // Set ID for existing checkpoints, null for new
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
        styleMapRotation: newVal.styleMapRotation ?? newVal.StyleMapRotation ?? '',
        peopleTerm: newVal.peopleTerm || '',
        checkpointTerm: newVal.checkpointTerm || '',
        isDynamic: newVal.isDynamic || false,
        locationUpdateScopeConfigurations: newVal.locationUpdateScopeConfigurations || [],
      };
    }
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    isMoving.value = false;
    checklistChanges.value = [];
    pendingNewChecklistItems.value = [];
    pendingNewNotes.value = [];
    // Clear pending changes in assignments tab if it exists
    if (assignmentsTabRef.value?.clearPendingChanges) {
      assignmentsTabRef.value.clearPendingChanges();
    }
    // Clear pending changes in checklist tab if it exists
    if (checklistTabRef.value?.resetLocalState) {
      checklistTabRef.value.resetLocalState();
    }
  } else {
    // Reset tracking when modal closes so next open re-initializes form
    currentLocationId.value = null;
    formInitialized.value = false;
  }
});

const updateForm = (newFormData) => {
  form.value = newFormData;
};

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

const handleOpenAssignModal = () => {
  emit('open-assign-modal');
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
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

.placeholder-message {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 150px;
  color: var(--text-secondary);
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
  color: var(--text-secondary);
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
  color: var(--text-dark);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

/* Accordion styles */
.accordion-item {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  overflow: hidden;
}

.accordion-header {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  width: 100%;
  padding: 0.875rem 1rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  text-align: left;
  transition: background-color 0.2s;
}

.accordion-header:hover {
  background: var(--bg-hover);
}

.accordion-header.expanded {
  background: var(--bg-active);
  border-bottom: 1px solid var(--border-color);
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
  color: var(--text-dark);
  font-size: 0.95rem;
}

.accordion-preview {
  flex: 1;
  text-align: right;
  color: var(--text-secondary);
  font-size: 0.85rem;
  font-weight: normal;
  margin-right: 0.5rem;
}

.accordion-arrow {
  color: var(--text-secondary);
  font-size: 0.75rem;
  flex-shrink: 0;
}

.accordion-content {
  padding: 1rem;
  background: var(--card-bg);
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  cursor: pointer;
  font-weight: normal;
  padding: 0.5rem 0;
}

.checkbox-label input[type="checkbox"] {
  width: 1.125rem;
  height: 1.125rem;
  cursor: pointer;
  flex-shrink: 0;
  margin: 0;
}

.checkbox-label span {
  line-height: 1.4;
}

/* Terminology dropdowns layout */
.terminology-row {
  display: grid;
  grid-template-columns: 1fr;
  gap: 1.25rem;
}

@media (min-width: 500px) {
  .terminology-row {
    grid-template-columns: 1fr 1fr;
  }
}

.terminology-field {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.terminology-field label {
  font-weight: 500;
  color: var(--text-dark);
  font-size: 0.9rem;
}

/* Modal title with icon */
.modal-title-with-icon {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin: 0;
  padding-right: 3rem;
  color: var(--text-dark);
}

.modal-title-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.incidents-tab {
  padding-top: 0.5rem;
}

.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
</style>
