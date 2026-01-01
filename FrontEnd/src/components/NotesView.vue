<template>
  <div class="notes-view">
    <div v-if="loading" class="loading-state">
      Loading notes...
    </div>

    <div v-else-if="error" class="error-state">
      <p>{{ error }}</p>
      <button @click="loadNotes" class="btn btn-secondary btn-small">
        Retry
      </button>
    </div>

    <div v-else-if="filteredNotes.length === 0" class="empty-state">
      <p>{{ emptyMessage }}</p>
    </div>

    <div v-else class="notes-list">
      <div
        v-for="note in sortedNotes"
        :key="note.noteId"
        class="note-item"
        :class="{ pinned: note.isPinned }"
      >
        <div class="note-header">
          <div class="note-title-row">
            <span v-if="note.isPinned" class="pin-icon" title="Pinned">📌</span>
            <span class="priority-indicator" :class="(note.priority || 'Normal').toLowerCase()"></span>
            <strong class="note-title">{{ note.title }}</strong>
          </div>
          <div class="note-meta">
            <span v-if="note.category" class="category-badge">{{ note.category }}</span>
            <span class="priority-badge" :class="(note.priority || 'Normal').toLowerCase()">
              {{ note.priority || 'Normal' }}
            </span>
          </div>
        </div>

        <div v-if="note.content" class="note-content">
          {{ note.content }}
        </div>

        <div class="note-footer">
          <span class="created-info">
            Created by {{ note.createdByName || 'Unknown' }} · {{ formatRelativeTime(note.createdAt) }}
          </span>
          <span v-if="note.matchedScope" class="scope-info">
            {{ note.matchedScope }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineExpose } from 'vue';
import { notesApi } from '../services/api';

const props = defineProps({
  eventId: {
    type: String,
    required: true,
  },
  locationId: {
    type: String,
    default: null,
  },
  areaId: {
    type: String,
    default: null,
  },
  marshalId: {
    type: String,
    default: null,
  },
  // All notes for the event (admin view) - if provided, we filter locally
  allNotes: {
    type: Array,
    default: null,
  },
  // For filtering notes by area/checkpoint when using allNotes
  locations: {
    type: Array,
    default: () => [],
  },
  areas: {
    type: Array,
    default: () => [],
  },
  // Assignments for determining which marshals are at which checkpoints
  assignments: {
    type: Array,
    default: () => [],
  },
});

const notes = ref([]);
const loading = ref(false);
const error = ref(null);

const emptyMessage = computed(() => {
  if (props.marshalId) {
    return 'No notes for this marshal.';
  } else if (props.areaId) {
    return 'No notes for this area.';
  } else if (props.locationId) {
    return 'No notes for this checkpoint.';
  }
  return 'No notes available.';
});

// Get checkpoints in the current area
const checkpointsInArea = computed(() => {
  if (!props.areaId) return [];
  return props.locations.filter(loc => loc.areaIds && loc.areaIds.includes(props.areaId));
});

// Get checkpoint IDs in the current area
const checkpointIdsInArea = computed(() => {
  return checkpointsInArea.value.map(cp => cp.id);
});

// Get marshal IDs assigned to checkpoints in the current area
const marshalIdsInArea = computed(() => {
  if (!props.areaId) return [];
  const marshalIds = new Set();
  props.assignments
    .filter(a => checkpointIdsInArea.value.includes(a.locationId))
    .forEach(a => marshalIds.add(a.marshalId));
  return Array.from(marshalIds);
});

// Get checkpoint IDs that a specific marshal is assigned to
const getCheckpointIdsForMarshal = (marshalId) => {
  return props.assignments
    .filter(a => a.marshalId === marshalId)
    .map(a => a.locationId);
};

// Get area IDs that a marshal is in (based on their checkpoint assignments)
const getAreaIdsForMarshal = (marshalId) => {
  const checkpointIds = getCheckpointIdsForMarshal(marshalId);
  const areaIds = new Set();
  for (const cpId of checkpointIds) {
    const checkpoint = props.locations.find(l => l.id === cpId);
    if (checkpoint?.areaIds) {
      checkpoint.areaIds.forEach(aId => areaIds.add(aId));
    }
  }
  return Array.from(areaIds);
};

// Check if a note matches a specific context and return the matched scope description
const getNoteMatchInfo = (note, context) => {
  if (!note.scopeConfigurations) return null;

  for (const config of note.scopeConfigurations) {
    // Area-based scopes
    if (config.itemType === 'Area') {
      if (context.type === 'area') {
        if (config.ids.includes(context.areaId) || config.ids.includes('ALL_AREAS')) {
          const scopeLabel = config.scope === 'EveryAreaLead' ? 'Area leads' : 'Area';
          return { matched: true, scope: scopeLabel };
        }
      }
      if (context.type === 'checkpoint') {
        const location = props.locations.find(l => l.id === context.locationId);
        const locationAreaIds = location?.areaIds || [];
        if (config.ids.includes('ALL_AREAS') || locationAreaIds.some(areaId => config.ids.includes(areaId))) {
          const scopeLabel = config.scope === 'EveryAreaLead' ? 'Area leads' : 'Area';
          return { matched: true, scope: scopeLabel };
        }
      }
    }

    // Checkpoint-based scopes
    if (config.itemType === 'Checkpoint') {
      if (context.type === 'checkpoint') {
        if (config.ids.includes(context.locationId) || config.ids.includes('ALL_CHECKPOINTS')) {
          return { matched: true, scope: 'Checkpoint' };
        }
      }
      if (context.type === 'area') {
        // Check if any checkpoint in this area is targeted
        const hasMatchingCheckpoint = context.checkpointIds.some(cpId =>
          config.ids.includes(cpId) || config.ids.includes('ALL_CHECKPOINTS')
        );
        if (hasMatchingCheckpoint) {
          return { matched: true, scope: 'Checkpoint in area' };
        }
      }
    }

    // Marshal-based scopes
    if (config.itemType === 'Marshal') {
      if (context.type === 'marshal') {
        if (config.ids.includes(context.marshalId) || config.ids.includes('ALL_MARSHALS')) {
          return { matched: true, scope: 'Marshal' };
        }
      }
      if (context.type === 'checkpoint') {
        // Check if any marshal at this checkpoint is targeted
        const marshalsAtCheckpoint = props.assignments
          .filter(a => a.locationId === context.locationId)
          .map(a => a.marshalId);
        const hasMatchingMarshal = config.ids.includes('ALL_MARSHALS') ||
          marshalsAtCheckpoint.some(mId => config.ids.includes(mId));
        if (hasMatchingMarshal) {
          return { matched: true, scope: 'Marshal at checkpoint' };
        }
      }
      if (context.type === 'area') {
        // Check if any marshal in this area is targeted
        const hasMatchingMarshal = config.ids.includes('ALL_MARSHALS') ||
          context.marshalIds.some(mId => config.ids.includes(mId));
        if (hasMatchingMarshal) {
          return { matched: true, scope: 'Marshal in area' };
        }
      }
    }

    // For marshal context, also check checkpoint and area scopes based on assignments
    if (context.type === 'marshal' && context.checkpointIds && context.areaIds) {
      // Check checkpoint scopes for marshals assigned checkpoints
      if (config.itemType === 'Checkpoint') {
        const hasMatchingCheckpoint = config.ids.includes('ALL_CHECKPOINTS') ||
          context.checkpointIds.some(cpId => config.ids.includes(cpId));
        if (hasMatchingCheckpoint) {
          return { matched: true, scope: 'Assigned checkpoint' };
        }
      }

      // Check area scopes for areas the marshal is in
      if (config.itemType === 'Area') {
        const hasMatchingArea = config.ids.includes('ALL_AREAS') ||
          context.areaIds.some(aId => config.ids.includes(aId));
        if (hasMatchingArea) {
          const scopeLabel = config.scope === 'EveryAreaLead' ? 'Area lead' : 'Area';
          return { matched: true, scope: scopeLabel };
        }
      }
    }
  }

  return null;
};

// Filter notes based on context
const filteredNotes = computed(() => {
  let notesToFilter = props.allNotes || notes.value;

  if (!notesToFilter || notesToFilter.length === 0) {
    return [];
  }

  // If we're filtering by area - show all notes relevant to anyone in this area
  if (props.areaId && props.allNotes) {
    const context = {
      type: 'area',
      areaId: props.areaId,
      checkpointIds: checkpointIdsInArea.value,
      marshalIds: marshalIdsInArea.value,
    };

    const matchedNotes = [];
    const seenNoteIds = new Set();

    for (const note of notesToFilter) {
      if (seenNoteIds.has(note.noteId)) continue;

      const matchInfo = getNoteMatchInfo(note, context);
      if (matchInfo) {
        seenNoteIds.add(note.noteId);
        matchedNotes.push({
          ...note,
          matchedScope: matchInfo.scope,
        });
      }
    }

    return matchedNotes;
  }

  // If we're filtering by checkpoint
  if (props.locationId && props.allNotes) {
    const context = {
      type: 'checkpoint',
      locationId: props.locationId,
    };

    const matchedNotes = [];
    const seenNoteIds = new Set();

    for (const note of notesToFilter) {
      if (seenNoteIds.has(note.noteId)) continue;

      const matchInfo = getNoteMatchInfo(note, context);
      if (matchInfo) {
        seenNoteIds.add(note.noteId);
        matchedNotes.push({
          ...note,
          matchedScope: matchInfo.scope,
        });
      }
    }

    return matchedNotes;
  }

  // If we're filtering by marshal
  if (props.marshalId && props.allNotes) {
    const marshalCheckpointIds = getCheckpointIdsForMarshal(props.marshalId);
    const marshalAreaIds = getAreaIdsForMarshal(props.marshalId);

    const context = {
      type: 'marshal',
      marshalId: props.marshalId,
      checkpointIds: marshalCheckpointIds,
      areaIds: marshalAreaIds,
    };

    const matchedNotes = [];
    const seenNoteIds = new Set();

    for (const note of notesToFilter) {
      if (seenNoteIds.has(note.noteId)) continue;

      const matchInfo = getNoteMatchInfo(note, context);
      if (matchInfo) {
        seenNoteIds.add(note.noteId);
        matchedNotes.push({
          ...note,
          matchedScope: matchInfo.scope,
        });
      }
    }

    return matchedNotes;
  }

  return notesToFilter;
});

const sortedNotes = computed(() => {
  const priorityOrder = { Urgent: 0, High: 1, Normal: 2, Low: 3 };

  return [...filteredNotes.value].sort((a, b) => {
    // Pinned first
    if (a.isPinned !== b.isPinned) {
      return a.isPinned ? -1 : 1;
    }

    // Then by priority
    const priorityA = priorityOrder[a.priority] ?? 2;
    const priorityB = priorityOrder[b.priority] ?? 2;
    if (priorityA !== priorityB) {
      return priorityA - priorityB;
    }

    // Then by display order
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }

    // Then by created date (newest first)
    return new Date(b.createdAt) - new Date(a.createdAt);
  });
});

const loadNotes = async () => {
  // If allNotes is provided, we use that and filter locally
  if (props.allNotes !== null) {
    return;
  }

  // Otherwise fetch notes for the specific context
  if (!props.eventId) return;

  loading.value = true;
  error.value = null;

  try {
    let response;

    if (props.marshalId) {
      response = await notesApi.getMarshalNotes(props.eventId, props.marshalId);
    } else {
      // For checkpoints and areas, we need to fetch all notes and filter
      // since there's no specific endpoint for them
      response = await notesApi.getByEvent(props.eventId);
    }

    notes.value = response.data || [];
  } catch (err) {
    console.error('Failed to load notes:', err);
    error.value = err.response?.data?.message || 'Failed to load notes';
  } finally {
    loading.value = false;
  }
};

// Watch for changes to load notes
watch(
  () => [props.eventId, props.locationId, props.areaId, props.marshalId],
  () => {
    if (props.allNotes === null) {
      loadNotes();
    }
  },
  { immediate: true }
);

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

defineExpose({
  loadNotes,
});
</script>

<style scoped>
.notes-view {
  padding: 0.5rem 0;
}

.loading-state,
.error-state,
.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: #666;
}

.error-state {
  color: #dc3545;
}

.empty-state {
  font-style: italic;
}

.notes-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.note-item {
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 1rem;
  transition: all 0.2s;
}

.note-item.pinned {
  border-left: 4px solid #667eea;
  background: #fafbff;
}

.note-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.note-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
}

.pin-icon {
  font-size: 0.85rem;
  flex-shrink: 0;
}

.priority-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.priority-indicator.urgent { background: #dc3545; }
.priority-indicator.high { background: #fd7e14; }
.priority-indicator.normal { background: #28a745; }
.priority-indicator.low { background: #6c757d; }

.note-title {
  font-size: 0.95rem;
  color: #333;
  word-wrap: break-word;
}

.note-meta {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.category-badge {
  padding: 0.15rem 0.5rem;
  background: #e9ecef;
  color: #495057;
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 500;
}

.priority-badge {
  padding: 0.15rem 0.5rem;
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 500;
  text-transform: uppercase;
}

.priority-badge.urgent {
  background: #f8d7da;
  color: #721c24;
}

.priority-badge.high {
  background: #fff3cd;
  color: #856404;
}

.priority-badge.normal {
  background: #d4edda;
  color: #155724;
}

.priority-badge.low {
  background: #e2e3e5;
  color: #383d41;
}

.note-content {
  font-size: 0.9rem;
  color: #555;
  line-height: 1.5;
  margin-bottom: 0.75rem;
  white-space: pre-wrap;
}

.note-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.75rem;
  color: #999;
  padding-top: 0.5rem;
  border-top: 1px solid #e9ecef;
}

.scope-info {
  font-style: italic;
}

.btn {
  padding: 0.4rem 0.8rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
}

.btn-small {
  padding: 0.3rem 0.6rem;
  font-size: 0.8rem;
}
</style>
