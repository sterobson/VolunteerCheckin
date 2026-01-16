<template>
  <!-- Content rendered inside parent's accordion section -->
  <div class="area-lead-content">
    <div v-if="loading" class="loading-state">
      Loading dashboard...
    </div>

    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
      <button @click="loadDashboard" class="btn btn-secondary">Retry</button>
    </div>

    <div v-else-if="sortedCheckpoints.length === 0" class="empty-state">
      <p>No {{ termsLower.checkpoints }} in your {{ termsLower.areas }}.</p>
    </div>

    <div v-else class="checkpoint-accordion">
      <div
        v-for="checkpoint in sortedCheckpoints"
        :key="checkpoint.checkpointId"
        class="checkpoint-accordion-section"
      >
        <button
          class="checkpoint-accordion-header"
          :class="{ active: expandedCheckpoint === checkpoint.checkpointId }"
          @click="toggleCheckpoint(checkpoint.checkpointId)"
        >
          <div class="checkpoint-header-content">
            <div class="checkpoint-title-row">
              <span class="checkpoint-name">{{ checkpoint.name }}</span>
              <span v-if="checkpoint.areaName" class="area-badge">{{ checkpoint.areaName }}</span>
            </div>
            <div v-if="checkpoint.description" class="checkpoint-description-preview">
              {{ truncateDescription(checkpoint.description) }}
            </div>
            <div class="checkpoint-status-row">
              <span class="status-badge marshals-status">
                {{ getCheckedInCount(checkpoint) }} / {{ checkpoint.marshals.length }} checked in
              </span>
              <span v-if="getTotalOutstandingTasks(checkpoint) > 0" class="status-badge tasks-status">
                {{ getTotalOutstandingTasks(checkpoint) }} tasks
              </span>
            </div>
          </div>
          <span class="accordion-icon">{{ expandedCheckpoint === checkpoint.checkpointId ? '−' : '+' }}</span>
        </button>

        <div v-if="expandedCheckpoint === checkpoint.checkpointId" class="checkpoint-accordion-content">
          <!-- Mini Map -->
          <div class="checkpoint-mini-map">
            <CommonMap
              :locations="[{ id: checkpoint.checkpointId, name: checkpoint.name, description: checkpoint.description, latitude: checkpoint.latitude, longitude: checkpoint.longitude }]"
              :route="route"
              :route-color="routeColor"
              :route-style="routeStyle"
              :route-weight="routeWeight"
              :center="{ lat: checkpoint.latitude, lng: checkpoint.longitude }"
              :zoom="16"
              :highlight-location-id="checkpoint.checkpointId"
              :marshal-mode="true"
              height="280px"
            />
          </div>

          <!-- Marshals at this checkpoint -->
          <div class="checkpoint-marshals-section">
            <div class="marshals-label">{{ terms.people }} ({{ checkpoint.marshals.length }})</div>

            <div v-if="checkpoint.marshals.length === 0" class="empty-state">
              No {{ termsLower.people }} assigned to this {{ termsLower.checkpoint }}.
            </div>

            <div v-else class="marshals-list">
              <div
                v-for="marshal in checkpoint.marshals"
                :key="marshal.marshalId"
                class="marshal-card"
                :class="{ 'is-checked-in': marshal.isCheckedIn }"
                @click="openMarshalDetails(marshal, checkpoint)"
              >
                <div class="marshal-header">
                  <div class="marshal-name-row">
                    <span class="marshal-name">{{ marshal.name }}</span>
                    <span v-if="marshal.outstandingTaskCount > 0" class="task-count">
                      {{ marshal.outstandingTaskCount }} tasks
                    </span>
                  </div>
                  <div class="check-status-row">
                    <div class="check-status" :class="{ 'checked-in': marshal.isCheckedIn }">
                      {{ marshal.isCheckedIn ? '✓ Checked In' : 'Not Checked In' }}
                    </div>
                    <button
                      @click.stop="handleCheckIn(marshal, checkpoint)"
                      class="quick-checkin-btn"
                      :class="{ 'undo-btn': marshal.isCheckedIn }"
                      :disabled="checkingIn === marshal.marshalId"
                    >
                      {{ checkingIn === marshal.marshalId ? '...' : (marshal.isCheckedIn ? 'Undo' : 'Check In') }}
                    </button>
                  </div>
                </div>
                <div v-if="marshal.isCheckedIn && marshal.checkInTime" class="check-in-time">
                  {{ formatTime(marshal.checkInTime) }}
                  <span v-if="marshal.checkInMethod"> ({{ formatCheckInMethod(marshal.checkInMethod) }})</span>
                </div>
                <div v-if="marshal.lastAccessedAt" class="last-access-time">
                  Last active: {{ formatRelativeTime(marshal.lastAccessedAt) }}
                </div>
              </div>
            </div>
          </div>

          <!-- Checkpoint tasks (outstanding and completed) -->
          <div v-if="checkpoint.outstandingTaskCount > 0 || (checkpoint.completedTasks && checkpoint.completedTasks.length > 0)" class="checkpoint-tasks-section">
            <div class="marshals-label">{{ terms.checkpoint }} tasks</div>
            <div class="tasks-list">
              <!-- Outstanding tasks -->
              <div
                v-for="task in checkpoint.outstandingTasks"
                :key="task.itemId"
                class="task-item"
              >
                <input
                  type="checkbox"
                  :disabled="savingTask"
                  @change="completeTask(task, checkpoint)"
                />
                <span class="task-text">{{ task.text }}</span>
              </div>
              <!-- Completed tasks -->
              <div
                v-for="task in (checkpoint.completedTasks || [])"
                :key="`completed-${task.itemId}`"
                class="task-item task-completed"
              >
                <input
                  type="checkbox"
                  checked
                  :disabled="savingTask"
                  @change="uncompleteCheckpointTask(task, checkpoint)"
                />
                <span class="task-text">{{ task.text }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Marshal Details Modal -->
    <BaseModal
      :show="showMarshalModal"
      :title="selectedMarshal?.name || 'Marshal Details'"
      size="large"
      @close="closeMarshalModal"
    >
      <div v-if="selectedMarshal" class="marshal-details-modal">
        <!-- Contact Details -->
        <div class="details-section">
          <h4>Contact Details</h4>
          <div class="contact-info">
            <div v-if="selectedMarshal.email" class="contact-row">
              <span class="contact-label">Email:</span>
              <a :href="`mailto:${selectedMarshal.email}`">{{ selectedMarshal.email }}</a>
            </div>
            <div v-if="selectedMarshal.phoneNumber" class="contact-row">
              <span class="contact-label">Phone:</span>
              <a :href="`tel:${selectedMarshal.phoneNumber}`">{{ selectedMarshal.phoneNumber }}</a>
            </div>
            <div v-if="!selectedMarshal.email && !selectedMarshal.phoneNumber" class="no-contact">
              No contact details available.
            </div>
          </div>
        </div>

        <!-- Check-in Status -->
        <div class="details-section">
          <h4>Check-in Status</h4>
          <div v-if="selectedMarshal.isCheckedIn" class="checked-in-section">
            <div class="checked-in-details">
              <span class="check-icon">✓</span>
              <div>
                <strong>Checked In</strong>
                <p class="check-time-detail">{{ formatDateTime(selectedMarshal.checkInTime) }}</p>
                <p v-if="selectedMarshal.checkInMethod" class="check-method">
                  Method: {{ formatCheckInMethod(selectedMarshal.checkInMethod) }}
                </p>
              </div>
            </div>
            <button
              @click="handleCheckIn(selectedMarshal, selectedCheckpoint)"
              class="btn btn-secondary undo-checkin-btn"
              :disabled="checkingIn === selectedMarshal.marshalId"
            >
              {{ checkingIn === selectedMarshal.marshalId ? 'Undoing...' : 'Undo Check-in' }}
            </button>
          </div>
          <div v-else class="not-checked-in-section">
            <div class="not-checked-in">
              <span class="not-checked-icon">✗</span>
              <span>Not checked in</span>
            </div>
            <button
              @click="handleCheckIn(selectedMarshal, selectedCheckpoint)"
              class="btn btn-primary check-in-btn"
              :disabled="checkingIn === selectedMarshal.marshalId"
            >
              {{ checkingIn === selectedMarshal.marshalId ? 'Checking in...' : 'Check In' }}
            </button>
          </div>
          <div v-if="selectedMarshal.lastAccessedAt" class="last-access-detail">
            Last active: {{ formatRelativeTime(selectedMarshal.lastAccessedAt) }}
          </div>
        </div>

        <!-- Tasks -->
        <div class="details-section">
          <h4>{{ terms.checklists }} ({{ selectedMarshal.outstandingTasks?.length || 0 }} outstanding)</h4>
          <div v-if="!selectedMarshal.outstandingTasks || selectedMarshal.outstandingTasks.length === 0" class="no-tasks">
            No outstanding tasks.
          </div>
          <div v-else class="tasks-list modal-tasks">
            <div
              v-for="task in selectedMarshal.outstandingTasks"
              :key="task.itemId"
              class="task-item"
            >
              <input
                type="checkbox"
                :disabled="savingTask"
                @change="completeTaskForMarshal(task)"
              />
              <span class="task-text">{{ task.text }}</span>
            </div>
          </div>
        </div>

        <!-- Contact Actions -->
        <div class="details-section contact-actions-section">
          <div class="contact-buttons">
            <a
              v-if="selectedMarshal.phoneNumber"
              :href="`tel:${selectedMarshal.phoneNumber}`"
              class="btn btn-primary"
            >
              Call
            </a>
            <a
              v-if="selectedMarshal.phoneNumber"
              :href="`sms:${selectedMarshal.phoneNumber}`"
              class="btn btn-secondary"
            >
              Text
            </a>
            <a
              v-if="selectedMarshal.email"
              :href="`mailto:${selectedMarshal.email}`"
              class="btn btn-secondary"
            >
              Email
            </a>
          </div>
        </div>
      </div>

      <template #actions>
        <button @click="closeMarshalModal" class="btn btn-secondary">Close</button>
      </template>
    </BaseModal>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import { areasApi, checklistApi, checkInApi, getOfflineMode, queueOfflineAction } from '../services/api';
import { useTerminology } from '../composables/useTerminology';
import { alphanumericCompare } from '../utils/sortUtils';
import { getCachedEventData, updateCachedField, cacheEventData } from '../services/offlineDb';
import { useOffline } from '../composables/useOffline';
import CommonMap from './common/CommonMap.vue';
import BaseModal from './BaseModal.vue';

const { terms, termsLower } = useTerminology();
const { updatePendingCount } = useOffline();

const props = defineProps({
  eventId: {
    type: String,
    required: true,
  },
  areaIds: {
    type: Array,
    default: () => [],
  },
  marshalId: {
    type: String,
    required: true,
  },
  route: {
    type: Array,
    default: () => [],
  },
  routeColor: {
    type: String,
    default: '',
  },
  routeStyle: {
    type: String,
    default: '',
  },
  routeWeight: {
    type: Number,
    default: null,
  },
});

const emit = defineEmits(['checklist-updated', 'loaded', 'checkin-updated']);

const loading = ref(false);
const error = ref(null);
const areas = ref([]);
const checkpoints = ref([]);
const expandedCheckpoint = ref(null);
const savingTask = ref(false);
const checkingIn = ref(null); // Tracks which marshal is being checked in

// Filter and sort checkpoints based on areaIds prop
const sortedCheckpoints = computed(() => {
  const areaIdSet = new Set(props.areaIds);
  // If no filter specified, show all checkpoints
  if (areaIdSet.size === 0) {
    return [...checkpoints.value].sort((a, b) => alphanumericCompare(a.name, b.name));
  }
  // Filter checkpoints that belong to any of the specified areas
  return checkpoints.value
    .filter(c => {
      const checkpointAreaIds = c.areaIds || [];
      return checkpointAreaIds.some(areaId => areaIdSet.has(areaId));
    })
    .sort((a, b) => alphanumericCompare(a.name, b.name));
});


// Truncate description for display in header
const truncateDescription = (description, maxLength = 60) => {
  if (!description) return '';
  if (description.length <= maxLength) return description;
  return description.substring(0, maxLength).trim() + '...';
};

// Modal state
const showMarshalModal = ref(false);
const selectedMarshal = ref(null);
const selectedCheckpoint = ref(null);

const loadDashboard = async () => {
  if (!props.eventId || props.areaIds.length === 0) return;

  loading.value = true;
  error.value = null;

  // Try to load from cache first if offline
  if (getOfflineMode()) {
    try {
      const cachedData = await getCachedEventData(props.eventId);
      if (cachedData?.areaLeadDashboard) {
        areas.value = cachedData.areaLeadDashboard.areas || [];
        checkpoints.value = cachedData.areaLeadDashboard.checkpoints || [];
        loading.value = false;
        return;
      }
    } catch (cacheErr) {
      console.warn('Failed to load cached dashboard:', cacheErr);
    }
    error.value = 'Offline - no cached data available';
    loading.value = false;
    return;
  }

  try {
    const response = await areasApi.getAreaLeadDashboard(props.eventId);
    areas.value = response.data.areas || [];
    checkpoints.value = response.data.checkpoints || [];

    // Cache the dashboard data
    try {
      // Convert to plain object to avoid IndexedDB serialization issues with Vue proxies
      const areaLeadDashboard = JSON.parse(JSON.stringify({
        areas: areas.value,
        checkpoints: checkpoints.value
      }));

      // Try to update existing cache, or create new cache entry
      const cachedData = await getCachedEventData(props.eventId);
      if (cachedData) {
        await updateCachedField(props.eventId, 'areaLeadDashboard', areaLeadDashboard);
      } else {
        await cacheEventData(props.eventId, { areaLeadDashboard });
      }
    } catch (cacheErr) {
      console.warn('Failed to cache dashboard:', cacheErr);
    }
  } catch (err) {
    console.error('Failed to load area lead dashboard:', err);

    // Try to load from cache on error
    try {
      const cachedData = await getCachedEventData(props.eventId);
      if (cachedData?.areaLeadDashboard) {
        console.log('Loading area lead dashboard from cache after error');
        areas.value = cachedData.areaLeadDashboard.areas || [];
        checkpoints.value = cachedData.areaLeadDashboard.checkpoints || [];
        loading.value = false;
        return;
      }
    } catch (cacheErr) {
      console.warn('Failed to load cached dashboard:', cacheErr);
    }

    error.value = err.response?.data?.message || 'Failed to load dashboard';
  } finally {
    loading.value = false;
  }
};

const toggleCheckpoint = (checkpointId) => {
  expandedCheckpoint.value = expandedCheckpoint.value === checkpointId ? null : checkpointId;
};

const getCheckedInCount = (checkpoint) => {
  return checkpoint.marshals.filter(m => m.isCheckedIn).length;
};

const getTotalOutstandingTasks = (checkpoint) => {
  const marshalTasks = checkpoint.marshals.reduce((sum, m) => sum + m.outstandingTaskCount, 0);
  return checkpoint.outstandingTaskCount + marshalTasks;
};

const openMarshalDetails = (marshal, checkpoint) => {
  selectedMarshal.value = marshal;
  selectedCheckpoint.value = checkpoint;
  showMarshalModal.value = true;
};

const closeMarshalModal = () => {
  showMarshalModal.value = false;
  selectedMarshal.value = null;
  selectedCheckpoint.value = null;
};

const handleCheckIn = async (marshal, checkpoint) => {
  if (checkingIn.value) return;

  // The assignment ID is on the marshal object itself (as 'id' or 'assignmentId')
  const assignmentId = marshal.assignmentId || marshal.id;
  if (!assignmentId) {
    console.error('No assignment ID found for marshal:', marshal);
    return;
  }

  checkingIn.value = marshal.marshalId;

  try {
    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checkin_toggle', {
        eventId: props.eventId,
        assignmentId: assignmentId
      });

      // Optimistic update
      marshal.isCheckedIn = true;
      marshal.checkInTime = new Date().toISOString();
      marshal.checkInMethod = 'AreaLead (pending)';

      await updatePendingCount();
      emit('checkin-updated');
    } else {
      await checkInApi.toggleCheckIn(props.eventId, assignmentId);

      // Reload the dashboard to get updated data
      await loadDashboard();
      emit('checkin-updated');

      // Update selected marshal in modal if it's open
      if (selectedMarshal.value?.marshalId === marshal.marshalId && selectedCheckpoint.value) {
        const updatedCheckpoint = checkpoints.value.find(c => c.checkpointId === selectedCheckpoint.value.checkpointId);
        if (updatedCheckpoint) {
          const updatedMarshal = updatedCheckpoint.marshals.find(m => m.marshalId === marshal.marshalId);
          if (updatedMarshal) {
            selectedMarshal.value = updatedMarshal;
          }
        }
      }
    }
  } catch (err) {
    console.error('Failed to check in marshal:', err);

    // If network error, queue for offline
    if (getOfflineMode() || !err.response) {
      await queueOfflineAction('checkin_toggle', {
        eventId: props.eventId,
        assignmentId: assignmentId
      });

      // Optimistic update
      marshal.isCheckedIn = true;
      marshal.checkInTime = new Date().toISOString();
      marshal.checkInMethod = 'AreaLead (pending)';

      await updatePendingCount();
      emit('checkin-updated');
    }
  } finally {
    checkingIn.value = null;
  }
};

const completeTask = async (task, checkpoint) => {
  if (savingTask.value) return;
  savingTask.value = true;

  // Use the first marshal at this checkpoint for completing the task
  const marshalId = checkpoint.marshals.length > 0 ? checkpoint.marshals[0].marshalId : props.marshalId;

  const actionData = {
    marshalId: marshalId,
    contextType: task.contextType,
    contextId: task.contextId,
  };

  try {
    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checklist_complete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      // Optimistic update - remove the task from the list
      checkpoint.outstandingTasks = checkpoint.outstandingTasks.filter(t => t.itemId !== task.itemId);
      checkpoint.outstandingTaskCount = Math.max(0, checkpoint.outstandingTaskCount - 1);

      await updatePendingCount();
      emit('checklist-updated');
    } else {
      await checklistApi.complete(props.eventId, task.itemId, actionData);

      // Reload the dashboard
      await loadDashboard();
      emit('checklist-updated');
    }
  } catch (err) {
    console.error('Failed to complete task:', err);

    // If network error, queue for offline
    if (getOfflineMode() || !err.response) {
      await queueOfflineAction('checklist_complete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      // Optimistic update
      checkpoint.outstandingTasks = checkpoint.outstandingTasks.filter(t => t.itemId !== task.itemId);
      checkpoint.outstandingTaskCount = Math.max(0, checkpoint.outstandingTaskCount - 1);

      await updatePendingCount();
      emit('checklist-updated');
    }
  } finally {
    savingTask.value = false;
  }
};

const uncompleteCheckpointTask = async (task, checkpoint) => {
  if (savingTask.value) return;
  savingTask.value = true;

  // Use the first marshal at this checkpoint
  const marshalId = checkpoint.marshals.length > 0 ? checkpoint.marshals[0].marshalId : props.marshalId;

  const actionData = {
    marshalId: marshalId,
    contextType: task.contextType,
    contextId: task.contextId,
    actorMarshalId: props.marshalId,
  };

  try {
    if (getOfflineMode()) {
      await queueOfflineAction('checklist_uncomplete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      // Optimistic update - move from completed to outstanding
      if (checkpoint.completedTasks) {
        checkpoint.completedTasks = checkpoint.completedTasks.filter(t => t.itemId !== task.itemId);
      }
      if (!checkpoint.outstandingTasks) checkpoint.outstandingTasks = [];
      checkpoint.outstandingTasks.push(task);
      checkpoint.outstandingTaskCount = (checkpoint.outstandingTaskCount || 0) + 1;

      await updatePendingCount();
      emit('checklist-updated');
    } else {
      await checklistApi.uncomplete(props.eventId, task.itemId, actionData);
      await loadDashboard();
      emit('checklist-updated');
    }
  } catch (err) {
    console.error('Failed to uncomplete task:', err);

    if (getOfflineMode() || !err.response) {
      await queueOfflineAction('checklist_uncomplete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      if (checkpoint.completedTasks) {
        checkpoint.completedTasks = checkpoint.completedTasks.filter(t => t.itemId !== task.itemId);
      }
      if (!checkpoint.outstandingTasks) checkpoint.outstandingTasks = [];
      checkpoint.outstandingTasks.push(task);
      checkpoint.outstandingTaskCount = (checkpoint.outstandingTaskCount || 0) + 1;

      await updatePendingCount();
      emit('checklist-updated');
    }
  } finally {
    savingTask.value = false;
  }
};

const completeTaskForMarshal = async (task) => {
  if (savingTask.value || !selectedMarshal.value) return;
  savingTask.value = true;

  const actionData = {
    marshalId: selectedMarshal.value.marshalId,
    contextType: task.contextType,
    contextId: task.contextId,
  };

  try {
    if (getOfflineMode()) {
      // Queue for offline sync
      await queueOfflineAction('checklist_complete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      // Optimistic update - remove the task from the marshal
      selectedMarshal.value.outstandingTasks = selectedMarshal.value.outstandingTasks.filter(t => t.itemId !== task.itemId);
      selectedMarshal.value.outstandingTaskCount = Math.max(0, selectedMarshal.value.outstandingTaskCount - 1);

      await updatePendingCount();
      emit('checklist-updated');
    } else {
      await checklistApi.complete(props.eventId, task.itemId, actionData);

      // Reload the dashboard
      await loadDashboard();

      // Update the selected marshal's tasks in the modal
      if (selectedMarshal.value && selectedCheckpoint.value) {
        const checkpoint = checkpoints.value.find(c => c.checkpointId === selectedCheckpoint.value.checkpointId);
        if (checkpoint) {
          const marshal = checkpoint.marshals.find(m => m.marshalId === selectedMarshal.value.marshalId);
          if (marshal) {
            selectedMarshal.value = marshal;
          }
        }
      }

      emit('checklist-updated');
    }
  } catch (err) {
    console.error('Failed to complete task:', err);

    // If network error, queue for offline
    if (getOfflineMode() || !err.response) {
      await queueOfflineAction('checklist_complete', {
        eventId: props.eventId,
        itemId: task.itemId,
        data: actionData
      });

      // Optimistic update
      selectedMarshal.value.outstandingTasks = selectedMarshal.value.outstandingTasks.filter(t => t.itemId !== task.itemId);
      selectedMarshal.value.outstandingTaskCount = Math.max(0, selectedMarshal.value.outstandingTaskCount - 1);

      await updatePendingCount();
      emit('checklist-updated');
    }
  } finally {
    savingTask.value = false;
  }
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
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffHours = diffMs / (1000 * 60 * 60);

  // If within 24 hours, just show time
  if (diffHours < 24 && diffHours >= 0) {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleString();
};

const formatRelativeTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays === 1) return 'yesterday';
  if (diffDays < 7) return `${diffDays}d ago`;
  return date.toLocaleDateString();
};

const formatCheckInMethod = (method) => {
  const methods = {
    'GPS': 'GPS',
    'Manual': 'Manual',
    'Admin': 'Admin',
    'AreaLead': `${terms.value.area} Lead`,
  };
  return methods[method] || method;
};

// Expose data and methods for parent component
defineExpose({
  areas,
  checkpoints,
  loading,
  loadDashboard,
});

watch(() => props.eventId, () => {
  loadDashboard();
}, { immediate: true });
</script>

<style scoped>
/* Content wrapper - no extra styling, inherits from accordion */
.area-lead-content {
  display: flex;
  flex-direction: column;
}

.loading-state,
.error-state,
.empty-state {
  padding: 1.5rem;
  text-align: center;
  color: var(--text-secondary);
}

.error-state {
  color: var(--danger);
}

.error-state button {
  margin-top: 1rem;
}

/* Checkpoint Accordion - matches MarshalView nested accordion */
.checkpoint-accordion {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checkpoint-accordion-section {
  background: var(--bg-secondary);
  border: 1px solid var(--border-light);
  border-radius: 10px;
  overflow: hidden;
}

.checkpoint-accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 1rem 1.25rem;
  background: var(--bg-secondary);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1rem;
  font-weight: 600;
  color: var(--text-dark);
  transition: background 0.2s;
  gap: 0.5rem;
}

.checkpoint-accordion-header:hover {
  background: var(--bg-hover);
}

.checkpoint-accordion-header.active {
  background: var(--bg-tertiary);
  border-bottom: 1px solid var(--border-light);
}

.checkpoint-header-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.checkpoint-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.checkpoint-name {
  font-weight: 600;
  color: var(--text-dark);
}

.area-badge {
  display: inline-block;
  padding: 0.2rem 0.6rem;
  background: var(--brand-primary);
  color: white;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: 12px;
}

.checkpoint-description-preview {
  font-size: 0.85rem;
  font-weight: 400;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.checkpoint-status-row {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.status-badge {
  font-size: 0.75rem;
  padding: 0.2rem 0.5rem;
  border-radius: 8px;
}

.marshals-status {
  background: var(--info-bg);
  color: var(--status-open);
}

.tasks-status {
  background: var(--warning-bg-light);
  color: var(--warning-dark);
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
}

.checkpoint-accordion-content {
  padding: 1rem 1.25rem;
  background: var(--card-bg);
}

/* Checkpoint Map - taller for better centering */
.checkpoint-mini-map {
  margin-bottom: 1rem;
  border-radius: 8px;
  overflow: hidden;
  border: 1px solid var(--border-light);
}

.checkpoint-mini-map :deep(.map-container) {
  height: 280px;
  min-height: 280px;
}

/* Marshals section */
.checkpoint-marshals-section {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.marshals-label {
  font-size: 0.8rem;
  color: var(--text-secondary);
  margin-bottom: 0.5rem;
  font-weight: 500;
}

.marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.marshal-card {
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border: 1px solid var(--border-light);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.marshal-card:hover {
  border-color: var(--brand-primary);
  box-shadow: 0 2px 8px var(--brand-shadow);
}

.marshal-card.is-checked-in {
  background: var(--checked-in-bg);
  border-color: var(--checked-in-border);
}

.marshal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.marshal-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.marshal-name {
  font-weight: 600;
  color: var(--text-dark);
}

.task-count {
  font-size: 0.7rem;
  padding: 0.15rem 0.4rem;
  background: var(--warning-bg-light);
  color: var(--warning-dark);
  border-radius: 6px;
}

.check-status-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.check-status {
  font-size: 0.75rem;
  padding: 0.2rem 0.5rem;
  border-radius: 8px;
  background: var(--danger-bg-light);
  color: var(--danger-dark);
  white-space: nowrap;
}

.check-status.checked-in {
  background: var(--success-bg-light);
  color: var(--success-dark);
}

.quick-checkin-btn {
  font-size: 0.7rem;
  padding: 0.2rem 0.5rem;
  background: var(--brand-primary);
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.2s;
}

.quick-checkin-btn:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.quick-checkin-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.quick-checkin-btn.undo-btn {
  background: var(--warning-dark);
}

.quick-checkin-btn.undo-btn:hover:not(:disabled) {
  background: var(--warning-dark);
}

.checked-in-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.undo-checkin-btn {
  align-self: flex-start;
}

.check-in-time {
  font-size: 0.75rem;
  color: var(--text-secondary);
  margin-top: 0.25rem;
}

.last-access-time {
  font-size: 0.7rem;
  color: var(--text-light);
  margin-top: 0.25rem;
  font-style: italic;
}

.last-access-detail {
  font-size: 0.85rem;
  color: var(--text-secondary);
  margin-top: 0.75rem;
  font-style: italic;
}

/* Checkpoint Tasks section */
.checkpoint-tasks-section {
  margin-top: 0.75rem;
  padding: 0.75rem;
  background: var(--bg-secondary);
  border-radius: 8px;
}

.tasks-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.task-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  background: var(--card-bg);
  border: 1px solid var(--border-light);
  border-radius: 6px;
}

.task-item input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.task-text {
  flex: 1;
  font-size: 0.9rem;
  color: var(--text-dark);
}

.task-item.task-completed {
  opacity: 0.7;
}

.task-item.task-completed .task-text {
  text-decoration: line-through;
  color: var(--text-muted);
}

/* Modal */
.marshal-details-modal {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.details-section {
  padding-bottom: 1.5rem;
  border-bottom: 1px solid var(--border-light);
}

.details-section:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.details-section h4 {
  margin: 0 0 1rem 0;
  font-size: 0.95rem;
  color: var(--text-dark);
}

.contact-info {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.contact-row {
  display: flex;
  gap: 0.5rem;
}

.contact-label {
  color: var(--text-secondary);
  font-weight: 500;
}

.contact-row a {
  color: var(--brand-primary);
  text-decoration: none;
}

.contact-row a:hover {
  text-decoration: underline;
}

.no-contact,
.no-tasks {
  color: var(--text-secondary);
  font-style: italic;
}

.checked-in-details {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  padding: 1rem;
  background: var(--success-bg-light);
  border-radius: 8px;
}

.check-icon {
  font-size: 1.5rem;
  color: var(--success-dark);
}

.check-time-detail {
  margin: 0.25rem 0 0 0;
  font-size: 0.9rem;
  color: var(--text-dark);
}

.check-method {
  margin: 0.25rem 0 0 0;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.not-checked-in-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.not-checked-in {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 1rem;
  background: var(--danger-bg-light);
  border-radius: 8px;
  color: var(--danger-dark);
}

.check-in-btn {
  align-self: flex-start;
}

.not-checked-icon {
  font-size: 1.25rem;
}

.modal-tasks {
  max-height: 200px;
  overflow-y: auto;
}

.contact-actions-section {
  padding-top: 1rem;
}

.contact-buttons {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.btn {
  padding: 0.6rem 1.5rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  text-decoration: none;
  display: inline-block;
  text-align: center;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--brand-primary-hover);
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: white;
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}

@media (max-width: 768px) {
  .section-header {
    padding: 1rem 1.25rem;
  }

  .checkpoint-header {
    padding: 1rem 1.25rem;
  }

  .checkpoint-content {
    padding: 1rem 1.25rem;
  }

  .checkpoint-map {
    height: 150px;
  }

  .marshal-header {
    flex-direction: column;
    gap: 0.5rem;
  }

  .contact-buttons {
    flex-direction: column;
  }

  .contact-buttons .btn {
    width: 100%;
  }
}

</style>
