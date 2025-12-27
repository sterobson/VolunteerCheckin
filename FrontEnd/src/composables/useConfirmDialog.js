/**
 * Composable for confirm dialog functionality
 * Provides a reusable way to show confirmation dialogs
 */

import { ref } from 'vue';

export function useConfirmDialog() {
  const showConfirmModal = ref(false);
  const confirmModalTitle = ref('');
  const confirmModalMessage = ref('');
  const confirmModalCallback = ref(null);

  /**
   * Show a confirmation dialog
   * @param {string} title - Dialog title
   * @param {string} message - Dialog message
   * @param {Function} callback - Function to call when confirmed
   */
  const showConfirm = (title, message, callback) => {
    confirmModalTitle.value = title;
    confirmModalMessage.value = message;
    confirmModalCallback.value = callback;
    showConfirmModal.value = true;
  };

  /**
   * Handle confirmation
   */
  const handleConfirmModalConfirm = () => {
    showConfirmModal.value = false;
    if (confirmModalCallback.value) {
      confirmModalCallback.value();
      confirmModalCallback.value = null;
    }
  };

  /**
   * Handle cancellation
   */
  const handleConfirmModalCancel = () => {
    showConfirmModal.value = false;
    confirmModalCallback.value = null;
  };

  /**
   * Close the modal
   */
  const closeConfirmModal = () => {
    showConfirmModal.value = false;
    confirmModalCallback.value = null;
  };

  return {
    // State
    showConfirmModal,
    confirmModalTitle,
    confirmModalMessage,

    // Methods
    showConfirm,
    handleConfirmModalConfirm,
    handleConfirmModalCancel,
    closeConfirmModal,
  };
}
