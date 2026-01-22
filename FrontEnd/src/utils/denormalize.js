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

/**
 * Denormalizes a normalized area lead dashboard response back to the original structure.
 *
 * Short field names (see response._ for mapping):
 *   Response: _d=defaults (bool), g=refs, sc=scopes, ct=contextTypes, cm=checkInMethods,
 *             td=taskDefinitions, ar=areas, cp=checkpoints
 *   TaskDefinition: r=refIndex (itemId), t=text
 *   CompactTask: ti=taskDefIndex, si=scopeIndex, cti=contextTypeIndex, ci=contextRefIndex, mi=marshalRefIndex
 *   Area: r=refIndex (areaId), n=name, c=color
 *   Checkpoint: r=refIndex, n=name, de=description, lat=latitude, lng=longitude,
 *               ani=areaNameIndex, ai=areaRefIndexes, ms=marshals, otc=outstandingTaskCount, ot=outstandingTasks
 *   Marshal: r=refIndex, ar=assignmentRefIndex, n=name, e=email, p=phoneNumber,
 *            ci=isCheckedIn, cit=checkInTime, cmi=checkInMethodIndex, la=lastAccessedAt,
 *            otc=outstandingTaskCount, ot=outstandingTasks
 *
 * @param {Object} response - The normalized area lead dashboard response
 * @returns {Object} - Dashboard in original format with all fields expanded
 */
export function denormalizeAreaLeadDashboard(response) {
  // If response has the field map (_), it's normalized and needs expansion
  // Otherwise it's already in final form (debug mode or legacy)
  if (!response || !response._) {
    return response;
  }

  const {
    _d: defaults = {},
    g: refs = [],
    sc: scopes = [],
    ct: contextTypes = [],
    cm: checkInMethods = [],
    td: taskDefinitions = [],
    ar: areas = [],
    cp: checkpoints = [],
  } = response;

  // Helper to resolve a ref index to an actual GUID
  const resolveRef = (refIndex) => {
    if (refIndex == null || refIndex < 0 || refIndex >= refs.length) return null;
    return refs[refIndex];
  };

  // Helper to expand a task
  const expandTask = (task) => {
    const taskDef = taskDefinitions[task.ti] || {};
    return {
      itemId: resolveRef(taskDef.r),
      text: taskDef.t || '',
      scope: task.si != null && task.si >= 0 ? scopes[task.si] || '' : '',
      contextType: task.cti != null && task.cti >= 0 ? contextTypes[task.cti] || '' : '',
      contextId: resolveRef(task.ci),
      marshalId: task.mi != null ? resolveRef(task.mi) : null,
    };
  };

  // Expand areas
  const expandedAreas = areas.map(area => ({
    areaId: resolveRef(area.r),
    name: area.n || '',
    color: area.c || '',
  }));

  // Expand checkpoints
  const expandedCheckpoints = checkpoints.map(cp => {
    // Get area name from areas array if index provided
    const areaName = cp.ani != null && cp.ani >= 0 && cp.ani < expandedAreas.length
      ? expandedAreas[cp.ani].name
      : null;

    // Expand area IDs
    const areaIds = (cp.ai || [])
      .map(refIdx => resolveRef(refIdx))
      .filter(id => id != null);

    // Expand checkpoint outstanding tasks
    const outstandingTasks = (cp.ot || []).map(expandTask);

    // Expand marshals
    const marshals = (cp.ms || []).map(marshal => {
      // Apply default for isCheckedIn
      const isCheckedIn = marshal.ci ?? defaults['m.ci'] ?? false;

      // Expand marshal outstanding tasks
      const marshalTasks = (marshal.ot || []).map(expandTask);

      return {
        marshalId: resolveRef(marshal.r),
        assignmentId: resolveRef(marshal.ar),
        name: marshal.n || '',
        email: marshal.e || null,
        phoneNumber: marshal.p || null,
        isCheckedIn,
        checkInTime: marshal.cit || null,
        checkInMethod: marshal.cmi != null && marshal.cmi >= 0 ? checkInMethods[marshal.cmi] || '' : null,
        lastAccessedAt: marshal.la || null,
        outstandingTaskCount: marshal.otc || 0,
        outstandingTasks: marshalTasks,
      };
    });

    return {
      checkpointId: resolveRef(cp.r),
      name: cp.n || '',
      description: cp.de || '',
      latitude: cp.lat,
      longitude: cp.lng,
      areaName,
      areaIds,
      marshals,
      outstandingTaskCount: cp.otc || 0,
      outstandingTasks,
    };
  });

  return {
    areas: expandedAreas,
    checkpoints: expandedCheckpoints,
  };
}

/**
 * Denormalizes a normalized incidents list response back to the original structure.
 *
 * Short field names (see response._ for mapping):
 *   Response: _d=defaults, g=refs, sv=severities, st=statuses, cm=checkInMethods, p=persons, i=incidents
 *   Person: r=refIndex, n=name, m=marshalRefIndex
 *   Incident: r=refIndex, ti=title, de=description, sv=severityIndex, it=incidentTime, ca=createdAt,
 *             lat=latitude, lng=longitude, st=statusIndex, rb=reportedByPersonIndex,
 *             ar=areaRefIndex, an=areaName, ctx=context, up=updates
 *   Context: cp=checkpoint, ms=marshalsPresentAtCheckpoint
 *   Checkpoint: r=refIndex, n=name, de=description, lat=latitude, lng=longitude, ai=areaRefIndexes, an=areaNames
 *   Marshal: r=refIndex, n=name, ci=wasCheckedIn, cit=checkInTime, cmi=checkInMethodIndex
 *   Update: r=refIndex, ts=timestamp, ap=authorPersonIndex, no=note, sc=statusChangeIndex
 *
 * @param {Object} response - The normalized incidents list response
 * @returns {Object} - Incidents list in original format with all fields expanded
 */
export function denormalizeIncidentsList(response) {
  // If response has the field map (_), it's normalized and needs expansion
  // Otherwise it's already in final form (debug mode or legacy)
  if (!response || !response._) {
    return response;
  }

  const {
    _d: defaults = {},
    g: refs = [],
    sv: severities = [],
    st: statuses = [],
    cm: checkInMethods = [],
    p: persons = [],
    i: incidents = [],
  } = response;

  // Helper to resolve a ref index to an actual GUID
  const resolveRef = (refIndex) => {
    if (refIndex == null || refIndex < 0 || refIndex >= refs.length) return null;
    return refs[refIndex];
  };

  // Helper to get person info
  const getPerson = (personIndex) => {
    if (personIndex == null || personIndex < 0 || personIndex >= persons.length) {
      return { personId: null, name: '', marshalId: null };
    }
    const person = persons[personIndex];
    return {
      personId: resolveRef(person.r),
      name: person.n || '',
      marshalId: person.m != null ? resolveRef(person.m) : null,
    };
  };

  // Expand incidents
  const expandedIncidents = incidents.map(inc => {
    const reportedBy = getPerson(inc.rb);

    // Expand context
    let context = { checkpoint: null, marshalsPresentAtCheckpoint: [] };
    if (inc.ctx) {
      // Expand checkpoint
      let checkpoint = null;
      if (inc.ctx.cp) {
        const cp = inc.ctx.cp;
        checkpoint = {
          checkpointId: resolveRef(cp.r),
          name: cp.n || '',
          description: cp.de || '',
          latitude: cp.lat,
          longitude: cp.lng,
          areaIds: (cp.ai || []).map(refIdx => resolveRef(refIdx)).filter(id => id != null),
          areaNames: cp.an || [],
        };
      }

      // Expand marshals present at checkpoint
      const marshalsPresentAtCheckpoint = (inc.ctx.ms || []).map(marshal => ({
        marshalId: resolveRef(marshal.r),
        name: marshal.n || '',
        wasCheckedIn: marshal.ci ?? defaults['m.ci'] ?? false,
        checkInTime: marshal.cit || null,
        checkInMethod: marshal.cmi != null && marshal.cmi >= 0 ? checkInMethods[marshal.cmi] || '' : null,
      }));

      context = { checkpoint, marshalsPresentAtCheckpoint };
    }

    // Expand updates
    const updates = (inc.up || []).map(update => {
      const author = getPerson(update.ap);
      return {
        updateId: resolveRef(update.r),
        timestamp: update.ts,
        authorPersonId: author.personId,
        authorName: author.name,
        note: update.no || '',
        statusChange: update.sc != null && update.sc >= 0 ? statuses[update.sc] || null : null,
      };
    });

    return {
      incidentId: resolveRef(inc.r),
      eventId: null, // Will be set from context if needed
      title: inc.ti || '',
      description: inc.de || '',
      severity: inc.sv != null && inc.sv >= 0 ? severities[inc.sv] || '' : '',
      incidentTime: inc.it,
      createdAt: inc.ca,
      latitude: inc.lat ?? null,
      longitude: inc.lng ?? null,
      status: inc.st != null && inc.st >= 0 ? statuses[inc.st] || '' : '',
      reportedBy: {
        personId: reportedBy.personId,
        name: reportedBy.name,
        marshalId: reportedBy.marshalId,
      },
      area: inc.ar != null ? {
        areaId: resolveRef(inc.ar),
        areaName: inc.an || '',
      } : null,
      context,
      updates,
    };
  });

  return {
    incidents: expandedIncidents,
  };
}

/**
 * Denormalizes a normalized checklist response back to the original flat array structure.
 * This allows existing frontend logic to work unchanged while benefiting
 * from reduced network payload size.
 *
 * Short field names (see response._ for mapping):
 *   Response: _d=defaults (bool), g=refs, s=scopes, at=actorTypes, ct=contextTypes,
 *             m=marshals, c=contexts, d=items (definitions), n=instances
 *   Marshal: r=refIndex, n=name
 *   Context: r=refIndex, t=typeIndex
 *   ScopeConfiguration: s=scopeIndex, t=itemType, i=refIndexes
 *   ItemDefinition: r=itemRefIndex, t=text, sc=scopeConfigurations, o=displayOrder,
 *                   r2=isRequired, vf=visibleFrom, vu=visibleUntil, mb=mustCompleteBy,
 *                   l=linksToCheckIn, lc=linkedCheckpointRefIndex, ln=linkedCheckpointName
 *   Instance: i=itemIndex, c=isCompleted, m=canBeCompletedByMe, a=actorIndex,
 *             at=actorTypeIndex, ca=completedAt, x=contextIndex, s=scopeIndex, o=ownerIndex
 *
 * @param {Object} response - The normalized checklist response
 * @returns {Array} - Flat array of checklist items with all fields expanded
 */
export function denormalizeChecklist(response) {
  // If response is an array, it's already in flat format (debug mode or legacy)
  if (Array.isArray(response)) {
    return response;
  }

  // If response doesn't have the field map (_), it's not normalized
  if (!response || !response._) {
    return response;
  }

  const {
    _d: defaults = {},
    g: refs = [],
    s: scopes = [],
    at: actorTypes = [],
    ct: contextTypes = [],
    m: marshals = [],
    c: contexts = [],
    d: items = [],
    n: instances = [],
  } = response;

  // Helper to resolve a ref index to an actual GUID
  const resolveRef = (refIndex) => {
    if (refIndex == null || refIndex < 0 || refIndex >= refs.length) return null;
    return refs[refIndex];
  };

  // Helper to get marshal info by index
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

  // Helper to get context info by index
  const getContext = (contextIndex) => {
    if (contextIndex == null || contextIndex < 0 || contextIndex >= contexts.length) {
      return { type: '', id: null };
    }
    const ctx = contexts[contextIndex];
    return {
      type: ctx.t != null && ctx.t >= 0 ? contextTypes[ctx.t] || '' : '',
      id: resolveRef(ctx.r),
    };
  };

  // Expand each instance back to full ChecklistItemWithStatus
  return instances.map(instance => {
    const itemDef = items[instance.i] || {};

    // Expand scope configurations
    const scopeConfigurations = (itemDef.sc || []).map(sc => ({
      scope: sc.s != null && sc.s >= 0 ? scopes[sc.s] || '' : '',
      itemType: sc.t || null,
      ids: (sc.i || []).map(refIdx => resolveRef(refIdx)).filter(id => id != null),
    }));

    // Get context info
    const ctx = getContext(instance.x);

    // Get actor info (who completed it)
    const actor = getMarshal(instance.a);
    const actorType = instance.at != null && instance.at >= 0 ? actorTypes[instance.at] || null : null;

    // Get owner info (whose task it is)
    const owner = getMarshal(instance.o);

    // Apply defaults for boolean properties
    const isCompleted = instance.c ?? defaults['n.c'] ?? false;
    const canBeCompletedByMe = instance.m ?? defaults['n.m'] ?? false;
    const isRequired = itemDef.r2 ?? defaults['d.r2'] ?? false;
    const linksToCheckIn = itemDef.l ?? defaults['d.l'] ?? false;

    return {
      itemId: resolveRef(itemDef.r),
      eventId: null, // Not included in normalized response, will be set from context if needed
      text: itemDef.t || '',
      scopeConfigurations,
      displayOrder: itemDef.o || 0,
      isRequired,
      visibleFrom: itemDef.vf || null,
      visibleUntil: itemDef.vu || null,
      mustCompleteBy: itemDef.mb || null,
      isCompleted,
      canBeCompletedByMe,
      completedByActorName: actor.name || null,
      completedByActorType: actorType,
      completedByActorId: actor.id,
      completedAt: instance.ca || null,
      completionContextType: ctx.type,
      completionContextId: ctx.id,
      matchedScope: instance.s != null && instance.s >= 0 ? scopes[instance.s] || '' : '',
      contextOwnerName: owner.name || null,
      contextOwnerMarshalId: owner.id,
      linksToCheckIn,
      linkedCheckpointId: resolveRef(itemDef.lc),
      linkedCheckpointName: itemDef.ln || null,
    };
  });
}
