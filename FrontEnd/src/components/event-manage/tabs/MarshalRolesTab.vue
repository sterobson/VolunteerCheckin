<template>
  <div class="tab-content roles-tab">
    <div v-if="roleDefinitions.length === 0" class="empty-state">
      <p>No roles have been defined for this event yet.</p>
      <p class="help-text">Create roles in the Roles tab to assign them to {{ termsLower.people }}.</p>
    </div>

    <div v-else class="roles-list">
      <label class="roles-label">Select roles for this {{ termsLower.person }}</label>

      <!-- Available roles as clickable items -->
      <div class="available-roles">
        <div
          v-for="role in availableRoles"
          :key="role.roleId"
          class="role-option"
          :class="{ selected: selectedRoles.includes(role.roleId) }"
          @click="toggleRole(role.roleId)"
        >
          <div class="role-checkbox">
            <input
              type="checkbox"
              :checked="selectedRoles.includes(role.roleId)"
              @click.stop
              @change="toggleRole(role.roleId)"
            />
          </div>
          <div class="role-info">
            <span class="role-name">{{ role.name }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, defineProps, defineEmits } from 'vue';
import { useTerminology } from '../../../composables/useTerminology';

const { terms, termsLower } = useTerminology();

const props = defineProps({
  roles: {
    type: Array,
    default: () => [],
  },
  roleDefinitions: {
    type: Array,
    default: () => [],
  },
});

const emit = defineEmits(['update:roles', 'input']);

const selectedRoles = computed(() => props.roles || []);

// Track initial selected roles for stable sorting (only updates after save/reload)
const initialSelectedRoles = ref(new Set());

// Capture initial selection state when component first receives roles
watch(() => props.roles, (newRoles) => {
  // Only set initial state when first loaded (set is empty)
  if (initialSelectedRoles.value.size === 0 && newRoles && newRoles.length > 0) {
    initialSelectedRoles.value = new Set(newRoles);
  }
}, { immediate: true });

// Sort roles: initially selected first, then by displayOrder, then alphabetically
const sortedRoleDefinitions = computed(() => {
  return [...props.roleDefinitions].sort((a, b) => {
    // Initially selected roles first (based on state when component loaded)
    const aInitiallySelected = initialSelectedRoles.value.has(a.roleId);
    const bInitiallySelected = initialSelectedRoles.value.has(b.roleId);
    if (aInitiallySelected !== bInitiallySelected) {
      return aInitiallySelected ? -1 : 1;
    }
    // Then by display order
    if (a.displayOrder !== b.displayOrder) {
      return (a.displayOrder || 0) - (b.displayOrder || 0);
    }
    // Then alphabetically
    return (a.name || '').localeCompare(b.name || '', undefined, { numeric: true, sensitivity: 'base' });
  });
});

// All roles available for selection
const availableRoles = computed(() => sortedRoleDefinitions.value);

// Toggle a role selection
const toggleRole = (roleId) => {
  const isSelected = selectedRoles.value.includes(roleId);
  let newRoles;
  if (isSelected) {
    newRoles = selectedRoles.value.filter(r => r !== roleId);
  } else {
    newRoles = [...selectedRoles.value, roleId];
  }
  emit('update:roles', newRoles);
  emit('input');
};
</script>

<style scoped>
.tab-content {
  padding-top: 0.5rem;
}

.roles-tab {
  min-height: 200px;
}

.empty-state {
  text-align: center;
  padding: 2rem 1rem;
  color: var(--text-secondary);
}

.empty-state p {
  margin: 0.5rem 0;
}

.roles-label {
  display: block;
  margin-bottom: 1rem;
  font-weight: 500;
  color: var(--text-primary);
}

.available-roles {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.role-option {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: var(--card-bg);
  border: 1px solid var(--border-color);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.role-option:hover {
  border-color: var(--accent-primary);
  background: var(--bg-secondary);
}

.role-option.selected {
  border-color: var(--accent-primary);
  background: var(--accent-primary-bg, rgba(59, 130, 246, 0.1));
}

.role-checkbox {
  flex-shrink: 0;
}

.role-checkbox input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: var(--accent-primary);
}

.role-info {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex: 1;
  min-width: 0;
}

.role-name {
  font-weight: 500;
  color: var(--text-primary);
}

.help-text {
  font-size: 0.85rem;
  color: var(--text-muted);
}
</style>
