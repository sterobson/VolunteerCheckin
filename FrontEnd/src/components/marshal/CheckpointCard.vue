<template>
  <div class="checkpoint-accordion-section">
    <button
      class="checkpoint-accordion-header"
      :class="{ active: isExpanded, 'checked-in': assignment.effectiveIsCheckedIn }"
      @click="$emit('toggle')"
    >
      <div class="checkpoint-header-content">
        <div class="checkpoint-title-row">
          <span class="checkpoint-icon" v-html="getCheckpointIconSvg(assignment.location)"></span>
          <span v-if="assignment.effectiveIsCheckedIn" class="checkpoint-check-icon">✓</span>
          <span class="checkpoint-name">{{ assignment.location?.name || assignment.locationName }}</span>
          <span v-if="assignment.areaName" class="area-badge">{{ assignment.areaName }}</span>
          <span v-if="assignment.location?.startTime || assignment.location?.endTime" class="checkpoint-time-badge">
            {{ formatTimeRange(assignment.location.startTime, assignment.location.endTime) }}
          </span>
          <span v-if="formattedDistance" class="checkpoint-distance-badge">
            {{ formattedDistance }}
          </span>
        </div>
        <div v-if="assignment.location?.description" class="checkpoint-description-preview">
          {{ assignment.location.description }}
        </div>
      </div>
      <span class="accordion-icon">{{ isExpanded ? '−' : '+' }}</span>
    </button>
    <div v-if="isExpanded" class="checkpoint-accordion-content">
      <!-- Mini-map for this checkpoint -->
      <div v-if="assignment.location" class="checkpoint-mini-map">
        <CommonMap
          ref="mapRef"
          :locations="allLocations"
          :route="route"
          :center="{ lat: assignment.location.latitude, lng: assignment.location.longitude }"
          :zoom="16"
          :user-location="userLocation"
          :highlight-location-id="assignment.locationId"
          :marshal-mode="true"
          :simplify-non-highlighted="true"
          :clickable="hasDynamicAssignment"
          :show-fullscreen="true"
          :fullscreen-title="fullscreenTitle"
          :fullscreen-header-style="branding.headerStyle"
          :fullscreen-header-text-color="branding.headerTextColor"
          :toolbar-actions="toolbarActions"
          :hide-recenter-button="true"
          height="280px"
          @map-click="(e) => $emit('map-click', e)"
          @location-click="(e) => $emit('location-click', e)"
          @action-click="(e) => $emit('action-click', e)"
          @visibility-change="(e) => $emit('visibility-change', e)"
        >
          <!-- Update location button for dynamic checkpoints (only shown if marshal has permission) -->
          <template v-if="canUpdateDynamicLocation" #fullscreen-footer>
            <button
              class="btn btn-primary"
              @click="$emit('update-location')"
            >
              <svg viewBox="0 0 24 24" fill="currentColor" width="18" height="18" style="margin-right: 0.5rem;">
                <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
              </svg>
              Update location
            </button>
          </template>
        </CommonMap>
      </div>

      <!-- My check-in toggle button -->
      <div class="my-checkin-section">
        <CheckInToggleButton
          :is-checked-in="assignment.effectiveIsCheckedIn"
          :check-in-time="assignment.checkInTime"
          :check-in-method="assignment.checkInMethod"
          :checked-in-by="assignment.checkedInBy"
          :marshal-name="currentMarshalName"
          :is-loading="isCheckingIn"
          @toggle="$emit('check-in')"
        />
        <div v-if="checkInError" class="check-in-error">{{ checkInError }}</div>
      </div>

      <!-- Dynamic checkpoint location update -->
      <div v-if="isDynamic" class="dynamic-location-section">
        <div class="dynamic-location-header">
          <span class="dynamic-badge">Dynamic {{ assignment.location.resolvedCheckpointTerm || termsLower.checkpoint }}</span>
          <span v-if="assignment.location.lastLocationUpdate" class="last-update">
            Last updated: {{ formatDateTime(assignment.location.lastLocationUpdate) }}
          </span>
        </div>
        <!-- Only show update buttons if marshal has permission -->
        <div v-if="canUpdateDynamicLocation" class="dynamic-location-actions">
          <button
            @click="$emit('update-location')"
            class="btn btn-update-location"
            :disabled="updatingLocation"
          >
            <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
            </svg>
            Update location
          </button>
          <button
            @click="$emit('toggle-auto-update')"
            class="btn btn-auto-update"
            :class="{ active: autoUpdateEnabled }"
            :title="autoUpdateEnabled ? 'Stop auto-updating' : 'Auto-update every 60 seconds'"
          >
            <svg class="btn-icon" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z"/>
            </svg>
            {{ autoUpdateEnabled ? 'Stop' : 'Auto' }}
          </button>
        </div>
      </div>

      <!-- Marshals on this checkpoint -->
      <div class="checkpoint-marshals">
        <div class="marshals-label">{{ terms.people }}:</div>
        <!-- Simple view for non-area leads -->
        <div v-if="!isAreaLeadForCheckpoint" class="marshals-list">
          <span
            v-for="m in assignment.allMarshals"
            :key="m.marshalId"
            class="marshal-tag"
            :class="{ 'is-you': m.marshalId === currentMarshalId, 'checked-in': m.effectiveIsCheckedIn }"
          >
            {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
            <span v-if="m.effectiveIsCheckedIn" class="check-badge">✓</span>
          </span>
        </div>
        <!-- Expandable view for area leads -->
        <div v-else class="marshals-list-expanded">
          <div
            v-for="m in assignment.allMarshals"
            :key="m.marshalId"
            class="marshal-card-mini"
            :class="{
              'is-you': m.marshalId === currentMarshalId,
              'checked-in': m.effectiveIsCheckedIn,
              'expanded': expandedMarshalId === m.marshalId
            }"
          >
            <button
              class="marshal-card-header"
              @click="$emit('toggle-marshal', m.marshalId)"
            >
              <span class="marshal-name-text">
                {{ m.marshalName }}{{ m.marshalId === currentMarshalId ? ' (you)' : '' }}
              </span>
              <span class="marshal-status-icons">
                <span v-if="m.effectiveIsCheckedIn" class="check-badge">✓</span>
                <span class="expand-icon">{{ expandedMarshalId === m.marshalId ? '−' : '+' }}</span>
              </span>
            </button>
            <div v-if="expandedMarshalId === m.marshalId" class="marshal-details-panel">
              <div class="detail-row">
                <span class="detail-label">Status:</span>
                <span :class="m.effectiveIsCheckedIn ? 'status-checked-in' : 'status-not-checked-in'">
                  {{ m.effectiveIsCheckedIn ? 'Checked in' : 'Not checked in' }}
                </span>
              </div>
              <div v-if="m.effectiveIsCheckedIn && m.checkInTime" class="detail-row">
                <span class="detail-label">Check-in time:</span>
                <span>{{ formatCheckInTime(m.checkInTime) }}</span>
              </div>
              <div v-if="m.checkInMethod" class="detail-row">
                <span class="detail-label">Method:</span>
                <span>{{ formatCheckInMethod(m.checkInMethod) }}</span>
              </div>
              <div v-if="m.email" class="detail-row">
                <span class="detail-label">Email:</span>
                <a :href="'mailto:' + m.email">{{ m.email }}</a>
              </div>
              <div v-if="m.phoneNumber" class="detail-row">
                <span class="detail-label">Phone:</span>
                <a :href="'tel:' + m.phoneNumber">{{ m.phoneNumber }}</a>
              </div>
              <div v-if="m.email || m.phoneNumber" class="contact-buttons-mini">
                <a v-if="m.phoneNumber" :href="'tel:' + m.phoneNumber" class="btn btn-sm btn-primary">Call</a>
                <a v-if="m.phoneNumber" :href="'sms:' + m.phoneNumber" class="btn btn-sm btn-secondary">Text</a>
                <a v-if="m.email" :href="'mailto:' + m.email" class="btn btn-sm btn-secondary">Email</a>
              </div>
              <!-- Check-in/check-out button for area leads (not for yourself) -->
              <div v-if="m.marshalId !== currentMarshalId" class="checkpoint-marshal-checkin">
                <CheckInToggleButton
                  :is-checked-in="m.effectiveIsCheckedIn"
                  :check-in-time="m.checkInTime"
                  :check-in-method="m.checkInMethod"
                  :checked-in-by="m.checkedInBy"
                  :marshal-name="m.marshalName"
                  :is-loading="checkingInMarshalId === m.id"
                  @toggle="$emit('marshal-check-in', m)"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Area Contacts for this checkpoint -->
      <div v-if="assignment.areaContacts.length > 0" class="checkpoint-area-contacts">
        <div class="marshals-label">{{ terms.area }} contacts:</div>
        <div class="contact-list">
          <ContactCard
            v-for="contact in assignment.areaContacts"
            :key="contact.marshalId"
            :name="contact.marshalName"
            :role="contact.role"
            :phone="contact.phone"
            :email="contact.email"
          />
        </div>
      </div>

      <!-- Notes for this checkpoint -->
      <div v-if="notes.length > 0" class="checkpoint-notes">
        <div class="marshals-label">Notes:</div>
        <div class="checkpoint-notes-list">
          <NoteCard
            v-for="note in notes"
            :key="note.noteId"
            :title="note.title"
            :content="note.content"
            :priority="note.priority"
            :is-pinned="note.isPinned"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, ref, computed, defineExpose, inject, toValue, reactive } from 'vue';
import CommonMap from '../common/CommonMap.vue';
import CheckInToggleButton from '../common/CheckInToggleButton.vue';
import ContactCard from './ContactCard.vue';
import NoteCard from './NoteCard.vue';
import { useTerminology } from '../../composables/useTerminology';
import { calculateDistance } from '../../utils/coordinateUtils';
import { canMarshalUpdateDynamicLocation } from '../../utils/scopeUtils';

const { terms, termsLower } = useTerminology();
const injectedBranding = inject('marshalBranding', {
  headerStyle: {},
  headerTextColor: '',
});

// Unwrap refs from injected branding (they may be computed refs or plain values)
// Use reactive() so Vue auto-unwraps the computed refs in templates
const branding = reactive({
  headerStyle: computed(() => toValue(injectedBranding.headerStyle)),
  headerTextColor: computed(() => toValue(injectedBranding.headerTextColor)),
});

const props = defineProps({
  assignment: {
    type: Object,
    required: true,
  },
  isExpanded: {
    type: Boolean,
    default: false,
  },
  allLocations: {
    type: Array,
    default: () => [],
  },
  route: {
    type: Array,
    default: () => [],
  },
  userLocation: {
    type: Object,
    default: null,
  },
  toolbarActions: {
    type: Array,
    default: () => [],
  },
  hasDynamicAssignment: {
    type: Boolean,
    default: false,
  },
  currentMarshalId: {
    type: String,
    default: '',
  },
  currentMarshalName: {
    type: String,
    default: '',
  },
  isCheckingIn: {
    type: Boolean,
    default: false,
  },
  checkInError: {
    type: String,
    default: '',
  },
  checkingInMarshalId: {
    type: String,
    default: null,
  },
  isAreaLeadForCheckpoint: {
    type: Boolean,
    default: false,
  },
  expandedMarshalId: {
    type: String,
    default: null,
  },
  notes: {
    type: Array,
    default: () => [],
  },
  updatingLocation: {
    type: Boolean,
    default: false,
  },
  autoUpdateEnabled: {
    type: Boolean,
    default: false,
  },
  areaLeadAreaIds: {
    type: Array,
    default: () => [],
  },
  allAssignmentLocationIds: {
    type: Array,
    default: () => [],
  },
});

defineEmits([
  'toggle',
  'map-click',
  'location-click',
  'action-click',
  'visibility-change',
  'update-location',
  'check-in',
  'toggle-marshal',
  'marshal-check-in',
  'toggle-auto-update',
]);

const mapRef = ref(null);

const isDynamic = computed(() => {
  return props.assignment.location?.isDynamic;
});

// Fullscreen title combines name and description
const fullscreenTitle = computed(() => {
  const name = props.assignment.location?.name || props.assignment.locationName;
  const description = props.assignment.location?.description;
  if (description) {
    return `${name} — ${description}`;
  }
  return name;
});

// Check if the current marshal can update this dynamic location based on scope configuration
const canUpdateDynamicLocation = computed(() => {
  if (!isDynamic.value) return false;

  const location = props.assignment.location;
  const scopeConfigurations = location?.locationUpdateScopeConfigurations ||
    location?.LocationUpdateScopeConfigurations || [];

  return canMarshalUpdateDynamicLocation({
    scopeConfigurations,
    marshalId: props.currentMarshalId,
    marshalAssignmentLocationIds: props.allAssignmentLocationIds,
    areaLeadAreaIds: props.areaLeadAreaIds,
    locationId: props.assignment.locationId,
    locationAreaIds: props.assignment.areaIds || [],
  });
});

// Calculate distance from user location to checkpoint
const distanceToCheckpoint = computed(() => {
  if (!props.userLocation?.lat || !props.userLocation?.lng) return null;
  const loc = props.assignment.location;
  if (!loc?.latitude || !loc?.longitude) return null;
  return calculateDistance(props.userLocation.lat, props.userLocation.lng, loc.latitude, loc.longitude);
});

// Format distance: < 1000m = round to nearest 10m, >= 1000m = round to nearest 0.1km
const formattedDistance = computed(() => {
  const dist = distanceToCheckpoint.value;
  if (dist == null) return null;
  if (dist < 1000) {
    return `${Math.round(dist / 10) * 10} m`;
  }
  return `${(Math.round(dist / 100) / 10).toFixed(1)} km`;
});

defineExpose({
  mapRef,
});

// Helper functions
const getCheckpointIconSvg = (location) => {
  const iconType = location?.iconType || 'checkpoint';
  const iconColor = location?.iconColor || '#667eea';

  const icons = {
    checkpoint: `<svg viewBox="0 0 24 24" fill="${iconColor}" width="20" height="20"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>`,
    flag: `<svg viewBox="0 0 24 24" fill="${iconColor}" width="20" height="20"><path d="M14.4 6L14 4H5v17h2v-7h5.6l.4 2h7V6z"/></svg>`,
    star: `<svg viewBox="0 0 24 24" fill="${iconColor}" width="20" height="20"><path d="M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z"/></svg>`,
    water: `<svg viewBox="0 0 24 24" fill="${iconColor}" width="20" height="20"><path d="M12 2c-5.33 4.55-8 8.48-8 11.8 0 4.98 3.8 8.2 8 8.2s8-3.22 8-8.2c0-3.32-2.67-7.25-8-11.8z"/></svg>`,
    medical: `<svg viewBox="0 0 24 24" fill="${iconColor}" width="20" height="20"><path d="M19 3H5c-1.1 0-1.99.9-1.99 2L3 19c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-1 11h-4v4h-4v-4H6v-4h4V6h4v4h4v4z"/></svg>`,
  };

  return icons[iconType] || icons.checkpoint;
};

const formatTimeRange = (start, end) => {
  const formatTime = (time) => {
    if (!time) return '';
    const date = new Date(time);
    return date.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
  };

  if (start && end) return `${formatTime(start)} - ${formatTime(end)}`;
  if (start) return `from ${formatTime(start)}`;
  if (end) return `until ${formatTime(end)}`;
  return '';
};

const formatCheckInTime = (time) => {
  if (!time) return '';
  return new Date(time).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
};

const formatCheckInMethod = (method) => {
  const methods = {
    manual: 'Manual',
    qr: 'QR Code',
    gps: 'GPS',
    admin: 'Admin',
  };
  return methods[method] || method;
};

const formatDateTime = (dateString) => {
  if (!dateString) return '';
  return new Date(dateString).toLocaleString();
};
</script>

<style scoped>
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

.checkpoint-accordion-header.checked-in {
  background: var(--checked-in-bg);
}

.checkpoint-accordion-header.checked-in.active {
  background: var(--success-bg-light);
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
  flex-wrap: wrap;
  gap: 0.5rem;
}

.checkpoint-icon {
  display: flex;
  align-items: center;
  flex-shrink: 0;
}

.checkpoint-check-icon {
  color: var(--checked-in-text);
  font-weight: 700;
  font-size: 1rem;
}

.checkpoint-name {
  font-weight: 600;
  color: var(--text-dark);
}

.area-badge {
  font-size: 0.75rem;
  padding: 0.15rem 0.5rem;
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
  border-radius: 10px;
  font-weight: 500;
}

.checkpoint-time-badge {
  font-size: 0.75rem;
  padding: 0.15rem 0.5rem;
  background: var(--info-bg);
  color: var(--info-blue);
  border-radius: 10px;
  font-weight: 500;
}

.checkpoint-distance-badge {
  font-size: 0.75rem;
  padding: 0.15rem 0.5rem;
  background: var(--bg-muted);
  color: var(--text-secondary);
  border-radius: 10px;
  font-weight: 500;
}

.checkpoint-description-preview {
  font-size: 0.85rem;
  color: var(--text-secondary);
  font-weight: 400;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--brand-primary);
  flex-shrink: 0;
}

.checkpoint-accordion-content {
  padding: 1rem 1.25rem;
}

.checkpoint-mini-map {
  margin-bottom: 1rem;
  border-radius: 8px;
  overflow: hidden;
}

.my-checkin-section {
  margin-bottom: 1rem;
}

.check-in-error {
  color: var(--danger);
  font-size: 0.85rem;
  margin-top: 0.5rem;
}

.checkpoint-marshals {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--card-bg);
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
  flex-wrap: wrap;
  gap: 0.5rem;
}

.marshal-tag {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.35rem 0.65rem;
  background: var(--bg-muted);
  border-radius: 16px;
  font-size: 0.85rem;
  color: var(--text-dark);
}

.marshal-tag.is-you {
  background: var(--brand-primary-bg);
  color: var(--brand-primary);
  font-weight: 500;
}

.marshal-tag.checked-in {
  background: var(--checked-in-bg);
  color: var(--checked-in-text);
}

.check-badge {
  font-size: 0.75rem;
  color: var(--checked-in-text);
}

.marshals-list-expanded {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.marshal-card-mini {
  background: var(--bg-muted);
  border-radius: 8px;
  overflow: hidden;
}

.marshal-card-mini.is-you {
  background: var(--brand-primary-bg);
}

.marshal-card-mini.checked-in {
  background: var(--checked-in-bg);
}

.marshal-card-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.5rem 0.75rem;
  background: transparent;
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 0.9rem;
}

.marshal-name-text {
  font-weight: 500;
  color: var(--text-dark);
}

.marshal-status-icons {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.expand-icon {
  font-size: 1rem;
  color: var(--text-secondary);
}

.marshal-details-panel {
  padding: 0.75rem;
  background: var(--card-bg);
  border-top: 1px solid var(--border-light);
}

.detail-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.35rem;
  font-size: 0.85rem;
}

.detail-label {
  color: var(--text-secondary);
  min-width: 80px;
}

.status-checked-in {
  color: var(--checked-in-text);
  font-weight: 500;
}

.status-not-checked-in {
  color: var(--text-muted);
}

.contact-buttons-mini {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.btn {
  padding: 0.4rem 0.75rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 500;
  font-size: 0.85rem;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}

.btn-primary {
  background: var(--brand-primary);
  color: white;
}

.btn-secondary {
  background: var(--btn-cancel-bg);
  color: var(--btn-cancel-text);
}

.btn-sm {
  padding: 0.3rem 0.6rem;
  font-size: 0.8rem;
}

.checkpoint-marshal-checkin {
  margin-top: 0.75rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--border-light);
}

.checkpoint-area-contacts {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--card-bg);
  border-radius: 8px;
}

.contact-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checkpoint-notes {
  margin: 0.75rem 0;
  padding: 0.75rem;
  background: var(--card-bg);
  border-radius: 8px;
}

.checkpoint-notes-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.dynamic-location-section {
  margin-top: 1rem;
  padding: 0.75rem;
  background: var(--info-bg);
  border-radius: 8px;
}

.dynamic-location-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.dynamic-badge {
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--info-blue);
  background: white;
  padding: 0.25rem 0.5rem;
  border-radius: 12px;
}

.last-update {
  font-size: 0.75rem;
  color: var(--text-secondary);
}

.dynamic-location-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-update-location,
.btn-auto-update {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0.5rem 0.75rem;
  font-size: 0.85rem;
  font-weight: 500;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-update-location {
  background: var(--brand-primary);
  color: white;
}

.btn-update-location:hover:not(:disabled) {
  background: var(--brand-primary-hover);
}

.btn-auto-update {
  background: var(--bg-muted);
  color: var(--text-secondary);
}

.btn-auto-update.active {
  background: var(--success-bg-light);
  color: var(--success-dark);
}

.btn-icon {
  width: 16px;
  height: 16px;
}

@media (max-width: 768px) {
  .checkpoint-accordion-content {
    padding: 0.75rem 1rem;
  }

  .checkpoint-area-contacts {
    padding: 0.5rem;
  }
}
</style>
