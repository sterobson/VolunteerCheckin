/**
 * Sorting helper utilities
 * Functions for sorting arrays with different data types
 */

/**
 * Sort alphanumeric strings (handles mixed numbers and text)
 * Sorts numbers numerically and text alphabetically
 * @param {string|number} a - First value to compare
 * @param {string|number} b - Second value to compare
 * @returns {number} Sort comparison result
 */
export const sortAlphanumeric = (a, b) => {
  // Convert to strings for comparison
  const aStr = String(a).trim();
  const bStr = String(b).trim();

  // Try to parse as numbers
  const aNum = parseFloat(aStr);
  const bNum = parseFloat(bStr);

  // Check if both are valid numbers and the string representation matches
  const aIsNum = !isNaN(aNum) && String(aNum) === aStr;
  const bIsNum = !isNaN(bNum) && String(bNum) === bStr;

  // If both are numbers, compare numerically
  if (aIsNum && bIsNum) {
    return aNum - bNum;
  }

  // Otherwise, use locale-aware string comparison
  return aStr.localeCompare(bStr, undefined, {
    numeric: true,
    sensitivity: 'base',
  });
};

/**
 * Create a sorting function for object properties
 * @param {string} key - Property key to sort by
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {Function} Sorting function
 */
export const sortByProperty = (key, ascending = true) => {
  return (a, b) => {
    const aVal = a[key];
    const bVal = b[key];

    if (aVal === null || aVal === undefined) return 1;
    if (bVal === null || bVal === undefined) return -1;

    const result = sortAlphanumeric(aVal, bVal);
    return ascending ? result : -result;
  };
};

/**
 * Sort by date
 * @param {string|Date} a - First date
 * @param {string|Date} b - Second date
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {number} Sort comparison result
 */
export const sortByDate = (a, b, ascending = true) => {
  const dateA = new Date(a);
  const dateB = new Date(b);

  if (isNaN(dateA)) return 1;
  if (isNaN(dateB)) return -1;

  const result = dateA - dateB;
  return ascending ? result : -result;
};

/**
 * Create a sorting function for date properties
 * @param {string} key - Property key to sort by
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {Function} Sorting function
 */
export const sortByDateProperty = (key, ascending = true) => {
  return (a, b) => {
    return sortByDate(a[key], b[key], ascending);
  };
};

/**
 * Sort by number
 * @param {number} a - First number
 * @param {number} b - Second number
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {number} Sort comparison result
 */
export const sortByNumber = (a, b, ascending = true) => {
  const numA = parseFloat(a);
  const numB = parseFloat(b);

  if (isNaN(numA)) return 1;
  if (isNaN(numB)) return -1;

  const result = numA - numB;
  return ascending ? result : -result;
};

/**
 * Create a sorting function for numeric properties
 * @param {string} key - Property key to sort by
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {Function} Sorting function
 */
export const sortByNumericProperty = (key, ascending = true) => {
  return (a, b) => {
    return sortByNumber(a[key], b[key], ascending);
  };
};

/**
 * Sort by boolean (false first, then true)
 * @param {boolean} a - First boolean
 * @param {boolean} b - Second boolean
 * @param {boolean} trueFirst - If true, sort true values first (default: false)
 * @returns {number} Sort comparison result
 */
export const sortByBoolean = (a, b, trueFirst = false) => {
  const result = (a === b) ? 0 : a ? 1 : -1;
  return trueFirst ? -result : result;
};

/**
 * Create a sorting function for boolean properties
 * @param {string} key - Property key to sort by
 * @param {boolean} trueFirst - If true, sort true values first (default: false)
 * @returns {Function} Sorting function
 */
export const sortByBooleanProperty = (key, trueFirst = false) => {
  return (a, b) => {
    return sortByBoolean(a[key], b[key], trueFirst);
  };
};

/**
 * Create a multi-level sorting function
 * @param {...Function} sortFunctions - Multiple sorting functions to apply in order
 * @returns {Function} Combined sorting function
 */
export const multiSort = (...sortFunctions) => {
  return (a, b) => {
    for (const sortFn of sortFunctions) {
      const result = sortFn(a, b);
      if (result !== 0) return result;
    }
    return 0;
  };
};

/**
 * Sort case-insensitive
 * @param {string} a - First string
 * @param {string} b - Second string
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {number} Sort comparison result
 */
export const sortCaseInsensitive = (a, b, ascending = true) => {
  const aLower = String(a).toLowerCase();
  const bLower = String(b).toLowerCase();

  const result = aLower.localeCompare(bLower);
  return ascending ? result : -result;
};

/**
 * Create a case-insensitive sorting function for properties
 * @param {string} key - Property key to sort by
 * @param {boolean} ascending - Sort direction (default: true)
 * @returns {Function} Sorting function
 */
export const sortByCaseInsensitiveProperty = (key, ascending = true) => {
  return (a, b) => {
    return sortCaseInsensitive(a[key], b[key], ascending);
  };
};
