/**
 * Utility functions for checking scope-based permissions.
 */

/**
 * Clean up orphaned scope configurations by removing references to deleted entities.
 * This should be called before saving entities with scope configurations.
 *
 * @param {Array} scopeConfigurations - The scope configurations to clean
 * @param {Object} validEntities - Object containing arrays of valid entities
 * @param {Array} validEntities.areas - Array of valid area objects with 'id' property
 * @param {Array} validEntities.locations - Array of valid location objects with 'id' property
 * @param {Array} validEntities.marshals - Array of valid marshal objects with 'id' property
 * @returns {Array} - Cleaned scope configurations with orphaned references removed
 */
export function cleanOrphanedScopeConfigurations(scopeConfigurations, { areas = [], locations = [], marshals = [] }) {
  if (!scopeConfigurations || scopeConfigurations.length === 0) {
    return scopeConfigurations;
  }

  const areaIds = new Set(areas.map(a => a.id));
  const locationIds = new Set(locations.map(l => l.id));
  const marshalIds = new Set(marshals.map(m => m.id));

  // Special IDs that should always be kept
  const specialIds = new Set(['ALL_MARSHALS', 'ALL_AREAS', 'ALL_CHECKPOINTS', 'THIS_CHECKPOINT']);

  const cleanedConfigs = scopeConfigurations
    .map(config => {
      const itemType = config.itemType;
      const ids = config.ids || [];

      // If no itemType or no IDs, keep the config as-is
      if (!itemType || ids.length === 0) {
        return config;
      }

      // Filter out orphaned IDs based on item type
      const validIds = ids.filter(id => {
        // Always keep special IDs
        if (specialIds.has(id)) {
          return true;
        }

        // Check if ID exists in the appropriate entity list
        if (itemType === 'Area') {
          return areaIds.has(id);
        }
        if (itemType === 'Checkpoint') {
          return locationIds.has(id);
        }
        if (itemType === 'Marshal') {
          return marshalIds.has(id);
        }

        // Unknown item type - keep the ID
        return true;
      });

      // Return updated config with cleaned IDs
      return {
        ...config,
        ids: validIds,
      };
    })
    // Remove configs that have no valid IDs left (unless they have no itemType)
    .filter(config => {
      if (!config.itemType) {
        return true;
      }
      return config.ids && config.ids.length > 0;
    });

  return cleanedConfigs;
}

/**
 * Check if a marshal can update a dynamic location based on its scope configurations.
 *
 * @param {Object} options - The options object
 * @param {Array} options.scopeConfigurations - The location's locationUpdateScopeConfigurations array
 * @param {string} options.marshalId - The current marshal's ID
 * @param {Array<string>} options.marshalAssignmentLocationIds - Location IDs where the marshal is assigned
 * @param {Array<string>} options.areaLeadAreaIds - Area IDs where the marshal is an area lead
 * @param {string} options.locationId - The ID of the dynamic location being checked
 * @param {Array<string>} options.locationAreaIds - Area IDs that the dynamic location belongs to
 * @returns {boolean} - True if the marshal can update the location
 */
export function canMarshalUpdateDynamicLocation({
  scopeConfigurations = [],
  marshalId,
  marshalAssignmentLocationIds = [],
  areaLeadAreaIds = [],
  locationId,
  locationAreaIds = [],
}) {
  // If no scope configurations, default to allowing updates (backwards compatibility)
  if (!scopeConfigurations || scopeConfigurations.length === 0) {
    return true;
  }

  for (const config of scopeConfigurations) {
    const scope = config.scope || config.Scope;
    const ids = config.ids || config.Ids || [];

    switch (scope) {
      case 'SpecificPeople':
        // Check if ALL_MARSHALS or this specific marshal is in the list
        if (ids.includes('ALL_MARSHALS') || ids.includes(marshalId)) {
          return true;
        }
        break;

      case 'EveryoneAtCheckpoints':
        // Check if the marshal is assigned to one of the specified checkpoints
        if (ids.includes('ALL_CHECKPOINTS')) {
          // Any marshal assigned to any checkpoint can update
          if (marshalAssignmentLocationIds.length > 0) {
            return true;
          }
        } else {
          // Check if THIS_CHECKPOINT is specified (resolves to the location being updated)
          if (ids.includes('THIS_CHECKPOINT') || ids.includes(locationId)) {
            if (marshalAssignmentLocationIds.includes(locationId)) {
              return true;
            }
          }
          // Check if marshal is assigned to any of the specified checkpoints
          const hasMatchingAssignment = ids.some(id =>
            marshalAssignmentLocationIds.includes(id)
          );
          if (hasMatchingAssignment) {
            return true;
          }
        }
        break;

      case 'EveryoneInAreas':
        // Check if the marshal is assigned to a checkpoint in one of the specified areas
        if (ids.includes('ALL_AREAS')) {
          // Any marshal in any area can update
          // For simplicity, if marshal has any assignment, they're in some area
          if (marshalAssignmentLocationIds.length > 0) {
            return true;
          }
        } else {
          // Check if any of the marshal's assignment areas match the scope
          // Note: We'd need the marshal's assignment areas, but for now we check if
          // the dynamic location's area matches (since marshal is viewing this location)
          const hasMatchingArea = ids.some(id =>
            locationAreaIds.includes(id)
          );
          if (hasMatchingArea && marshalAssignmentLocationIds.includes(locationId)) {
            return true;
          }
        }
        break;

      case 'EveryAreaLead':
        // Check if the marshal is an area lead for one of the specified areas
        if (ids.includes('ALL_AREAS')) {
          // Any area lead can update
          if (areaLeadAreaIds.length > 0) {
            return true;
          }
        } else {
          // Check if marshal is area lead for any of the specified areas
          const isAreaLeadForScope = ids.some(id =>
            areaLeadAreaIds.includes(id)
          );
          if (isAreaLeadForScope) {
            return true;
          }
        }
        break;

      default:
        // Unknown scope type - skip
        break;
    }
  }

  // No matching scope found
  return false;
}
