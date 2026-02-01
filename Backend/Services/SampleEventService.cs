using System.Text.Json;
using Humanizer;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Services;

/// <summary>
/// Service for creating sample/demo events with generated data.
/// Sample events are fully functional but automatically expire after 4 hours.
/// </summary>
public class SampleEventService : ISampleEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IAreaRepository _areaRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IMarshalRepository _marshalRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IEventContactRepository _eventContactRepository;
    private readonly ILayerRepository _layerRepository;
    private readonly IEventRoleRepository _eventRoleRepository;
    private readonly IPersonRepository _personRepository;
    private readonly INoteRepository _noteRepository;
    private readonly TableStorageService _tableStorageService;
    private readonly EventService _eventService;

    // Sample first names for generating marshal names (traditionally English)
    private static readonly string[] FirstNames =
    [
        "Alice", "Arthur", "Benjamin", "Catherine", "Charles", "Charlotte", "Daniel", "Dorothy",
        "Edward", "Eleanor", "Elizabeth", "Emily", "Frederick", "George", "Grace", "Hannah",
        "Harold", "Harry", "Henry", "James", "Jane", "John", "Lucy", "Margaret",
        "Mary", "Michael", "Oliver", "Peter", "Richard", "Robert", "Thomas", "William"
    ];

    // Sample last names for generating marshal names (traditionally English)
    private static readonly string[] LastNames =
    [
        "Adams", "Baker", "Brown", "Carter", "Clarke", "Collins", "Cooper", "Davies",
        "Edwards", "Evans", "Green", "Hall", "Harris", "Hill", "Hughes", "Jackson",
        "Johnson", "Jones", "King", "Lewis", "Martin", "Miller", "Moore", "Morris",
        "Robinson", "Smith", "Taylor", "Thompson", "Walker", "White", "Williams", "Wilson"
    ];

    public SampleEventService(
        IEventRepository eventRepository,
        IAreaRepository areaRepository,
        ILocationRepository locationRepository,
        IMarshalRepository marshalRepository,
        IAssignmentRepository assignmentRepository,
        IChecklistItemRepository checklistItemRepository,
        IEventContactRepository eventContactRepository,
        ILayerRepository layerRepository,
        IEventRoleRepository eventRoleRepository,
        IPersonRepository personRepository,
        INoteRepository noteRepository,
        TableStorageService tableStorageService,
        EventService eventService)
    {
        _eventRepository = eventRepository;
        _areaRepository = areaRepository;
        _locationRepository = locationRepository;
        _marshalRepository = marshalRepository;
        _assignmentRepository = assignmentRepository;
        _checklistItemRepository = checklistItemRepository;
        _eventContactRepository = eventContactRepository;
        _layerRepository = layerRepository;
        _eventRoleRepository = eventRoleRepository;
        _personRepository = personRepository;
        _noteRepository = noteRepository;
        _tableStorageService = tableStorageService;
        _eventService = eventService;
    }

    /// <summary>
    /// Create a new sample event with generated data.
    /// Returns the event ID and an admin magic code for accessing the event.
    /// </summary>
    public async Task<SampleEventResult> CreateSampleEventAsync(List<SampleCheckpoint> checkpoints, string routeJson, string deviceFingerprint, string? clientIp, string? timeZoneId = null)
    {
        string eventId = Guid.NewGuid().ToString();
        DateTime now = DateTime.UtcNow;
        DateTime expiresAt = now.AddHours(Constants.SampleEventLifetimeHours);

        // Validate and default timezone
        string effectiveTimeZone = GetValidTimeZone(timeZoneId);

        // Calculate next Sunday at 10am in the user's timezone, converted to UTC for storage
        DateTime nextSunday = GetNextSundayAt10Am(effectiveTimeZone);

        // Create the event entity
        EventEntity eventEntity = new()
        {
            RowKey = eventId,
            Name = "The Howtown Hustle",
            Description = "This ficticious event nestled in the gorgeous hills of The Lake District is the perfect showcase of some of the features of this service.",
            EventDate = nextSunday,
            TimeZoneId = effectiveTimeZone,
            IsActive = true,
            CreatedDate = now,
            IsSampleEvent = true,
            ExpiresAt = expiresAt,
            SchemaVersion = EventEntity.CurrentSchemaVersion
        };

        // Set default payload with some nice styling
        EventPayload payload = new()
        {
            Terminology = new TerminologyPayload
            {
                Person = "Marshals",
                Location = "Checkpoints",
                Area = "Areas",
                Task = "Checklists",
                Course = "Course"
            },
            Styling = new StylingPayload
            {
                Locations = new LocationStylingPayload
                {
                    DefaultType = "default",
                    DefaultBackgroundShape = "circle",
                    DefaultBackgroundColor = "#667eea"
                },
                Branding = new BrandingPayload
                {
                    AccentColour = "#667eea",
                    HeaderGradientStart = "#667eea",
                    HeaderGradientEnd = "#764ba2"
                }
            }
        };
        eventEntity.SetPayload(payload);

        await _eventRepository.AddAsync(eventEntity);

        // Create layer with route
        await CreateLayerAsync(eventId, routeJson);

        // Create areas
        Dictionary<string, string> areaIdMap = await CreateAreasAsync(eventId);

        // Create checkpoints from provided data
        List<LocationEntity> locations = await CreateCheckpointsAsync(eventId, checkpoints, areaIdMap);

        // Create notes for specific checkpoints
        await CreateNotesAsync(eventId, locations);

        // Create 30 marshals with random names (including some surname pairs)
        List<MarshalEntity> marshals = await CreateMarshalsAsync(eventId);

        // Create assignments with realistic staffing patterns (ensuring each area has coverage)
        await CreateAssignmentsAsync(eventId, locations, marshals, areaIdMap);

        // Create area leads - one marshal from each area (must be assigned to a checkpoint in that area)
        await CreateAreaLeadsAsync(eventId, locations, marshals, areaIdMap);

        // Create sample checklist items
        await CreateChecklistItemsAsync(eventId, areaIdMap);

        // Create sample contacts (including area lead contacts linked to marshals)
        await CreateContactsAsync(eventId, locations, marshals, areaIdMap);

        // Generate an admin magic code for this sample event
        // This will be used to access the admin panel without authentication
        string adminCode = AuthService.GenerateMagicCode();

        // Store the admin code in a special table for sample events (includes rate limit info)
        await StoreSampleEventAdminCodeAsync(eventId, adminCode, expiresAt, deviceFingerprint, clientIp);

        return new SampleEventResult
        {
            EventId = eventId,
            AdminCode = adminCode,
            ExpiresAt = expiresAt,
            MarshalCount = marshals.Count,
            CheckpointCount = locations.Count
        };
    }

    /// <summary>
    /// Validates the timezone ID and returns a valid one, defaulting to UTC if invalid.
    /// </summary>
    private static string GetValidTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return "UTC";
        }

        try
        {
            // Try to find the timezone - this validates it exists
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return timeZoneId;
        }
        catch (TimeZoneNotFoundException)
        {
            return "UTC";
        }
    }

    /// <summary>
    /// Calculate the next Sunday at 10:00 AM in the specified timezone, returned as UTC.
    /// </summary>
    private static DateTime GetNextSundayAt10Am(string timeZoneId)
    {
        TimeZoneInfo tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        DateTime nowInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        DateTime todayInTz = nowInTz.Date;

        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)todayInTz.DayOfWeek + 7) % 7;

        // If today is Sunday, get next Sunday instead
        if (daysUntilSunday == 0)
        {
            daysUntilSunday = 7;
        }

        // Next Sunday at 10am in the user's timezone
        DateTime nextSundayLocal = todayInTz.AddDays(daysUntilSunday).AddHours(10);

        // Convert to UTC for storage
        return TimeZoneInfo.ConvertTimeToUtc(nextSundayLocal, tz);
    }

    private async Task CreateLayerAsync(string eventId, string routeJson)
    {
        string layerId = Guid.NewGuid().ToString();
        LayerEntity layer = new()
        {
            PartitionKey = eventId,
            RowKey = layerId,
            EventId = eventId,
            Name = "Main Route",
            DisplayOrder = 0,
            GpxRouteJson = routeJson,
            RouteColor = "#3b82f6", // Blue
            RouteStyle = "line",
            RouteWeight = 4,
            CreatedDate = DateTime.UtcNow
        };

        await _layerRepository.AddAsync(layer);
    }

    private async Task<Dictionary<string, string>> CreateAreasAsync(string eventId)
    {
        Dictionary<string, string> areaIdMap = new();

        // First, create the default "Unassigned" area (required by the system)
        string defaultAreaId = Guid.NewGuid().ToString();
        AreaEntity defaultArea = new()
        {
            PartitionKey = eventId,
            RowKey = defaultAreaId,
            EventId = eventId,
            Name = Constants.DefaultAreaName,
            Description = Constants.DefaultAreaDescription,
            ContactsJson = "[]",
            PolygonJson = "[]",
            IsDefault = true,
            DisplayOrder = 0,
            CreatedDate = DateTime.UtcNow
        };
        await _areaRepository.AddAsync(defaultArea);
        areaIdMap[Constants.DefaultAreaName] = defaultAreaId;

        // Area polygons (rounded to 6dp)
        string startFinishPolygon = """[{"Lat":54.571220,"Lng":-2.859042},{"Lat":54.566284,"Lng":-2.856338},{"Lat":54.566022,"Lng":-2.867839},{"Lat":54.570822,"Lng":-2.868483}]""";
        string northPolygon = """[{"Lat":54.564800,"Lng":-2.880000},{"Lat":54.564800,"Lng":-2.855000},{"Lat":54.555000,"Lng":-2.855000},{"Lat":54.555000,"Lng":-2.880000}]""";
        string southPolygon = """[{"Lat":54.549139,"Lng":-2.889233},{"Lat":54.552423,"Lng":-2.843399},{"Lat":54.534407,"Lng":-2.840137},{"Lat":54.532316,"Lng":-2.885113}]""";

        (string name, string description, string color, string polygon)[] areas =
        [
            ("Start/Finish", "Start and finish area, including timing mats and merchandise.", "#22c55e", startFinishPolygon),  // Green
            ("North of the course", "Northern checkpoints excluding the start and finish area. Generally low risk areas with good terrain.", "#3b82f6", northPolygon), // Blue
            ("South of the course", "Southern checkpoints, much more remote and lacking in mobile coverage. Paths are less firm underfoot, especially in wet conditions.", "#f59e0b", southPolygon)  // Orange
        ];

        int order = 1; // Start at 1 since default area is 0
        foreach ((string name, string description, string color, string polygon) in areas)
        {
            string areaId = Guid.NewGuid().ToString();
            AreaEntity area = new()
            {
                PartitionKey = eventId,
                RowKey = areaId,
                EventId = eventId,
                Name = name,
                Description = description,
                Color = color,
                IsDefault = false,
                PolygonJson = polygon,
                DisplayOrder = order++,
                CreatedDate = DateTime.UtcNow
            };

            await _areaRepository.AddAsync(area);
            areaIdMap[name] = areaId;
        }

        return areaIdMap;
    }

    private async Task<List<LocationEntity>> CreateCheckpointsAsync(
        string eventId,
        List<SampleCheckpoint> checkpoints,
        Dictionary<string, string> areaIdMap)
    {
        List<LocationEntity> locations = [];

        foreach (SampleCheckpoint checkpoint in checkpoints)
        {
            string locationId = Guid.NewGuid().ToString();

            // Map area name to ID
            List<string> areaIds = [];
            if (!string.IsNullOrEmpty(checkpoint.AreaName) && areaIdMap.TryGetValue(checkpoint.AreaName, out string? areaId))
            {
                areaIds.Add(areaId);
            }

            LocationEntity location = new()
            {
                PartitionKey = eventId,
                RowKey = locationId,
                EventId = eventId,
                Name = checkpoint.Name,
                Description = checkpoint.Description ?? string.Empty,
                Latitude = checkpoint.Latitude,
                Longitude = checkpoint.Longitude,
                RequiredMarshals = checkpoint.RequiredMarshals,
                SchemaVersion = LocationEntity.CurrentSchemaVersion
            };

            // Set payload with area associations and styling
            LocationPayload locationPayload = new()
            {
                AreaIds = areaIds,
                Style = new LocationStylePayload
                {
                    Type = checkpoint.StyleType ?? "default",
                    MapRotation = checkpoint.StyleMapRotation?.ToString() ?? string.Empty
                }
            };
            location.SetPayload(locationPayload);

            await _locationRepository.AddAsync(location);
            locations.Add(location);
        }

        return locations;
    }

    /// <summary>
    /// Creates notes for specific checkpoints.
    /// </summary>
    private async Task CreateNotesAsync(string eventId, List<LocationEntity> locations)
    {
        // Find CP6 (the photo checkpoint)
        LocationEntity? cp6 = locations.FirstOrDefault(l => l.Name == "CP6");
        if (cp6 == null)
        {
            return;
        }

        string noteId = Guid.NewGuid().ToString();
        List<ScopeConfiguration> scopes =
        [
            new ScopeConfiguration
            {
                Scope = "EveryoneAtCheckpoints",
                ItemType = "Checkpoint",
                Ids = [cp6.RowKey]
            }
        ];

        NoteEntity note = new()
        {
            PartitionKey = eventId,
            RowKey = noteId,
            EventId = eventId,
            NoteId = noteId,
            Title = "Photography",
            Content = "There's a bench available to use. Many runners like to try and get jumping photographs, so try to humour them if you can.\n\nRunners making an 'X' with their arms do not wish to have their photos taken, so please do not upload these.",
            ScopeConfigurationsJson = JsonSerializer.Serialize(scopes),
            DisplayOrder = 0,
            Priority = Constants.NotePriorityNormal,
            CreatedByName = "System",
            CreatedAt = DateTime.UtcNow
        };

        await _noteRepository.AddAsync(note);
    }

    /// <summary>
    /// Creates 30 marshals with random names. A couple share surnames and have
    /// "Needs to be with {first name}" in their additional details.
    /// </summary>
    private async Task<List<MarshalEntity>> CreateMarshalsAsync(string eventId)
    {
        List<MarshalEntity> marshals = [];
        Random random = new();
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);

        // Create 2 surname pairs (4 marshals total)
        List<(string firstName1, string firstName2, string sharedLastName)> surnamePairs =
        [
            (FirstNames[random.Next(FirstNames.Length)], FirstNames[random.Next(FirstNames.Length)], LastNames[random.Next(LastNames.Length)]),
            (FirstNames[random.Next(FirstNames.Length)], FirstNames[random.Next(FirstNames.Length)], LastNames[random.Next(LastNames.Length)])
        ];

        // Ensure unique first names within each pair
        foreach ((string firstName1, string firstName2, string sharedLastName) in surnamePairs)
        {
            string actualFirstName1 = firstName1;
            string actualFirstName2 = firstName2;

            // Make sure the two first names are different
            while (actualFirstName2.Equals(actualFirstName1, StringComparison.OrdinalIgnoreCase))
            {
                actualFirstName2 = FirstNames[random.Next(FirstNames.Length)];
            }

            string name1 = $"{actualFirstName1} {sharedLastName}";
            string name2 = $"{actualFirstName2} {sharedLastName}";

            // Ensure we haven't used these names already
            while (usedNames.Contains(name1))
            {
                actualFirstName1 = FirstNames[random.Next(FirstNames.Length)];
                name1 = $"{actualFirstName1} {sharedLastName}";
            }
            usedNames.Add(name1);

            while (usedNames.Contains(name2) || name2.Equals(name1, StringComparison.OrdinalIgnoreCase))
            {
                actualFirstName2 = FirstNames[random.Next(FirstNames.Length)];
                name2 = $"{actualFirstName2} {sharedLastName}";
            }
            usedNames.Add(name2);

            // Create first marshal of the pair
            string marshalId1 = Guid.NewGuid().ToString();
            MarshalEntity marshal1 = new()
            {
                PartitionKey = eventId,
                RowKey = marshalId1,
                EventId = eventId,
                MarshalId = marshalId1,
                Name = name1,
                Notes = $"Needs to be with {actualFirstName2}",
                MagicCode = AuthService.GenerateMagicCode(),
                CreatedDate = DateTime.UtcNow
            };
            await _marshalRepository.AddAsync(marshal1);
            marshals.Add(marshal1);

            // Create second marshal of the pair
            string marshalId2 = Guid.NewGuid().ToString();
            MarshalEntity marshal2 = new()
            {
                PartitionKey = eventId,
                RowKey = marshalId2,
                EventId = eventId,
                MarshalId = marshalId2,
                Name = name2,
                Notes = $"Needs to be with {actualFirstName1}",
                MagicCode = AuthService.GenerateMagicCode(),
                CreatedDate = DateTime.UtcNow
            };
            await _marshalRepository.AddAsync(marshal2);
            marshals.Add(marshal2);
        }

        // Create remaining marshals (26 more to reach 30 total)
        for (int i = marshals.Count; i < 30; i++)
        {
            string name;
            do
            {
                string firstName = FirstNames[random.Next(FirstNames.Length)];
                string lastName = LastNames[random.Next(LastNames.Length)];
                name = $"{firstName} {lastName}";
            } while (usedNames.Contains(name));

            usedNames.Add(name);

            string marshalId = Guid.NewGuid().ToString();
            MarshalEntity marshal = new()
            {
                PartitionKey = eventId,
                RowKey = marshalId,
                EventId = eventId,
                MarshalId = marshalId,
                Name = name,
                MagicCode = AuthService.GenerateMagicCode(),
                CreatedDate = DateTime.UtcNow
            };

            await _marshalRepository.AddAsync(marshal);
            marshals.Add(marshal);
        }

        return marshals;
    }

    /// <summary>
    /// Creates assignments with realistic staffing patterns:
    /// - Each non-default area gets at least one staffed checkpoint (for area leads)
    /// - Surname pairs (first 4 marshals) are assigned together to checkpoints
    /// - Most checkpoints are fully staffed (2 marshals)
    /// - A couple are partially staffed (1 marshal)
    /// - 1-2 checkpoints are left unstaffed
    /// - Some marshals remain unassigned
    /// </summary>
    private async Task CreateAssignmentsAsync(
        string eventId,
        List<LocationEntity> locations,
        List<MarshalEntity> marshals,
        Dictionary<string, string> areaIdMap)
    {
        Random random = new();
        HashSet<string> staffedLocationIds = [];
        int marshalIndex = 0;

        // Group locations by area
        Dictionary<string, List<LocationEntity>> areaToLocations = new();
        foreach (LocationEntity location in locations)
        {
            LocationPayload payload = location.GetPayload();
            foreach (string areaId in payload.AreaIds)
            {
                if (!areaToLocations.ContainsKey(areaId))
                {
                    areaToLocations[areaId] = [];
                }
                areaToLocations[areaId].Add(location);
            }
        }

        // First, ensure each non-default area has at least one staffed checkpoint
        foreach (KeyValuePair<string, string> kvp in areaIdMap)
        {
            string areaName = kvp.Key;
            string areaId = kvp.Value;

            if (areaName == Constants.DefaultAreaName)
            {
                continue;
            }

            if (!areaToLocations.TryGetValue(areaId, out List<LocationEntity>? areaLocations) || areaLocations.Count == 0)
            {
                continue;
            }

            // Pick a random checkpoint from this area that hasn't been staffed yet
            List<LocationEntity> unstaffedInArea = areaLocations.Where(l => !staffedLocationIds.Contains(l.RowKey)).ToList();
            if (unstaffedInArea.Count == 0 || marshalIndex >= marshals.Count - 1)
            {
                continue;
            }

            LocationEntity location = unstaffedInArea[random.Next(unstaffedInArea.Count)];
            staffedLocationIds.Add(location.RowKey);

            // Assign 2 marshals to ensure the area has coverage
            for (int j = 0; j < 2 && marshalIndex < marshals.Count; j++)
            {
                MarshalEntity marshal = marshals[marshalIndex++];
                await CreateAssignmentAsync(eventId, location.RowKey, marshal);
            }
        }

        // Now assign the surname pairs (next 4 marshals) to random remaining checkpoints
        List<LocationEntity> remainingLocations = locations.Where(l => !staffedLocationIds.Contains(l.RowKey)).OrderBy(_ => random.Next()).ToList();
        int locationIndex = 0;

        for (int pairIndex = 0; pairIndex < 2 && marshalIndex < marshals.Count - 1 && locationIndex < remainingLocations.Count; pairIndex++)
        {
            MarshalEntity marshal1 = marshals[marshalIndex++];
            MarshalEntity marshal2 = marshals[marshalIndex++];
            LocationEntity location = remainingLocations[locationIndex++];
            staffedLocationIds.Add(location.RowKey);

            await CreateAssignmentAsync(eventId, location.RowKey, marshal1);
            await CreateAssignmentAsync(eventId, location.RowKey, marshal2);
        }

        // Get remaining unstaffed locations
        remainingLocations = locations.Where(l => !staffedLocationIds.Contains(l.RowKey)).OrderBy(_ => random.Next()).ToList();

        // Leave 2 checkpoints completely unstaffed
        int unstaffedCount = Math.Min(2, remainingLocations.Count);
        List<LocationEntity> locationsToStaff = remainingLocations.Take(remainingLocations.Count - unstaffedCount).ToList();

        // Partially staff 2 checkpoints (1 marshal each), rest get 2
        int partiallyStaffedCount = 2;

        for (int i = 0; i < locationsToStaff.Count && marshalIndex < marshals.Count; i++)
        {
            LocationEntity location = locationsToStaff[i];
            int staffCount = i < partiallyStaffedCount ? 1 : 2;

            for (int j = 0; j < staffCount && marshalIndex < marshals.Count; j++)
            {
                MarshalEntity marshal = marshals[marshalIndex++];
                await CreateAssignmentAsync(eventId, location.RowKey, marshal);
            }
        }

        // Some marshals will be left unassigned, which is intentional
    }

    private async Task CreateAssignmentAsync(string eventId, string locationId, MarshalEntity marshal)
    {
        AssignmentEntity assignment = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            LocationId = locationId,
            MarshalId = marshal.MarshalId,
            MarshalName = marshal.Name
        };

        await _assignmentRepository.AddAsync(assignment);
    }

    /// <summary>
    /// Creates area lead roles for each non-default area.
    /// Picks a random marshal from those assigned to checkpoints in that area.
    /// </summary>
    private async Task CreateAreaLeadsAsync(
        string eventId,
        List<LocationEntity> locations,
        List<MarshalEntity> marshals,
        Dictionary<string, string> areaIdMap)
    {
        Random random = new();

        // Get all assignments to know which marshals are at which checkpoints
        IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);
        Dictionary<string, List<string>> locationToMarshalIds = allAssignments
            .GroupBy(a => a.LocationId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.MarshalId).ToList());

        // Build a map of area ID to checkpoints in that area
        Dictionary<string, List<LocationEntity>> areaToLocations = new();
        foreach (LocationEntity location in locations)
        {
            LocationPayload payload = location.GetPayload();
            foreach (string areaId in payload.AreaIds)
            {
                if (!areaToLocations.ContainsKey(areaId))
                {
                    areaToLocations[areaId] = [];
                }
                areaToLocations[areaId].Add(location);
            }
        }

        // Track which marshals have been made area leads
        HashSet<string> usedMarshalIds = [];

        // For each non-default area, find marshals assigned to checkpoints in that area
        foreach (KeyValuePair<string, string> kvp in areaIdMap)
        {
            string areaName = kvp.Key;
            string areaId = kvp.Value;

            // Skip the default "Unassigned" area
            if (areaName == Constants.DefaultAreaName)
            {
                continue;
            }

            // Find all checkpoints in this area
            if (!areaToLocations.TryGetValue(areaId, out List<LocationEntity>? areaLocations) || areaLocations.Count == 0)
            {
                continue;
            }

            // Collect all marshals assigned to checkpoints in this area
            List<string> candidateMarshalIds = [];
            foreach (LocationEntity location in areaLocations)
            {
                if (locationToMarshalIds.TryGetValue(location.RowKey, out List<string>? marshalIds))
                {
                    foreach (string marshalId in marshalIds)
                    {
                        if (!usedMarshalIds.Contains(marshalId) && !candidateMarshalIds.Contains(marshalId))
                        {
                            candidateMarshalIds.Add(marshalId);
                        }
                    }
                }
            }

            if (candidateMarshalIds.Count == 0)
            {
                continue;
            }

            // Pick a random marshal from the candidates
            string selectedMarshalId = candidateMarshalIds[random.Next(candidateMarshalIds.Count)];
            usedMarshalIds.Add(selectedMarshalId);

            // Find the marshal entity
            MarshalEntity? marshal = marshals.FirstOrDefault(m => m.MarshalId == selectedMarshalId);
            if (marshal == null)
            {
                continue;
            }

            // Create a PersonEntity for this marshal (required for EventRole)
            string personId = Guid.NewGuid().ToString();
            PersonEntity person = new()
            {
                PartitionKey = "PERSON",
                RowKey = personId,
                PersonId = personId,
                Name = marshal.Name,
                IsSystemAdmin = false,
                CreatedAt = DateTime.UtcNow
            };
            await _personRepository.AddAsync(person);

            // Link the marshal to the person
            marshal.PersonId = personId;
            await _marshalRepository.UpdateUnconditionalAsync(marshal);

            // Create the area lead role
            string roleId = Guid.NewGuid().ToString();
            EventRoleEntity areaLeadRole = new()
            {
                PartitionKey = personId,
                RowKey = EventRoleEntity.CreateRowKey(eventId, roleId),
                PersonId = personId,
                EventId = eventId,
                Role = Constants.RoleEventAreaLead,
                AreaIdsJson = JsonSerializer.Serialize(new[] { areaId }),
                GrantedByPersonId = string.Empty,
                GrantedAt = DateTime.UtcNow
            };
            await _eventRoleRepository.AddAsync(areaLeadRole);
        }
    }

    private async Task CreateChecklistItemsAsync(string eventId, Dictionary<string, string> areaIdMap)
    {
        int order = 0;

        // First, create the highest priority task - scoped to every marshal at all checkpoints
        string hiVisItemId = Guid.NewGuid().ToString();
        List<ScopeConfiguration> hiVisScopes =
        [
            new ScopeConfiguration
            {
                Scope = "EveryoneAtCheckpoints",
                ItemType = "Checkpoint",
                Ids = ["ALL_CHECKPOINTS"]
            }
        ];

        ChecklistItemEntity hiVisItem = new()
        {
            PartitionKey = eventId,
            RowKey = hiVisItemId,
            EventId = eventId,
            ItemId = hiVisItemId,
            Text = "Collect your hi-vis vest from your area lead",
            ScopeConfigurationsJson = JsonSerializer.Serialize(hiVisScopes),
            DisplayOrder = order++,
            IsRequired = true,
            CreatedDate = DateTime.UtcNow
        };
        await _checklistItemRepository.AddAsync(hiVisItem);

        // Then create the per-checkpoint tasks
        string[] items =
        [
            "Set up checkpoint signage",
            "Check radio communication",
            "Confirm emergency contact numbers",
            "Review safety procedures",
            "Check first aid kit"
        ];

        foreach (string text in items)
        {
            string itemId = Guid.NewGuid().ToString();

            // Create scope for all checkpoints (one completion per checkpoint)
            List<ScopeConfiguration> scopes =
            [
                new ScopeConfiguration
                {
                    Scope = "OnePerCheckpoint",
                    ItemType = "Checkpoint",
                    Ids = ["ALL_CHECKPOINTS"]
                }
            ];

            ChecklistItemEntity item = new()
            {
                PartitionKey = eventId,
                RowKey = itemId,
                EventId = eventId,
                ItemId = itemId,
                Text = text,
                ScopeConfigurationsJson = JsonSerializer.Serialize(scopes),
                DisplayOrder = order++,
                IsRequired = order <= 4, // First 4 are required (including hi-vis)
                CreatedDate = DateTime.UtcNow
            };

            await _checklistItemRepository.AddAsync(item);
        }

        // Add a check-in task linked to check-in status
        string checkInItemId = Guid.NewGuid().ToString();
        List<ScopeConfiguration> checkInScopes =
        [
            new ScopeConfiguration
            {
                Scope = "EveryoneAtCheckpoints",
                ItemType = "Checkpoint",
                Ids = ["ALL_CHECKPOINTS"]
            }
        ];

        ChecklistItemEntity checkInItem = new()
        {
            PartitionKey = eventId,
            RowKey = checkInItemId,
            EventId = eventId,
            ItemId = checkInItemId,
            Text = "Check-in to your location",
            ScopeConfigurationsJson = JsonSerializer.Serialize(checkInScopes),
            DisplayOrder = order++,
            IsRequired = true,
            LinksToCheckIn = true,
            CreatedDate = DateTime.UtcNow
        };
        await _checklistItemRepository.AddAsync(checkInItem);

        // Task for all area leads - ensure everyone has equipment and checked in
        string areaLeadTaskId = Guid.NewGuid().ToString();
        List<ScopeConfiguration> areaLeadScopes =
        [
            new ScopeConfiguration
            {
                Scope = "EveryAreaLead",
                ItemType = "Area",
                Ids = ["ALL_AREAS"]
            }
        ];

        ChecklistItemEntity areaLeadTask = new()
        {
            PartitionKey = eventId,
            RowKey = areaLeadTaskId,
            EventId = eventId,
            ItemId = areaLeadTaskId,
            Text = "Ensure that everyone in your area has collected their equipment and has checked in",
            ScopeConfigurationsJson = JsonSerializer.Serialize(areaLeadScopes),
            DisplayOrder = order++,
            IsRequired = true,
            CreatedDate = DateTime.UtcNow
        };
        await _checklistItemRepository.AddAsync(areaLeadTask);

        // Task for one marshal per checkpoint in South area - rescue services notification
        if (areaIdMap.TryGetValue("South of the course", out string? southAreaId))
        {
            string rescueTaskId = Guid.NewGuid().ToString();
            List<ScopeConfiguration> rescueScopes =
            [
                new ScopeConfiguration
                {
                    Scope = "OnePerCheckpoint",
                    ItemType = "Area",
                    Ids = [southAreaId]
                }
            ];

            ChecklistItemEntity rescueTask = new()
            {
                PartitionKey = eventId,
                RowKey = rescueTaskId,
                EventId = eventId,
                ItemId = rescueTaskId,
                Text = "Ensure that rescue services know of your location",
                ScopeConfigurationsJson = JsonSerializer.Serialize(rescueScopes),
                DisplayOrder = order++,
                IsRequired = false,
                CreatedDate = DateTime.UtcNow
            };
            await _checklistItemRepository.AddAsync(rescueTask);
        }

        // Task for Start/Finish area lead only - verify merchandise tables
        if (areaIdMap.TryGetValue("Start/Finish", out string? startFinishAreaId))
        {
            string merchandiseTaskId = Guid.NewGuid().ToString();
            List<ScopeConfiguration> merchandiseScopes =
            [
                new ScopeConfiguration
                {
                    Scope = "OneLeadPerArea",
                    ItemType = "Area",
                    Ids = [startFinishAreaId]
                }
            ];

            ChecklistItemEntity merchandiseTask = new()
            {
                PartitionKey = eventId,
                RowKey = merchandiseTaskId,
                EventId = eventId,
                ItemId = merchandiseTaskId,
                Text = "Verify that merchandise tables are ready",
                ScopeConfigurationsJson = JsonSerializer.Serialize(merchandiseScopes),
                DisplayOrder = order++,
                IsRequired = false,
                CreatedDate = DateTime.UtcNow
            };
            await _checklistItemRepository.AddAsync(merchandiseTask);
        }
    }

    private async Task CreateContactsAsync(
        string eventId,
        List<LocationEntity> locations,
        List<MarshalEntity> marshals,
        Dictionary<string, string> areaIdMap)
    {
        Random random = new();
        HashSet<string> usedNames = [];
        int displayOrder = 0;

        // Helper to generate a unique random name
        string GenerateRandomName()
        {
            string name;
            do
            {
                string firstName = FirstNames[random.Next(FirstNames.Length)];
                string lastName = LastNames[random.Next(LastNames.Length)];
                name = $"{firstName} {lastName}";
            } while (usedNames.Contains(name));
            usedNames.Add(name);
            return name;
        }

        // Helper to generate a fake UK mobile number (07XXX XXXXXX format)
        string GenerateFakeUkPhone()
        {
            // UK mobile prefixes (07 followed by valid network codes)
            string[] mobilePrefixes = ["07700", "07711", "07722", "07733", "07744", "07755", "07766", "07777", "07788", "07799"];
            string prefix = mobilePrefixes[random.Next(mobilePrefixes.Length)];
            int suffix = random.Next(100000, 999999);
            return $"{prefix} {suffix}";
        }

        // Create an event director contact
        EventContactEntity directorContact = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            ContactId = Guid.NewGuid().ToString(),
            Name = GenerateRandomName(),
            Phone = GenerateFakeUkPhone(),
            RolesJson = JsonSerializer.Serialize(new[] { "EventDirector" }),
            ScopeConfigurationsJson = JsonSerializer.Serialize(new[]
            {
                new ScopeConfiguration
                {
                    Scope = "EveryoneInAreas",
                    ItemType = "Area",
                    Ids = ["ALL_AREAS"]
                }
            }),
            ShowInEmergencyInfo = true,
            IsPinned = true,
            DisplayOrder = displayOrder++,
            CreatedAt = DateTime.UtcNow
        };

        await _eventContactRepository.AddAsync(directorContact);

        // Create an emergency contact
        EventContactEntity emergencyContact = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            ContactId = Guid.NewGuid().ToString(),
            Name = GenerateRandomName(),
            Phone = GenerateFakeUkPhone(),
            RolesJson = JsonSerializer.Serialize(new[] { "EmergencyContact" }),
            ScopeConfigurationsJson = JsonSerializer.Serialize(new[]
            {
                new ScopeConfiguration
                {
                    Scope = "EveryoneInAreas",
                    ItemType = "Area",
                    Ids = ["ALL_AREAS"]
                }
            }),
            ShowInEmergencyInfo = true,
            DisplayOrder = displayOrder++,
            CreatedAt = DateTime.UtcNow
        };

        await _eventContactRepository.AddAsync(emergencyContact);

        // Create a medical contact
        EventContactEntity medicalContact = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            ContactId = Guid.NewGuid().ToString(),
            Name = GenerateRandomName(),
            Phone = GenerateFakeUkPhone(),
            RolesJson = JsonSerializer.Serialize(new[] { "MedicalLead" }),
            ScopeConfigurationsJson = JsonSerializer.Serialize(new[]
            {
                new ScopeConfiguration
                {
                    Scope = "EveryoneInAreas",
                    ItemType = "Area",
                    Ids = ["ALL_AREAS"]
                }
            }),
            ShowInEmergencyInfo = true,
            DisplayOrder = displayOrder++,
            CreatedAt = DateTime.UtcNow
        };

        await _eventContactRepository.AddAsync(medicalContact);

        // Create a safety officer contact for the south zone only
        if (areaIdMap.TryGetValue("South of the course", out string? southAreaId))
        {
            EventContactEntity safetyContact = new()
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = eventId,
                ContactId = Guid.NewGuid().ToString(),
                Name = GenerateRandomName(),
                Phone = GenerateFakeUkPhone(),
                RolesJson = JsonSerializer.Serialize(new[] { "SafetyOfficer" }),
                ScopeConfigurationsJson = JsonSerializer.Serialize(new[]
                {
                    new ScopeConfiguration
                    {
                        Scope = "EveryoneInAreas",
                        ItemType = "Area",
                        Ids = [southAreaId]
                    }
                }),
                ShowInEmergencyInfo = true,
                DisplayOrder = displayOrder++,
                CreatedAt = DateTime.UtcNow
            };

            await _eventContactRepository.AddAsync(safetyContact);
        }

        // Create area lead contacts - one for each non-default area, linked to a marshal in that area
        // Get all assignments to know which marshals are at which checkpoints
        IEnumerable<AssignmentEntity> allAssignments = await _assignmentRepository.GetByEventAsync(eventId);
        Dictionary<string, List<string>> locationToMarshalIds = allAssignments
            .GroupBy(a => a.LocationId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.MarshalId).ToList());

        // Build a map of area ID to checkpoints in that area
        Dictionary<string, List<LocationEntity>> areaToLocations = new();
        foreach (LocationEntity location in locations)
        {
            LocationPayload payload = location.GetPayload();
            foreach (string areaId in payload.AreaIds)
            {
                if (!areaToLocations.ContainsKey(areaId))
                {
                    areaToLocations[areaId] = [];
                }
                areaToLocations[areaId].Add(location);
            }
        }

        // Track which marshals have been used as area leads
        HashSet<string> usedMarshalIds = [];

        foreach (KeyValuePair<string, string> kvp in areaIdMap)
        {
            string areaName = kvp.Key;
            string areaId = kvp.Value;

            // Skip the default "Unassigned" area
            if (areaName == Constants.DefaultAreaName)
            {
                continue;
            }

            // Find all checkpoints in this area
            if (!areaToLocations.TryGetValue(areaId, out List<LocationEntity>? areaLocations) || areaLocations.Count == 0)
            {
                continue;
            }

            // Collect all marshals assigned to checkpoints in this area
            List<string> candidateMarshalIds = [];
            foreach (LocationEntity location in areaLocations)
            {
                if (locationToMarshalIds.TryGetValue(location.RowKey, out List<string>? marshalIds))
                {
                    foreach (string marshalId in marshalIds)
                    {
                        if (!usedMarshalIds.Contains(marshalId) && !candidateMarshalIds.Contains(marshalId))
                        {
                            candidateMarshalIds.Add(marshalId);
                        }
                    }
                }
            }

            if (candidateMarshalIds.Count == 0)
            {
                continue;
            }

            // Pick a random marshal from the candidates
            string selectedMarshalId = candidateMarshalIds[random.Next(candidateMarshalIds.Count)];
            usedMarshalIds.Add(selectedMarshalId);

            // Find the marshal entity
            MarshalEntity? marshal = marshals.FirstOrDefault(m => m.MarshalId == selectedMarshalId);
            if (marshal == null)
            {
                continue;
            }

            // Create an area lead contact linked to this marshal
            EventContactEntity areaLeadContact = new()
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString(),
                EventId = eventId,
                ContactId = Guid.NewGuid().ToString(),
                Name = marshal.Name,
                Phone = GenerateFakeUkPhone(),
                MarshalId = marshal.MarshalId,
                RolesJson = JsonSerializer.Serialize(new[] { "AreaLead" }),
                ScopeConfigurationsJson = JsonSerializer.Serialize(new[]
                {
                    new ScopeConfiguration
                    {
                        Scope = "EveryoneInAreas",
                        ItemType = "Area",
                        Ids = [areaId]
                    }
                }),
                ShowInEmergencyInfo = false,
                DisplayOrder = displayOrder++,
                CreatedAt = DateTime.UtcNow
            };

            await _eventContactRepository.AddAsync(areaLeadContact);
        }
    }

    private async Task StoreSampleEventAdminCodeAsync(string eventId, string adminCode, DateTime expiresAt, string deviceFingerprint, string? clientIp)
    {
        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();

        SampleEventAdminEntity entity = new()
        {
            PartitionKey = "sample",
            RowKey = adminCode.ToUpperInvariant(),
            EventId = eventId,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            DeviceFingerprint = deviceFingerprint,
            ClientIp = clientIp ?? string.Empty
        };

        await table.UpsertEntityAsync(entity);
    }

    /// <summary>
    /// Recover a sample event by device fingerprint.
    /// Returns the active (unexpired) event for this device, or null if none exists.
    /// </summary>
    public async Task<SampleEventRecoveryResult?> GetSampleEventByDeviceFingerprintAsync(string deviceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(deviceFingerprint) || deviceFingerprint == "unknown")
        {
            return null;
        }

        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();
        DateTime now = DateTime.UtcNow;

        // Query for an active (unexpired) sample event from this device
        await foreach (SampleEventAdminEntity entity in table.QueryAsync<SampleEventAdminEntity>(
            e => e.PartitionKey == "sample"))
        {
            if (entity.DeviceFingerprint == deviceFingerprint && entity.ExpiresAt > now)
            {
                return new SampleEventRecoveryResult
                {
                    EventId = entity.EventId,
                    AdminCode = entity.RowKey, // RowKey is the admin code
                    ExpiresAt = entity.ExpiresAt
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Look up a sample event by its admin code.
    /// Returns null if not found or expired.
    /// </summary>
    public async Task<string?> GetEventIdByAdminCodeAsync(string adminCode)
    {
        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();

        try
        {
            Azure.Response<SampleEventAdminEntity> response = await table.GetEntityAsync<SampleEventAdminEntity>(
                "sample",
                adminCode.ToUpperInvariant());

            SampleEventAdminEntity entity = response.Value;

            // Check if expired
            if (entity.ExpiresAt < DateTime.UtcNow)
            {
                return null;
            }

            return entity.EventId;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <summary>
    /// Check if a device fingerprint is rate limited.
    /// A device is only rate limited if it has an active (unexpired) sample event.
    /// Once the event expires, the user can create a new one.
    /// </summary>
    public async Task<bool> IsDeviceRateLimitedAsync(string deviceFingerprint)
    {
        if (string.IsNullOrWhiteSpace(deviceFingerprint) || deviceFingerprint == "unknown")
        {
            return false; // Can't rate limit unknown devices, rely on IP
        }

        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();
        DateTime now = DateTime.UtcNow;

        // Only rate limit if device has an unexpired sample event
        await foreach (SampleEventAdminEntity entity in table.QueryAsync<SampleEventAdminEntity>(
            e => e.PartitionKey == "sample"))
        {
            if (entity.DeviceFingerprint == deviceFingerprint && entity.ExpiresAt > now)
            {
                return true; // Has an active sample event
            }
        }

        return false;
    }

    /// <summary>
    /// Check if an IP address is rate limited (max 3 active sample events per IP).
    /// Only counts unexpired sample events.
    /// </summary>
    public async Task<bool> IsIpRateLimitedAsync(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();
        DateTime now = DateTime.UtcNow;

        // Count active (unexpired) sample events from this IP
        int count = 0;
        await foreach (SampleEventAdminEntity entity in table.QueryAsync<SampleEventAdminEntity>(
            e => e.PartitionKey == "sample"))
        {
            if (entity.ClientIp == ipAddress && entity.ExpiresAt > now)
            {
                count++;
                if (count >= 3)
                {
                    return true; // Rate limited
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Delete all data for an expired sample event.
    /// Delegates to EventService which handles all event-related deletions.
    /// </summary>
    public async Task DeleteSampleEventAsync(string eventId)
    {
        await _eventService.DeleteEventWithAllDataAsync(eventId);
    }

    /// <summary>
    /// Get all expired sample events from EventEntity table.
    /// </summary>
    public async Task<List<EventEntity>> GetExpiredSampleEventsAsync()
    {
        IEnumerable<EventEntity> allEvents = await _eventRepository.GetAllAsync();
        DateTime now = DateTime.UtcNow;

        return allEvents
            .Where(e => e.IsSampleEvent && e.ExpiresAt.HasValue && e.ExpiresAt.Value < now)
            .ToList();
    }

    /// <summary>
    /// Get all expired sample event admin entries.
    /// This catches cases where the EventEntity might be missing or misconfigured.
    /// </summary>
    public async Task<List<SampleEventAdminEntity>> GetExpiredSampleEventAdminEntriesAsync()
    {
        Azure.Data.Tables.TableClient table = _tableStorageService.GetSampleEventAdminTable();
        DateTime now = DateTime.UtcNow;
        List<SampleEventAdminEntity> expired = [];

        await foreach (SampleEventAdminEntity entity in table.QueryAsync<SampleEventAdminEntity>(
            e => e.PartitionKey == "sample"))
        {
            if (entity.ExpiresAt < now)
            {
                expired.Add(entity);
            }
        }

        return expired;
    }

    /// <summary>
    /// Delete a sample event by its admin entry (used for orphaned cleanup).
    /// </summary>
    public async Task DeleteSampleEventByAdminEntryAsync(SampleEventAdminEntity adminEntry)
    {
        // Delete via EventService - it handles the case where some entities may not exist
        await _eventService.DeleteEventWithAllDataAsync(adminEntry.EventId);
    }
}

/// <summary>
/// Result of creating a sample event.
/// </summary>
public class SampleEventResult
{
    public required string EventId { get; init; }
    public required string AdminCode { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required int MarshalCount { get; init; }
    public required int CheckpointCount { get; init; }
}

/// <summary>
/// Result of recovering a sample event by device fingerprint.
/// </summary>
public class SampleEventRecoveryResult
{
    public required string EventId { get; init; }
    public required string AdminCode { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

/// <summary>
/// Input for creating a sample checkpoint.
/// </summary>
public class SampleCheckpoint
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public string? AreaName { get; init; }
    public int RequiredMarshals { get; init; } = 2;
    public string? StyleType { get; init; }
    public int? StyleMapRotation { get; init; }
}

/// <summary>
/// Entity for storing sample event admin codes.
/// PartitionKey = "sample", RowKey = admin code (uppercase)
/// </summary>
public class SampleEventAdminEntity : Azure.Data.Tables.ITableEntity
{
    public string PartitionKey { get; set; } = "sample";
    public string RowKey { get; set; } = string.Empty; // Admin code
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public string EventId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // For rate limiting
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string ClientIp { get; set; } = string.Empty;
}
