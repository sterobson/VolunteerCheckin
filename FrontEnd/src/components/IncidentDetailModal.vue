<template>
  <BaseModal
    :show="show"
    :title="modalTitle"
    size="large"
    @close="$emit('close')"
  >
    <template #header>
      <div class="incident-detail-header">
        <div class="header-title-row">
          <span class="severity-indicator" :class="incident?.severity"></span>
          <h2 class="modal-title">{{ modalTitle }}</h2>
        </div>
        <div class="header-badges">
          <span class="severity-badge" :class="incident?.severity">
            {{ formatSeverity(incident?.severity) }}
          </span>
          <!-- Status dropdown (clickable when canManage) -->
          <div v-if="canManage" class="status-dropdown-container" ref="statusDropdownRef">
            <button
              type="button"
              class="status-badge status-dropdown-trigger disable-on-load"
              :class="incident?.status"
              @click="toggleStatusDropdown"
              :disabled="updating"
            >
              {{ formatStatus(incident?.status) }}
              <span class="dropdown-arrow">▼</span>
            </button>
            <div v-if="showStatusDropdown" class="status-dropdown-menu">
              <button
                v-for="status in availableStatuses"
                :key="status.value"
                type="button"
                class="status-dropdown-item"
                :class="{ active: incident?.status === status.value }"
                @click="selectStatus(status.value)"
                :disabled="incident?.status === status.value"
              >
                <span class="status-dot" :class="status.value"></span>
                {{ status.label }}
              </button>
            </div>
          </div>
          <span v-else class="status-badge" :class="incident?.status">
            {{ formatStatus(incident?.status) }}
          </span>
        </div>
      </div>
    </template>

    <div v-if="incident" class="incident-detail-content">
      <!-- Title & Description Section (Accordion) -->
      <div class="accordion-section">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.description }"
          @click="toggleSection('description')"
        >
          <span class="accordion-title">Description</span>
          <span class="accordion-arrow">{{ expandedSections.description ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.description" class="accordion-content">
          <p class="description-text">{{ incident.description }}</p>
        </div>
      </div>

      <!-- Time & Location Section (Accordion) -->
      <div class="accordion-section">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.timeLocation }"
          @click="toggleSection('timeLocation')"
        >
          <span class="accordion-title">Time & location</span>
          <span class="accordion-arrow">{{ expandedSections.timeLocation ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.timeLocation" class="accordion-content">
          <div class="info-grid">
            <div class="info-item">
              <span class="info-label">Incident time</span>
              <span class="info-value">{{ formatDateTime(incident.incidentTime) }}</span>
            </div>
            <div class="info-item">
              <span class="info-label">Reported at</span>
              <span class="info-value">{{ formatDateTime(incident.createdAt) }}</span>
            </div>
            <div v-if="incident.context?.checkpoint" class="info-item">
              <span class="info-label">{{ termsSentence.checkpoint }}</span>
              <span class="info-value">{{ formatCheckpointName(incident.context.checkpoint) }}</span>
            </div>
            <div v-if="incident.area" class="info-item">
              <span class="info-label">{{ termsSentence.area }}</span>
              <span class="info-value">{{ incident.area.areaName }}</span>
            </div>
            <div v-if="hasGpsLocation" class="info-item">
              <span class="info-label">GPS location</span>
              <span class="info-value">
                {{ incident.latitude.toFixed(6) }}, {{ incident.longitude.toFixed(6) }}
              </span>
            </div>
          </div>
          <!-- Map showing incident location with route and checkpoints -->
          <div v-if="hasGpsLocation" class="incident-map-container">
            <CommonMap
              :locations="checkpoints"
              :route="route"
              :route-color="routeColor"
              :route-style="routeStyle"
              :route-weight="routeWeight"
              :layers="layers"
              :center="incidentMapCenter"
              :zoom="15"
              :user-location="incidentMarker"
              height="200px"
            />
          </div>
        </div>
      </div>

      <!-- Reporter Section (Accordion) -->
      <div class="accordion-section">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.reporter }"
          @click="toggleSection('reporter')"
        >
          <span class="accordion-title">Reported by</span>
          <span class="accordion-preview">{{ incident.reportedBy?.name || 'Unknown' }}</span>
          <span class="accordion-arrow">{{ expandedSections.reporter ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.reporter" class="accordion-content">
          <div class="reporter-info">
            <span class="reporter-name">{{ incident.reportedBy?.name || 'Unknown' }}</span>
          </div>
        </div>
      </div>

      <!-- Context: Who was at the checkpoint (Accordion) -->
      <div v-if="sortedMarshalsAtCheckpoint.length" class="accordion-section">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.marshals }"
          @click="toggleSection('marshals')"
        >
          <span class="accordion-title">{{ terms.peoplePlural }} at {{ terms.checkpoint.toLowerCase() }}</span>
          <span class="accordion-preview">{{ sortedMarshalsAtCheckpoint.length }}</span>
          <span class="accordion-arrow">{{ expandedSections.marshals ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.marshals" class="accordion-content">
          <div class="marshals-list">
            <div
              v-for="marshal in sortedMarshalsAtCheckpoint"
              :key="marshal.marshalId"
              class="marshal-item"
              :class="{ 'checked-in': marshal.wasCheckedIn }"
            >
              <span class="marshal-name">{{ marshal.name }}</span>
              <span v-if="marshal.wasCheckedIn" class="checkin-badge success">
                Checked in {{ formatRelativeTime(marshal.checkInTime) }}
              </span>
              <span v-else class="checkin-badge not-checked-in">
                Not checked in
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- Updates Section (Accordion) -->
      <div class="accordion-section">
        <button
          type="button"
          class="accordion-header"
          :class="{ expanded: expandedSections.notes }"
          @click="toggleSection('notes')"
        >
          <span class="accordion-title">Updates</span>
          <span v-if="incident.updates?.length" class="accordion-preview">{{ incident.updates.length }}</span>
          <span class="accordion-arrow">{{ expandedSections.notes ? '▲' : '▼' }}</span>
        </button>
        <div v-if="expandedSections.notes" class="accordion-content">
          <div v-if="incident.updates?.length" class="notes-timeline">
            <div
              v-for="(update, index) in incident.updates"
              :key="index"
              class="note-item"
            >
              <div class="note-header">
                <span class="note-author">{{ update.authorName || 'System' }}</span>
                <span class="note-time">{{ formatRelativeTime(update.timestamp) }}</span>
              </div>
              <div v-if="update.statusChange" class="note-status-change">
                Status changed to <span class="status-badge" :class="update.statusChange">
                  {{ formatStatus(update.statusChange) }}
                </span>
              </div>
              <p v-else-if="update.note" class="note-text">{{ update.note }}</p>
            </div>
          </div>
          <div v-else class="no-notes">
            No updates yet.
          </div>
          <button
            type="button"
            class="btn btn-primary add-note-btn disable-on-load"
            @click="$emit('open-add-note')"
          >
            Add update
          </button>
        </div>
      </div>
    </div>

    <template #footer>
      <button class="btn btn-secondary" @click="$emit('close')">Close</button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import BaseModal from './BaseModal.vue';
import CommonMap from './common/CommonMap.vue';
import { useTerminology } from '../composables/useTerminology';

const { terms, termsSentence } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    required: true,
  },
  incident: {
    type: Object,
    default: null,
  },
  canManage: {
    type: Boolean,
    default: false,
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
  layers: {
    type: Array,
    default: () => [],
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'status-change', 'open-add-note']);

const updating = ref(false);
const showStatusDropdown = ref(false);
const statusDropdownRef = ref(null);

// Accordion state - description open by default
const expandedSections = ref({
  description: true,
  timeLocation: false,
  reporter: false,
  marshals: false,
  notes: false,
});

// Reset state when modal opens
watch(() => props.show, (isShowing) => {
  if (isShowing) {
    expandedSections.value = {
      description: true,
      timeLocation: false,
      reporter: false,
      marshals: false,
      notes: false,
    };
    showStatusDropdown.value = false;
  }
});

// Close dropdown when clicking outside
const handleClickOutside = (event) => {
  if (statusDropdownRef.value && !statusDropdownRef.value.contains(event.target)) {
    showStatusDropdown.value = false;
  }
};

watch(showStatusDropdown, (isOpen) => {
  if (isOpen) {
    document.addEventListener('click', handleClickOutside);
  } else {
    document.removeEventListener('click', handleClickOutside);
  }
});

const toggleStatusDropdown = () => {
  showStatusDropdown.value = !showStatusDropdown.value;
};

const selectStatus = (newStatus) => {
  if (props.incident?.status === newStatus) return;
  showStatusDropdown.value = false;
  handleStatusChange(newStatus);
};

const toggleSection = (section) => {
  // Only one section open at a time
  const isCurrentlyOpen = expandedSections.value[section];
  // Close all sections
  Object.keys(expandedSections.value).forEach(key => {
    expandedSections.value[key] = false;
  });
  // Open the clicked section if it wasn't already open
  if (!isCurrentlyOpen) {
    expandedSections.value[section] = true;
  }
};

// Check if incident has GPS location
const hasGpsLocation = computed(() => {
  return props.incident?.latitude != null && props.incident?.longitude != null;
});

// Map center for displaying incident location
const incidentMapCenter = computed(() => {
  if (hasGpsLocation.value) {
    return { lat: props.incident.latitude, lng: props.incident.longitude };
  }
  return null;
});

// Incident marker for displaying on map
const incidentMarker = computed(() => {
  if (!hasGpsLocation.value) return null;
  return {
    lat: props.incident.latitude,
    lng: props.incident.longitude,
  };
});

// Modal title - use title if available, otherwise truncated description
const modalTitle = computed(() => {
  if (!props.incident) return 'Incident Details';
  if (props.incident.title) return props.incident.title;
  // Truncate description to ~50 chars
  const desc = props.incident.description || '';
  if (desc.length <= 50) return desc;
  return desc.substring(0, 47) + '...';
});

// Sort marshals alphabetically by name
const sortedMarshalsAtCheckpoint = computed(() => {
  const marshals = props.incident?.context?.marshalsPresentAtCheckpoint || [];
  return [...marshals].sort((a, b) =>
    (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' })
  );
});

const availableStatuses = [
  { value: 'open', label: 'Open' },
  { value: 'acknowledged', label: 'Acknowledged' },
  { value: 'in_progress', label: 'In Progress' },
  { value: 'resolved', label: 'Resolved' },
  { value: 'closed', label: 'Closed' },
];

const formatSeverity = (severity) => {
  const labels = {
    low: 'Low',
    medium: 'Medium',
    high: 'High',
    critical: 'Critical',
  };
  return labels[severity] || severity;
};

const formatStatus = (status) => {
  const labels = {
    open: 'Open',
    acknowledged: 'Acknowledged',
    in_progress: 'In Progress',
    resolved: 'Resolved',
    closed: 'Closed',
  };
  return labels[status] || status;
};

// Format checkpoint name with description (truncated if needed)
const formatCheckpointName = (checkpoint) => {
  if (!checkpoint) return '';
  let name = checkpoint.name || '';
  if (checkpoint.description) {
    const desc = checkpoint.description;
    const maxDescLen = 40;
    const truncatedDesc = desc.length > maxDescLen ? desc.substring(0, maxDescLen - 3) + '...' : desc;
    name += ` - ${truncatedDesc}`;
  }
  return name;
};

// Format date/time respecting locale, no seconds, converted to local timezone
const formatDateTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  // Format with locale settings, no seconds
  return date.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
};

const formatRelativeTime = (dateString) => {
  if (!dateString) return '';
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now - date;
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString();
};

const handleStatusChange = async (newStatus) => {
  if (props.incident?.status === newStatus) return;
  updating.value = true;
  try {
    emit('status-change', { incidentId: props.incident.incidentId, status: newStatus });
  } finally {
    updating.value = false;
  }
};
</script>

<style scoped>
.incident-detail-header {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.header-title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.severity-indicator {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  flex-shrink: 0;
}

.severity-indicator.critical { background: var(--severity-critical); }
.severity-indicator.high { background: var(--severity-high); }
.severity-indicator.medium { background: var(--severity-medium); }
.severity-indicator.low { background: var(--severity-low); }

.modal-title {
  margin: 0;
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.header-badges {
  display: flex;
  gap: 0.5rem;
}

.severity-badge,
.status-badge {
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

/* Status dropdown */
.status-dropdown-container {
  position: relative;
}

.status-dropdown-trigger {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  cursor: pointer;
  border: none;
  transition: opacity 0.2s;
}

.status-dropdown-trigger:hover:not(:disabled) {
  opacity: 0.85;
}

.status-dropdown-trigger:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.dropdown-arrow {
  font-size: 0.55rem;
  opacity: 0.7;
}

.status-dropdown-menu {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 0.25rem;
  background: var(--bg-primary);
  border: 1px solid var(--border-color);
  border-radius: 6px;
  box-shadow: var(--shadow-md);
  z-index: 100;
  min-width: 140px;
  overflow: hidden;
}

.status-dropdown-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.5rem 0.75rem;
  border: none;
  background: transparent;
  color: var(--text-primary);
  font-size: 0.8rem;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.15s;
}

.status-dropdown-item:hover:not(:disabled) {
  background: var(--bg-hover);
}

.status-dropdown-item.active {
  background: var(--bg-secondary);
  font-weight: 600;
}

.status-dropdown-item:disabled {
  cursor: default;
}

.status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.status-dot.open { background: var(--status-open); }
.status-dot.acknowledged { background: var(--status-acknowledged); }
.status-dot.in_progress { background: var(--status-in-progress); }
.status-dot.resolved { background: var(--status-resolved); }
.status-dot.closed { background: var(--status-closed); }

.severity-badge.critical { background: var(--severity-critical-bg); color: var(--severity-critical-text); }
.severity-badge.high { background: var(--severity-high-bg); color: var(--severity-high-text); }
.severity-badge.medium { background: var(--severity-medium-bg); color: var(--severity-medium-text); }
.severity-badge.low { background: var(--severity-low-bg); color: var(--severity-low-text); }

.status-badge.open { background: var(--status-open-bg); color: var(--status-open); }
.status-badge.acknowledged { background: var(--status-acknowledged-bg); color: var(--status-acknowledged); }
.status-badge.in_progress { background: var(--status-in-progress-bg); color: var(--status-in-progress); }
.status-badge.resolved { background: var(--status-resolved-bg); color: var(--status-resolved); }
.status-badge.closed { background: var(--status-closed-bg); color: var(--status-closed); }

.incident-detail-content {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

/* Accordion styles */
.accordion-section {
  border: 1px solid var(--border-color);
  border-radius: 6px;
  overflow: hidden;
}

.accordion-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  width: 100%;
  padding: 0.6rem 0.75rem;
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
  background: var(--bg-tertiary);
  border-bottom: 1px solid var(--border-color);
}

.accordion-title {
  font-weight: 600;
  font-size: 0.85rem;
  color: var(--text-primary);
  flex: 1;
}

.accordion-preview {
  font-size: 0.8rem;
  color: var(--text-muted);
  margin-right: 0.5rem;
}

.accordion-arrow {
  color: var(--text-muted);
  font-size: 0.65rem;
  flex-shrink: 0;
}

.accordion-content {
  padding: 0.75rem;
  background: var(--bg-primary);
}

.description-text {
  margin: 0;
  line-height: 1.5;
  color: var(--text-primary);
  white-space: pre-wrap;
  font-size: 0.9rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 0.75rem;
}

.incident-map-container {
  margin-top: 0.75rem;
}

.incident-location-map {
  height: 200px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--border-color);
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.info-label {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.info-value {
  font-size: 0.85rem;
  color: var(--text-primary);
}

.reporter-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.reporter-name {
  font-weight: 500;
  color: var(--text-primary);
}

.marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.marshal-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.4rem 0.6rem;
  background: var(--bg-secondary);
  border-radius: 4px;
}

.marshal-item.checked-in {
  background: var(--success-bg-light);
}

.marshal-name {
  font-weight: 500;
  font-size: 0.85rem;
  color: var(--text-primary);
}

.checkin-badge {
  font-size: 0.7rem;
  padding: 0.15rem 0.4rem;
  border-radius: 3px;
}

.checkin-badge.success {
  background: var(--success-border);
  color: var(--status-resolved);
}

.checkin-badge.not-checked-in {
  background: var(--danger-border);
  color: var(--danger-dark);
}

.notes-timeline {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.note-item {
  padding: 0.6rem;
  background: var(--bg-secondary);
  border-radius: 4px;
}

.note-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.4rem;
}

.note-author {
  font-weight: 500;
  font-size: 0.85rem;
  color: var(--text-primary);
}

.note-time {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.note-status-change {
  margin-bottom: 0.4rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.note-status-change .status-badge {
  font-size: 0.65rem;
  padding: 0.1rem 0.4rem;
}

.note-text {
  margin: 0;
  font-size: 0.85rem;
  color: var(--text-primary);
  line-height: 1.4;
}

.no-notes {
  color: var(--text-muted);
  font-style: italic;
  font-size: 0.85rem;
  margin-bottom: 0.75rem;
}

.add-note-btn {
  margin-top: 0;
}

.btn {
  padding: 0.4rem 0.85rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--accent-primary-hover);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

@media (max-width: 480px) {
  .info-grid {
    grid-template-columns: 1fr;
  }

  .status-buttons {
    flex-direction: column;
  }

  .status-button {
    text-align: center;
  }
}
</style>
