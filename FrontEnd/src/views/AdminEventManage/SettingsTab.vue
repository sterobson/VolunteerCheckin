<template>
  <div class="settings-tab">
    <div class="accordion">
      <!-- Administrators Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'admins' }"
          @click="toggleSection('admins')"
        >
          <span class="accordion-title">Administrators ({{ admins.length }})</span>
          <span class="accordion-icon">{{ expandedSection === 'admins' ? '-' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'admins'" class="accordion-content">
          <AdminsList
            :admins="admins"
            @add-admin="$emit('add-admin')"
            @remove-admin="$emit('remove-admin', $event)"
          />
        </div>
      </div>

      <!-- Terminology Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'terminology' }"
          @click="toggleSection('terminology')"
        >
          <span class="accordion-title">Terminology</span>
          <span class="accordion-icon">{{ expandedSection === 'terminology' ? '-' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'terminology'" class="accordion-content">
          <p class="section-description">
            Customise how things are named throughout the application.
          </p>

          <div class="terminology-grid">
            <div class="term-group">
              <label>{{ terms.people }} are called</label>
              <select
                v-model="localForm.peopleTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.people" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.checkpoints }} are called</label>
              <select
                v-model="localForm.checkpointTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in checkpointOptionsWithLabels" :key="option.value" :value="option.value">
                  {{ option.label }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.areas }} are called</label>
              <select
                v-model="localForm.areaTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.area" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.checklists }} are called</label>
              <select
                v-model="localForm.checklistTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.checklist" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>

            <div class="term-group">
              <label>{{ terms.course }} is called</label>
              <select
                v-model="localForm.courseTerm"
                class="form-input"
                @change="emitFormChange"
              >
                <option v-for="option in terminologyOptions.course" :key="option" :value="option">
                  {{ option }}
                </option>
              </select>
            </div>
          </div>

          <div class="form-actions">
            <button @click="handleSubmit" class="btn btn-primary" :disabled="!formDirty">
              Save
            </button>
          </div>
        </div>
      </div>

      <!-- Checkpoint appearance section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'appearance' }"
          @click="toggleSection('appearance')"
        >
          <span class="accordion-title">{{ terms.checkpoint }} appearance</span>
          <span class="accordion-icon">{{ expandedSection === 'appearance' ? '-' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'appearance'" class="accordion-content">
          <p class="section-description">
            Set the default marker style for all {{ termsLower.checkpoints }} in this event.
            Individual {{ termsLower.areas }} and {{ termsLower.checkpoints }} can override this setting.
          </p>

          <CheckpointStylePicker
            :style-type="localForm.defaultCheckpointStyleType || 'default'"
            :style-color="localForm.defaultCheckpointStyleColor || ''"
            :style-background-shape="localForm.defaultCheckpointStyleBackgroundShape || ''"
            :style-background-color="localForm.defaultCheckpointStyleBackgroundColor || ''"
            :style-border-color="localForm.defaultCheckpointStyleBorderColor || ''"
            :style-icon-color="localForm.defaultCheckpointStyleIconColor || ''"
            :style-size="localForm.defaultCheckpointStyleSize || ''"
            :style-map-rotation="localForm.defaultCheckpointStyleMapRotation ?? ''"
            icon-label="Default marker style"
            level="event"
            :show-preview="true"
            @update:style-type="handleStyleChange('defaultCheckpointStyleType', $event)"
            @update:style-color="handleStyleChange('defaultCheckpointStyleColor', $event)"
            @update:style-background-shape="handleStyleChange('defaultCheckpointStyleBackgroundShape', $event)"
            @update:style-background-color="handleStyleChange('defaultCheckpointStyleBackgroundColor', $event)"
            @update:style-border-color="handleStyleChange('defaultCheckpointStyleBorderColor', $event)"
            @update:style-icon-color="handleStyleChange('defaultCheckpointStyleIconColor', $event)"
            @update:style-size="handleStyleChange('defaultCheckpointStyleSize', $event)"
            @update:style-map-rotation="handleStyleChange('defaultCheckpointStyleMapRotation', $event)"
          />

          <div class="form-actions">
            <button @click="handleSubmit" class="btn btn-primary" :disabled="!formDirty">
              Save
            </button>
          </div>
        </div>
      </div>

      <!-- Marshal Mode Branding Section -->
      <div class="accordion-section">
        <button
          class="accordion-header"
          :class="{ active: expandedSection === 'branding' }"
          @click="toggleSection('branding')"
        >
          <span class="accordion-title">{{ terms.person }} branding</span>
          <span class="accordion-icon">{{ expandedSection === 'branding' ? '-' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'branding'" class="accordion-content">
          <p class="section-description">
            Customise the appearance of the {{ termsLower.person }} view to match your organisation's branding.
          </p>

          <MarshalModeMockup
            ref="brandingMockupRef"
            :branding="brandingData"
            :event-name="localForm.name"
            :event-date="formatEventDate(localForm.eventDate)"
            :event-id="eventId"
            :admin-email="adminEmail"
            @update:branding="handleBrandingChange"
            @logo-staged-change="handleLogoStagedChange"
          />

          <div class="form-actions">
            <button @click="resetBranding" class="btn btn-secondary" :disabled="isSaving">
              Reset to default
            </button>
            <button @click="handleSubmit" class="btn btn-primary" :disabled="(!formDirty && !hasLogoChanges) || isSaving">
              {{ isSaving ? 'Saving...' : 'Save' }}
            </button>
          </div>
        </div>
      </div>

      <!-- Danger Zone Section -->
      <div class="accordion-section danger-section">
        <button
          class="accordion-header danger-header"
          :class="{ active: expandedSection === 'danger' }"
          @click="toggleSection('danger')"
        >
          <span class="accordion-title">Danger zone</span>
          <span class="accordion-icon">{{ expandedSection === 'danger' ? '-' : '+' }}</span>
        </button>
        <div v-if="expandedSection === 'danger'" class="accordion-content">
          <div class="danger-zone-content">
            <div class="danger-item">
              <div class="danger-info">
                <h4>Delete this event</h4>
                <p>
                  Once you delete an event, there is no going back. All data associated with this event
                  will be permanently deleted, including all {{ termsLower.checkpoints }}, {{ termsLower.areas }},
                  {{ termsLower.people }}, assignments, and {{ termsLower.checklists }}.
                </p>
              </div>
              <button
                class="btn btn-danger"
                @click="showDeleteConfirmation"
                :disabled="isDeleting"
              >
                {{ isDeleting ? 'Deleting...' : 'Delete event' }}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Delete Confirmation Modal -->
    <Teleport to="body">
      <div v-if="showDeleteModal" class="modal-overlay" @click.self="cancelDelete">
        <div class="modal delete-modal">
          <h2>Delete event?</h2>
          <p class="delete-warning">
            You are about to permanently delete <strong>{{ localForm.name }}</strong>.
            This action cannot be undone.
          </p>
          <p class="delete-details">
            All event data will be deleted, including:
          </p>
          <ul class="delete-list">
            <li>{{ termsSentence.course }} and route data</li>
            <li>All {{ termsLower.checkpoints }} and {{ termsLower.areas }}</li>
            <li>All {{ termsLower.people }} and their assignments</li>
            <li>All {{ termsLower.checklists }} and completions</li>
            <li>All incidents and notes</li>
            <li>All contacts and administrator access</li>
          </ul>
          <p class="delete-confirm-text">
            To confirm, type the event name: <strong>{{ localForm.name }}</strong>
          </p>
          <input
            v-model="deleteConfirmText"
            type="text"
            class="form-input delete-confirm-input"
            :placeholder="localForm.name"
            @keydown.enter="confirmDelete"
          />
          <div class="modal-actions">
            <button class="btn btn-secondary" @click="cancelDelete">Cancel</button>
            <button
              class="btn btn-danger"
              @click="confirmDelete"
              :disabled="deleteConfirmText !== localForm.name || isDeleting"
            >
              {{ isDeleting ? 'Deleting...' : 'Delete event permanently' }}
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { ref, watch, computed, onMounted, defineProps, defineEmits } from 'vue';
import { useRouter } from 'vue-router';
import { terminologyOptions, getCheckpointOptionLabel, useTerminology } from '../../composables/useTerminology';
import { useAuthStore } from '../../stores/auth';
import { formatDate as formatEventDate } from '../../utils/dateFormatters';
import { eventsApi } from '../../services/api';
import { removeAllSessionsForEvent } from '../../services/marshalSessionService';
import AdminsList from '../../components/event-manage/lists/AdminsList.vue';
import CheckpointStylePicker from '../../components/CheckpointStylePicker.vue';
import MarshalModeMockup from '../../components/MarshalModeMockup.vue';
import { getDefaultBranding } from '../../constants/brandingPresets';

const { terms, termsLower, termsSentence } = useTerminology();
const authStore = useAuthStore();
const router = useRouter();
const adminEmail = computed(() => authStore.adminEmail || '');

const props = defineProps({
  eventData: {
    type: Object,
    required: true,
  },
  admins: {
    type: Array,
    default: () => [],
  },
  formDirty: {
    type: Boolean,
    default: false,
  },
  eventId: {
    type: String,
    default: '',
  },
});

const emit = defineEmits([
  'submit',
  'add-admin',
  'remove-admin',
  'update:formDirty',
]);

const localForm = ref({ ...props.eventData });
const brandingMockupRef = ref(null);
const isSaving = ref(false);

// Accordion state persistence
const ACCORDION_STORAGE_KEY = 'admin_settings_accordion';
const getStorageKey = () => props.eventId ? `${ACCORDION_STORAGE_KEY}_${props.eventId}` : ACCORDION_STORAGE_KEY;

const expandedSection = ref(null);

// Load saved accordion state on mount
onMounted(() => {
  const saved = localStorage.getItem(getStorageKey());
  if (saved) {
    expandedSection.value = saved;
  }
});

// Compute checkpoint options with dynamic labels based on current people term
const checkpointOptionsWithLabels = computed(() => {
  const peopleTerm = localForm.value.peopleTerm || 'Marshals';
  return terminologyOptions.checkpoint.map(option => ({
    value: option,
    label: getCheckpointOptionLabel(option, peopleTerm),
  }));
});

const toggleSection = (section) => {
  const newValue = expandedSection.value === section ? null : section;
  expandedSection.value = newValue;
  // Persist accordion state
  if (newValue) {
    localStorage.setItem(getStorageKey(), newValue);
  } else {
    localStorage.removeItem(getStorageKey());
  }
};

watch(() => props.eventData, (newVal) => {
  localForm.value = { ...newVal };
  // Reset staged logo changes when form data is refreshed
  hasLogoChanges.value = false;
}, { deep: true });

const emitFormChange = () => {
  emit('update:formDirty', true);
};

const handleStyleChange = (field, value) => {
  localForm.value[field] = value;
  emitFormChange();
};

// Branding data computed from form fields
const brandingData = computed(() => ({
  headerGradientStart: localForm.value.brandingHeaderGradientStart || '',
  headerGradientEnd: localForm.value.brandingHeaderGradientEnd || '',
  logoUrl: localForm.value.brandingLogoUrl || '',
  logoPosition: localForm.value.brandingLogoPosition || '',
  accentColor: localForm.value.brandingAccentColor || '',
  pageGradientStart: localForm.value.brandingPageGradientStart || '',
  pageGradientEnd: localForm.value.brandingPageGradientEnd || '',
}));

// Track if there are staged logo changes
const hasLogoChanges = ref(false);

// Delete event state
const showDeleteModal = ref(false);
const deleteConfirmText = ref('');
const isDeleting = ref(false);

const showDeleteConfirmation = () => {
  deleteConfirmText.value = '';
  showDeleteModal.value = true;
};

const cancelDelete = () => {
  showDeleteModal.value = false;
  deleteConfirmText.value = '';
};

const confirmDelete = async () => {
  if (deleteConfirmText.value !== localForm.value.name || isDeleting.value) {
    return;
  }

  isDeleting.value = true;

  try {
    // Request deletion from the backend
    await eventsApi.requestDeletion(props.eventId);

    // Clear all local sessions for this event (marshal sessions, sample event data)
    removeAllSessionsForEvent(props.eventId);

    // Log out the admin
    await authStore.logout();

    // Navigate to my events page
    router.push('/myevents');
  } catch (error) {
    console.error('Failed to request event deletion:', error);
    alert(error.response?.data?.message || 'Failed to delete event. Please try again.');
    isDeleting.value = false;
  }
};

const handleLogoStagedChange = (hasStagedChanges) => {
  hasLogoChanges.value = hasStagedChanges;
};

const handleBrandingChange = (branding) => {
  localForm.value.brandingHeaderGradientStart = branding.headerGradientStart;
  localForm.value.brandingHeaderGradientEnd = branding.headerGradientEnd;
  localForm.value.brandingLogoUrl = branding.logoUrl;
  localForm.value.brandingLogoPosition = branding.logoPosition;
  localForm.value.brandingAccentColor = branding.accentColor;
  localForm.value.brandingPageGradientStart = branding.pageGradientStart;
  localForm.value.brandingPageGradientEnd = branding.pageGradientEnd;
  emitFormChange();
};

const resetBranding = () => {
  const defaults = getDefaultBranding();
  localForm.value.brandingHeaderGradientStart = '';
  localForm.value.brandingHeaderGradientEnd = '';
  localForm.value.brandingLogoUrl = '';
  localForm.value.brandingLogoPosition = '';
  localForm.value.brandingAccentColor = '';
  localForm.value.brandingPageGradientStart = '';
  localForm.value.brandingPageGradientEnd = '';
  // Reset staged logo changes when branding is reset
  hasLogoChanges.value = false;
  // Reset the logo uploader's staged state
  brandingMockupRef.value?.resetLogo();
  emitFormChange();
};

const handleSubmit = async () => {
  if (isSaving.value) return;
  isSaving.value = true;

  try {
    // Handle staged logo upload/delete before submitting the form
    if (hasLogoChanges.value && brandingMockupRef.value) {
      // Check if we're deleting the logo (not uploading a new one)
      const isPendingDelete = brandingMockupRef.value.isLogoPendingDelete();

      if (isPendingDelete) {
        // Handle logo deletion
        const deleteResult = await brandingMockupRef.value.deleteStagedLogo();
        if (!deleteResult.success) {
          console.error('Failed to delete logo:', deleteResult.error);
          return;
        }
        // Clear the URL in the form
        localForm.value.brandingLogoUrl = '';
      } else {
        // Handle staged logo upload
        const uploadResult = await brandingMockupRef.value.uploadStagedLogo();
        if (!uploadResult.success) {
          console.error('Failed to upload logo:', uploadResult.error);
          return;
        }
        // Update the logo URL with the server URL if we uploaded a new logo
        if (uploadResult.logoUrl && !uploadResult.logoUrl.startsWith('blob:')) {
          localForm.value.brandingLogoUrl = uploadResult.logoUrl;
        }
      }

      // Clear the logo changes flag after successful operation
      hasLogoChanges.value = false;
    }

    emit('submit', { ...localForm.value });
  } finally {
    isSaving.value = false;
  }
};
</script>

<style scoped>
.settings-tab {
  width: 100%;
}

.accordion {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.accordion-section {
  background: var(--card-bg);
  border-radius: 8px;
  box-shadow: var(--shadow-md);
  overflow: hidden;
}

.accordion-header {
  width: 100%;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  background: var(--card-bg);
  border: none;
  cursor: pointer;
  text-align: left;
  font-size: 1.1rem;
  font-weight: 600;
  color: var(--text-primary);
  transition: background 0.2s;
}

.accordion-header:hover {
  background: var(--bg-tertiary);
}

.accordion-header.active {
  background: var(--bg-active);
  color: var(--accent-primary);
}

.accordion-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.accordion-icon {
  font-size: 1.5rem;
  font-weight: 300;
  color: var(--accent-primary);
}

.accordion-content {
  padding: 1.5rem;
  border-top: 1px solid var(--border-color);
}

.form-actions {
  margin-top: 1.5rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border-color);
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.section-description {
  color: var(--text-secondary);
  font-size: 0.9rem;
  margin: 0 0 1.5rem 0;
}

.terminology-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 1.5rem;
}

.term-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.term-group label {
  font-weight: 500;
  color: var(--text-primary);
  font-size: 0.9rem;
}

.form-input {
  width: 100%;
  padding: 0.5rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
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
  background: var(--accent-success);
  color: white;
}

.btn-primary:hover {
  background: var(--accent-success-hover, #16a34a);
}

.btn-primary:disabled {
  background: var(--text-muted);
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

.btn-danger {
  background: #dc2626;
  color: white;
  font-weight: 500;
}

.btn-danger:hover:not(:disabled) {
  background: #b91c1c;
}

.btn-danger:disabled {
  background: #9ca3af;
  cursor: not-allowed;
}

/* Danger Zone Styles */
.danger-section {
  border: 1px solid #fca5a5;
}

.danger-header {
  color: #dc2626;
}

.danger-header:hover {
  background: #fef2f2;
}

.danger-header.active {
  background: #fee2e2;
  color: #dc2626;
}

.danger-header .accordion-icon {
  color: #dc2626;
}

.danger-zone-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.danger-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1.5rem;
  padding: 1rem;
  background: #fef2f2;
  border: 1px solid #fecaca;
  border-radius: 6px;
}

.danger-info h4 {
  margin: 0 0 0.5rem 0;
  color: #991b1b;
  font-size: 1rem;
}

.danger-info p {
  margin: 0;
  color: #7f1d1d;
  font-size: 0.875rem;
  line-height: 1.5;
}

/* Delete Modal Styles */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--card-bg);
  border-radius: 8px;
  padding: 1.5rem;
  max-width: 500px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.delete-modal h2 {
  margin: 0 0 1rem 0;
  color: #dc2626;
  font-size: 1.25rem;
}

.delete-warning {
  margin: 0 0 1rem 0;
  color: var(--text-primary);
  font-size: 0.95rem;
}

.delete-details {
  margin: 0 0 0.5rem 0;
  color: var(--text-secondary);
  font-size: 0.9rem;
}

.delete-list {
  margin: 0 0 1rem 0;
  padding-left: 1.5rem;
  color: var(--text-secondary);
  font-size: 0.875rem;
}

.delete-list li {
  margin-bottom: 0.25rem;
}

.delete-confirm-text {
  margin: 1rem 0 0.5rem 0;
  color: var(--text-primary);
  font-size: 0.9rem;
}

.delete-confirm-input {
  margin-bottom: 1rem;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 1rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border-color);
}

@media (max-width: 600px) {
  .terminology-grid {
    grid-template-columns: 1fr;
  }

  .accordion-header {
    padding: 1rem;
  }

  .accordion-content {
    padding: 1rem;
  }

  .danger-item {
    flex-direction: column;
    gap: 1rem;
  }

  .danger-item .btn-danger {
    align-self: flex-start;
  }
}
</style>
