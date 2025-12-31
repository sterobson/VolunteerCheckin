<template>
  <BaseModal
    :show="show"
    :title="modalTitle"
    size="large"
    :confirm-on-close="true"
    :is-dirty="isDirty"
    @close="handleClose"
  >
    <!-- Tabs in header (only show when editing) -->
    <template v-if="isEditing" #tab-header>
      <TabHeader
        v-model="activeTab"
        :tabs="[
          { value: 'details', label: 'Details' },
          { value: 'checkpoints', label: 'Checkpoints' },
          { value: 'checklist', label: 'Checklist' }
        ]"
      />
    </template>

    <!-- Details Tab (or whole content when creating) -->
    <MarshalDetailsTab
      v-if="activeTab === 'details' || !isEditing"
      :form="form"
      :event-id="eventId"
      :marshal-id="isEditing ? marshal?.id : null"
      @update:form="form = $event"
      @input="handleInput"
    />

    <!-- Checkpoints Tab (only when editing) -->
    <MarshalCheckpointsTab
      v-if="activeTab === 'checkpoints' && isEditing"
      ref="checkpointsTabRef"
      :assignments="assignments"
      :available-locations="availableLocations"
      @input="handleInput"
      @remove-assignment="handleRemoveAssignment"
      @assign-to-location="handleAssignToLocation"
    />

    <!-- Checklist Tab (only when editing) -->
    <MarshalChecklistView
      v-if="activeTab === 'checklist' && isEditing"
      ref="checklistTabRef"
      v-model="checklistChanges"
      :event-id="eventId"
      :marshal-id="marshal.id"
      :locations="allLocations"
      :areas="areas"
      @change="handleChecklistChange"
    />

    <!-- Custom footer with left and right aligned buttons -->
    <template #footer>
      <div class="custom-footer">
        <button
          v-if="isEditing"
          type="button"
          @click="handleDelete"
          class="btn btn-danger"
        >
          Delete marshal
        </button>
        <div v-else></div>
        <button type="button" @click="handleSave" class="btn btn-primary">
          {{ isEditing ? 'Save changes' : 'Add marshal' }}
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
});

const emit = defineEmits([
  'close',
  'save',
  'delete',
  'toggle-check-in',
  'remove-assignment',
  'assign-to-location',
  'update:isDirty',
]);

const activeTab = ref('details');
const checkpointsTabRef = ref(null);
const checklistTabRef = ref(null);
const checklistChanges = ref([]);
const form = ref({
  name: '',
  email: '',
  phoneNumber: '',
  notes: '',
});

const modalTitle = computed(() => {
  const baseTitle = props.isEditing ? 'Edit marshal' : 'Create marshal';
  const name = form.value.name?.trim();
  return name ? `${baseTitle} - ${name}` : baseTitle;
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

const handleInput = () => {
  emit('update:isDirty', true);
};

const handleChecklistChange = () => {
  emit('update:isDirty', true);
};

const handleSave = () => {
  const formData = {
    ...form.value,
    // Include pending check-in changes from checkpoints tab
    checkInChanges: checkpointsTabRef.value?.getPendingChanges?.() || [],
    // Include pending checklist changes
    checklistChanges: checklistChanges.value || [],
  };
  emit('save', formData);
};

const handleDelete = () => {
  emit('delete');
};

const handleRemoveAssignment = (assignment) => {
  emit('remove-assignment', assignment);
};

const handleAssignToLocation = (locationId) => {
  emit('assign-to-location', locationId);
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
