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
  const confirmModalIsDanger = ref(false);
  const confirmModalConfirmText = ref('OK');

  /**
   * Show a confirmation dialog
   * @param {string} title - Dialog title
   * @param {string} message - Dialog message
   * @param {Function} callback - Function to call when confirmed
   * @param {Object} options - Optional settings (isDanger, confirmText)
   */
  const showConfirm = (title, message, callback, options = {}) => {
    confirmModalTitle.value = title;
    confirmModalMessage.value = message;
    confirmModalCallback.value = callback;
    confirmModalIsDanger.value = options.isDanger || false;
    confirmModalConfirmText.value = options.confirmText || 'OK';
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
    confirmModalIsDanger,
    confirmModalConfirmText,

    // Methods
    showConfirm,
    handleConfirmModalConfirm,
    handleConfirmModalCancel,
    closeConfirmModal,
  };
}
