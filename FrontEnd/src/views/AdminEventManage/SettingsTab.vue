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
            :branding="brandingData"
            :event-name="localForm.name"
            :event-date="formatEventDate(localForm.eventDate)"
            :event-id="eventId"
            :admin-email="adminEmail"
            @update:branding="handleBrandingChange"
          />

          <div class="form-actions">
            <button @click="resetBranding" class="btn btn-secondary">
              Reset to default
            </button>
            <button @click="handleSubmit" class="btn btn-primary" :disabled="!formDirty">
              Save
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, computed, onMounted, defineProps, defineEmits } from 'vue';
import { terminologyOptions, getCheckpointOptionLabel, useTerminology } from '../../composables/useTerminology';
import { useAuthStore } from '../../stores/auth';
import { formatDate as formatEventDate } from '../../utils/dateFormatters';
import AdminsList from '../../components/event-manage/lists/AdminsList.vue';
import CheckpointStylePicker from '../../components/CheckpointStylePicker.vue';
import MarshalModeMockup from '../../components/MarshalModeMockup.vue';
import { getDefaultBranding } from '../../constants/brandingPresets';

const { terms, termsLower } = useTerminology();
const authStore = useAuthStore();
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
  emitFormChange();
};

const handleSubmit = () => {
  emit('submit', { ...localForm.value });
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
}
</style>
