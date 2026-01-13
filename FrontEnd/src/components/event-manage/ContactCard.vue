<template>
  <div
    class="contact-card"
    :class="{
      'is-pending': isPending,
      'is-marked-for-removal': isMarkedForRemoval,
      'is-primary': contact.isPrimary && !isMarkedForRemoval,
      'is-clickable': clickable && !isMarkedForRemoval
    }"
    @click="handleClick"
  >
    <div class="contact-card-header">
      <div class="contact-card-title" :class="{ clickable: clickable && !isMarkedForRemoval }">
        <div class="contact-name-row">
          <span v-if="contact.isPrimary && !isMarkedForRemoval" class="primary-badge" title="Primary contact">★</span>
          <span class="contact-name">{{ contact.name }}</span>
        </div>
        <span v-if="contact.role" class="contact-role">{{ formatRoleName(contact.role) }}</span>
      </div>
      <span
        v-if="contact.showInEmergencyInfo && !isMarkedForRemoval"
        class="emergency-indicator"
        title="Shown in emergency info"
      >🚨</span>
      <span
        v-if="hasNoScope && !isMarkedForRemoval && !isPending"
        class="no-scope-warning"
        title="This contact has no visibility scope configured"
      >!</span>
      <button
        v-if="showRemoveButton && !isMarkedForRemoval"
        type="button"
        class="contact-remove-btn"
        :title="removeTitle"
        @click.stop="$emit('remove', contact.contactId)"
      >
        &times;
      </button>
      <button
        v-if="isMarkedForRemoval"
        type="button"
        class="contact-undo-btn"
        title="Cancel removal"
        @click.stop="$emit('undo-remove', contact.contactId)"
      >
        Undo
      </button>
    </div>

    <div v-if="contact.phone || contact.email" class="contact-card-details">
      <span v-if="contact.phone" class="contact-phone">{{ contact.phone }}</span>
      <span v-if="contact.email" class="contact-email">{{ contact.email }}</span>
    </div>

    <div v-if="contact.notes && showNotes" class="contact-card-notes">
      {{ truncateContent(contact.notes) }}
    </div>

    <div v-if="showScopes && contact.scopeConfigurations?.length > 0 && !isMarkedForRemoval" class="contact-card-scopes">
      <span
        v-for="(config, index) in contact.scopeConfigurations"
        :key="index"
        class="scope-badge"
      >
        {{ formatScopeConfig(config) }}
      </span>
    </div>

    <div v-if="isPending" class="contact-card-badge">
      <span class="pending-indicator">Will be added on save</span>
    </div>

    <div v-if="isMarkedForRemoval" class="contact-card-badge">
      <span class="removal-indicator">Will be removed on save</span>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import { useTerminology } from '../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  contact: {
    type: Object,
    required: true,
  },
  isPending: {
    type: Boolean,
    default: false,
  },
  isMarkedForRemoval: {
    type: Boolean,
    default: false,
  },
  showRemoveButton: {
    type: Boolean,
    default: false,
  },
  removeTitle: {
    type: String,
    default: 'Remove',
  },
  showNotes: {
    type: Boolean,
    default: false,
  },
  showScopes: {
    type: Boolean,
    default: false,
  },
  clickable: {
    type: Boolean,
    default: true,
  },
});

const emit = defineEmits(['click', 'remove', 'undo-remove']);

// Check if contact has no scope configured
const hasNoScope = computed(() => {
  const scopes = props.contact.scopeConfigurations;
  return !scopes || scopes.length === 0;
});

const handleClick = () => {
  if (props.clickable && !props.isPending && !props.isMarkedForRemoval) {
    emit('click', props.contact);
  }
};

const formatRoleName = (role) => {
  if (!role) return '';
  if (role === 'AreaLead') {
    return `${terms.value.area} Lead`;
  }
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

const formatScope = (scope) => {
  const scopeMap = {
    'Everyone': 'Everyone',
    'EveryoneInAreas': `Everyone in ${termsLower.value.areas}`,
    'EveryoneAtCheckpoints': `Everyone at ${termsLower.value.checkpoints}`,
    'SpecificPeople': `Specific ${termsLower.value.people}`,
    'EveryAreaLead': `Every ${termsLower.value.area} lead`,
  };
  return scopeMap[scope] || scope;
};

const formatScopeConfig = (config) => {
  if (!config) return '';

  const scopeName = formatScope(config.scope);

  if (config.itemType === null) {
    return scopeName;
  }

  const ids = config.ids || [];

  if (ids.includes('ALL_MARSHALS')) {
    return `${scopeName} (Everyone)`;
  }
  if (ids.includes('ALL_AREAS')) {
    return `${scopeName} (All ${termsLower.value.areas})`;
  }
  if (ids.includes('ALL_CHECKPOINTS')) {
    return `${scopeName} (All ${termsLower.value.checkpoints})`;
  }

  const count = ids.length;
  if (count === 0) {
    return scopeName;
  }

  return `${scopeName} (${count})`;
};

const truncateContent = (content) => {
  if (!content) return '';
  const maxLength = 100;
  if (content.length <= maxLength) return content;
  return content.substring(0, maxLength) + '...';
};
</script>

<style scoped>
.contact-card {
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 0.875rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  transition: border-color 0.2s, box-shadow 0.2s;
}

.contact-card.is-clickable {
  cursor: pointer;
}

.contact-card.is-clickable:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-sm);
}

.contact-card.is-pending {
  border-style: dashed;
  border-color: var(--warning);
  background: var(--warning-bg-light);
}

.contact-card.is-marked-for-removal {
  border-style: dashed;
  border-color: var(--danger);
  background: var(--status-danger-bg);
  opacity: 0.8;
}

.contact-card.is-marked-for-removal .contact-name,
.contact-card.is-marked-for-removal .contact-role,
.contact-card.is-marked-for-removal .contact-card-details {
  text-decoration: line-through;
  color: var(--text-muted);
}

.contact-card.is-primary {
  border-left: 4px solid var(--accent-warning);
  background: var(--status-warning-bg);
}

.contact-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 0.5rem;
}

.contact-card-title {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.contact-card-title.clickable:hover .contact-name {
  color: var(--accent-primary);
}

.contact-name-row {
  display: flex;
  align-items: center;
  gap: 0.375rem;
}

.primary-badge {
  color: var(--accent-warning);
  font-size: 0.9rem;
}

.contact-name {
  font-weight: 600;
  color: var(--text-primary);
  transition: color 0.15s;
}

.contact-role {
  font-size: 0.8rem;
  color: var(--text-secondary);
}

.emergency-indicator {
  font-size: 0.9rem;
  flex-shrink: 0;
  cursor: help;
}

.no-scope-warning {
  width: 20px;
  height: 20px;
  background: var(--danger);
  color: white;
  border-radius: 50%;
  font-size: 0.85rem;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  cursor: help;
}

.contact-remove-btn {
  width: 24px;
  height: 24px;
  border: none;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  font-size: 1.1rem;
  line-height: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all 0.15s;
}

.contact-remove-btn:hover {
  background: var(--danger);
  color: white;
}

.contact-undo-btn {
  padding: 0.25rem 0.5rem;
  border: none;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.75rem;
  font-weight: 500;
  flex-shrink: 0;
  transition: all 0.15s;
}

.contact-undo-btn:hover {
  background: var(--accent-primary);
  color: white;
}

.contact-card-details {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.contact-phone::before {
  content: '\1F4DE ';
}

.contact-email::before {
  content: '\2709 ';
}

.contact-card-notes {
  font-size: 0.8rem;
  color: var(--text-secondary);
  line-height: 1.4;
}

.contact-card-scopes {
  display: flex;
  gap: 0.375rem;
  flex-wrap: wrap;
  margin-top: 0.25rem;
}

.scope-badge {
  padding: 0.15rem 0.5rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 10px;
  font-size: 0.65rem;
  font-weight: 500;
}

.contact-card-badge {
  margin-top: 0.25rem;
}

.pending-indicator {
  font-size: 0.75rem;
  color: var(--warning-orange);
  font-style: italic;
}

.removal-indicator {
  font-size: 0.75rem;
  color: var(--danger);
  font-style: italic;
}
</style>
