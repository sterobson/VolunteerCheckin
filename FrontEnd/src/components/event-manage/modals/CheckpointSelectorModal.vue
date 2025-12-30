<template>
  <BaseModal
    :show="show"
    :title="`Assign checkpoints to ${areaName}`"
    size="medium"
    @close="handleClose"
  >
    <div class="checkpoint-selector-section">
      <div class="search-box">
        <input
          v-model="searchQuery"
          type="text"
          placeholder="Search checkpoints..."
          class="form-input"
        />
      </div>

      <div class="checkpoint-list">
        <div
          v-for="checkpoint in filteredCheckpoints"
          :key="checkpoint.id"
          class="checkpoint-item"
          @click="toggleCheckpoint(checkpoint.id)"
        >
          <label class="checkpoint-label">
            <input
              type="checkbox"
              :checked="isSelected(checkpoint.id)"
              @change="toggleCheckpoint(checkpoint.id)"
              @click.stop
            />
            <div class="checkpoint-info">
              <strong>{{ checkpoint.name }}</strong>
              <span v-if="checkpoint.description" class="checkpoint-description">
                {{ checkpoint.description }}
              </span>
              <div class="checkpoint-meta">
                <span v-if="checkpoint.areaId && checkpoint.areaId !== currentAreaId" class="area-badge">
                  {{ getAreaName(checkpoint.areaId) }}
                </span>
                <span class="marshals-badge">
                  {{ checkpoint.checkedInCount || 0 }}/{{ checkpoint.requiredMarshals }} marshals
                </span>
              </div>
            </div>
          </label>
          <div v-if="checkpoint.areaId && checkpoint.areaId !== currentAreaId" class="warning-badge">
            Will move from {{ getAreaName(checkpoint.areaId) }}
          </div>
        </div>

        <div v-if="filteredCheckpoints.length === 0" class="empty-state">
          <p v-if="searchQuery">No checkpoints match your search</p>
          <p v-else>No checkpoints available</p>
        </div>
      </div>
    </div>

    <!-- Action buttons -->
    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
      <button
        @click="handleAssign"
        class="btn btn-primary"
        :disabled="selectedCheckpointIds.length === 0"
      >
        Assign selected ({{ selectedCheckpointIds.length }})
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits, watch } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { alphanumericCompare } from '../../../utils/sortUtils';

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  areaName: {
    type: String,
    default: '',
  },
  currentAreaId: {
    type: String,
    default: null,
  },
  checkpoints: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'assign']);

const searchQuery = ref('');
const selectedCheckpointIds = ref([]);

watch(() => props.show, (newVal) => {
  if (!newVal) {
    // Reset state when modal closes
    searchQuery.value = '';
    selectedCheckpointIds.value = [];
  }
});

const filteredCheckpoints = computed(() => {
  const query = searchQuery.value.toLowerCase();
  const filtered = props.checkpoints.filter((checkpoint) => {
    const nameMatch = checkpoint.name.toLowerCase().includes(query);
    const descMatch = checkpoint.description?.toLowerCase().includes(query);
    return nameMatch || descMatch;
  });

  // Sort with natural/alphanumeric sorting (so "2" comes before "10")
  return filtered.sort((a, b) => alphanumericCompare(a.name, b.name));
});

const isSelected = (checkpointId) => {
  return selectedCheckpointIds.value.includes(checkpointId);
};

const toggleCheckpoint = (checkpointId) => {
  const index = selectedCheckpointIds.value.indexOf(checkpointId);
  if (index > -1) {
    selectedCheckpointIds.value.splice(index, 1);
  } else {
    selectedCheckpointIds.value.push(checkpointId);
  }
};

const getAreaName = (areaId) => {
  const area = props.areas.find((a) => a.id === areaId);
  return area ? area.name : 'Unknown';
};

const handleAssign = () => {
  if (selectedCheckpointIds.value.length > 0) {
    emit('assign', selectedCheckpointIds.value);
  }
};

const handleClose = () => {
  emit('close');
};
</script>

<style scoped>
.checkpoint-selector-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.search-box {
  margin-bottom: 0.5rem;
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 0.9rem;
  box-sizing: border-box;
}

.checkpoint-list {
  max-height: 400px;
  overflow-y: auto;
  border: 1px solid #e0e0e0;
  border-radius: 4px;
}

.checkpoint-item {
  padding: 1rem;
  border-bottom: 1px solid #e0e0e0;
  cursor: pointer;
  transition: background-color 0.2s;
}

.checkpoint-item:last-child {
  border-bottom: none;
}

.checkpoint-item:hover {
  background-color: #f8f9fa;
}

.checkpoint-label {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  cursor: pointer;
}

.checkpoint-label input[type="checkbox"] {
  margin-top: 0.25rem;
  cursor: pointer;
  width: 1.1rem;
  height: 1.1rem;
  flex-shrink: 0;
}

.checkpoint-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  flex: 1;
}

.checkpoint-info strong {
  color: #333;
}

.checkpoint-description {
  font-size: 0.85rem;
  color: #666;
}

.checkpoint-meta {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.area-badge {
  padding: 0.2rem 0.5rem;
  background: #e0e0e0;
  border-radius: 4px;
  font-size: 0.75rem;
  color: #666;
}

.marshals-badge {
  padding: 0.2rem 0.5rem;
  background: #e7f3ff;
  border-radius: 4px;
  font-size: 0.75rem;
  color: #0056b3;
}

.warning-badge {
  margin-top: 0.5rem;
  padding: 0.5rem;
  background: #fff3cd;
  border: 1px solid #ffc107;
  border-radius: 4px;
  font-size: 0.8rem;
  color: #856404;
}

.empty-state {
  text-align: center;
  padding: 2rem;
  color: #999;
}

.empty-state p {
  margin: 0;
  font-size: 0.9rem;
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

.btn-primary:hover:not(:disabled) {
  background: #0056b3;
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}
</style>
