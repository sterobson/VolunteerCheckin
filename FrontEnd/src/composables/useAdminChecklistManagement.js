/**
 * Composable for admin checklist management
 * Handles modal state, form data, and checklist operations in admin context
 */

import { ref } from 'vue';
import { checklistApi } from '../services/api';

export function useAdminChecklistManagement(eventId) {
  // Data
  const checklistItems = ref([]);
  const checklistCompletionReport = ref(null);
  const checklistDetailedReport = ref(null);
  const isLoadingDetailedReport = ref(false);

  // Modal state
  const showEditChecklistItem = ref(false);
  const checklistItemInitialTab = ref('details');

  // Selected item
  const selectedChecklistItem = ref(null);

  /**
   * Load checklists for event
   */
  const loadChecklists = async () => {
    try {
      const [itemsResponse, reportResponse] = await Promise.all([
        checklistApi.getByEvent(eventId.value),
        checklistApi.getReport(eventId.value),
      ]);
      checklistItems.value = itemsResponse.data;
      checklistCompletionReport.value = reportResponse.data;
      return { items: itemsResponse.data, report: reportResponse.data };
    } catch (error) {
      console.error('Failed to load checklists:', error);
      throw error;
    }
  };

  /**
   * Load detailed report for reporting views
   */
  const loadDetailedReport = async () => {
    try {
      isLoadingDetailedReport.value = true;
      const response = await checklistApi.getDetailedReport(eventId.value);
      checklistDetailedReport.value = response.data;
      return response.data;
    } catch (error) {
      console.error('Failed to load detailed checklist report:', error);
      throw error;
    } finally {
      isLoadingDetailedReport.value = false;
    }
  };

  /**
   * Open modal to add new checklist item
   */
  const handleAddChecklistItem = () => {
    selectedChecklistItem.value = null;
    checklistItemInitialTab.value = 'details';
    showEditChecklistItem.value = true;
  };

  /**
   * Select a checklist item for editing
   */
  const handleSelectChecklistItem = (item, tab = 'details') => {
    selectedChecklistItem.value = item;
    checklistItemInitialTab.value = tab;
    showEditChecklistItem.value = true;
  };

  /**
   * Close edit checklist item modal
   */
  const closeEditChecklistItemModal = () => {
    showEditChecklistItem.value = false;
    selectedChecklistItem.value = null;
  };

  /**
   * Save checklist item (create or update)
   */
  const saveChecklistItem = async (formData) => {
    let result = null;

    if (selectedChecklistItem.value && selectedChecklistItem.value.itemId) {
      // Update existing item
      await checklistApi.update(eventId.value, selectedChecklistItem.value.itemId, formData);
      result = { updated: true };
    } else {
      // Create new item
      const response = await checklistApi.create(eventId.value, formData);
      result = response.data;
    }

    return result;
  };

  /**
   * Delete a checklist item
   */
  const deleteChecklistItem = async (itemId) => {
    await checklistApi.delete(eventId.value, itemId);
  };

  /**
   * Complete a checklist item
   */
  const completeChecklistItem = async (itemId, payload) => {
    await checklistApi.complete(eventId.value, itemId, payload);
  };

  /**
   * Uncomplete a checklist item
   */
  const uncompleteChecklistItem = async (itemId, payload) => {
    await checklistApi.uncomplete(eventId.value, itemId, payload);
  };

  /**
   * Process multiple checklist changes
   */
  const processChecklistChanges = async (changes) => {
    const results = [];
    for (const change of changes) {
      try {
        const requestBody = {
          marshalId: change.marshalId,
        };

        if (change.contextType && change.contextId) {
          requestBody.contextType = change.contextType;
          requestBody.contextId = change.contextId;
        }

        if (change.complete) {
          await checklistApi.complete(eventId.value, change.itemId, requestBody);
        } else {
          await checklistApi.uncomplete(eventId.value, change.itemId, requestBody);
        }
        results.push({ success: true, change });
      } catch (error) {
        console.error('Failed to process checklist change:', error);
        results.push({ success: false, change, error });
      }
    }
    return results;
  };

  return {
    // Data
    checklistItems,
    checklistCompletionReport,
    checklistDetailedReport,
    isLoadingDetailedReport,

    // Modal state
    showEditChecklistItem,
    checklistItemInitialTab,

    // Selected item
    selectedChecklistItem,

    // Methods
    loadChecklists,
    loadDetailedReport,
    handleAddChecklistItem,
    handleSelectChecklistItem,
    closeEditChecklistItemModal,
    saveChecklistItem,
    deleteChecklistItem,
    completeChecklistItem,
    uncompleteChecklistItem,
    processChecklistChanges,
  };
}
