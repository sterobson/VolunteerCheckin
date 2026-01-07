<template>
  <div class="contacts-tab">
    <div class="contacts-tab-header">
      <h2>Contacts</h2>
      <div class="button-group">
        <button @click="$emit('add-contact')" class="btn btn-primary">
          Add contact
        </button>
      </div>
    </div>

    <!-- Role Filter -->
    <div class="filters-section">
      <div class="filter-group">
        <h4>Filter by role:</h4>
        <label class="filter-checkbox">
          <input
            type="checkbox"
            :checked="showAllRoles"
            @change="toggleAllRoles"
          />
          All roles
        </label>
        <div v-if="!showAllRoles" class="checkbox-dropdown">
          <div class="checkbox-list">
            <label
              v-for="role in allRoles"
              :key="role"
              class="checkbox-item"
            >
              <input
                type="checkbox"
                :checked="filterRoles.includes(role)"
                @change="toggleRole(role)"
              />
              {{ formatRoleName(role) }}
            </label>
          </div>
        </div>
      </div>

      <button v-if="hasActiveFilters" @click="clearFilters" class="btn btn-secondary btn-small">
        Clear filters
      </button>
    </div>

    <!-- Contacts List -->
    <div class="contacts-list">
      <div v-if="filteredContacts.length === 0" class="empty-state">
        <p>{{ hasActiveFilters ? 'No contacts match the selected filters.' : 'No contacts yet. Create one to get started!' }}</p>
      </div>

      <div
        v-for="contact in sortedContacts"
        :key="contact.contactId"
        class="contact-card"
        :class="{ primary: contact.isPrimary }"
        @click="$emit('select-contact', contact)"
      >
        <div class="contact-header">
          <div class="contact-title-row">
            <span v-if="contact.isPrimary" class="primary-badge" title="Primary contact">★</span>
            <strong>{{ contact.name }}</strong>
            <span class="role-badge">{{ formatRoleName(contact.role) }}</span>
          </div>
          <div class="contact-details">
            <span v-if="contact.phone" class="contact-phone">{{ contact.phone }}</span>
            <span v-if="contact.email" class="contact-email">{{ contact.email }}</span>
          </div>
        </div>
        <div v-if="contact.notes" class="contact-notes">
          {{ truncateContent(contact.notes) }}
        </div>
        <div class="contact-scopes">
          <span
            v-for="(config, index) in contact.scopeConfigurations"
            :key="index"
            class="scope-badge"
          >
            {{ formatScopeConfig(config) }}
          </span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import { alphanumericCompare } from '../../utils/sortUtils';
import { useTerminology } from '../../composables/useTerminology';

const { termsLower } = useTerminology();

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
});

const emit = defineEmits(['add-contact', 'select-contact']);

const showAllRoles = ref(true);
const filterRoles = ref([]);

// All unique roles from contacts and available roles
const allRoles = computed(() => {
  const rolesSet = new Set([
    ...(props.availableRoles.builtInRoles || []),
    ...(props.availableRoles.customRoles || []),
    ...props.contacts.map(c => c.role),
  ]);
  return Array.from(rolesSet).sort();
});

const hasActiveFilters = computed(() => {
  return !showAllRoles.value;
});

const toggleAllRoles = () => {
  showAllRoles.value = !showAllRoles.value;
  if (!showAllRoles.value && filterRoles.value.length === 0) {
    filterRoles.value = [...allRoles.value];
  }
};

const toggleRole = (role) => {
  const index = filterRoles.value.indexOf(role);
  if (index >= 0) {
    filterRoles.value.splice(index, 1);
  } else {
    filterRoles.value.push(role);
  }
};

const clearFilters = () => {
  showAllRoles.value = true;
  filterRoles.value = [];
};

const filteredContacts = computed(() => {
  let contacts = props.contacts;

  if (!showAllRoles.value && filterRoles.value.length > 0) {
    contacts = contacts.filter(contact => filterRoles.value.includes(contact.role));
  }

  return contacts;
});

const sortedContacts = computed(() => {
  return [...filteredContacts.value].sort((a, b) => {
    // Primary contacts first
    if (a.isPrimary !== b.isPrimary) {
      return a.isPrimary ? -1 : 1;
    }

    // Then by role
    const roleCompare = a.role.localeCompare(b.role);
    if (roleCompare !== 0) return roleCompare;

    // Then by display order
    if (a.displayOrder !== b.displayOrder) {
      return a.displayOrder - b.displayOrder;
    }

    // Then by name
    return alphanumericCompare(a.name, b.name);
  });
});

const formatRoleName = (role) => {
  if (!role) return '';
  // Special handling for AreaLead - use terminology
  if (role === 'AreaLead') {
    return `${termsLower.value.area.charAt(0).toUpperCase() + termsLower.value.area.slice(1)} Lead`;
  }
  // Convert PascalCase/camelCase to words
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};

const formatScope = (scope) => {
  // Use terminology for dynamic terms
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
.contacts-tab {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.contacts-tab-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.contacts-tab-header h2 {
  margin: 0;
  font-size: 1.5rem;
  color: var(--text-primary);
}

.button-group {
  display: flex;
  gap: 0.75rem;
}

.filters-section {
  display: flex;
  flex-wrap: wrap;
  gap: 1.5rem;
  padding: 1rem;
  background: var(--bg-tertiary);
  border-radius: 8px;
  align-items: flex-start;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-width: 200px;
}

.filter-group h4 {
  margin: 0;
  font-size: 0.9rem;
  font-weight: 600;
  color: var(--text-primary);
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-primary);
}

.filter-checkbox input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.checkbox-dropdown {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-left: 1.5rem;
}

.checkbox-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-height: 200px;
  overflow-y: auto;
}

.checkbox-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: var(--text-primary);
  padding: 0.25rem 0;
}

.checkbox-item input[type="checkbox"] {
  cursor: pointer;
  width: 1rem;
  height: 1rem;
  flex-shrink: 0;
}

.empty-state {
  text-align: center;
  padding: 3rem 1rem;
  color: var(--text-muted);
  font-style: italic;
}

.contacts-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.contact-card {
  padding: 1rem 1.25rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.contact-card:hover {
  border-color: var(--accent-primary);
  box-shadow: var(--shadow-md);
}

.contact-card.primary {
  border-left: 4px solid var(--accent-warning);
  background: var(--status-warning-bg);
}

.contact-header {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.contact-title-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.primary-badge {
  color: var(--accent-warning);
  font-size: 1rem;
}

.contact-title-row strong {
  font-size: 1rem;
  color: var(--text-primary);
}

.role-badge {
  padding: 0.2rem 0.6rem;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.contact-details {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.9rem;
  color: var(--text-secondary);
}

.contact-phone::before {
  content: '📞 ';
}

.contact-email::before {
  content: '✉ ';
}

.contact-notes {
  font-size: 0.9rem;
  color: var(--text-secondary);
  line-height: 1.4;
  margin-bottom: 0.75rem;
}

.contact-scopes {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.scope-badge {
  padding: 0.2rem 0.6rem;
  background: var(--accent-primary);
  color: white;
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 500;
}

.btn {
  padding: 0.6rem 1.2rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  font-weight: 500;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--accent-primary);
  color: white;
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-secondary {
  background: var(--bg-tertiary);
  color: var(--text-primary);
}

.btn-secondary:hover {
  background: var(--bg-hover);
}

@media (max-width: 768px) {
  .filters-section {
    flex-direction: column;
  }

  .filter-group {
    width: 100%;
  }

  .contact-details {
    flex-direction: column;
    gap: 0.25rem;
  }
}
</style>
