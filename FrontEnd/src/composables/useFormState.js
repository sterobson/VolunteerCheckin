/**
 * Composable for form state management
 * Provides dirty state tracking and unsaved changes warning
 */

import { ref, computed, watch } from 'vue';

export function useFormState(initialData = {}, options = {}) {
  const {
    warnOnUnsavedChanges = true,
    deepCompare = true,
  } = options;

  const formData = ref({ ...initialData });
  const originalData = ref({ ...initialData });
  const isDirty = ref(false);
  const errors = ref({});

  /**
   * Check if form has unsaved changes
   */
  const hasUnsavedChanges = computed(() => {
    if (!deepCompare) {
      return isDirty.value;
    }

    return JSON.stringify(formData.value) !== JSON.stringify(originalData.value);
  });

  /**
   * Reset form to original state
   */
  const resetForm = () => {
    formData.value = { ...originalData.value };
    isDirty.value = false;
    errors.value = {};
  };

  /**
   * Update original data (call after successful save)
   * @param {Object} newData - Optional new data to set as original
   */
  const markAsSaved = (newData = null) => {
    if (newData !== null) {
      originalData.value = { ...newData };
      formData.value = { ...newData };
    } else {
      originalData.value = { ...formData.value };
    }
    isDirty.value = false;
    errors.value = {};
  };

  /**
   * Set form data
   * @param {Object} newData - New form data
   * @param {boolean} resetOriginal - If true, also update original data (default: true)
   */
  const setFormData = (newData, resetOriginal = true) => {
    formData.value = { ...newData };
    if (resetOriginal) {
      originalData.value = { ...newData };
      isDirty.value = false;
    }
  };

  /**
   * Set a specific field value
   * @param {string} field - Field name
   * @param {any} value - New value
   */
  const setField = (field, value) => {
    formData.value[field] = value;
    isDirty.value = true;

    // Clear error for this field
    if (errors.value[field]) {
      delete errors.value[field];
    }
  };

  /**
   * Get a specific field value
   * @param {string} field - Field name
   * @returns {any} Field value
   */
  const getField = (field) => {
    return formData.value[field];
  };

  /**
   * Set validation error for a field
   * @param {string} field - Field name
   * @param {string} message - Error message
   */
  const setError = (field, message) => {
    errors.value[field] = message;
  };

  /**
   * Set multiple errors at once
   * @param {Object} errorObj - Object with field names as keys and error messages as values
   */
  const setErrors = (errorObj) => {
    errors.value = { ...errorObj };
  };

  /**
   * Clear error for a specific field
   * @param {string} field - Field name
   */
  const clearError = (field) => {
    delete errors.value[field];
  };

  /**
   * Clear all errors
   */
  const clearErrors = () => {
    errors.value = {};
  };

  /**
   * Check if a specific field has an error
   * @param {string} field - Field name
   * @returns {boolean} True if field has error
   */
  const hasError = (field) => {
    return !!errors.value[field];
  };

  /**
   * Get error message for a specific field
   * @param {string} field - Field name
   * @returns {string|null} Error message or null
   */
  const getError = (field) => {
    return errors.value[field] || null;
  };

  /**
   * Check if form has any errors
   * @returns {boolean} True if any errors exist
   */
  const hasErrors = computed(() => {
    return Object.keys(errors.value).length > 0;
  });

  /**
   * Check if form is valid (no errors and required fields filled)
   * @param {string[]} requiredFields - Array of required field names
   * @returns {boolean} True if valid
   */
  const isValid = (requiredFields = []) => {
    if (hasErrors.value) return false;

    for (const field of requiredFields) {
      const value = formData.value[field];
      if (value === null || value === undefined || value === '') {
        return false;
      }
    }

    return true;
  };

  // Watch for form changes to update dirty state
  watch(
    formData,
    () => {
      isDirty.value = true;
    },
    { deep: true }
  );

  // Warn on page unload if there are unsaved changes
  if (warnOnUnsavedChanges && typeof window !== 'undefined') {
    const handleBeforeUnload = (e) => {
      if (hasUnsavedChanges.value) {
        e.preventDefault();
        e.returnValue = '';
      }
    };

    window.addEventListener('beforeunload', handleBeforeUnload);

    // Note: You'll need to call cleanup manually or use onBeforeUnmount in the component
    const cleanup = () => {
      window.removeEventListener('beforeunload', handleBeforeUnload);
    };

    return {
      // State
      formData,
      isDirty,
      hasUnsavedChanges,
      errors,
      hasErrors,

      // Methods
      resetForm,
      markAsSaved,
      setFormData,
      setField,
      getField,
      setError,
      setErrors,
      clearError,
      clearErrors,
      hasError,
      getError,
      isValid,
      cleanup,
    };
  }

  return {
    // State
    formData,
    isDirty,
    hasUnsavedChanges,
    errors,
    hasErrors,

    // Methods
    resetForm,
    markAsSaved,
    setFormData,
    setField,
    getField,
    setError,
    setErrors,
    clearError,
    clearErrors,
    hasError,
    getError,
    isValid,
  };
}
