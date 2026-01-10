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
          v-if="withScopeCount > 0"
          variant="success"
          :active="activeFilter === 'with-scope'"
          @click="setFilter('with-scope')"
        >
          {{ withScopeCount }} assigned
        </StatusPill>
        <StatusPill
          v-if="noScopeCount > 0"
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

    <!-- Contacts Grid -->
    <ContactsGrid
      :contacts="sortedContacts"
      :show-notes="true"
      :show-scopes="true"
      :empty-message="emptyMessage"
      @select="$emit('select-contact', $event)"
    />
  </div>
</template>

<script setup>
import { ref, computed, defineProps, defineEmits } from 'vue';
import ContactsGrid from '../../components/event-manage/ContactsGrid.vue';
import StatusPill from '../../components/StatusPill.vue';

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

// Format role name for search matching
const formatRoleName = (role) => {
  if (!role) return '';
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
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
      const roleText = formatRoleName(contact.role);
      const searchableText = normalizeText(
        `${contact.name || ''} ${roleText} ${contact.email || ''} ${contact.phone || ''}`
      );

      // All search terms must match
      return searchTerms.every(term => searchableText.includes(term));
    });
  }

  return result;
});

// Sort alphabetically by name
const sortedContacts = computed(() => {
  return [...filteredContacts.value].sort((a, b) => {
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
</style>
