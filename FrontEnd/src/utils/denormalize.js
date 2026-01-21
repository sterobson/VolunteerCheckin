/**
 * Denormalizes a normalized event status response back to the original structure.
 * This allows existing frontend logic to work unchanged while benefiting
 * from reduced network payload size.
 *
 * Short field names (see response._ for mapping):
 *   Response: _d=defaults (bool), _ds=stringDefaults, g=refs, m=marshals, cm=checkInMethods, sc=scopes, l=locations
 *   Marshal: r=refIndex, n=name
 *   Assignment: r=refIndex, m=marshalIndex, ci=isCheckedIn, ct=checkInTime,
 *               cla=checkInLatitude, clo=checkInLongitude, cm=checkInMethodIndex, cb=checkedInByIndex
 *   Location: r=refIndex, n=name, de=description, lat=latitude, lng=longitude,
 *             rm=requiredMarshals, cc=checkedInCount, a=assignments, w=what3Words,
 *             st=startTime, et=endTime, ai=areaRefIndexes, dy=isDynamic,
 *             lus=locationUpdateScopes, llu=lastLocationUpdate
 *             + style properties (sty, sc, sbs, sbc, sboc, sic, ssz, smr)
 *             + resolved styles (rsty, rsc, rsbs, rsbc, rsboc, rsic, rssz, rsmr)
 *             + terminology (pt, cpt)
 *
 * @param {Object} response - The normalized event status response
 * @returns {Object} - Event status in original format with all fields expanded
 */
export function denormalizeEventStatus(response) {
  // If response has the field map (_), it's normalized and needs expansion
  // Otherwise it's already in final form (debug mode or legacy)
  if (!response || !response._) {
    return response;
  }

  const {
    _d: defaults = {},
    _ds: stringDefaults = {},
    g: refs = [],
    m: marshals = [],
    cm: checkInMethods = [],
    sc: scopes = [],
    l: locations = [],
  } = response;

  // Helper to resolve a ref index to an actual GUID
  const resolveRef = (refIndex) => {
    if (refIndex == null || refIndex < 0 || refIndex >= refs.length) return null;
    return refs[refIndex];
  };

  // Helper to get marshal info
  const getMarshal = (marshalIndex) => {
    if (marshalIndex == null || marshalIndex < 0 || marshalIndex >= marshals.length) {
      return { id: null, name: null };
    }
    const marshal = marshals[marshalIndex];
    return {
      id: resolveRef(marshal.r),
      name: marshal.n || '',
    };
  };

  // Helper to apply string defaults (null/undefined means use default)
  const applyStringDefault = (value, key) => value ?? stringDefaults[key] ?? '';

  // Expand locations
  const expandedLocations = locations.map(loc => {
    // Expand assignments
    const expandedAssignments = (loc.a || []).map(assignment => {
      const marshal = getMarshal(assignment.m);
      const checkedInByMarshal = getMarshal(assignment.cb);

      // Apply defaults for isCheckedIn
      const isCheckedIn = assignment.ci ?? defaults['a.ci'] ?? false;

      return {
        id: resolveRef(assignment.r),
        eventId: null, // Not needed, will be set from context if required
        locationId: resolveRef(loc.r),
        marshalId: marshal.id,
        marshalName: marshal.name,
        isCheckedIn,
        checkInTime: assignment.ct || null,
        checkInLatitude: assignment.cla ?? null,
        checkInLongitude: assignment.clo ?? null,
        checkInMethod: assignment.cm != null ? checkInMethods[assignment.cm] || '' : '',
        checkedInBy: checkedInByMarshal.name || '',
      };
    });

    // Expand area IDs
    const areaIds = (loc.ai || [])
      .map(refIdx => resolveRef(refIdx))
      .filter(id => id != null);

    // Expand location update scope configurations
    const locationUpdateScopeConfigurations = (loc.lus || []).map(scope => ({
      scope: scope.s != null && scope.s >= 0 ? scopes[scope.s] || '' : '',
      itemType: scope.t || null,
      ids: (scope.i || []).map(refIdx => resolveRef(refIdx)).filter(id => id != null),
    }));

    // Apply defaults for isDynamic
    const isDynamic = loc.dy ?? defaults['l.dy'] ?? false;

    return {
      id: resolveRef(loc.r),
      name: loc.n || '',
      description: loc.de || '',
      latitude: loc.lat,
      longitude: loc.lng,
      requiredMarshals: loc.rm || 0,
      checkedInCount: loc.cc || 0,
      assignments: expandedAssignments,
      what3Words: loc.w || '',
      startTime: loc.st || null,
      endTime: loc.et || null,
      areaIds,
      // Raw style properties (apply string defaults)
      styleType: applyStringDefault(loc.sty, 'l.sty'),
      styleColor: applyStringDefault(loc.sc, 'l.sc'),
      styleBackgroundShape: applyStringDefault(loc.sbs, 'l.sbs'),
      styleBackgroundColor: applyStringDefault(loc.sbc, 'l.sbc'),
      styleBorderColor: applyStringDefault(loc.sboc, 'l.sboc'),
      styleIconColor: applyStringDefault(loc.sic, 'l.sic'),
      styleSize: applyStringDefault(loc.ssz, 'l.ssz'),
      styleMapRotation: applyStringDefault(loc.smr, 'l.smr'),
      // Resolved style properties (apply string defaults)
      resolvedStyleType: applyStringDefault(loc.rsty, 'l.rsty'),
      resolvedStyleColor: applyStringDefault(loc.rsc, 'l.rsc'),
      resolvedStyleBackgroundShape: applyStringDefault(loc.rsbs, 'l.rsbs'),
      resolvedStyleBackgroundColor: applyStringDefault(loc.rsbc, 'l.rsbc'),
      resolvedStyleBorderColor: applyStringDefault(loc.rsboc, 'l.rsboc'),
      resolvedStyleIconColor: applyStringDefault(loc.rsic, 'l.rsic'),
      resolvedStyleSize: applyStringDefault(loc.rssz, 'l.rssz'),
      resolvedStyleMapRotation: applyStringDefault(loc.rsmr, 'l.rsmr'),
      // Terminology
      peopleTerm: loc.pt || '',
      checkpointTerm: loc.cpt || '',
      // Dynamic checkpoint settings
      isDynamic,
      locationUpdateScopeConfigurations,
      lastLocationUpdate: loc.llu || null,
    };
  });

  return {
    eventId: null, // Will be set from context if needed
    locations: expandedLocations,
  };
}
