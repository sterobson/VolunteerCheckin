/**
 * Composable for event administrators management
 * Handles loading, adding, and removing event administrators
 */

import { ref } from 'vue';
import { eventAdminsApi } from '../services/api';

export function useEventAdmins(eventId) {
  // Data
  const eventAdmins = ref([]);

  // Modal state
  const showAddAdmin = ref(false);

  /**
   * Load event administrators
   */
  const loadEventAdmins = async () => {
    try {
      const response = await eventAdminsApi.getAdmins(eventId.value);
      eventAdmins.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load event admins:', error);
      throw error;
    }
  };

  /**
   * Open add admin modal
   */
  const openAddAdminModal = () => {
    showAddAdmin.value = true;
  };

  /**
   * Close add admin modal
   */
  const closeAddAdminModal = () => {
    showAddAdmin.value = false;
  };

  /**
   * Add a new administrator by email
   */
  const addAdmin = async (email) => {
    await eventAdminsApi.addAdmin(eventId.value, email);
  };

  /**
   * Remove an administrator by email
   */
  const removeAdmin = async (userEmail) => {
    await eventAdminsApi.removeAdmin(eventId.value, userEmail);
  };

  return {
    // Data
    eventAdmins,

    // Modal state
    showAddAdmin,

    // Methods
    loadEventAdmins,
    openAddAdminModal,
    closeAddAdminModal,
    addAdmin,
    removeAdmin,
  };
}
