<template>
  <BaseModal
    :show="show"
    :title="`Edit checkpoint: ${location?.name || ''}`"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header -->
    <template #tab-header>
      <TabHeader
        v-model="activeTab"
        :tabs="[
          { value: 'details', label: 'Details', icon: 'details' },
          { value: 'location', label: 'Location', icon: 'location' },
          { value: 'marshals', label: 'Marshals', icon: 'marshal' },
          { value: 'checklist', label: 'Checklist', icon: 'checklist' },
          { value: 'notes', label: 'Notes', icon: 'notes' }
        ]"
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
      @update:form="form = $event"
      @input="handleInput"
      @remove-assignment="handleRemoveAssignment"
      @assign-marshal="handleAssignMarshal"
    />

    <!-- Checklist Tab -->
    <CheckpointChecklistView
      v-if="activeTab === 'checklist'"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :location-id="location.id"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
      @change="handleChecklistChange"
    />

    <!-- Notes Tab -->
    <NotesView
      v-if="activeTab === 'notes'"
      :event-id="eventId"
      :location-id="location?.id"
      :all-notes="notes"
      :locations="allLocations"
      :areas="areas"
      :assignments="assignments"
    />

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete checkpoint
        </button>
        <button type="button" @click="handleSave" class="btn btn-primary">
          Save changes
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
    };
  }
}, { immediate: true, deep: true });

watch(() => props.show, (newVal) => {
  if (newVal) {
    activeTab.value = 'details';
    isMoving.value = false;
    checklistChanges.value = [];
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
  emit('move-location', isMoving.value);
};

const handleChecklistChange = (changes) => {
  checklistChanges.value = changes;
  handleInput();
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

const handleAssignMarshal = () => {
  emit('assign-marshal');
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
</style>
