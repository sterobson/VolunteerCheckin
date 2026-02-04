/**
 * Composable for event users management with role-based access control
 * Handles loading, adding, updating, and removing event users
 * Replaces useEventAdmins.js
 */

import { ref, computed } from 'vue';
import { eventUsersApi } from '../services/api';

// Role hierarchy (highest to lowest privilege)
export const ROLE_HIERARCHY = [
  'EventOwner',
  'EventAdministrator',
  'EventContributor',
  'EventViewer'
];

// Role display configuration
export const ROLE_CONFIG = {
  EventOwner: {
    label: 'Owner',
    description: 'Full control including event deletion and managing other owners',
    color: '#7c3aed', // Purple
    badgeClass: 'role-owner'
  },
  EventAdministrator: {
    label: 'Administrator',
    description: 'Can modify event data and manage non-owner users',
    color: '#2563eb', // Blue
    badgeClass: 'role-administrator'
  },
  EventContributor: {
    label: 'Contributor',
    description: 'Can modify event data but cannot manage users',
    color: '#059669', // Green
    badgeClass: 'role-contributor'
  },
  EventViewer: {
    label: 'Viewer',
    description: 'Read-only access to view event data',
    color: '#6b7280', // Gray
    badgeClass: 'role-viewer'
  }
};

/**
 * Get display label for a role
 */
export function getRoleLabel(role) {
  return ROLE_CONFIG[role]?.label || role;
}

/**
 * Get role description
 */
export function getRoleDescription(role) {
  return ROLE_CONFIG[role]?.description || '';
}

/**
 * Get role color
 */
export function getRoleColor(role) {
  return ROLE_CONFIG[role]?.color || '#6b7280';
}

/**
 * Get role badge CSS class
 */
export function getRoleBadgeClass(role) {
  return ROLE_CONFIG[role]?.badgeClass || 'role-unknown';
}

/**
 * Compare roles by hierarchy (for sorting)
 * Returns negative if a is higher privilege, positive if b is higher
 */
export function compareRoles(roleA, roleB) {
  const indexA = ROLE_HIERARCHY.indexOf(roleA);
  const indexB = ROLE_HIERARCHY.indexOf(roleB);
  // Handle unknown roles by putting them at the end
  const safeIndexA = indexA === -1 ? ROLE_HIERARCHY.length : indexA;
  const safeIndexB = indexB === -1 ? ROLE_HIERARCHY.length : indexB;
  return safeIndexA - safeIndexB;
}

export function useEventUsers(eventId) {
  // Data
  const eventUsers = ref([]);
  const myPermissions = ref(null);
  const isLoading = ref(false);
  const error = ref(null);

  // Modal state
  const showAddUser = ref(false);

  /**
   * Computed: sorted users (by role hierarchy, then by name alphabetically)
   */
  const sortedUsers = computed(() => {
    return [...eventUsers.value].sort((a, b) => {
      // First sort by role hierarchy
      const roleCompare = compareRoles(a.role, b.role);
      if (roleCompare !== 0) return roleCompare;
      // Then sort alphabetically by name (case-insensitive)
      const nameA = a.userName || a.userEmail || '';
      const nameB = b.userName || b.userEmail || '';
      return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
    });
  });

  /**
   * Computed: permission helpers from myPermissions
   */
  const canManageOwners = computed(() => myPermissions.value?.canManageOwners ?? false);
  const canManageUsers = computed(() => myPermissions.value?.canManageUsers ?? false);
  const canModifyEvent = computed(() => myPermissions.value?.canModifyEvent ?? false);
  const canDeleteEvent = computed(() => myPermissions.value?.canDeleteEvent ?? false);
  const isReadOnly = computed(() => myPermissions.value?.isReadOnly ?? true);
  const myRole = computed(() => myPermissions.value?.role ?? 'None');

  /**
   * Computed: available roles for adding users (filtered by current user's permissions)
   */
  const availableRolesToAssign = computed(() => {
    if (!canManageUsers.value) return [];

    if (canManageOwners.value) {
      // Owners can assign all roles
      return ROLE_HIERARCHY;
    } else {
      // Administrators can only assign non-owner roles
      return ROLE_HIERARCHY.filter(role => role !== 'EventOwner');
    }
  });

  /**
   * Check if current user can change a specific user's role
   */
  const canChangeUserRole = (user) => {
    if (!canManageUsers.value) return false;

    // Can't change your own role
    // Note: We don't have current personId here, so this check happens in the backend
    // But we can still show the UI control

    // Only owners can change owner roles
    if (user.role === 'EventOwner' && !canManageOwners.value) return false;

    return true;
  };

  /**
   * Check if current user can remove a specific user
   */
  const canRemoveUser = (user) => {
    if (!canManageUsers.value) return false;

    // Only owners can remove other owners
    if (user.role === 'EventOwner' && !canManageOwners.value) return false;

    return true;
  };

  /**
   * Load event users
   */
  const loadEventUsers = async () => {
    isLoading.value = true;
    error.value = null;
    try {
      const response = await eventUsersApi.getUsers(eventId.value);
      eventUsers.value = response.data;
      return response.data;
    } catch (err) {
      console.error('Failed to load event users:', err);
      error.value = err.response?.data?.message || 'Failed to load users';
      throw err;
    } finally {
      isLoading.value = false;
    }
  };

  /**
   * Load current user's permissions for this event
   */
  const loadMyPermissions = async () => {
    try {
      const response = await eventUsersApi.getMyPermissions(eventId.value);
      myPermissions.value = response.data;
      return response.data;
    } catch (err) {
      console.error('Failed to load permissions:', err);
      // Don't throw - permissions errors shouldn't block the UI
      // Just set default restrictive permissions
      myPermissions.value = {
        role: 'None',
        canManageOwners: false,
        canManageUsers: false,
        canModifyEvent: false,
        canDeleteEvent: false,
        isReadOnly: true
      };
      return myPermissions.value;
    }
  };

  /**
   * Open add user modal
   */
  const openAddUserModal = () => {
    showAddUser.value = true;
  };

  /**
   * Close add user modal
   */
  const closeAddUserModal = () => {
    showAddUser.value = false;
  };

  /**
   * Add a new user with a specific role and optional name
   */
  const addUser = async (email, role, name = null) => {
    const response = await eventUsersApi.addUser(eventId.value, email, role, name);
    // Add to local list
    eventUsers.value.push(response.data);
    return response.data;
  };

  /**
   * Update a user's role
   */
  const updateUserRole = async (personId, newRole) => {
    const response = await eventUsersApi.updateUserRole(eventId.value, personId, newRole);
    // Update local list
    const index = eventUsers.value.findIndex(u => u.personId === personId);
    if (index !== -1) {
      eventUsers.value[index] = response.data;
    }
    return response.data;
  };

  /**
   * Remove a user from the event
   */
  const removeUser = async (personId) => {
    await eventUsersApi.removeUser(eventId.value, personId);
    // Remove from local list
    eventUsers.value = eventUsers.value.filter(u => u.personId !== personId);
  };

  return {
    // Data
    eventUsers,
    sortedUsers,
    myPermissions,
    isLoading,
    error,

    // Modal state
    showAddUser,

    // Computed permissions
    canManageOwners,
    canManageUsers,
    canModifyEvent,
    canDeleteEvent,
    isReadOnly,
    myRole,
    availableRolesToAssign,

    // Permission checks
    canChangeUserRole,
    canRemoveUser,

    // Methods
    loadEventUsers,
    loadMyPermissions,
    openAddUserModal,
    closeAddUserModal,
    addUser,
    updateUserRole,
    removeUser,
  };
}
