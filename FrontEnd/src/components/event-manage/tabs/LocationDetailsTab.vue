<template>
  <div class="tab-content">
    <form @submit.prevent="$emit('save')">
      <div class="form-group">
        <label>Name</label>
        <input
          :value="form.name"
          @input="handleInput('name', $event.target.value)"
          type="text"
          required
          class="form-input"
        />
      </div>

      <div class="form-group">
        <label>Description (optional)</label>
        <input
          :value="form.description"
          @input="handleInput('description', $event.target.value)"
          type="text"
          class="form-input"
        />
      </div>

      <div class="form-group">
        <label>Area (auto-assigned by location)</label>
        <div class="area-display">
          <span v-if="checkpointAreas.length === 0" class="no-area">
            Not in any area polygon
          </span>
          <span
            v-for="area in checkpointAreas"
            :key="area.id"
            class="area-badge"
            :style="{ backgroundColor: area.color || '#667eea' }"
          >
            {{ area.name }}
          </span>
        </div>
        <small class="form-help">
          Areas are automatically assigned based on checkpoint location within area boundaries
        </small>
      </div>

      <div class="form-group">
        <label class="checkbox-label">
          <input
            :checked="form.useCustomTimes"
            @change="handleCustomTimesToggle($event.target.checked)"
            type="checkbox"
          />
          Use custom date/time for this checkpoint
        </label>
        <small class="form-help">
          By default, marshals are expected during the event date/time. Enable this to set a specific time range.
        </small>
      </div>

      <div v-if="form.useCustomTimes" class="custom-times-section">
        <div class="form-group">
          <label>Start Date & Time (optional)</label>
          <input
            :value="form.startTime"
            @input="handleInput('startTime', $event.target.value)"
            type="datetime-local"
            class="form-input"
          />
          <small class="form-help">
            When marshal should arrive at this checkpoint
          </small>
        </div>

        <div class="form-group">
          <label>End Date & Time (optional)</label>
          <input
            :value="form.endTime"
            @input="handleInput('endTime', $event.target.value)"
            type="datetime-local"
            class="form-input"
          />
          <small class="form-help">
            When marshal can leave this checkpoint
          </small>
        </div>
      </div>
    </form>
  </div>
</template>

<script setup>
import { defineProps, defineEmits, computed } from 'vue';
import { formatDateForInput } from '../../../utils/dateFormatters';

const props = defineProps({
  form: {
    type: Object,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  eventDate: {
    type: String,
    default: '',
  },
});

const emit = defineEmits(['update:form', 'input', 'save']);

const checkpointAreas = computed(() => {
  // Get areaIds from form (could be areaIds or AreaIds due to backend casing)
  const areaIds = props.form.areaIds || props.form.AreaIds || [];

  // Map area IDs to area objects
  return areaIds
    .map(areaId => props.areas.find(a => a.id === areaId))
    .filter(area => area !== undefined);
});

const handleInput = (field, value) => {
  emit('update:form', { ...props.form, [field]: value });
  emit('input');
};

const handleCustomTimesToggle = (checked) => {
  const updatedForm = { ...props.form, useCustomTimes: checked };

  if (!checked) {
    // Clear times when disabling custom times
    updatedForm.startTime = '';
    updatedForm.endTime = '';
  } else {
    // When enabling custom times, default to event date if times are empty
    if (!updatedForm.startTime && !updatedForm.endTime && props.eventDate) {
      const defaultTime = formatDateForInput(props.eventDate);
      updatedForm.startTime = defaultTime;
      updatedForm.endTime = defaultTime;
    }
  }

  emit('update:form', updatedForm);
  emit('input');
};
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

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  font-weight: 500;
  color: #333;
}

.checkbox-label input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
}

.form-help {
  display: block;
  color: #666;
  font-size: 0.85rem;
  margin-top: 0.25rem;
  font-weight: normal;
}

.custom-times-section {
  margin-left: 1.5rem;
  padding-left: 1rem;
  border-left: 3px solid #007bff;
  background: #f8f9fa;
  padding: 1rem;
  border-radius: 4px;
  margin-top: 0.5rem;
}

.area-display {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding: 0.5rem 0;
}

.area-badge {
  display: inline-block;
  padding: 0.35rem 0.75rem;
  border-radius: 16px;
  color: white;
  font-size: 0.85rem;
  font-weight: 500;
}

.no-area {
  color: #999;
  font-style: italic;
  font-size: 0.9rem;
}
</style>
