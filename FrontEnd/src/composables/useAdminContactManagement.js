/**
 * Composable for admin contact management
 * Handles modal state, form data, and contact operations in admin context
 */

import { ref } from 'vue';
import { contactsApi } from '../services/api';

export function useAdminContactManagement(eventId) {
  // Data
  const contacts = ref([]);
  const contactRoles = ref({ builtInRoles: [], customRoles: [] });

  // Modal state
  const showEditContact = ref(false);
  const contactInitialTab = ref('details');

  // Selected contact
  const selectedContact = ref(null);

  // Pending area for pre-populating scope
  const pendingAreaForContact = ref(null);

  /**
   * Load contacts and roles for event
   */
  const loadContacts = async () => {
    try {
      const [contactsResponse, rolesResponse] = await Promise.all([
        contactsApi.getByEvent(eventId.value),
        contactsApi.getRoles(eventId.value),
      ]);
      contacts.value = contactsResponse.data;
      contactRoles.value = rolesResponse.data;
      return { contacts: contactsResponse.data, roles: rolesResponse.data };
    } catch (error) {
      console.error('Failed to load contacts:', error);
      throw error;
    }
  };

  /**
   * Open modal to add new contact
   */
  const handleAddContact = () => {
    selectedContact.value = null;
    contactInitialTab.value = 'details';
    pendingAreaForContact.value = null;
    showEditContact.value = true;
  };

  /**
   * Open modal to add contact for specific area
   */
  const handleAddAreaContact = (area) => {
    selectedContact.value = null;
    contactInitialTab.value = 'details';
    pendingAreaForContact.value = area;
    showEditContact.value = true;
  };

  /**
   * Select a contact for editing
   */
  const handleSelectContact = (contact) => {
    selectedContact.value = contact;
    contactInitialTab.value = 'details';
    showEditContact.value = true;
  };

  /**
   * Edit a contact (from area modal)
   */
  const handleEditContactFromArea = (contact) => {
    selectedContact.value = contact;
    contactInitialTab.value = 'details';
    showEditContact.value = true;
  };

  /**
   * Close edit contact modal
   */
  const closeEditContactModal = () => {
    showEditContact.value = false;
    selectedContact.value = null;
    pendingAreaForContact.value = null;
  };

  /**
   * Save contact (create or update)
   */
  const saveContact = async (formData) => {
    if (selectedContact.value && selectedContact.value.contactId) {
      // Update existing contact
      await contactsApi.update(eventId.value, selectedContact.value.contactId, formData);
    } else {
      // Create new contact
      await contactsApi.create(eventId.value, formData);
    }
  };

  /**
   * Delete a contact
   */
  const deleteContact = async (contactId) => {
    await contactsApi.delete(eventId.value, contactId);
  };

  return {
    // Data
    contacts,
    contactRoles,

    // Modal state
    showEditContact,
    contactInitialTab,

    // Selected contact
    selectedContact,

    // Area context
    pendingAreaForContact,

    // Methods
    loadContacts,
    handleAddContact,
    handleAddAreaContact,
    handleSelectContact,
    handleEditContactFromArea,
    closeEditContactModal,
    saveContact,
    deleteContact,
  };
}
