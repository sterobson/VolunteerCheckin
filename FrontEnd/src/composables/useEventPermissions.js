/**
 * Composable for event permissions context using provide/inject pattern
 * Allows child components to access the current user's permissions for the event
 */

import { inject, provide, readonly, ref, computed } from 'vue';

const EVENT_PERMISSIONS_KEY = Symbol('eventPermissions');

/**
 * Default permissions (restrictive)
 */
const DEFAULT_PERMISSIONS = {
  role: 'None',
  canManageOwners: false,
  canManageUsers: false,
  canModifyEvent: false,
  canDeleteEvent: false,
  isReadOnly: true
};

/**
 * Provide event permissions to child components
 * Called in AdminEventManage.vue
 *
 * @param {Object} permissionsRef - A ref containing the permissions object
 */
export function provideEventPermissions(permissionsRef) {
  // Create computed properties for easy access
  const permissions = computed(() => permissionsRef.value || DEFAULT_PERMISSIONS);

  const context = {
    // Raw permissions object
    permissions: readonly(permissions),

    // Convenience computed properties
    role: computed(() => permissions.value.role),
    canManageOwners: computed(() => permissions.value.canManageOwners),
    canManageUsers: computed(() => permissions.value.canManageUsers),
    canModifyEvent: computed(() => permissions.value.canModifyEvent),
    canDeleteEvent: computed(() => permissions.value.canDeleteEvent),
    isReadOnly: computed(() => permissions.value.isReadOnly),

    // Helper for conditional rendering
    hasAnyRole: computed(() => permissions.value.role !== 'None'),
    isOwner: computed(() => permissions.value.role === 'EventOwner'),
    isAdministrator: computed(() => permissions.value.role === 'EventAdministrator'),
    isContributor: computed(() => permissions.value.role === 'EventContributor'),
    isViewer: computed(() => permissions.value.role === 'EventViewer'),
  };

  provide(EVENT_PERMISSIONS_KEY, context);

  return context;
}

/**
 * Use event permissions in child components
 * Returns the permissions context provided by a parent component
 *
 * @returns {Object} Permissions context with computed properties
 */
export function useEventPermissions() {
  const context = inject(EVENT_PERMISSIONS_KEY);

  if (!context) {
    // Return default restrictive permissions if not provided
    // This allows components to work even if permissions aren't set up yet
    console.warn('useEventPermissions called without a provider. Using default restrictive permissions.');
    return {
      permissions: readonly(ref(DEFAULT_PERMISSIONS)),
      role: computed(() => DEFAULT_PERMISSIONS.role),
      canManageOwners: computed(() => DEFAULT_PERMISSIONS.canManageOwners),
      canManageUsers: computed(() => DEFAULT_PERMISSIONS.canManageUsers),
      canModifyEvent: computed(() => DEFAULT_PERMISSIONS.canModifyEvent),
      canDeleteEvent: computed(() => DEFAULT_PERMISSIONS.canDeleteEvent),
      isReadOnly: computed(() => DEFAULT_PERMISSIONS.isReadOnly),
      hasAnyRole: computed(() => false),
      isOwner: computed(() => false),
      isAdministrator: computed(() => false),
      isContributor: computed(() => false),
      isViewer: computed(() => false),
    };
  }

  return context;
}
