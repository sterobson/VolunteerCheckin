<template>
  <div v-if="leadAreas.length > 0" class="area-lead-section">
    <div v-for="area in leadAreas" :key="area.id" class="area-card">
      <div class="area-header" :style="{ borderLeftColor: area.color || '#667eea' }">
        <h3>{{ area.name }}</h3>
        <span class="area-lead-badge">Area Lead</span>
      </div>

      <!-- Marshals in this area -->
      <div class="marshals-section">
        <h4>Marshals in Your Area ({{ areaMarshals(area.id).length }})</h4>

        <div v-if="loadingMarshals" class="loading">Loading marshals...</div>

        <div v-else-if="areaMarshals(area.id).length === 0" class="empty-state">
          No marshals assigned to checkpoints in this area.
        </div>

        <div v-else class="marshals-list">
          <div
            v-for="marshal in areaMarshals(area.id)"
            :key="marshal.marshalId"
            class="marshal-card"
            :class="{ 'is-checked-in': marshal.isCheckedIn }"
          >
            <div class="marshal-header">
              <div class="marshal-name">{{ marshal.name }}</div>
              <div class="check-status" :class="{ 'checked-in': marshal.isCheckedIn }">
                {{ marshal.isCheckedIn ? '✓ Checked In' : 'Not Checked In' }}
              </div>
            </div>

            <div class="marshal-checkpoint">
              {{ marshal.checkpointName }}
              <span v-if="marshal.checkpointDescription" class="checkpoint-desc">
                - {{ marshal.checkpointDescription }}
              </span>
            </div>

            <div class="marshal-contact">
              <div v-if="marshal.email" class="contact-item">
                <span class="contact-label">Email:</span>
                <a :href="`mailto:${marshal.email}`">{{ marshal.email }}</a>
              </div>
              <div v-if="marshal.phone" class="contact-item">
                <span class="contact-label">Phone:</span>
                <a :href="`tel:${marshal.phone}`">{{ marshal.phone }}</a>
              </div>
            </div>

            <div v-if="marshal.isCheckedIn && marshal.checkInTime" class="check-in-info">
              Checked in at {{ formatTime(marshal.checkInTime) }}
              <span v-if="marshal.checkInMethod"> ({{ marshal.checkInMethod }})</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Area Checklist -->
      <div class="checklist-section">
        <h4>Area Checklist</h4>

        <div v-if="loadingChecklists[area.id]" class="loading">Loading checklist...</div>

        <div v-else-if="!areaChecklists[area.id] || areaChecklists[area.id].length === 0" class="empty-state">
          No checklist items for this area.
        </div>

        <div v-else class="checklist-items">
          <div
            v-for="item in areaChecklists[area.id]"
            :key="`${area.id}-${item.itemId}-${item.completionContextId}`"
            class="checklist-item"
            :class="{ 'item-completed': item.isCompleted }"
          >
            <div class="item-checkbox">
              <input
                type="checkbox"
                :checked="item.isCompleted"
                :disabled="!item.canBeCompletedByMe || savingChecklist"
                @change="handleToggleChecklist(area.id, item)"
              />
            </div>
            <div class="item-content">
              <div class="item-text">{{ item.text }}</div>
              <div v-if="item.contextOwnerName" class="item-owner">
                For: {{ item.contextOwnerName }}
              </div>
              <div v-if="item.isCompleted" class="completion-info">
                <span class="completion-text">
                  Completed by {{ item.completedByActorName || 'Unknown' }}
                </span>
                <span class="completion-time">
                  {{ formatDateTime(item.completedAt) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, onMounted } from 'vue';
import { areasApi, checklistApi, assignmentsApi, marshalsApi, locationsApi } from '../services/api';

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
});

const emit = defineEmits(['checklist-updated']);

const areas = ref([]);
const marshals = ref([]);
const assignments = ref([]);
const locations = ref([]);
const areaChecklists = ref({});
const loadingMarshals = ref(false);
const loadingChecklists = ref({});
const savingChecklist = ref(false);

const leadAreas = computed(() => {
  return areas.value.filter(area => props.areaIds.includes(area.id));
});

const areaMarshals = (areaId) => {
  // Get checkpoints in this area
  const areaCheckpoints = locations.value.filter(loc => {
    return loc.areaIds && loc.areaIds.includes(areaId);
  });
  const checkpointIds = areaCheckpoints.map(c => c.id);

  // Get assignments for those checkpoints
  const areaAssignments = assignments.value.filter(a => checkpointIds.includes(a.locationId));

  // Build marshal info with checkpoint details
  return areaAssignments.map(assignment => {
    const marshal = marshals.value.find(m => m.id === assignment.marshalId);
    const checkpoint = areaCheckpoints.find(c => c.id === assignment.locationId);

    return {
      marshalId: assignment.marshalId,
      name: marshal?.name || assignment.marshalName || 'Unknown',
      email: marshal?.email || null,
      phone: marshal?.phoneNumber || null,
      isCheckedIn: assignment.isCheckedIn,
      checkInTime: assignment.checkInTime,
      checkInMethod: assignment.checkInMethod,
      checkpointName: checkpoint?.name || 'Unknown',
      checkpointDescription: checkpoint?.description || null,
    };
  });
};

const loadData = async () => {
  if (!props.eventId || props.areaIds.length === 0) return;

  loadingMarshals.value = true;

  try {
    // Load areas
    const areasResponse = await areasApi.getByEvent(props.eventId);
    areas.value = areasResponse.data || [];

    // Load locations
    const locationsResponse = await locationsApi.getByEvent(props.eventId);
    locations.value = locationsResponse.data || [];

    // Load assignments
    const assignmentsResponse = await assignmentsApi.getByEvent(props.eventId);
    assignments.value = assignmentsResponse.data || [];

    // Load marshals (with contact details - area leads can see these)
    const marshalsResponse = await marshalsApi.getByEvent(props.eventId);
    marshals.value = marshalsResponse.data || [];

    // Load checklists for each area
    await loadAreaChecklists();
  } catch (error) {
    console.error('Failed to load area data:', error);
  } finally {
    loadingMarshals.value = false;
  }
};

const loadAreaChecklists = async () => {
  for (const areaId of props.areaIds) {
    loadingChecklists.value[areaId] = true;

    try {
      const response = await checklistApi.getAreaChecklist(props.eventId, areaId);
      areaChecklists.value[areaId] = response.data || [];
    } catch (error) {
      console.error(`Failed to load checklist for area ${areaId}:`, error);
      areaChecklists.value[areaId] = [];
    } finally {
      loadingChecklists.value[areaId] = false;
    }
  }
};

const handleToggleChecklist = async (areaId, item) => {
  if (savingChecklist.value) return;

  savingChecklist.value = true;

  try {
    if (item.isCompleted) {
      await checklistApi.uncomplete(props.eventId, item.itemId, {
        marshalId: props.marshalId,
        contextType: item.completionContextType,
        contextId: item.completionContextId,
      });
    } else {
      await checklistApi.complete(props.eventId, item.itemId, {
        marshalId: props.marshalId,
        contextType: item.completionContextType,
        contextId: item.completionContextId,
      });
    }

    // Reload this area's checklist
    loadingChecklists.value[areaId] = true;
    const response = await checklistApi.getAreaChecklist(props.eventId, areaId);
    areaChecklists.value[areaId] = response.data || [];
    loadingChecklists.value[areaId] = false;

    emit('checklist-updated');
  } catch (error) {
    console.error('Failed to toggle checklist item:', error);
  } finally {
    savingChecklist.value = false;
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
  return new Date(dateString).toLocaleString();
};

watch(() => [props.eventId, props.areaIds], () => {
  loadData();
}, { immediate: true });

onMounted(() => {
  loadData();
});
</script>

<style scoped>
.area-lead-section {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.area-card {
  background: white;
  border-radius: 12px;
  padding: 0;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
  overflow: hidden;
}

.area-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  background: #f8f9fa;
  border-left: 4px solid #667eea;
}

.area-header h3 {
  margin: 0;
  color: #333;
}

.area-lead-badge {
  background: #667eea;
  color: white;
  padding: 0.25rem 0.75rem;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
}

.marshals-section,
.checklist-section {
  padding: 1.5rem 2rem;
  border-top: 1px solid #e0e0e0;
}

.marshals-section h4,
.checklist-section h4 {
  margin: 0 0 1rem 0;
  color: #333;
  font-size: 1rem;
}

.loading,
.empty-state {
  padding: 1rem;
  text-align: center;
  color: #666;
  font-style: italic;
}

.marshals-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.marshal-card {
  padding: 1rem;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  transition: all 0.2s;
}

.marshal-card.is-checked-in {
  background: #f1f8f4;
  border-color: #c8e6c9;
}

.marshal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.5rem;
}

.marshal-name {
  font-weight: 600;
  color: #333;
}

.check-status {
  font-size: 0.8rem;
  padding: 0.2rem 0.5rem;
  border-radius: 8px;
  background: #ffebee;
  color: #c62828;
}

.check-status.checked-in {
  background: #e8f5e9;
  color: #2e7d32;
}

.marshal-checkpoint {
  font-size: 0.9rem;
  color: #666;
  margin-bottom: 0.5rem;
}

.checkpoint-desc {
  color: #999;
}

.marshal-contact {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.85rem;
}

.contact-item {
  display: flex;
  gap: 0.25rem;
}

.contact-label {
  color: #666;
}

.contact-item a {
  color: #667eea;
  text-decoration: none;
}

.contact-item a:hover {
  text-decoration: underline;
}

.check-in-info {
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: #4caf50;
}

/* Checklist styles */
.checklist-items {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.checklist-item {
  display: flex;
  gap: 0.75rem;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
}

.checklist-item.item-completed {
  background: #f1f8f4;
  border-color: #c8e6c9;
}

.item-checkbox {
  display: flex;
  align-items: flex-start;
  padding-top: 0.2rem;
}

.item-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
}

.item-checkbox input[type="checkbox"]:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.item-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.item-text {
  font-size: 0.9rem;
  color: #333;
}

.item-owner {
  font-size: 0.8rem;
  color: #667eea;
  font-weight: 500;
}

.completion-info {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: 0.75rem;
}

.completion-text {
  color: #4caf50;
}

.completion-time {
  color: #999;
}

@media (max-width: 768px) {
  .area-header {
    padding: 1rem 1.5rem;
    flex-direction: column;
    gap: 0.5rem;
    align-items: flex-start;
  }

  .marshals-section,
  .checklist-section {
    padding: 1rem 1.5rem;
  }

  .marshal-contact {
    flex-direction: column;
    gap: 0.25rem;
  }
}
</style>
