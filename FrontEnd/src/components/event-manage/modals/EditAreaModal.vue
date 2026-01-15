<template>
  <BaseModal
    :show="show"
    :title="area && area.id ? `Edit ${termsLower.area}: ${area.name}` : `Create new ${termsLower.area}`"
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
    <div v-if="activeTab === 'details'" class="tab-content">
      <form @submit.prevent="handleSave">
        <div class="form-group">
          <label>Name *</label>
          <input
            v-model="form.name"
            type="text"
            required
            class="form-input"
            :disabled="form.isDefault"
            @input="handleInput"
          />
        </div>

        <div class="form-group">
          <label>Description</label>
          <textarea
            v-model="form.description"
            class="form-input"
            rows="3"
            @input="handleInput"
          ></textarea>
        </div>

      </form>
    </div>

    <!-- Boundary Tab -->
    <div v-if="activeTab === 'boundary'" class="tab-content">
      <div class="boundary-section">
        <!-- Show editable map if boundary exists -->
        <div v-if="form.polygon && form.polygon.length > 0" class="boundary-preview">
          <CommonMap
            mode="edit-polygon"
            :editing-polygon="form.polygon"
            :route="route"
            :locations="checkpoints"
            :areas="otherAreas"
            :all-locations-for-bounds="polygonBoundsLocations"
            height="300px"
            @polygon-update="handlePolygonUpdate"
          />
          <p class="boundary-hint">Drag points to move them. Click a point to delete it (minimum 3 required).</p>
        </div>

        <div class="boundary-actions">
          <button
            v-if="!form.polygon || form.polygon.length === 0"
            @click="handleDrawBoundary"
            class="btn btn-primary btn-full"
          >
            Draw on map
          </button>
          <template v-else>
            <button
              @click="handleDrawBoundary"
              class="btn btn-secondary"
            >
              Redraw
            </button>
            <button
              @click="showClearBoundaryConfirm = true"
              class="btn btn-danger"
            >
              Delete boundary
            </button>
          </template>
        </div>

      </div>
    </div>

    <!-- Clear boundary confirmation modal -->
    <ConfirmModal
      :show="showClearBoundaryConfirm"
      title="Delete boundary"
      message="Are you sure you want to delete this boundary? This cannot be undone."
      confirm-text="Delete"
      :is-danger="true"
      @confirm="confirmClearBoundary"
      @cancel="showClearBoundaryConfirm = false"
    />

    <!-- Contacts Tab -->
    <div v-if="activeTab === 'contacts'" class="tab-content">
      <div class="contacts-header">
        <h3 class="section-title">{{ terms.area }} contacts ({{ displayContacts.length }})</h3>
        <button @click="showAddContactModal = true" class="btn btn-primary btn-small">
          Add contact
        </button>
      </div>

      <!-- Show existing contacts scoped to this area + pending contacts -->
      <ContactsGrid
        :contacts="displayContacts"
        :show-remove-button="true"
        :remove-title="getContactRemoveTitle"
        :empty-message="`No contacts assigned to this ${termsLower.area}.`"
        :empty-hint="`Click 'Add contact' to assign contacts who will be visible to ${termsLower.people} in this ${termsLower.area}.`"
        :allow-reorder="isExistingArea"
        :role-definitions="roleDefinitions"
        @select="$emit('edit-contact', $event)"
        @remove="handleContactRemove"
        @undo-remove="handleUndoRemove"
        @reorder="handleContactsReorder"
      />
    </div>

    <!-- Add Contact Modal -->
    <AddAreaContactModal
      :show="showAddContactModal"
      :area-name="form.name || 'this ' + termsLower.area"
      :area-id="props.area?.id || ''"
      :contacts="props.contacts"
      :marshals="props.marshals"
      :exclude-contact-ids="excludeContactIds"
      @close="showAddContactModal = false"
      @select-contact="handleContactSelected"
      @create-from-marshal="handleCreateFromMarshal"
      @create-new="handleCreateNewContact"
    />

    <!-- Checkpoints Tab (only when editing) -->
    <div v-if="activeTab === 'checkpoints' && isExistingArea" class="tab-content">
      <h3 class="section-title">{{ getAreaCheckpointTerm() }} in {{ area?.name }} ({{ areaCheckpoints.length }})</h3>

      <CheckpointsGrid
        :checkpoints="sortedAreaCheckpoints"
        :areas="areas"
        :empty-message="`No ${getAreaCheckpointTermLower()} in this ${termsLower.area} yet.`"
        @select="$emit('select-checkpoint', $event)"
      />
    </div>

    <!-- Checkpoints placeholder when creating -->
    <div v-if="activeTab === 'checkpoints' && !isExistingArea" class="placeholder-message">
      <p>{{ getAreaCheckpointTerm() }} will be automatically assigned based on boundaries after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Checklists Tab (only when editing) -->
    <CheckpointChecklistView
      v-if="activeTab === 'checklists' && isExistingArea"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :area-id="area.id"
      :locations="allLocations"
      :areas="areas"
      :assignments="areaAssignments"
      :marshals="marshals"
      :allow-reorder="true"
      @change="handleChecklistChange"
      @reorder="handleChecklistsReorder"
    />

    <!-- Add new checklists for this area -->
    <ChecklistPreviewForArea
      v-if="activeTab === 'checklists' && isExistingArea"
      v-model="pendingNewChecklistItems"
      @change="handleInput"
    />

    <!-- Checklists placeholder when creating -->
    <div v-if="activeTab === 'checklists' && !isExistingArea" class="placeholder-message">
      <p>{{ terms.checklists }} can be assigned after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Notes Tab (only when editing) -->
    <NotesView
      v-if="activeTab === 'notes' && isExistingArea"
      :event-id="eventId"
      :area-id="area?.id"
      :all-notes="notes"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
      :marshals="marshals"
      :allow-reorder="true"
      @reorder="handleNotesReorder"
    />

    <!-- Add new notes for this area -->
    <NotesPreviewForArea
      v-if="activeTab === 'notes' && isExistingArea"
      v-model="pendingNewNotes"
      @change="handleInput"
    />

    <!-- Notes placeholder when creating -->
    <div v-if="activeTab === 'notes' && !isExistingArea" class="placeholder-message">
      <p>Notes can be viewed after the {{ termsLower.area }} is created.</p>
    </div>

    <!-- Appearance Tab -->
    <div v-if="activeTab === 'appearance'" class="tab-content appearance-tab">
      <!-- Area polygon colour accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.polygonColor }"
          @click="toggleSection('polygonColor')"
        >
          <div class="accordion-icon" v-html="polygonPreviewSvg"></div>
          <span class="accordion-title">{{ terms.area }} polygon colour</span>
          <span class="accordion-arrow">{{ expandedSections.polygonColor ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.polygonColor" class="accordion-content">
          <p class="section-description">
            Choose a colour for this {{ termsLower.area }}'s boundary on the map.
          </p>
          <div class="color-picker">
            <div
              v-for="colorOption in AREA_COLORS"
              :key="colorOption.hex"
              class="color-swatch"
              :style="{ backgroundColor: colorOption.hex }"
              :class="{ selected: form.color === colorOption.hex }"
              :title="colorOption.name"
              @click="selectColor(colorOption.hex)"
            />
          </div>
        </div>
      </div>

      <!-- Checkpoint marker style accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.iconStyle }"
          @click="toggleSection('iconStyle')"
        >
          <div class="accordion-icon" v-html="iconStylePreviewSvg"></div>
          <span class="accordion-title">{{ getAreaCheckpointTerm(false) }} marker style</span>
          <span class="accordion-arrow">{{ expandedSections.iconStyle ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.iconStyle" class="accordion-content">
          <p class="section-description">
            Set a default marker style for all {{ getAreaCheckpointTermLower() }} in this {{ termsLower.area }}.
            Individual {{ getAreaCheckpointTermLower() }} can override this setting.
          </p>
          <CheckpointStylePicker
            :style-type="form.checkpointStyleType || 'default'"
            :style-color="form.checkpointStyleColor || ''"
            :style-background-shape="form.checkpointStyleBackgroundShape || ''"
            :style-background-color="form.checkpointStyleBackgroundColor || ''"
            :style-border-color="form.checkpointStyleBorderColor || ''"
            :style-icon-color="form.checkpointStyleIconColor || ''"
            :style-size="form.checkpointStyleSize || ''"
            :style-map-rotation="form.checkpointStyleMapRotation ?? ''"
            :inherited-style-type="eventDefaultStyleType"
            :inherited-style-color="eventDefaultStyleColor"
            :inherited-background-shape="eventDefaultStyleBackgroundShape || ''"
            :inherited-background-color="eventDefaultStyleBackgroundColor || eventDefaultStyleColor || ''"
            :inherited-border-color="eventDefaultStyleBorderColor || ''"
            :inherited-icon-color="eventDefaultStyleIconColor || ''"
            :inherited-size="eventDefaultStyleSize || ''"
            :inherited-map-rotation="eventDefaultStyleMapRotation ?? ''"
            default-label="Event default"
            icon-label="Marker style"
            level="area"
            :show-preview="true"
            @update:style-type="handleStyleInput('checkpointStyleType', $event)"
            @update:style-color="handleStyleInput('checkpointStyleColor', $event)"
            @update:style-background-shape="handleStyleInput('checkpointStyleBackgroundShape', $event)"
            @update:style-background-color="handleStyleInput('checkpointStyleBackgroundColor', $event)"
            @update:style-border-color="handleStyleInput('checkpointStyleBorderColor', $event)"
            @update:style-icon-color="handleStyleInput('checkpointStyleIconColor', $event)"
            @update:style-size="handleStyleInput('checkpointStyleSize', $event)"
            @update:style-map-rotation="handleStyleInput('checkpointStyleMapRotation', $event)"
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
          <span class="accordion-title">{{ getEffectivePeopleTerm() }} terminology</span>
          <span class="accordion-preview">{{ form.peopleTerm || `${eventPeopleTerm} (event default)` }}</span>
          <span class="accordion-arrow">{{ expandedSections.peopleTerm ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.peopleTerm" class="accordion-content">
          <p class="section-description">
            Choose what {{ getEffectivePeopleTerm().toLowerCase() }} are called in this {{ termsLower.area }}.
            This will be inherited by {{ getAreaCheckpointTermLower() }} unless they override it.
          </p>
          <div class="form-group">
            <label>What are people called?</label>
            <select
              v-model="form.peopleTerm"
              class="form-input"
              @change="handleInput"
            >
              <option value="">Use event default ({{ eventPeopleTerm }})</option>
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

      <!-- Checkpoint terminology accordion -->
      <div class="accordion-item">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.checkpointTerm }"
          @click="toggleSection('checkpointTerm')"
        >
          <div class="accordion-icon">
            <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7z" fill="#10B981"/>
              <circle cx="12" cy="9" r="2.5" fill="white"/>
            </svg>
          </div>
          <span class="accordion-title">{{ getAreaCheckpointTerm(false) }} terminology</span>
          <span class="accordion-preview">{{ form.checkpointTerm ? getCheckpointOptionLabel(form.checkpointTerm, getEffectivePeopleTerm()) : `${eventCheckpointTerm} (event default)` }}</span>
          <span class="accordion-arrow">{{ expandedSections.checkpointTerm ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.checkpointTerm" class="accordion-content">
          <p class="section-description">
            Choose what {{ getAreaCheckpointTermLower() }} are called in this {{ termsLower.area }}.
          </p>
          <div class="form-group">
            <label>What are {{ getAreaCheckpointTermLower() }} called?</label>
            <select
              v-model="form.checkpointTerm"
              class="form-input"
              @change="handleInput"
            >
              <option value="">Use event default ({{ eventCheckpointTerm }})</option>
              <option
                v-for="option in terminologyOptions.checkpoint"
                :key="option"
                :value="option"
              >
                {{ getCheckpointOptionLabel(option, getEffectivePeopleTerm()) }}
              </option>
            </select>
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
        <div class="footer-left">
          <button
            v-if="isExistingArea && !form.isDefault"
            type="button"
            @click="handleDelete"
            class="btn btn-danger"
          >
            Delete
          </button>
          <button
            v-if="!isExistingArea && !isLastTab"
            type="button"
            @click="goToNextTab"
            class="btn btn-secondary mobile-only"
          >
            {{ nextTabButtonText }}
          </button>
        </div>
        <button type="button" @click="handleSave" class="btn btn-success">
          Save
        </button>
      </div>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch, onMounted } from 'vue';
import BaseModal from '../../BaseModal.vue';
import TabHeader from '../../TabHeader.vue';
import CheckpointChecklistView from '../../CheckpointChecklistView.vue';
import NotesView from '../../NotesView.vue';
import ChecklistPreviewForArea from '../../ChecklistPreviewForArea.vue';
import NotesPreviewForArea from '../../NotesPreviewForArea.vue';
import CheckpointStylePicker from '../../CheckpointStylePicker.vue';
import IncidentCard from '../../IncidentCard.vue';
import ConfirmModal from '../../ConfirmModal.vue';
import CommonMap from '../../common/CommonMap.vue';
import AddAreaContactModal from './AddAreaContactModal.vue';
import ContactsGrid from '../ContactsGrid.vue';
import CheckpointsGrid from '../CheckpointsGrid.vue';
import { AREA_COLORS, DEFAULT_AREA_COLOR, getNextAvailableColor } from '../../../constants/areaColors';
import { checklistApi } from '../../../services/api';
import { useTerminology, terminologyOptions, getCheckpointOptionLabel, getSingularTerm, getPluralTerm } from '../../../composables/useTerminology';
import {
  generateCheckpointSvg,
} from '../../../constants/checkpointIcons';

const { terms, termsLower } = useTerminology();

// Get effective people term for this area (for checkpoint option display)
const getEffectivePeopleTerm = () => {
  if (form.value.peopleTerm) return form.value.peopleTerm;
  return props.eventPeopleTerm || 'Marshals';
};


// Get effective checkpoint term for this area (resolves "Person points" dynamically)
const getAreaCheckpointTerm = (plural = true) => {
  const storedTerm = form.value.checkpointTerm || props.eventCheckpointTerm || 'Checkpoints';
  const peopleTerm = getEffectivePeopleTerm();

  if (plural) {
    return getPluralTerm('checkpoint', storedTerm, peopleTerm);
  } else {
    return getSingularTerm('checkpoint', storedTerm, peopleTerm);
  }
};

const getAreaCheckpointTermLower = (plural = true) => {
  return getAreaCheckpointTerm(plural).toLowerCase();
};


const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  area: {
    type: Object,
    default: null,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  isDirty: {
    type: Boolean,
    default: false,
  },
  eventId: {
    type: String,
    required: true,
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  assignments: {
    type: Array,
    default: () => [],
  },
  notes: {
    type: Array,
    default: () => [],
  },
  contacts: {
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
  eventCheckpointTerm: {
    type: String,
    default: 'Checkpoints',
  },
  incidents: {
    type: Array,
    default: () => [],
  },
  roleDefinitions: {
    type: Array,
    default: () => [],
  },
  initialTab: {
    type: String,
    default: 'details',
  },
  route: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'draw-boundary',
  'update:isDirty',
  'select-checkpoint',
  'create-contact-from-marshal',
  'create-new-contact',
  'edit-contact',
  'select-incident',
  'reorder-notes',
  'reorder-checklists',
  'reorder-contacts',
]);

const activeTab = ref('details');
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const checkpointChecklistStatus = ref({});
const loadingChecklistStatus = ref({});
const showClearBoundaryConfirm = ref(false);
const showAddContactModal = ref(false);
const pendingContacts = ref([]);
const contactsToRemove = ref([]);
const pendingNewChecklistItems = ref([]);
const pendingNewNotes = ref([]);

const form = ref({
  name: '',
  description: '',
  color: DEFAULT_AREA_COLOR,
  displayOrder: 0,
  isDefault: false,
  polygon: [],
  checkpointStyleType: 'default',
  checkpointStyleColor: '',
  checkpointStyleBackgroundShape: '',
  checkpointStyleBackgroundColor: '',
  checkpointStyleBorderColor: '',
  checkpointStyleIconColor: '',
  checkpointStyleSize: '',
  checkpointStyleMapRotation: '',
  peopleTerm: '',
  checkpointTerm: '',
});

// Helper to create empty form for new area
const getEmptyForm = () => ({
  name: '',
  description: '',
  color: getNextAvailableColor(props.areas),
  displayOrder: props.areas.length,
  isDefault: false,
  polygon: [],
  checkpointStyleType: 'default',
  checkpointStyleColor: '',
  checkpointStyleBackgroundShape: '',
  checkpointStyleBackgroundColor: '',
  checkpointStyleBorderColor: '',
  checkpointStyleIconColor: '',
  checkpointStyleSize: '',
  checkpointStyleMapRotation: '',
  peopleTerm: '',
  checkpointTerm: '',
});

// Accordion expanded state - with persistence
const ACCORDION_STORAGE_KEY = 'edit_area_accordion';
const getAccordionStorageKey = () => props.eventId ? `${ACCORDION_STORAGE_KEY}_${props.eventId}` : ACCORDION_STORAGE_KEY;

const expandedSections = ref({
  polygonColor: false,
  iconStyle: false,
  peopleTerm: false,
  checkpointTerm: false,
});

// Load saved accordion state on mount
onMounted(() => {
  const saved = localStorage.getItem(getAccordionStorageKey());
  if (saved) {
    try {
      const parsed = JSON.parse(saved);
      expandedSections.value = { ...expandedSections.value, ...parsed };
    } catch {
      // Ignore parse errors
    }
  }
});

const toggleSection = (section) => {
  expandedSections.value[section] = !expandedSections.value[section];
  // Persist accordion state
  localStorage.setItem(getAccordionStorageKey(), JSON.stringify(expandedSections.value));
};

// Generate polygon preview SVG with current area color
const polygonPreviewSvg = computed(() => {
  const size = 24;
  const color = form.value.color || DEFAULT_AREA_COLOR;
  // Draw an irregular polygon shape to represent an area boundary
  const points = '4,8 7,3 14,2 20,5 22,12 19,18 12,21 5,19 2,14';
  return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
    <polygon points="${points}" fill="${color}" stroke="#fff" stroke-width="1.5" opacity="0.8"/>
  </svg>`;
});

// Generate marker style preview SVG - each property inherits independently from event
const iconStylePreviewSvg = computed(() => {
  // Check if this area has any custom style set
  const hasAreaStyle = form.value.checkpointStyleType !== 'default'
    || form.value.checkpointStyleBackgroundShape
    || form.value.checkpointStyleBackgroundColor
    || form.value.checkpointStyleBorderColor
    || form.value.checkpointStyleIconColor
    || form.value.checkpointStyleSize;

  // Check if event has any default style set
  const hasEventStyle = props.eventDefaultStyleType !== 'default'
    || props.eventDefaultStyleBackgroundShape
    || props.eventDefaultStyleBackgroundColor
    || props.eventDefaultStyleColor
    || props.eventDefaultStyleBorderColor
    || props.eventDefaultStyleIconColor
    || props.eventDefaultStyleSize;

  // If nothing is set anywhere, show gradient
  if (!hasAreaStyle && !hasEventStyle) {
    const size = 24;
    return `<svg width="${size}" height="${size}" viewBox="0 0 ${size} ${size}" xmlns="http://www.w3.org/2000/svg">
      <defs>
        <linearGradient id="areaDefaultGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style="stop-color:#10B981"/>
          <stop offset="50%" style="stop-color:#F59E0B"/>
          <stop offset="100%" style="stop-color:#EF4444"/>
        </linearGradient>
      </defs>
      <circle cx="${size / 2}" cy="${size / 2}" r="${size / 2 - 2}" fill="url(#areaDefaultGrad)" stroke="#fff" stroke-width="1.5"/>
    </svg>`;
  }

  // Resolve each property: area value → event default → fallback
  const resolvedType = (form.value.checkpointStyleType && form.value.checkpointStyleType !== 'default')
    ? form.value.checkpointStyleType
    : (props.eventDefaultStyleType && props.eventDefaultStyleType !== 'default' ? props.eventDefaultStyleType : 'default');

  const resolvedBackgroundShape = form.value.checkpointStyleBackgroundShape
    || props.eventDefaultStyleBackgroundShape
    || 'circle';

  const resolvedBackgroundColor = form.value.checkpointStyleBackgroundColor
    || props.eventDefaultStyleBackgroundColor
    || props.eventDefaultStyleColor
    || '#667eea';

  const resolvedBorderColor = form.value.checkpointStyleBorderColor
    || props.eventDefaultStyleBorderColor
    || '#ffffff';

  const resolvedIconColor = form.value.checkpointStyleIconColor
    || props.eventDefaultStyleIconColor
    || '#ffffff';

  const resolvedSize = form.value.checkpointStyleSize
    || props.eventDefaultStyleSize
    || '100';

  // Generate icon with resolved properties
  return generateCheckpointSvg({
    type: resolvedType !== 'default' ? resolvedType : resolvedBackgroundShape,
    backgroundShape: resolvedBackgroundShape,
    backgroundColor: resolvedBackgroundColor,
    borderColor: resolvedBorderColor,
    iconColor: resolvedIconColor,
    size: resolvedSize,
    outputSize: 24,
  });
});

// Computed property for whether this is an existing area
const isExistingArea = computed(() => {
  return props.area && props.area.id;
});

// Convert polygon points to locations for auto-fit bounds
const polygonBoundsLocations = computed(() => {
  if (!form.value.polygon || form.value.polygon.length === 0) {
    return [];
  }
  // Convert polygon points to location-like objects for MapView's fitBounds
  return form.value.polygon.map((point, index) => ({
    id: `polygon-point-${index}`,
    latitude: point.lat,
    longitude: point.lng,
  }));
});

// Other areas (excluding the one being edited) for display on the map - shown in gray
const otherAreas = computed(() => {
  if (!props.area?.id) return props.areas.map(a => ({ ...a, color: '#999999' }));
  return props.areas
    .filter(a => a.id !== props.area.id)
    .map(a => ({ ...a, color: '#999999' }));
});

// IDs of checkpoints in this area (for highlighting)
const areaCheckpointIds = computed(() => {
  return areaCheckpoints.value.map(cp => cp.id);
});

// All tabs - incidents shown conditionally
const availableTabs = computed(() => {
  const tabs = [
    { value: 'details', label: 'Details', icon: 'details' },
    { value: 'boundary', label: 'Boundary', icon: 'area' },
    { value: 'contacts', label: 'Contacts', icon: 'marshal' },
    { value: 'checkpoints', label: getAreaCheckpointTerm(), icon: 'checkpoint' },
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

const nextTab = computed(() => {
  if (isLastTab.value) return null;
  return availableTabs.value[currentTabIndex.value + 1];
});

// Button text for next tab button (mobile only)
const nextTabButtonText = computed(() => {
  if (!nextTab.value) return '';
  return `${nextTab.value.label}...`;
});

// Compute assignments for checkpoints in this area
const areaAssignments = computed(() => {
  if (!props.area || !props.area.id) return [];

  // Get all checkpoints in this area
  const checkpointIds = props.checkpoints
    .filter(cp => cp.areaIds && cp.areaIds.includes(props.area.id))
    .map(cp => cp.id);

  // Get all assignments for those checkpoints
  return props.assignments.filter(a => checkpointIds.includes(a.locationId));
});

// Get checkpoints in this area
const areaCheckpoints = computed(() => {
  if (!props.area || !props.area.id) return [];
  return props.checkpoints.filter(cp => cp.areaIds && cp.areaIds.includes(props.area.id));
});

// Sort checkpoints alphanumerically
const sortedAreaCheckpoints = computed(() => {
  const sorted = [...areaCheckpoints.value];
  sorted.sort((a, b) => {
    // Extract numbers from names for natural sorting
    const aMatch = a.name.match(/^(\d+)/);
    const bMatch = b.name.match(/^(\d+)/);

    if (aMatch && bMatch) {
      const aNum = parseInt(aMatch[1]);
      const bNum = parseInt(bMatch[1]);
      if (aNum !== bNum) return aNum - bNum;
    }

    return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' });
  });
  return sorted;
});

// Get contacts scoped to this area
const areaContacts = computed(() => {
  if (!props.area || !props.area.id) return [];

  return props.contacts.filter(contact => {
    if (!contact.scopeConfigurations) return false;

    return contact.scopeConfigurations.some(config => {
      // Check if this contact is visible to everyone in specific areas and this area is included
      if (config.scope === 'EveryoneInAreas' && config.itemType === 'Area') {
        // ALL_AREAS means all areas
        if (config.ids?.includes('ALL_AREAS')) return true;
        // Check if this specific area is in the list
        return config.ids?.includes(props.area.id);
      }
      return false;
    });
  });
});

// Combined list of area contacts + pending contacts for display (including those marked for removal)
const displayContacts = computed(() => {
  const existingIds = new Set(areaContacts.value.map(c => c.contactId));
  const existing = areaContacts.value
    .map(c => ({
      ...c,
      isPending: false,
      isExisting: true,
      isMarkedForRemoval: contactsToRemove.value.includes(c.contactId),
    }));
  // Filter out pending contacts that are already in the existing list
  const pending = pendingContacts.value
    .filter(c => !existingIds.has(c.contactId))
    .map(c => ({ ...c, isPending: true, isExisting: false, isMarkedForRemoval: false }));
  return [...existing, ...pending].sort((a, b) =>
    a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
  );
});

// Contact IDs to exclude from the selector (already scoped + pending)
const excludeContactIds = computed(() => {
  const scopedIds = areaContacts.value.map(c => c.contactId);
  const pendingIds = pendingContacts.value.map(c => c.contactId);
  return [...scopedIds, ...pendingIds];
});

// Lazy load checklist status when checkpoints tab is viewed
const loadChecklistStatus = async (checkpointId) => {
  if (checkpointChecklistStatus.value[checkpointId] || loadingChecklistStatus.value[checkpointId]) {
    return; // Already loaded or loading
  }

  loadingChecklistStatus.value[checkpointId] = true;

  try {
    const response = await checklistApi.getCheckpointChecklist(props.eventId, checkpointId);
    const items = response.data || [];

    checkpointChecklistStatus.value[checkpointId] = {
      total: items.length,
      completed: items.filter(item => item.isCompleted).length,
    };
  } catch (error) {
    console.error('Failed to load checklist status for checkpoint:', checkpointId, error);
    checkpointChecklistStatus.value[checkpointId] = { total: 0, completed: 0 };
  } finally {
    loadingChecklistStatus.value[checkpointId] = false;
  }
};

watch(() => props.area, (newVal) => {
  if (newVal) {
    // Parse PolygonJson from backend
    const polygon = newVal.polygon
      ? (typeof newVal.polygon === 'string' ? JSON.parse(newVal.polygon) : newVal.polygon)
      : [];

    form.value = {
      name: newVal.name || '',
      description: newVal.description || '',
      color: newVal.color || DEFAULT_AREA_COLOR,
      displayOrder: newVal.displayOrder || 0,
      isDefault: newVal.isDefault || false,
      polygon: polygon,
      checkpointStyleType: newVal.checkpointStyleType || 'default',
      checkpointStyleColor: newVal.checkpointStyleColor || '',
      checkpointStyleBackgroundShape: newVal.checkpointStyleBackgroundShape || '',
      checkpointStyleBackgroundColor: newVal.checkpointStyleBackgroundColor || '',
      checkpointStyleBorderColor: newVal.checkpointStyleBorderColor || '',
      checkpointStyleIconColor: newVal.checkpointStyleIconColor || '',
      checkpointStyleSize: newVal.checkpointStyleSize || '',
      checkpointStyleMapRotation: newVal.checkpointStyleMapRotation ?? '',
      peopleTerm: newVal.peopleTerm || '',
      checkpointTerm: newVal.checkpointTerm || '',
    };
  } else {
    // Creating new area
    form.value = getEmptyForm();
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = props.initialTab || 'details';
    checklistChanges.value = [];
    checkpointChecklistStatus.value = {};
    loadingChecklistStatus.value = {};
    pendingContacts.value = [];
    contactsToRemove.value = [];
    pendingNewChecklistItems.value = [];
    pendingNewNotes.value = [];
    showAddContactModal.value = false;
    // Reset form when opening for a new area
    if (!isExistingArea.value) {
      form.value = getEmptyForm();
    }
    // Clear pending changes in checklist tab if it exists
    if (checklistTabRef.value?.resetLocalState) {
      checklistTabRef.value.resetLocalState();
    }
  }
});

// Watch for activeTab changes to lazy load checklist status
watch(activeTab, (newTab) => {
  if (newTab === 'checkpoints' && props.area && props.area.id) {
    // Lazy load checklist status for all checkpoints in this area
    areaCheckpoints.value.forEach(checkpoint => {
      setTimeout(() => loadChecklistStatus(checkpoint.id), 0);
    });
  }
});

const handleInput = () => {
  emit('update:isDirty', true);
};

const selectColor = (colorHex) => {
  form.value.color = colorHex;
  handleInput();
};

const handleStyleInput = (field, value) => {
  form.value[field] = value;
  handleInput();
};

const handleDrawBoundary = () => {
  // Emit current form data so parent can preserve it while drawing
  emit('draw-boundary', form.value);
};

const handlePolygonUpdate = (points) => {
  form.value.polygon = points;
  handleInput();
};

const confirmClearBoundary = () => {
  form.value.polygon = [];
  showClearBoundaryConfirm.value = false;
  handleInput();
};

const handleCreateFromMarshal = (marshal) => {
  // Close the add contact modal and emit to parent to open contact modal with marshal pre-selected
  showAddContactModal.value = false;
  emit('create-contact-from-marshal', { marshal, area: props.area });
};

const handleCreateNewContact = (name) => {
  // Close the add contact modal and emit to parent to open contact modal with name pre-filled
  showAddContactModal.value = false;
  emit('create-new-contact', { name, area: props.area });
};

const handleContactSelected = (contact) => {
  // Add to pending contacts list
  if (!pendingContacts.value.find(c => c.contactId === contact.contactId)) {
    pendingContacts.value.push(contact);
    handleInput(); // Mark form as dirty
  }
  showAddContactModal.value = false;
};

const removePendingContact = (contactId) => {
  pendingContacts.value = pendingContacts.value.filter(c => c.contactId !== contactId);
  handleInput();
};

const removeExistingContact = (contactId) => {
  if (!contactsToRemove.value.includes(contactId)) {
    contactsToRemove.value.push(contactId);
    handleInput();
  }
};

const getContactRemoveTitle = (contact) => {
  return contact.isPending ? 'Remove pending contact' : `Remove from ${termsLower.value.area}`;
};

const handleContactRemove = (contactId) => {
  const contact = displayContacts.value.find(c => c.contactId === contactId);
  if (contact?.isPending) {
    removePendingContact(contactId);
  } else {
    removeExistingContact(contactId);
  }
};

const handleUndoRemove = (contactId) => {
  contactsToRemove.value = contactsToRemove.value.filter(id => id !== contactId);
  handleInput();
};

const handleChecklistChange = (changes) => {
  checklistChanges.value = changes;
  handleInput();
};

const goToNextTab = () => {
  if (nextTab.value) {
    activeTab.value = nextTab.value.value;
  }
};

const handleNotesReorder = (changes) => {
  emit('reorder-notes', changes);
};

const handleChecklistsReorder = (changes) => {
  emit('reorder-checklists', changes);
};

const handleContactsReorder = (changes) => {
  emit('reorder-contacts', changes);
};

const handleSave = () => {
  // For create: EventId, Name, Description, Color, Polygon (array or null)
  // For update: Name, Description, Color, Polygon (array or null), DisplayOrder
  const formData = props.area ? {
    // Update request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
    polygon: form.value.polygon.length > 0 ? form.value.polygon : null,
    displayOrder: form.value.displayOrder,
    checkpointStyleType: form.value.checkpointStyleType,
    checkpointStyleColor: form.value.checkpointStyleColor,
    checkpointStyleBackgroundShape: form.value.checkpointStyleBackgroundShape,
    checkpointStyleBackgroundColor: form.value.checkpointStyleBackgroundColor,
    checkpointStyleBorderColor: form.value.checkpointStyleBorderColor,
    checkpointStyleIconColor: form.value.checkpointStyleIconColor,
    checkpointStyleSize: form.value.checkpointStyleSize,
    checkpointStyleMapRotation: form.value.checkpointStyleMapRotation != null && form.value.checkpointStyleMapRotation !== ''
      ? String(form.value.checkpointStyleMapRotation)
      : null,
    peopleTerm: form.value.peopleTerm,
    checkpointTerm: form.value.checkpointTerm,
    // Include pending checklist changes
    checklistChanges: checklistChanges.value || [],
    // Include pending contacts to add to this area
    pendingContacts: pendingContacts.value || [],
    // Include contacts to remove from this area
    contactsToRemove: contactsToRemove.value || [],
    // Include pending new checklist items and notes (for this area)
    pendingNewChecklistItems: pendingNewChecklistItems.value || [],
    pendingNewNotes: pendingNewNotes.value || [],
  } : {
    // Create request
    name: form.value.name,
    description: form.value.description,
    color: form.value.color,
    polygon: form.value.polygon.length > 0 ? form.value.polygon : null,
    checkpointStyleType: form.value.checkpointStyleType,
    checkpointStyleColor: form.value.checkpointStyleColor,
    checkpointStyleBackgroundShape: form.value.checkpointStyleBackgroundShape,
    checkpointStyleBackgroundColor: form.value.checkpointStyleBackgroundColor,
    checkpointStyleBorderColor: form.value.checkpointStyleBorderColor,
    checkpointStyleIconColor: form.value.checkpointStyleIconColor,
    checkpointStyleSize: form.value.checkpointStyleSize,
    checkpointStyleMapRotation: form.value.checkpointStyleMapRotation != null && form.value.checkpointStyleMapRotation !== ''
      ? String(form.value.checkpointStyleMapRotation)
      : null,
    peopleTerm: form.value.peopleTerm,
    checkpointTerm: form.value.checkpointTerm,
    // Include pending contacts to add to this area
    pendingContacts: pendingContacts.value || [],
  };

  emit('save', formData);
};

const handleDelete = () => {
  emit('delete', props.area.id);
};

const handleClose = () => {
  emit('close');
};

// Expose methods for parent component to manage contacts
defineExpose({
  addPendingContact: (contact) => {
    if (contact && !pendingContacts.value.find(c => c.contactId === contact.contactId)) {
      pendingContacts.value.push(contact);
      handleInput(); // Mark form as dirty
    }
  },
  removeContactById: (contactId) => {
    // Remove from pending contacts if present
    const hadPending = pendingContacts.value.some(c => c.contactId === contactId);
    pendingContacts.value = pendingContacts.value.filter(c => c.contactId !== contactId);
    // Also remove from contactsToRemove if present (no longer needed since contact is deleted)
    contactsToRemove.value = contactsToRemove.value.filter(id => id !== contactId);
    if (hadPending) {
      handleInput();
    }
  },
});
</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-primary);
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  resize: vertical;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-input:disabled {
  background-color: var(--bg-tertiary);
  cursor: not-allowed;
}

.color-picker {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.color-swatch {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  cursor: pointer;
  border: 3px solid transparent;
  transition: all 0.2s;
  box-shadow: var(--shadow-sm);
}

.color-swatch:hover {
  transform: scale(1.1);
}

.color-swatch.selected {
  border-color: var(--text-primary);
  box-shadow: 0 0 0 2px var(--bg-primary), 0 0 0 4px var(--text-primary);
}

.section-title {
  margin: 0 0 1rem 0;
  font-size: 1rem;
  color: var(--text-primary);
}

.boundary-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.boundary-preview {
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--border-color);
}

.boundary-actions {
  display: flex;
  gap: 1rem;
}

.boundary-hint {
  margin: 0.5rem 0 0 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
  text-align: center;
}

.custom-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.footer-left {
  display: flex;
  gap: 0.5rem;
}

.mobile-only {
  display: none;
}

@media (max-width: 768px) {
  .mobile-only {
    display: inline-block;
  }
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-full {
  width: 100%;
}

.btn-small {
  padding: 0.375rem 0.75rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

.btn-success {
  background: var(--success);
  color: white;
}

.btn-success:hover {
  background: var(--success-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

/* Contacts Tab Styles */
.contacts-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.contacts-header .section-title {
  margin: 0;
}

@media (max-width: 640px) {
  .contacts-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.75rem;
  }
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
  background: var(--bg-tertiary);
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
  color: var(--text-primary);
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

.incidents-tab {
  padding-top: 0.5rem;
}

.incidents-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
</style>
