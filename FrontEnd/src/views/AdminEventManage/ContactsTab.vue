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
  color: #333;
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
  background: #f8f9fa;
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
  color: #333;
}

.filter-checkbox {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  cursor: pointer;
  color: #333;
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
  color: #333;
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
  color: #999;
  font-style: italic;
}

.contacts-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.contact-card {
  padding: 1rem 1.25rem;
  background: white;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.contact-card:hover {
  border-color: #667eea;
  box-shadow: 0 2px 8px rgba(102, 126, 234, 0.15);
}

.contact-card.primary {
  border-left: 4px solid #ffc107;
  background: #fffef5;
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
  color: #ffc107;
  font-size: 1rem;
}

.contact-title-row strong {
  font-size: 1rem;
  color: #333;
}

.role-badge {
  padding: 0.2rem 0.6rem;
  background: #e9ecef;
  color: #495057;
  border-radius: 12px;
  font-size: 0.75rem;
  font-weight: 500;
}

.contact-details {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.9rem;
  color: #666;
}

.contact-phone::before {
  content: '📞 ';
}

.contact-email::before {
  content: '✉ ';
}

.contact-notes {
  font-size: 0.9rem;
  color: #666;
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
  background: #667eea;
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
  background: #007bff;
  color: white;
}

.btn-primary:hover {
  background: #0056b3;
}

.btn-secondary {
  background: #6c757d;
  color: white;
}

.btn-secondary:hover {
  background: #545b62;
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
