using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for building normalized incidents responses.
/// </summary>
public static class IncidentsHelper
{
    /// <summary>
    /// Field mapping for debugging - maps short field names to full names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMap = new()
    {
        // Response level
        ["_d"] = "defaults (bool defaults for omitted properties)",
        ["g"] = "refs (GUIDs)",
        ["sv"] = "severities",
        ["st"] = "statuses",
        ["cm"] = "checkInMethods",
        ["p"] = "persons",
        ["i"] = "incidents",
        // Person reference
        ["p.r"] = "refIndex (personId)",
        ["p.n"] = "name",
        ["p.m"] = "marshalRefIndex",
        // Compact incident
        ["i.r"] = "refIndex (incidentId)",
        ["i.ti"] = "title",
        ["i.de"] = "description",
        ["i.sv"] = "severityIndex",
        ["i.it"] = "incidentTime",
        ["i.ca"] = "createdAt",
        ["i.lat"] = "latitude",
        ["i.lng"] = "longitude",
        ["i.st"] = "statusIndex",
        ["i.rb"] = "reportedByPersonIndex",
        ["i.ar"] = "areaRefIndex",
        ["i.an"] = "areaName",
        ["i.ctx"] = "context",
        ["i.up"] = "updates",
        // Context
        ["ctx.cp"] = "checkpoint",
        ["ctx.ms"] = "marshalsPresentAtCheckpoint",
        // Checkpoint
        ["cp.r"] = "refIndex (checkpointId)",
        ["cp.n"] = "name",
        ["cp.de"] = "description",
        ["cp.lat"] = "latitude",
        ["cp.lng"] = "longitude",
        ["cp.ai"] = "areaRefIndexes",
        ["cp.an"] = "areaNames",
        // Marshal
        ["m.r"] = "refIndex (marshalId)",
        ["m.n"] = "name",
        ["m.ci"] = "wasCheckedIn",
        ["m.cit"] = "checkInTime",
        ["m.cmi"] = "checkInMethodIndex",
        // Update
        ["up.r"] = "refIndex (updateId)",
        ["up.ts"] = "timestamp",
        ["up.ap"] = "authorPersonIndex",
        ["up.no"] = "note",
        ["up.sc"] = "statusChangeIndex"
    };

    /// <summary>
    /// Converts an IncidentsListResponse to a normalized response with lookup tables.
    /// </summary>
#pragma warning disable MA0051 // Method is too long - builds complex normalized response
    public static NormalizedIncidentsListResponse BuildNormalizedResponse(IncidentsListResponse response)
    {
        // Build lookup tables
        List<string> refs = [];
        List<string> severities = [];
        List<string> statuses = [];
        List<string> checkInMethods = [];
        List<IncidentPersonRef> persons = [];
        List<CompactIncident> compactIncidents = [];

        // Index lookups
        Dictionary<string, int> refIndex = [];
        Dictionary<string, int> severityIndex = [];
        Dictionary<string, int> statusIndex = [];
        Dictionary<string, int> checkInMethodIndex = [];
        Dictionary<string, int> personIndex = [];  // keyed by personId

        // Count booleans for defaults
        int wasCheckedInTrue = 0;
        int wasCheckedInTotal = 0;

        // First pass: count booleans to determine defaults
        foreach (IncidentResponse incident in response.Incidents)
        {
            if (incident.Context?.MarshalsPresentAtCheckpoint != null)
            {
                foreach (IncidentMarshalInfo marshal in incident.Context.MarshalsPresentAtCheckpoint)
                {
                    wasCheckedInTotal++;
                    if (marshal.WasCheckedIn) wasCheckedInTrue++;
                }
            }
        }

        // Compute bool defaults (majority value)
        bool defaultWasCheckedIn = wasCheckedInTotal > 0 && wasCheckedInTrue >= wasCheckedInTotal - wasCheckedInTrue;

        Dictionary<string, bool> defaults = new()
        {
            ["m.ci"] = defaultWasCheckedIn
        };

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

        int GetOrAddSeverity(string? severity)
        {
            if (string.IsNullOrEmpty(severity)) return -1;
            if (!severityIndex.TryGetValue(severity, out int index))
            {
                index = severities.Count;
                severityIndex[severity] = index;
                severities.Add(severity);
            }
            return index;
        }

        int GetOrAddStatus(string? status)
        {
            if (string.IsNullOrEmpty(status)) return -1;
            if (!statusIndex.TryGetValue(status, out int index))
            {
                index = statuses.Count;
                statusIndex[status] = index;
                statuses.Add(status);
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

        int GetOrAddPerson(string personId, string name, string? marshalId)
        {
            if (!personIndex.TryGetValue(personId, out int index))
            {
                index = persons.Count;
                personIndex[personId] = index;
                int? marshalRefIdx = string.IsNullOrEmpty(marshalId) ? null : GetOrAddRef(marshalId);
                int personRefIdx = GetOrAddRef(personId);
                persons.Add(new IncidentPersonRef(personRefIdx, name, marshalRefIdx));
            }
            return index;
        }

        // Build compact incidents
        foreach (IncidentResponse incident in response.Incidents)
        {
            int incidentRefIdx = GetOrAddRef(incident.IncidentId);
            int severityIdx = GetOrAddSeverity(incident.Severity);
            int statusIdx = GetOrAddStatus(incident.Status);
            int reportedByIdx = GetOrAddPerson(incident.ReportedBy.PersonId, incident.ReportedBy.Name, incident.ReportedBy.MarshalId);

            // Area
            int? areaRefIdx = incident.Area != null ? GetOrAddRef(incident.Area.AreaId) : null;
            string? areaName = incident.Area?.AreaName;

            // Context
            CompactIncidentContext? compactContext = null;
            if (incident.Context != null)
            {
                CompactIncidentCheckpoint? compactCheckpoint = null;
                if (incident.Context.Checkpoint != null)
                {
                    IncidentCheckpointInfo cp = incident.Context.Checkpoint;
                    int cpRefIdx = GetOrAddRef(cp.CheckpointId);
                    List<int>? areaRefIndexes = cp.AreaIds.Count > 0
                        ? cp.AreaIds.Select(areaId => GetOrAddRef(areaId)).Where(idx => idx >= 0).ToList()
                        : null;
                    if (areaRefIndexes?.Count == 0) areaRefIndexes = null;
                    List<string>? areaNames = cp.AreaNames.Count > 0 ? cp.AreaNames : null;

                    compactCheckpoint = new CompactIncidentCheckpoint(
                        cpRefIdx,
                        cp.Name,
                        string.IsNullOrEmpty(cp.Description) ? null : cp.Description,
                        cp.Latitude,
                        cp.Longitude,
                        areaRefIndexes,
                        areaNames
                    );
                }

                List<CompactIncidentMarshal>? compactMarshals = null;
                if (incident.Context.MarshalsPresentAtCheckpoint.Count > 0)
                {
                    compactMarshals = [];
                    foreach (IncidentMarshalInfo marshal in incident.Context.MarshalsPresentAtCheckpoint)
                    {
                        int marshalRefIdx = GetOrAddRef(marshal.MarshalId);
                        int? checkInMethodIdx = GetOrAddCheckInMethod(marshal.CheckInMethod) is int cmIdx && cmIdx >= 0 ? cmIdx : null;

                        // Apply default for wasCheckedIn
                        bool? wasCheckedIn = marshal.WasCheckedIn == defaultWasCheckedIn ? null : marshal.WasCheckedIn;

                        compactMarshals.Add(new CompactIncidentMarshal(
                            marshalRefIdx,
                            marshal.Name,
                            wasCheckedIn,
                            marshal.CheckInTime,
                            checkInMethodIdx
                        ));
                    }
                }

                if (compactCheckpoint != null || compactMarshals != null)
                {
                    compactContext = new CompactIncidentContext(compactCheckpoint, compactMarshals);
                }
            }

            // Updates
            List<CompactIncidentUpdate>? compactUpdates = null;
            if (incident.Updates.Count > 0)
            {
                compactUpdates = [];
                foreach (IncidentUpdateInfo update in incident.Updates)
                {
                    int updateRefIdx = GetOrAddRef(update.UpdateId);
                    int authorPersonIdx = GetOrAddPerson(update.AuthorPersonId, update.AuthorName, null);
                    int? statusChangeIdx = string.IsNullOrEmpty(update.StatusChange) ? null : GetOrAddStatus(update.StatusChange);

                    compactUpdates.Add(new CompactIncidentUpdate(
                        updateRefIdx,
                        update.Timestamp,
                        authorPersonIdx,
                        update.Note,
                        statusChangeIdx
                    ));
                }
            }

            compactIncidents.Add(new CompactIncident(
                incidentRefIdx,
                incident.Title,
                string.IsNullOrEmpty(incident.Description) ? null : incident.Description,
                severityIdx,
                incident.IncidentTime,
                incident.CreatedAt,
                incident.Latitude,
                incident.Longitude,
                statusIdx,
                reportedByIdx,
                areaRefIdx,
                areaName,
                compactContext,
                compactUpdates
            ));
        }

        return new NormalizedIncidentsListResponse(
            FieldMap,
            defaults,
            refs,
            severities,
            statuses,
            checkInMethods,
            persons,
            compactIncidents
        );
    }
#pragma warning restore MA0051

    /// <summary>
    /// Returns true if the X-Debug header is present.
    /// </summary>
    public static bool IsDebugRequest(Microsoft.AspNetCore.Http.HttpRequest req) =>
        req.Headers.ContainsKey("X-Debug");
}
