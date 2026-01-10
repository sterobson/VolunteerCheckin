<template>
  <BaseModal
    :show="show"
    :title="`Add contact to ${areaName}`"
    size="medium"
    @close="handleClose"
  >
    <div class="search-section">
      <div class="form-group">
        <label>Search or create</label>
        <input
          ref="searchInputRef"
          v-model="searchQuery"
          type="text"
          class="form-input"
          placeholder="Filter by name..."
        />
      </div>

      <!-- Options list -->
      <div class="suggestions-list">
        <!-- Create new contact option -->
        <div
          class="suggestion-item create-new"
          @click="handleCreateNew"
        >
          <span class="suggestion-icon create-icon">+</span>
          <div class="suggestion-content">
            <span class="suggestion-name">Create new contact</span>
            <span v-if="searchQuery.trim()" class="suggestion-detail">"{{ searchQuery.trim() }}"</span>
          </div>
        </div>

        <!-- Full matches -->
        <div
          v-for="item in fullMatches"
          :key="item.key"
          class="suggestion-item"
          @click="item.type === 'marshal' ? handleSelectMarshal(item.data) : handleSelectContact(item.data)"
        >
          <span class="suggestion-icon" :class="item.type === 'marshal' ? 'marshal-icon' : 'contact-icon'">
            {{ item.type === 'marshal' ? 'M' : 'C' }}
          </span>
          <div class="suggestion-content">
            <span class="suggestion-name">{{ item.name }}</span>
            <span v-if="item.detail" class="suggestion-detail">{{ item.detail }}</span>
          </div>
          <span class="suggestion-badge" :class="{ 'contact-badge': item.type === 'contact' }">
            {{ item.type === 'marshal' ? terms.person : 'Contact' }}
          </span>
        </div>

        <!-- Partial matches section -->
        <template v-if="hasPartialMatches">
          <div class="suggestion-section-header">Partial matches</div>
          <div
            v-for="item in partialMatches"
            :key="item.key"
            class="suggestion-item"
            @click="item.type === 'marshal' ? handleSelectMarshal(item.data) : handleSelectContact(item.data)"
          >
            <span class="suggestion-icon" :class="item.type === 'marshal' ? 'marshal-icon' : 'contact-icon'">
              {{ item.type === 'marshal' ? 'M' : 'C' }}
            </span>
            <div class="suggestion-content">
              <span class="suggestion-name">{{ item.name }}</span>
              <span v-if="item.detail" class="suggestion-detail">{{ item.detail }}</span>
            </div>
            <span class="suggestion-badge" :class="{ 'contact-badge': item.type === 'contact' }">
              {{ item.type === 'marshal' ? terms.person : 'Contact' }}
            </span>
          </div>
        </template>

        <!-- Empty state when filtering returns no results -->
        <div
          v-if="searchQuery.trim() && fullMatches.length === 0 && partialMatches.length === 0"
          class="no-results"
        >
          No matching {{ terms.people.toLowerCase() }} or contacts found.
        </div>

        <!-- Empty state when no marshals or contacts exist at all -->
        <div
          v-if="!searchQuery.trim() && fullMatches.length === 0"
          class="no-results"
        >
          No {{ terms.people.toLowerCase() }} or contacts available to add.
        </div>
      </div>
    </div>

    <template #actions>
      <button @click="handleClose" class="btn btn-secondary">
        Cancel
      </button>
    </template>
  </BaseModal>
</template>

<script setup>
import { ref, computed, watch, nextTick } from 'vue';
import BaseModal from '../../BaseModal.vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms } = useTerminology();

const props = defineProps({
  show: {
    type: Boolean,
    default: false,
  },
  areaName: {
    type: String,
    default: '',
  },
  areaId: {
    type: String,
    default: '',
  },
  contacts: {
    type: Array,
    default: () => [],
  },
  marshals: {
    type: Array,
    default: () => [],
  },
  excludeContactIds: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['close', 'select-contact', 'create-from-marshal', 'create-new']);

const searchQuery = ref('');
const searchInputRef = ref(null);

// Get marshal IDs that are already linked to contacts in the area
const marshalIdsInArea = computed(() => {
  return props.contacts
    .filter(c => props.excludeContactIds.includes(c.contactId) && c.marshalId)
    .map(c => c.marshalId);
});

// Get marshal IDs that are linked to any contact (to avoid showing both marshal and their contact)
const marshalIdsLinkedToContacts = computed(() => {
  return props.contacts
    .filter(c => c.marshalId)
    .map(c => c.marshalId);
});

// All available contacts (excluding those already in area)
const availableContacts = computed(() => {
  return props.contacts
    .filter(contact => !props.excludeContactIds.includes(contact.contactId));
});

// All available marshals (excluding those already in area via contact link AND those linked to any contact)
const availableMarshals = computed(() => {
  return props.marshals
    .filter(marshal =>
      !marshalIdsInArea.value.includes(marshal.id) &&
      !marshalIdsLinkedToContacts.value.includes(marshal.id)
    );
});

// Normalize text for searching - remove punctuation and convert to lowercase
const normalizeText = (text) => {
  if (!text) return '';
  return text.toLowerCase().replace(/[.,\-_'"()]/g, ' ').replace(/\s+/g, ' ').trim();
};

// Split search query into individual terms
const searchTerms = computed(() => {
  const normalized = normalizeText(searchQuery.value);
  if (!normalized) return [];
  return normalized.split(' ').filter(term => term.length > 0);
});

// Calculate match score for an item
// Returns: { score: number, isPartial: boolean }
// score = number of matching terms, isPartial = true if not all terms match
const calculateMatchScore = (searchableText, terms) => {
  if (terms.length === 0) return { score: 1, isPartial: false };

  const normalized = normalizeText(searchableText);
  let matchCount = 0;

  for (const term of terms) {
    if (normalized.includes(term)) {
      matchCount++;
    }
  }

  return {
    score: matchCount,
    isPartial: matchCount > 0 && matchCount < terms.length,
    isFullMatch: matchCount === terms.length,
  };
};

// Combined and sorted list of marshals and contacts
const combinedList = computed(() => {
  const terms = searchTerms.value;
  const items = [];

  // Add contacts
  availableContacts.value.forEach(contact => {
    const searchableText = `${contact.name || ''} ${contact.role || ''}`;
    const match = calculateMatchScore(searchableText, terms);

    if (terms.length === 0 || match.score > 0) {
      items.push({
        key: `contact-${contact.contactId}`,
        type: 'contact',
        name: contact.name,
        detail: contact.role ? formatRoleName(contact.role) : null,
        data: contact,
        matchScore: match.score,
        isPartial: match.isPartial,
        isFullMatch: match.isFullMatch,
      });
    }
  });

  // Add marshals (only those not linked to contacts)
  availableMarshals.value.forEach(marshal => {
    const searchableText = `${marshal.name || ''} ${marshal.email || ''}`;
    const match = calculateMatchScore(searchableText, terms);

    if (terms.length === 0 || match.score > 0) {
      items.push({
        key: `marshal-${marshal.id}`,
        type: 'marshal',
        name: marshal.name,
        detail: marshal.email || marshal.phoneNumber || null,
        data: marshal,
        matchScore: match.score,
        isPartial: match.isPartial,
        isFullMatch: match.isFullMatch,
      });
    }
  });

  // Sort by match score (descending), then by name
  items.sort((a, b) => {
    if (b.matchScore !== a.matchScore) {
      return b.matchScore - a.matchScore;
    }
    return a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
  });

  return items;
});

// Split combined list into full matches and partial matches
const fullMatches = computed(() => {
  if (searchTerms.value.length === 0) return combinedList.value;
  return combinedList.value.filter(item => item.isFullMatch);
});

const partialMatches = computed(() => {
  if (searchTerms.value.length === 0) return [];
  return combinedList.value.filter(item => item.isPartial);
});

const hasPartialMatches = computed(() => partialMatches.value.length > 0);

watch(() => props.show, async (newVal) => {
  if (newVal) {
    // Reset state when modal opens
    searchQuery.value = '';
    // Focus search input
    await nextTick();
    searchInputRef.value?.focus();
  }
});

const handleSelectMarshal = (marshal) => {
  // Check if this marshal is already linked to a contact
  const existingContact = props.contacts.find(c => c.marshalId === marshal.id);
  if (existingContact) {
    // Check if that contact is already in the area
    if (props.excludeContactIds.includes(existingContact.contactId)) {
      // Contact already in area - do nothing (shouldn't normally happen as it should be filtered)
      return;
    }
    // Add the existing contact instead of creating new
    emit('select-contact', existingContact);
  } else {
    // No contact linked to this marshal - open create form
    emit('create-from-marshal', marshal);
  }
};

const handleSelectContact = (contact) => {
  emit('select-contact', contact);
};

const handleCreateNew = () => {
  emit('create-new', searchQuery.value.trim());
};

const handleClose = () => {
  emit('close');
};

const formatRoleName = (role) => {
  if (!role) return '';
  return role
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, str => str.toUpperCase())
    .trim();
};
</script>

<style scoped>
.search-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.form-group {
  margin-bottom: 0;
}

.form-group label {
  display: block;
  margin-bottom: 0.5rem;
  font-weight: 500;
  color: var(--text-primary);
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  border: 1px solid var(--border-color);
  border-radius: 6px;
  font-size: 1rem;
  box-sizing: border-box;
  background: var(--input-bg);
  color: var(--text-primary);
}

.form-input:focus {
  outline: none;
  border-color: var(--accent-primary);
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.2);
}

.suggestions-list {
  border: 1px solid var(--border-color);
  border-radius: 6px;
  overflow: hidden;
  max-height: 350px;
  overflow-y: auto;
}

.suggestion-section-header {
  padding: 0.5rem 0.75rem;
  background: var(--bg-tertiary);
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-color);
  position: sticky;
  top: 0;
  z-index: 1;
}

.suggestion-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem;
  cursor: pointer;
  transition: background-color 0.15s;
  border-bottom: 1px solid var(--border-color);
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: var(--bg-hover);
}

.suggestion-item.create-new {
  background: var(--bg-tertiary);
  border-bottom: 2px solid var(--border-color);
  position: sticky;
  top: 0;
  z-index: 2;
}

.suggestion-item.create-new:hover {
  background: var(--accent-primary);
  color: var(--btn-primary-text);
}

.suggestion-item.create-new:hover .suggestion-icon,
.suggestion-item.create-new:hover .suggestion-detail {
  color: var(--btn-primary-text);
  opacity: 0.9;
}

.suggestion-icon {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 0.9rem;
  flex-shrink: 0;
  background: var(--accent-primary);
  color: white;
}

.suggestion-icon.create-icon {
  font-size: 1.5rem;
  font-weight: 300;
  line-height: 1;
}

.suggestion-icon.marshal-icon {
  background: var(--accent-info);
}

.suggestion-icon.contact-icon {
  background: var(--accent-success);
}

.suggestion-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.suggestion-name {
  font-weight: 500;
  color: var(--text-primary);
}

.suggestion-detail {
  font-size: 0.85rem;
  color: var(--text-secondary);
}

.suggestion-badge {
  padding: 0.2rem 0.5rem;
  background: var(--bg-tertiary);
  color: var(--text-secondary);
  border-radius: 12px;
  font-size: 0.7rem;
  font-weight: 500;
  text-transform: uppercase;
  flex-shrink: 0;
}

.suggestion-badge.contact-badge {
  background: var(--status-success-bg);
  color: var(--accent-success);
}

.no-results {
  padding: 1.5rem;
  text-align: center;
  color: var(--text-secondary);
  font-size: 0.9rem;
  line-height: 1.5;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-secondary {
  background: var(--btn-secondary-bg);
  color: var(--btn-secondary-text);
}

.btn-secondary:hover {
  background: var(--btn-secondary-hover);
}
</style>
