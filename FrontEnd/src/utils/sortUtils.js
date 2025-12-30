/**
 * Alphanumeric sort utility
 * Sorts strings with numeric prefixes sorted numerically
 * Example: "2 Zebra" comes before "10 Alpaca"
 */

/**
 * Compare two strings with alphanumeric sorting
 * @param {string} a - First string to compare
 * @param {string} b - Second string to compare
 * @returns {number} - Sort order (-1, 0, or 1)
 */
export function alphanumericCompare(a, b) {
  return a.localeCompare(b, undefined, {
    numeric: true,
    sensitivity: 'base'
  });
}

/**
 * Sort an array of objects by a specific field using alphanumeric comparison
 * @param {Array} array - Array to sort
 * @param {string|Function} fieldOrGetter - Field name or getter function
 * @returns {Array} - Sorted array (new array, original unchanged)
 */
export function sortByField(array, fieldOrGetter) {
  return [...array].sort((a, b) => {
    const aValue = typeof fieldOrGetter === 'function'
      ? fieldOrGetter(a)
      : a[fieldOrGetter];
    const bValue = typeof fieldOrGetter === 'function'
      ? fieldOrGetter(b)
      : b[fieldOrGetter];

    return alphanumericCompare(
      String(aValue || ''),
      String(bValue || '')
    );
  });
}
