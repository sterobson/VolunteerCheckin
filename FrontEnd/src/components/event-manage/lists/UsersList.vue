<template>
  <div class="users-list-container">
    <div class="users-header">
      <button
        v-if="canManageUsers"
        @click="$emit('add-user')"
        class="btn btn-small btn-primary"
      >
        Add user
      </button>
    </div>

    <div class="users-list">
      <div
        v-for="user in sortedUsers"
        :key="user.personId"
        class="user-item"
      >
        <div class="user-info">
          <div class="user-header">
            <strong class="user-name">{{ user.userName || user.userEmail }}</strong>
            <span
              class="role-badge"
              :class="getRoleBadgeClass(user.role)"
              :style="{ '--role-color': getRoleColor(user.role) }"
            >
              {{ getRoleLabel(user.role) }}
            </span>
          </div>
          <span class="user-email">{{ user.userEmail }}</span>
        </div>

        <div class="user-actions">
          <!-- Role dropdown for changing roles -->
          <select
            v-if="canChangeRole(user)"
            :value="user.role"
            @change="handleRoleChange(user, $event.target.value)"
            class="role-select"
          >
            <option
              v-for="role in getAvailableRolesForUser(user)"
              :key="role"
              :value="role"
            >
              {{ getRoleLabel(role) }}
            </option>
          </select>

          <!-- Remove button -->
          <button
            v-if="canRemove(user)"
            @click="$emit('remove-user', user.personId)"
            class="btn btn-small btn-danger"
          >
            Remove
          </button>
        </div>
      </div>

      <div v-if="sortedUsers.length === 0" class="empty-state">
        No users found.
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, defineProps, defineEmits } from 'vue';
import {
  getRoleLabel,
  getRoleColor,
  getRoleBadgeClass,
  ROLE_HIERARCHY
} from '../../../composables/useEventUsers';

const props = defineProps({
  users: {
    type: Array,
    required: true,
  },
  canManageUsers: {
    type: Boolean,
    default: false,
  },
  canManageOwners: {
    type: Boolean,
    default: false,
  },
  currentPersonId: {
    type: String,
    default: '',
  },
});

const emit = defineEmits(['add-user', 'remove-user', 'change-role']);

// Sort users by role hierarchy, then by name
const sortedUsers = computed(() => {
  return [...props.users].sort((a, b) => {
    // First sort by role hierarchy
    const indexA = ROLE_HIERARCHY.indexOf(a.role);
    const indexB = ROLE_HIERARCHY.indexOf(b.role);
    const safeIndexA = indexA === -1 ? ROLE_HIERARCHY.length : indexA;
    const safeIndexB = indexB === -1 ? ROLE_HIERARCHY.length : indexB;
    if (safeIndexA !== safeIndexB) return safeIndexA - safeIndexB;

    // Then sort alphabetically by name (case-insensitive)
    const nameA = a.userName || a.userEmail || '';
    const nameB = b.userName || b.userEmail || '';
    return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
  });
});

/**
 * Check if current user can change this user's role
 */
const canChangeRole = (user) => {
  if (!props.canManageUsers) return false;

  // Can't change your own role
  if (user.personId === props.currentPersonId) return false;

  // Only owners can change owner roles
  if (user.role === 'EventOwner' && !props.canManageOwners) return false;

  return true;
};

/**
 * Check if current user can remove this user
 */
const canRemove = (user) => {
  if (!props.canManageUsers) return false;

  // Can't remove yourself
  if (user.personId === props.currentPersonId) return false;

  // Only owners can remove other owners
  if (user.role === 'EventOwner' && !props.canManageOwners) return false;

  return true;
};

/**
 * Get available roles for changing a specific user's role
 */
const getAvailableRolesForUser = (user) => {
  if (props.canManageOwners) {
    // Owners can assign all roles
    return ROLE_HIERARCHY;
  } else {
    // Administrators can only assign non-owner roles
    return ROLE_HIERARCHY.filter(role => role !== 'EventOwner');
  }
};

/**
 * Handle role change
 */
const handleRoleChange = (user, newRole) => {
  if (newRole !== user.role) {
    emit('change-role', { personId: user.personId, role: newRole });
  }
};
</script>

<style scoped>
.users-list-container {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.users-header {
  display: flex;
  justify-content: flex-start;
}

.btn {
  padding: 0.5rem 1rem;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 0.9rem;
  transition: background-color 0.2s;
}

.btn-small {
  padding: 0.4rem 0.8rem;
  font-size: 0.85rem;
}

.btn-primary {
  background: var(--btn-primary-bg);
  color: var(--btn-primary-text);
}

.btn-primary:hover {
  background: var(--btn-primary-hover);
}

.btn-danger {
  background: var(--danger);
  color: var(--btn-primary-text);
}

.btn-danger:hover {
  background: var(--danger-hover);
}

.users-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.user-item {
  padding: 1rem;
  border: 2px solid var(--border-light);
  border-radius: 6px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: var(--card-bg);
  gap: 1rem;
}

.user-info {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
  flex: 1;
}

.user-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.user-name {
  color: var(--text-primary);
  word-break: break-word;
}

.user-email {
  font-size: 0.85rem;
  color: var(--text-secondary);
  word-break: break-all;
}

.role-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.5rem;
  border-radius: 9999px;
  font-size: 0.7rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.025em;
  background-color: var(--role-color);
  color: white;
  white-space: nowrap;
}

.role-owner {
  --role-color: #7c3aed;
}

.role-administrator {
  --role-color: #2563eb;
}

.role-contributor {
  --role-color: #059669;
}

.role-viewer {
  --role-color: #6b7280;
}

.user-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.role-select {
  padding: 0.4rem 0.6rem;
  border: 1px solid var(--border-medium);
  border-radius: 4px;
  font-size: 0.85rem;
  background: var(--input-bg);
  color: var(--text-primary);
  cursor: pointer;
}

.role-select:focus {
  outline: none;
  border-color: var(--accent-primary);
}

.empty-state {
  padding: 2rem;
  text-align: center;
  color: var(--text-secondary);
  font-style: italic;
}

@media (max-width: 500px) {
  .user-item {
    flex-direction: column;
    align-items: flex-start;
  }

  .user-actions {
    width: 100%;
    justify-content: flex-end;
    margin-top: 0.5rem;
    padding-top: 0.5rem;
    border-top: 1px solid var(--border-light);
  }
}
</style>
