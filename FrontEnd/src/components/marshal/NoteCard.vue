<template>
  <div class="checkpoint-note-item" :class="priorityClass">
    <div class="checkpoint-note-header">
      <span v-if="isPinned" class="pin-icon" title="Pinned">&#128204;</span>
      <span class="priority-indicator" :class="priorityClass"></span>
      <strong class="checkpoint-note-title">{{ title }}</strong>
      <span class="priority-badge" :class="priorityClass">
        {{ priority }}
      </span>
    </div>
    <div v-if="content" class="checkpoint-note-content">
      {{ content }}
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps } from 'vue';

const props = defineProps({
  title: {
    type: String,
    required: true,
  },
  content: {
    type: String,
    default: '',
  },
  priority: {
    type: String,
    default: 'Normal',
  },
  isPinned: {
    type: Boolean,
    default: false,
  },
});

const priorityClass = computed(() => (props.priority || 'Normal').toLowerCase());
</script>

<style scoped>
.checkpoint-note-item {
  background: var(--card-bg);
  border-radius: 6px;
  padding: 0.75rem;
  border-left: 3px solid var(--text-muted);
}

.checkpoint-note-item.emergency {
  border-left-color: var(--danger);
  background: rgba(239, 68, 68, 0.1);
}

.checkpoint-note-item.urgent {
  border-left-color: var(--warning);
  background: rgba(245, 158, 11, 0.1);
}

.checkpoint-note-item.high {
  border-left-color: #f97316;
}

.checkpoint-note-item.normal {
  border-left-color: var(--accent-primary);
}

.checkpoint-note-item.low {
  border-left-color: var(--text-muted);
}

.checkpoint-note-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.pin-icon {
  font-size: 0.85rem;
}

.priority-indicator {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.priority-indicator.emergency { background: var(--danger); }
.priority-indicator.urgent { background: var(--warning); }
.priority-indicator.high { background: #f97316; }
.priority-indicator.normal { background: var(--accent-primary); }
.priority-indicator.low { background: var(--text-muted); }

.checkpoint-note-title {
  font-size: 0.9rem;
  color: var(--text-dark);
  flex: 1;
}

.priority-badge {
  font-size: 0.7rem;
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
  font-weight: 500;
  text-transform: uppercase;
}

.priority-badge.emergency { background: var(--danger); color: white; }
.priority-badge.urgent { background: var(--warning); color: white; }
.priority-badge.high { background: #f97316; color: white; }
.priority-badge.normal { background: var(--bg-secondary); color: var(--text-secondary); }
.priority-badge.low { background: var(--bg-secondary); color: var(--text-muted); }

.checkpoint-note-content {
  margin-top: 0.5rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
  line-height: 1.4;
  white-space: pre-wrap;
}
</style>
