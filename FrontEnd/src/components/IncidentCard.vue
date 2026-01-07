<template>
  <div
    class="incident-card"
    :class="[`severity-${incident.severity}`, `status-${incident.status}`]"
    @click="$emit('select', incident)"
  >
    <div class="incident-header">
      <div class="incident-title-row">
        <span class="severity-indicator" :class="incident.severity"></span>
        <strong class="incident-title">{{ displayTitle }}</strong>
      </div>
      <div class="incident-badges">
        <span class="severity-badge" :class="incident.severity">
          {{ formatSeverity(incident.severity) }}
        </span>
        <span class="status-badge" :class="incident.status">
          {{ formatStatus(incident.status) }}
        </span>
      </div>
    </div>

    <div v-if="incident.title && incident.description" class="incident-description">
      {{ truncateDescription(incident.description) }}
    </div>

    <div class="incident-context">
      <div v-if="incident.context?.checkpoint" class="context-item">
        <span class="context-icon">📍</span>
        <span>{{ incident.context.checkpoint.name }}</span>
      </div>
      <div v-else-if="incident.latitude && incident.longitude" class="context-item">
        <span class="context-icon">🌐</span>
        <span>GPS Location</span>
      </div>
      <div v-if="incident.area" class="context-item">
        <span class="context-icon">🗺️</span>
        <span>{{ incident.area.areaName }}</span>
      </div>
    </div>

    <div class="incident-footer">
      <span class="reporter-info">
        Reported by {{ incident.reportedBy?.name || 'Unknown' }}
      </span>
      <span class="time-info">
        {{ formatRelativeTime(incident.incidentTime || incident.createdAt) }}
      </span>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';

const props = defineProps({
  incident: {
    type: Object,
    required: true,
  },
});

defineEmits(['select']);

// Show title if available, otherwise truncate description for display
const displayTitle = computed(() => {
  if (props.incident.title) {
    return props.incident.title;
  }
  // Use truncated description as title
  const desc = props.incident.description || '';
  if (desc.length <= 60) return desc;
  return desc.substring(0, 57).trim() + '...';
});

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

const truncateDescription = (text, maxLength = 120) => {
  if (!text || text.length <= maxLength) return text;
  return text.substring(0, maxLength).trim() + '...';
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
</script>

<style scoped>
.incident-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 1rem;
  cursor: pointer;
  transition: all 0.2s;
}

.incident-card:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-sm);
}

/* Severity left border */
.incident-card.severity-critical {
  border-left: 4px solid #dc3545;
}

.incident-card.severity-high {
  border-left: 4px solid #fd7e14;
}

.incident-card.severity-medium {
  border-left: 4px solid #ffc107;
}

.incident-card.severity-low {
  border-left: 4px solid #667eea;
}

.incident-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.incident-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
}

.severity-indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
}

.severity-indicator.critical { background: #dc3545; }
.severity-indicator.high { background: #fd7e14; }
.severity-indicator.medium { background: #ffc107; }
.severity-indicator.low { background: #667eea; }

.incident-title {
  font-size: 0.95rem;
  color: var(--text-primary);
  word-wrap: break-word;
}

.incident-badges {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.severity-badge,
.status-badge {
  padding: 0.15rem 0.5rem;
  border-radius: 10px;
  font-size: 0.7rem;
  font-weight: 500;
  text-transform: uppercase;
}

/* Severity badges */
.severity-badge.critical {
  background: #f8d7da;
  color: #721c24;
}

.severity-badge.high {
  background: #fff3cd;
  color: #856404;
}

.severity-badge.medium {
  background: #fff9e6;
  color: #856404;
}

.severity-badge.low {
  background: #e8edff;
  color: #3d5afe;
}

/* Status badges */
.status-badge.open {
  background: #e3f2fd;
  color: #1565c0;
}

.status-badge.acknowledged {
  background: #fff3e0;
  color: #e65100;
}

.status-badge.in_progress {
  background: #f3e5f5;
  color: #7b1fa2;
}

.status-badge.resolved {
  background: #e8f5e9;
  color: #2e7d32;
}

.status-badge.closed {
  background: #f5f5f5;
  color: #616161;
}

.incident-description {
  font-size: 0.9rem;
  color: var(--text-secondary);
  line-height: 1.5;
  margin-bottom: 0.75rem;
}

.incident-context {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.context-item {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.context-icon {
  font-size: 0.85rem;
}

.incident-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.75rem;
  color: var(--text-muted);
  padding-top: 0.5rem;
  border-top: 1px solid var(--border-color);
}

@media (max-width: 480px) {
  .incident-header {
    flex-direction: column;
    gap: 0.5rem;
  }

  .incident-badges {
    align-self: flex-start;
  }
}
</style>
