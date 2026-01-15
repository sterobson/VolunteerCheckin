/**
 * Composable for admin role definition management
 * Handles modal state, form data, and role operations in admin context
 */

import { ref } from 'vue';
import { roleDefinitionsApi } from '../services/api';

export function useAdminRoleManagement(eventId) {
  // Data
  const roleDefinitions = ref([]);

  // Modal state
  const showEditRole = ref(false);
  const roleInitialTab = ref('details');

  // Selected role
  const selectedRole = ref(null);

  // People for current role (loaded when viewing assignments)
  const rolePeople = ref([]);
  const loadingPeople = ref(false);

  /**
   * Load role definitions for event
   */
  const loadRoleDefinitions = async () => {
    try {
      const response = await roleDefinitionsApi.getAll(eventId.value);
      roleDefinitions.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load role definitions:', error);
      throw error;
    }
  };

  /**
   * Open modal to add new role
   */
  const handleAddRole = () => {
    selectedRole.value = null;
    roleInitialTab.value = 'details';
    rolePeople.value = [];
    showEditRole.value = true;
  };

  /**
   * Select a role for editing
   */
  const handleSelectRole = (role) => {
    selectedRole.value = role;
    roleInitialTab.value = 'details';
    rolePeople.value = [];
    showEditRole.value = true;
  };

  /**
   * Close edit role modal
   */
  const closeEditRoleModal = () => {
    showEditRole.value = false;
    selectedRole.value = null;
    rolePeople.value = [];
  };

  /**
   * Save role (create or update)
   */
  const saveRole = async (formData) => {
    if (selectedRole.value && selectedRole.value.roleId) {
      // Update existing role
      const response = await roleDefinitionsApi.update(eventId.value, selectedRole.value.roleId, formData);
      return response.data;
    } else {
      // Create new role
      const response = await roleDefinitionsApi.create(eventId.value, formData);
      return response.data;
    }
  };

  /**
   * Delete a role
   */
  const deleteRole = async (roleId) => {
    await roleDefinitionsApi.delete(eventId.value, roleId);
  };

  /**
   * Load people for a role (unified list of marshals and contacts)
   */
  const loadPeopleForRole = async (roleId) => {
    loadingPeople.value = true;
    try {
      const response = await roleDefinitionsApi.getPeople(eventId.value, roleId);
      rolePeople.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load people for role:', error);
      throw error;
    } finally {
      loadingPeople.value = false;
    }
  };

  /**
   * Update role assignments
   * @param {string} roleId - The role to update assignments for
   * @param {Object} changes - Object with marshalIdsToAdd, marshalIdsToRemove, contactIdsToAdd, contactIdsToRemove
   */
  const updateRoleAssignments = async (roleId, changes) => {
    const response = await roleDefinitionsApi.updatePeople(eventId.value, roleId, changes);
    rolePeople.value = response.data;
    return response.data;
  };

  /**
   * Reorder role definitions
   * @param {Array} items - Array of { id, displayOrder } objects
   */
  const reorderRoles = async (items) => {
    await roleDefinitionsApi.reorder(eventId.value, items);
  };

  return {
    // Data
    roleDefinitions,

    // Modal state
    showEditRole,
    roleInitialTab,

    // Selected role
    selectedRole,

    // People for role
    rolePeople,
    loadingPeople,

    // Methods
    loadRoleDefinitions,
    handleAddRole,
    handleSelectRole,
    closeEditRoleModal,
    saveRole,
    deleteRole,
    loadPeopleForRole,
    updateRoleAssignments,
    reorderRoles,
  };
}
