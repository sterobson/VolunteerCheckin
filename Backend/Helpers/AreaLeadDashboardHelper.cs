using VolunteerCheckin.Functions.Models;

namespace VolunteerCheckin.Functions.Helpers;

/// <summary>
/// Helper class for building normalized area lead dashboard responses.
/// </summary>
public static class AreaLeadDashboardHelper
{
    /// <summary>
    /// Field mapping for debugging - maps short field names to full names.
    /// </summary>
    private static readonly Dictionary<string, string> FieldMap = new()
    {
        // Response level
        ["_d"] = "defaults (bool defaults for omitted properties)",
        ["g"] = "refs (GUIDs)",
        ["sc"] = "scopes",
        ["ct"] = "contextTypes",
        ["cm"] = "checkInMethods",
        ["td"] = "taskDefinitions",
        ["ar"] = "areas",
        ["cp"] = "checkpoints",
        // Task definition
        ["td.r"] = "refIndex (itemId)",
        ["td.t"] = "text",
        // Compact task
        ["t.ti"] = "taskDefIndex",
        ["t.si"] = "scopeIndex",
        ["t.cti"] = "contextTypeIndex",
        ["t.ci"] = "contextRefIndex",
        ["t.mi"] = "marshalRefIndex",
        // Area
        ["ar.r"] = "refIndex (areaId)",
        ["ar.n"] = "name",
        ["ar.c"] = "color",
        // Checkpoint
        ["cp.r"] = "refIndex (checkpointId)",
        ["cp.n"] = "name",
        ["cp.de"] = "description",
        ["cp.lat"] = "latitude",
        ["cp.lng"] = "longitude",
        ["cp.ani"] = "areaNameIndex (into areas)",
        ["cp.ai"] = "areaRefIndexes",
        ["cp.ms"] = "marshals",
        ["cp.otc"] = "outstandingTaskCount",
        ["cp.ot"] = "outstandingTasks",
        // Marshal
        ["m.r"] = "refIndex (marshalId)",
        ["m.ar"] = "assignmentRefIndex",
        ["m.n"] = "name",
        ["m.e"] = "email",
        ["m.p"] = "phoneNumber",
        ["m.ci"] = "isCheckedIn",
        ["m.cit"] = "checkInTime",
        ["m.cmi"] = "checkInMethodIndex",
        ["m.la"] = "lastAccessedAt",
        ["m.otc"] = "outstandingTaskCount",
        ["m.ot"] = "outstandingTasks"
    };

    /// <summary>
    /// Converts an AreaLeadDashboardResponse to a normalized response with lookup tables.
    /// </summary>
#pragma warning disable MA0051 // Method is too long - builds complex normalized response
    public static NormalizedAreaLeadDashboardResponse BuildNormalizedResponse(AreaLeadDashboardResponse response)
    {
        // Build lookup tables
        List<string> refs = [];
        List<string> scopes = [];
        List<string> contextTypes = [];
        List<string> checkInMethods = [];
        List<AreaLeadTaskDefinition> taskDefinitions = [];
        List<CompactAreaLeadArea> compactAreas = [];
        List<CompactAreaLeadCheckpoint> compactCheckpoints = [];

        // Index lookups
        Dictionary<string, int> refIndex = [];
        Dictionary<string, int> scopeIndex = [];
        Dictionary<string, int> contextTypeIndex = [];
        Dictionary<string, int> checkInMethodIndex = [];
        Dictionary<string, int> taskDefIndex = [];  // keyed by itemId

        // Count booleans for defaults
        int isCheckedInTrue = 0;
        int isCheckedInTotal = 0;

        // First pass: count booleans to determine defaults
        foreach (AreaLeadCheckpointInfo checkpoint in response.Checkpoints)
        {
            foreach (AreaLeadMarshalInfo marshal in checkpoint.Marshals)
            {
                isCheckedInTotal++;
                if (marshal.IsCheckedIn) isCheckedInTrue++;
            }
        }

        // Compute bool defaults (majority value)
        bool defaultIsCheckedIn = isCheckedInTotal > 0 && isCheckedInTrue >= isCheckedInTotal - isCheckedInTrue;

        Dictionary<string, bool> defaults = new()
        {
            ["m.ci"] = defaultIsCheckedIn
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

        int GetOrAddContextType(string? contextType)
        {
            if (string.IsNullOrEmpty(contextType)) return -1;
            if (!contextTypeIndex.TryGetValue(contextType, out int index))
            {
                index = contextTypes.Count;
                contextTypeIndex[contextType] = index;
                contextTypes.Add(contextType);
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

        int GetOrAddTaskDef(string itemId, string text)
        {
            if (!taskDefIndex.TryGetValue(itemId, out int index))
            {
                index = taskDefinitions.Count;
                taskDefIndex[itemId] = index;
                int rIdx = GetOrAddRef(itemId);
                taskDefinitions.Add(new AreaLeadTaskDefinition(rIdx, text));
            }
            return index;
        }

        CompactAreaLeadTask ConvertTask(AreaLeadTaskInfo task)
        {
            int taskIdx = GetOrAddTaskDef(task.ItemId, task.Text);
            int sIdx = GetOrAddScope(task.Scope);
            int ctIdx = GetOrAddContextType(task.ContextType);
            int ciIdx = GetOrAddRef(task.ContextId);
            int? miIdx = string.IsNullOrEmpty(task.MarshalId) ? null : GetOrAddRef(task.MarshalId);
            return new CompactAreaLeadTask(taskIdx, sIdx, ctIdx, ciIdx, miIdx);
        }

        // Build compact areas
        foreach (AreaLeadAreaInfo area in response.Areas)
        {
            int areaRefIdx = GetOrAddRef(area.AreaId);
            compactAreas.Add(new CompactAreaLeadArea(areaRefIdx, area.Name, area.Color));
        }

        // Build compact checkpoints
        foreach (AreaLeadCheckpointInfo checkpoint in response.Checkpoints)
        {
            int checkpointRefIdx = GetOrAddRef(checkpoint.CheckpointId);

            // Convert area IDs to ref indexes
            List<int>? areaRefIndexes = checkpoint.AreaIds.Count > 0
                ? checkpoint.AreaIds.Select(areaId => GetOrAddRef(areaId)).Where(idx => idx >= 0).ToList()
                : null;
            if (areaRefIndexes?.Count == 0) areaRefIndexes = null;

            // Find area name index (into compactAreas)
            int? areaNameIndex = null;
            if (!string.IsNullOrEmpty(checkpoint.AreaName))
            {
                for (int i = 0; i < compactAreas.Count; i++)
                {
                    if (compactAreas[i].Name == checkpoint.AreaName)
                    {
                        areaNameIndex = i;
                        break;
                    }
                }
            }

            // Convert checkpoint outstanding tasks
            List<CompactAreaLeadTask>? compactCheckpointTasks = checkpoint.OutstandingTasks.Count > 0
                ? checkpoint.OutstandingTasks.Select(ConvertTask).ToList()
                : null;

            // Convert marshals
            List<CompactAreaLeadMarshal>? compactMarshals = null;
            if (checkpoint.Marshals.Count > 0)
            {
                compactMarshals = [];
                foreach (AreaLeadMarshalInfo marshal in checkpoint.Marshals)
                {
                    int marshalRefIdx = GetOrAddRef(marshal.MarshalId);
                    int assignmentRefIdx = GetOrAddRef(marshal.AssignmentId);
                    int? checkInMethodIdx = GetOrAddCheckInMethod(marshal.CheckInMethod) is int cmIdx && cmIdx >= 0 ? cmIdx : null;

                    // Apply default for isCheckedIn
                    bool? isCheckedIn = marshal.IsCheckedIn == defaultIsCheckedIn ? null : marshal.IsCheckedIn;

                    // Convert marshal outstanding tasks
                    List<CompactAreaLeadTask>? compactMarshalTasks = marshal.OutstandingTasks.Count > 0
                        ? marshal.OutstandingTasks.Select(ConvertTask).ToList()
                        : null;

                    compactMarshals.Add(new CompactAreaLeadMarshal(
                        marshalRefIdx,
                        assignmentRefIdx,
                        marshal.Name,
                        string.IsNullOrEmpty(marshal.Email) ? null : marshal.Email,
                        string.IsNullOrEmpty(marshal.PhoneNumber) ? null : marshal.PhoneNumber,
                        isCheckedIn,
                        marshal.CheckInTime,
                        checkInMethodIdx,
                        marshal.LastAccessedAt,
                        marshal.OutstandingTaskCount,
                        compactMarshalTasks
                    ));
                }
            }

            compactCheckpoints.Add(new CompactAreaLeadCheckpoint(
                checkpointRefIdx,
                checkpoint.Name,
                string.IsNullOrEmpty(checkpoint.Description) ? null : checkpoint.Description,
                checkpoint.Latitude,
                checkpoint.Longitude,
                areaNameIndex,
                areaRefIndexes,
                compactMarshals,
                checkpoint.OutstandingTaskCount,
                compactCheckpointTasks
            ));
        }

        return new NormalizedAreaLeadDashboardResponse(
            FieldMap,
            defaults,
            refs,
            scopes,
            contextTypes,
            checkInMethods,
            taskDefinitions,
            compactAreas,
            compactCheckpoints
        );
    }
#pragma warning restore MA0051

    /// <summary>
    /// Returns true if the X-Debug header is present.
    /// </summary>
    public static bool IsDebugRequest(Microsoft.AspNetCore.Http.HttpRequest req) =>
        req.Headers.ContainsKey("X-Debug");
}
