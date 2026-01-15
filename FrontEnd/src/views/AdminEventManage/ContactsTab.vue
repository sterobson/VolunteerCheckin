<template>
  <div class="contacts-tab">
    <div class="tab-header">
      <div class="button-group">
        <button @click="$emit('add-contact')" class="btn btn-primary">
          Add contact
        </button>
      </div>
      <div class="status-pills" v-if="contacts.length > 0">
        <StatusPill
          variant="neutral"
          :active="activeFilter === 'all'"
          @click="setFilter('all')"
        >
          {{ contacts.length }} total
        </StatusPill>
        <StatusPill
          v-if="withScopeCount > 0 && withScopeCount < contacts.length"
          variant="success"
          :active="activeFilter === 'with-scope'"
          @click="setFilter('with-scope')"
        >
          {{ withScopeCount }} assigned
        </StatusPill>
        <StatusPill
          v-if="noScopeCount > 0 && noScopeCount < contacts.length"
          variant="danger"
          :active="activeFilter === 'no-scope'"
          @click="setFilter('no-scope')"
        >
          {{ noScopeCount }} unassigned
        </StatusPill>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters-section">
      <div class="search-group">
        <input
          v-model="searchQuery"
          type="text"
          class="search-input"
          placeholder="Search by name, role, email, or phone..."
        />
      </div>
    </div>

    <!-- Contacts List -->
    <div class="contacts-list">
      <div v-if="filteredContacts.length === 0" class="empty-state">
        <p>{{ emptyMessage }}</p>
      </div>

      <DraggableList
        v-else
        :items="sortedContacts"
        item-key="contactId"
        pinned-key="isPinned"
        layout="grid"
        :disabled="!!searchQuery || activeFilter !== 'all'"
        @reorder="handleReorder"
      >
        <template #item="{ element: contact }">
          <div
            class="contact-card"
            :class="{ pinned: contact.isPinned, primary: contact.isPrimary }"
          >
            <DragHandle v-if="!searchQuery && activeFilter === 'all'" />
            <div class="contact-content" @click="$emit('select-contact', contact)">
              <div class="contact-header">
                <div class="contact-name-row">
                  <span v-if="contact.isPinned" class="pin-icon" title="Pinned">📌</span>
                  <span v-if="contact.isPrimary" class="primary-badge" title="Primary contact">★</span>
                  <strong>{{ contact.name }}</strong>
                  <span v-if="contact.showInEmergencyInfo" class="emergency-indicator" title="Shown in emergency info">🚨</span>
                </div>
                <div class="contact-roles">
                  <span
                    v-for="role in getContactRoles(contact)"
                    :key="role"
                    class="role-badge"
                    :class="{ 'emergency-role': role === 'Emergency' }"
                  >
                    {{ formatRoleName(role) }}
                  </span>
                </div>
              </div>
              <div class="contact-details">
                <span v-if="contact.phone" class="contact-detail">{{ contact.phone }}</span>
                <span v-if="contact.email" class="contact-detail">{{ contact.email }}</span>
              </div>
              <div v-if="contact.notes" class="contact-notes">
                {{ truncateNotes(contact.notes) }}
              </div>
              <ScopedAssignmentPills
                v-if="contact.scopeConfigurations && contact.scopeConfigurations.length > 0"
                :scope-configurations="contact.scopeConfigurations"
                :areas="areas"
                :locations="locations"
                :marshals="marshals"
              />
              <div v-else class="no-scope-warning">
                No visibility scope configured
              </div>
            </div>
          </div>
        </template>
      </DraggableList>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue';
import StatusPill from '../../components/StatusPill.vue';
import ScopedAssignmentPills from '../../components/common/ScopedAssignmentPills.vue';
import DraggableList from '../../components/common/DraggableList.vue';
import DragHandle from '../../components/common/DragHandle.vue';

const props = defineProps({
  contacts: {
    type: Array,
    required: true,
  },
  areas: {
    type: Array,
    default: () => [],
  },
  locations: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  availableRoles: {
    type: Object,
    default: () => ({ builtInRoles: [], customRoles: [] }),
  },
  roleDefinitions: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['add-contact', 'select-contact', 'reorder']);

const searchQuery = ref('');
const activeFilter = ref('all');

// Count contacts without scope
const noScopeCount = computed(() => {
  return props.contacts.filter(c => !c.scopeConfigurations || c.scopeConfigurations.length === 0).length;
});

// Count contacts with scope
const withScopeCount = computed(() => {
  return props.contacts.filter(c => c.scopeConfigurations && c.scopeConfigurations.length > 0).length;
});

const setFilter = (filter) => {
  activeFilter.value = filter;
};

// Normalize text for searching - remove punctuation
const normalizeText = (text) => {
  if (!text) return '';
  return text.toLowerCase().replace(/[.,\-_'"()]/g, ' ').replace(/\s+/g, ' ').trim();
};

// Build a lookup map for role definitions
const roleDefinitionMap = computed(() => {
  const map = new Map();
  for (const rd of props.roleDefinitions) {
    map.set(rd.roleId, rd.name);
    // Also map by name for legacy support
    map.set(rd.name, rd.name);
  }
  return map;
});

// Format role name for display - looks up GUID in role definitions
const formatRoleName = (role) => {
  if (!role) return '';

  // Try to look up in role definitions first (for GUIDs)
  const definitionName = roleDefinitionMap.value.get(role);
  if (definitionName) {
    return definitionName;
  }

  // Fallback: format legacy role names (e.g., "AreaLead" -> "Area Lead")
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

// Get all roles for a contact (handles both array and legacy single role)
const getContactRoles = (contact) => {
  if (contact.roles && Array.isArray(contact.roles) && contact.roles.length > 0) {
    return contact.roles;
  }
  return contact.role ? [contact.role] : [];
};

// Get all role names for a contact as text for searching
const getContactRolesText = (contact) => {
  return getContactRoles(contact).map(formatRoleName).join(' ');
};

// Truncate notes for preview
const truncateNotes = (notes) => {
  if (!notes) return '';
  const maxLength = 100;
  if (notes.length <= maxLength) return notes;
  return notes.substring(0, maxLength) + '...';
};

// Search filtering - searches name + role with multi-term support
const filteredContacts = computed(() => {
  let result = props.contacts;

  // Apply status filter
  if (activeFilter.value === 'no-scope') {
    result = result.filter(c => !c.scopeConfigurations || c.scopeConfigurations.length === 0);
  } else if (activeFilter.value === 'with-scope') {
    result = result.filter(c => c.scopeConfigurations && c.scopeConfigurations.length > 0);
  }

  // Apply search filter
  if (searchQuery.value.trim()) {
    const normalizedQuery = normalizeText(searchQuery.value);
    const searchTerms = normalizedQuery.split(' ').filter(t => t.length > 0);

    result = result.filter(contact => {
      const rolesText = getContactRolesText(contact);
      const searchableText = normalizeText(
        `${contact.name || ''} ${rolesText} ${contact.email || ''} ${contact.phone || ''}`
      );

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
});

// Sort: pinned first, then by display order, then alphabetically by name
const sortedContacts = computed(() => {
  return [...filteredContacts.value].sort((a, b) => {
    // Pinned first
    if (a.isPinned !== b.isPinned) {
      return a.isPinned ? -1 : 1;
    }

    // Then by display order
    const orderA = a.displayOrder || 0;
    const orderB = b.displayOrder || 0;
    if (orderA !== orderB) {
      return orderA - orderB;
    }

    // Then alphabetically by name
    return (a.name || '').localeCompare(b.name || '', undefined, { sensitivity: 'base' });
  });
});

// Empty message based on filters
const emptyMessage = computed(() => {
  const hasSearch = searchQuery.value.trim();

  if (activeFilter.value === 'no-scope') {
    if (hasSearch) {
      return 'No unassigned contacts match your search.';
    }
    return 'All contacts are assigned.';
  }
  if (activeFilter.value === 'with-scope') {
    if (hasSearch) {
      return 'No assigned contacts match your search.';
    }
    return 'No contacts are assigned yet.';
  }
  if (hasSearch) {
    return 'No contacts match your search.';
  }
  return 'No contacts yet. Create one to get started!';
});

const handleReorder = ({ changes }) => {
  emit('reorder', changes);
};
</script>

<style scoped>
.contacts-tab {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.status-pills {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.button-group {
  display: flex;
  gap: 0.75rem;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.filters-section {
  padding: 0.75rem 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
}

.search-group {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.search-input {
  flex: 1;
  max-width: 400px;
  padding: 0.5rem 0.75rem;
  border: 1px solid var(--input-border);
  border-radius: 4px;
  font-size: 0.9rem;
  background: var(--input-bg);
  color: var(--text-primary);
}

.search-input:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.search-input::placeholder {
  color: var(--text-muted);
}

.contacts-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.contact-card {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 1rem 1.25rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  transition: all 0.2s;
  height: 100%;
  box-sizing: border-box;
}

.contact-card:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.contact-card.pinned {
  border-left: 4px solid var(--accent-primary);
  background: var(--bg-secondary);
}

.contact-card.primary {
  border-left: 4px solid var(--warning-color);
  background: var(--warning-bg);
}

.contact-card.pinned.primary {
  border-left: 4px solid var(--accent-primary);
}

.contact-content {
  flex: 1;
  cursor: pointer;
  min-width: 0;
}

.contact-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.contact-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.pin-icon {
  font-size: 0.9rem;
}

.primary-badge {
  color: var(--warning-color);
  font-size: 1rem;
}

.emergency-indicator {
  font-size: 0.9rem;
  margin-left: auto;
  cursor: help;
}

.contact-name-row strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.contact-roles {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.role-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.role-badge.emergency-role {
  background: var(--danger-bg);
  color: var(--danger-color);
}

.contact-details {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  margin-bottom: 0.5rem;
}

.contact-detail {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.contact-notes {
  font-size: 0.85rem;
  color: var(--text-muted);
  margin-bottom: 0.5rem;
  font-style: italic;
}

.no-scope-warning {
  font-size: 0.8rem;
  color: var(--danger-color);
  padding: 0.25rem 0.5rem;
  background: var(--danger-bg);
  border-radius: 4px;
  display: inline-block;
}

@media (max-width: 768px) {
  .contact-header {
    flex-direction: column;
    gap: 0.5rem;
  }

  .contact-roles {
    order: -1;
  }
}
</style>
