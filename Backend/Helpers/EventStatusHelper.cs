using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for building normalized event status responses.
/// </summary>
public static class EventStatusHelper
{
    /// <summary>
    /// Field mapping for debugging - maps short field names to full names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMap = new()
    {
        // Response level
        ["_d"] = "defaults (bool defaults for omitted properties)",
        ["_ds"] = "stringDefaults (string defaults for omitted style properties)",
        ["g"] = "refs (GUIDs)",
        ["m"] = "marshals",
        ["cm"] = "checkInMethods",
        ["sc"] = "scopes",
        ["l"] = "locations",
        // Marshal reference
        ["m.r"] = "refIndex",
        ["m.n"] = "name",
        // Assignment
        ["a.r"] = "refIndex (assignment ID)",
        ["a.m"] = "marshalIndex",
        ["a.ci"] = "isCheckedIn",
        ["a.ct"] = "checkInTime",
        ["a.cla"] = "checkInLatitude",
        ["a.clo"] = "checkInLongitude",
        ["a.cm"] = "checkInMethodIndex",
        ["a.cb"] = "checkedInByIndex (marshal)",
        // Location
        ["l.r"] = "refIndex (location ID)",
        ["l.n"] = "name",
        ["l.de"] = "description",
        ["l.lat"] = "latitude",
        ["l.lng"] = "longitude",
        ["l.rm"] = "requiredMarshals",
        ["l.cc"] = "checkedInCount",
        ["l.a"] = "assignments",
        ["l.w"] = "what3Words",
        ["l.st"] = "startTime",
        ["l.et"] = "endTime",
        ["l.ai"] = "areaRefIndexes",
        // Raw styles
        ["l.sty"] = "styleType",
        ["l.sc"] = "styleColor",
        ["l.sbs"] = "styleBackgroundShape",
        ["l.sbc"] = "styleBackgroundColor",
        ["l.sboc"] = "styleBorderColor",
        ["l.sic"] = "styleIconColor",
        ["l.ssz"] = "styleSize",
        ["l.smr"] = "styleMapRotation",
        // Resolved styles
        ["l.rsty"] = "resolvedStyleType",
        ["l.rsc"] = "resolvedStyleColor",
        ["l.rsbs"] = "resolvedStyleBackgroundShape",
        ["l.rsbc"] = "resolvedStyleBackgroundColor",
        ["l.rsboc"] = "resolvedStyleBorderColor",
        ["l.rsic"] = "resolvedStyleIconColor",
        ["l.rssz"] = "resolvedStyleSize",
        ["l.rsmr"] = "resolvedStyleMapRotation",
        // Other location fields
        ["l.pt"] = "peopleTerm",
        ["l.cpt"] = "checkpointTerm",
        ["l.dy"] = "isDynamic",
        ["l.lus"] = "locationUpdateScopes",
        ["l.llu"] = "lastLocationUpdate",
        // Location update scope
        ["lus.s"] = "scopeIndex",
        ["lus.t"] = "itemType",
        ["lus.i"] = "refIndexes"
    };

    /// <summary>
    /// Converts an EventStatusResponse to a normalized response with lookup tables.
    /// </summary>
#pragma warning disable MA0051 // Method is too long - builds complex normalized response
    public static NormalizedEventStatusResponse BuildNormalizedResponse(EventStatusResponse response)
    {
        // Build lookup tables
        List<string> refs = [];
        List<MarshalReference> marshals = [];
        List<string> checkInMethods = [];
        List<string> scopes = [];
        List<CompactLocation> compactLocations = [];

        // Index lookups
        Dictionary<string, int> refIndex = [];
        Dictionary<string, int> marshalIndex = [];  // keyed by marshalId
        Dictionary<string, int> checkInMethodIndex = [];
        Dictionary<string, int> scopeIndex = [];

        // Count booleans for defaults
        int isCheckedInTrue = 0;
        int isCheckedInTotal = 0;
        int isDynamicTrue = 0;
        int isDynamicTotal = 0;

        // Count style values for string defaults
        Dictionary<string, Dictionary<string, int>> styleValueCounts = new()
        {
            ["l.sty"] = [],
            ["l.sc"] = [],
            ["l.sbs"] = [],
            ["l.sbc"] = [],
            ["l.sboc"] = [],
            ["l.sic"] = [],
            ["l.ssz"] = [],
            ["l.smr"] = [],
            ["l.rsty"] = [],
            ["l.rsc"] = [],
            ["l.rsbs"] = [],
            ["l.rsbc"] = [],
            ["l.rsboc"] = [],
            ["l.rsic"] = [],
            ["l.rssz"] = [],
            ["l.rsmr"] = []
        };

        void CountStyleValue(string key, string? value)
        {
            string v = value ?? "";
            if (!styleValueCounts[key].TryGetValue(v, out int count))
            {
                styleValueCounts[key][v] = 1;
            }
            else
            {
                styleValueCounts[key][v] = count + 1;
            }
        }

        // First pass: count booleans and style values
        foreach (LocationStatusResponse location in response.Locations)
        {
            isDynamicTotal++;
            if (location.IsDynamic) isDynamicTrue++;

            // Count style values
            CountStyleValue("l.sty", location.StyleType);
            CountStyleValue("l.sc", location.StyleColor);
            CountStyleValue("l.sbs", location.StyleBackgroundShape);
            CountStyleValue("l.sbc", location.StyleBackgroundColor);
            CountStyleValue("l.sboc", location.StyleBorderColor);
            CountStyleValue("l.sic", location.StyleIconColor);
            CountStyleValue("l.ssz", location.StyleSize);
            CountStyleValue("l.smr", location.StyleMapRotation);
            CountStyleValue("l.rsty", location.ResolvedStyleType);
            CountStyleValue("l.rsc", location.ResolvedStyleColor);
            CountStyleValue("l.rsbs", location.ResolvedStyleBackgroundShape);
            CountStyleValue("l.rsbc", location.ResolvedStyleBackgroundColor);
            CountStyleValue("l.rsboc", location.ResolvedStyleBorderColor);
            CountStyleValue("l.rsic", location.ResolvedStyleIconColor);
            CountStyleValue("l.rssz", location.ResolvedStyleSize);
            CountStyleValue("l.rsmr", location.ResolvedStyleMapRotation);

            foreach (AssignmentResponse assignment in location.Assignments)
            {
                isCheckedInTotal++;
                if (assignment.IsCheckedIn) isCheckedInTrue++;
            }
        }

        // Compute bool defaults (majority value)
        bool defaultIsCheckedIn = isCheckedInTotal > 0 && isCheckedInTrue >= isCheckedInTotal - isCheckedInTrue;
        bool defaultIsDynamic = isDynamicTotal > 0 && isDynamicTrue >= isDynamicTotal - isDynamicTrue;

        Dictionary<string, bool> defaults = new()
        {
            ["a.ci"] = defaultIsCheckedIn,
            ["l.dy"] = defaultIsDynamic
        };

        // Compute string defaults (most common value for each style property)
        Dictionary<string, string> stringDefaults = [];
        foreach (KeyValuePair<string, Dictionary<string, int>> kvp in styleValueCounts)
        {
            if (kvp.Value.Count > 0)
            {
                string mostCommon = kvp.Value.OrderByDescending(v => v.Value).First().Key;
                // Only add to defaults if it will save bytes (appears more than once)
                int mostCommonCount = kvp.Value[mostCommon];
                if (mostCommonCount > 1)
                {
                    stringDefaults[kvp.Key] = mostCommon;
                }
            }
        }

        int GetOrAddRef(string? guid)
        {
            if (string.IsNullOrEmpty(guid)) return -1;
            if (!refIndex.TryGetValue(guid, out int index))
            {
                index = refs.Count;
                refIndex[guid] = index;
                refs.Add(guid);
            }
            return index;
        }

        int GetOrAddMarshal(string? marshalId, string? marshalName)
        {
            if (string.IsNullOrEmpty(marshalId)) return -1;
            if (!marshalIndex.TryGetValue(marshalId, out int index))
            {
                index = marshals.Count;
                marshalIndex[marshalId] = index;
                int rIdx = GetOrAddRef(marshalId);
                marshals.Add(new MarshalReference(rIdx, marshalName ?? ""));
            }
            return index;
        }

        int GetOrAddCheckInMethod(string? method)
        {
            if (string.IsNullOrEmpty(method)) return -1;
            if (!checkInMethodIndex.TryGetValue(method, out int index))
            {
                index = checkInMethods.Count;
                checkInMethodIndex[method] = index;
                checkInMethods.Add(method);
            }
            return index;
        }

        int GetOrAddScope(string? scope)
        {
            if (string.IsNullOrEmpty(scope)) return -1;
            if (!scopeIndex.TryGetValue(scope, out int index))
            {
                index = scopes.Count;
                scopeIndex[scope] = index;
                scopes.Add(scope);
            }
            return index;
        }

        // Second pass: build compact locations
        foreach (LocationStatusResponse location in response.Locations)
        {
            int locationRefIdx = GetOrAddRef(location.Id);

            // Convert area IDs to ref indexes
            List<int> areaRefIndexes = location.AreaIds
                .Select(areaId => GetOrAddRef(areaId))
                .Where(idx => idx >= 0)
                .ToList();

            // Convert assignments
            List<CompactAssignment> compactAssignments = [];
            foreach (AssignmentResponse assignment in location.Assignments)
            {
                int assignmentRefIdx = GetOrAddRef(assignment.Id);
                int marshalIdx = GetOrAddMarshal(assignment.MarshalId, assignment.MarshalName);
                int? checkInMethodIdx = string.IsNullOrEmpty(assignment.CheckInMethod)
                    ? null
                    : GetOrAddCheckInMethod(assignment.CheckInMethod);

                // For CheckedInBy, we need to look up by name since we don't have the ID
                // The CheckedInBy field contains a name, not an ID, so we'll store it differently
                int? checkedInByIdx = null;
                if (!string.IsNullOrEmpty(assignment.CheckedInBy))
                {
                    // Try to find existing marshal by name, otherwise add as new
                    KeyValuePair<string, int>? existingMarshal = marshalIndex
                        .FirstOrDefault(kvp => marshals[kvp.Value].Name == assignment.CheckedInBy);
                    if (existingMarshal.HasValue && existingMarshal.Value.Key != null)
                    {
                        checkedInByIdx = existingMarshal.Value.Value;
                    }
                    // If not found, we can't create a proper marshal entry without ID, so leave as null
                }

                // Apply default for isCheckedIn
                bool? isCheckedIn = assignment.IsCheckedIn == defaultIsCheckedIn ? null : assignment.IsCheckedIn;

                compactAssignments.Add(new CompactAssignment(
                    assignmentRefIdx,
                    marshalIdx,
                    isCheckedIn,
                    assignment.CheckInTime,
                    assignment.CheckInLatitude,
                    assignment.CheckInLongitude,
                    checkInMethodIdx,
                    checkedInByIdx
                ));
            }

            // Convert location update scope configurations
            List<CompactLocationUpdateScope>? compactScopes = null;
            if (location.LocationUpdateScopeConfigurations?.Count > 0)
            {
                compactScopes = location.LocationUpdateScopeConfigurations
                    .Select(sc => new CompactLocationUpdateScope(
                        GetOrAddScope(sc.Scope) is int idx && idx >= 0 ? idx : 0,
                        sc.ItemType,
                        sc.Ids.Select(id => GetOrAddRef(id)).Where(i => i >= 0).ToList()
                    ))
                    .ToList();
            }

            // Apply default for isDynamic
            bool? isDynamic = location.IsDynamic == defaultIsDynamic ? null : location.IsDynamic;

            // Convert empty strings to null, and apply string defaults
            // Returns null if value is empty or matches the default (will be omitted from JSON)
            string? ApplyDefault(string key, string? value)
            {
                string v = value ?? "";
                if (string.IsNullOrEmpty(v)) return null;
                if (stringDefaults.TryGetValue(key, out string? defaultValue) && v == defaultValue) return null;
                return v;
            }

            compactLocations.Add(new CompactLocation(
                locationRefIdx,
                location.Name,
                string.IsNullOrEmpty(location.Description) ? null : location.Description,
                location.Latitude,
                location.Longitude,
                location.RequiredMarshals,
                location.CheckedInCount,
                compactAssignments,
                string.IsNullOrEmpty(location.What3Words) ? null : location.What3Words,
                location.StartTime,
                location.EndTime,
                areaRefIndexes,
                // Raw styles (apply defaults)
                ApplyDefault("l.sty", location.StyleType),
                ApplyDefault("l.sc", location.StyleColor),
                ApplyDefault("l.sbs", location.StyleBackgroundShape),
                ApplyDefault("l.sbc", location.StyleBackgroundColor),
                ApplyDefault("l.sboc", location.StyleBorderColor),
                ApplyDefault("l.sic", location.StyleIconColor),
                ApplyDefault("l.ssz", location.StyleSize),
                ApplyDefault("l.smr", location.StyleMapRotation),
                // Resolved styles (apply defaults)
                ApplyDefault("l.rsty", location.ResolvedStyleType),
                ApplyDefault("l.rsc", location.ResolvedStyleColor),
                ApplyDefault("l.rsbs", location.ResolvedStyleBackgroundShape),
                ApplyDefault("l.rsbc", location.ResolvedStyleBackgroundColor),
                ApplyDefault("l.rsboc", location.ResolvedStyleBorderColor),
                ApplyDefault("l.rsic", location.ResolvedStyleIconColor),
                ApplyDefault("l.rssz", location.ResolvedStyleSize),
                ApplyDefault("l.rsmr", location.ResolvedStyleMapRotation),
                // Terminology
                string.IsNullOrEmpty(location.PeopleTerm) ? null : location.PeopleTerm,
                string.IsNullOrEmpty(location.CheckpointTerm) ? null : location.CheckpointTerm,
                // Dynamic
                isDynamic,
                compactScopes,
                location.LastLocationUpdate
            ));
        }

        return new NormalizedEventStatusResponse(
            FieldMap,
            defaults,
            stringDefaults,
            refs,
            marshals,
            checkInMethods,
            scopes,
            compactLocations
        );
    }
#pragma warning restore MA0051

    /// <summary>
    /// Returns true if the X-Debug header is present.
    /// </summary>
    public static bool IsDebugRequest(Microsoft.AspNetCore.Http.HttpRequest req) =>
        req.Headers.ContainsKey("X-Debug");
}
