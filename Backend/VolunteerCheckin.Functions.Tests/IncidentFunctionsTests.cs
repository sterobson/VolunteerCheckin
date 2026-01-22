using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for incident visibility rules.
///
/// Setup:
/// - Three checkpoints: C1, C2, C3
/// - Area A1 contains C2 and C3 (C1 is not in any area)
/// - Marshals:
///   - M1 assigned to C1
///   - M2 assigned to C2
///   - M3a and M3b assigned to C3
///   - M4 and M5 are unassigned
/// - L1 is area lead for A1
/// - Admin1 is event admin
/// </summary>
[TestClass]
public class IncidentFunctionsTests
{
    private Mock<ILogger<IncidentFunctions>> _mockLogger = null!;
    private Mock<IIncidentRepository> _mockIncidentRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private IncidentFunctions _functions = null!;

    // Test IDs
    private const string EventId = "event-1";
    private const string AreaA1Id = "area-a1";

    private const string CheckpointC1Id = "checkpoint-c1";
    private const string CheckpointC2Id = "checkpoint-c2";
    private const string CheckpointC3Id = "checkpoint-c3";

    private const string MarshalM1Id = "marshal-m1";
    private const string MarshalM2Id = "marshal-m2";
    private const string MarshalM3aId = "marshal-m3a";
    private const string MarshalM3bId = "marshal-m3b";
    private const string MarshalM4Id = "marshal-m4";
    private const string MarshalM5Id = "marshal-m5";

    private const string PersonM1Id = "person-m1";
    private const string PersonM2Id = "person-m2";
    private const string PersonM3aId = "person-m3a";
    private const string PersonM3bId = "person-m3b";
    private const string PersonM4Id = "person-m4";
    private const string PersonM5Id = "person-m5";
    private const string PersonL1Id = "person-l1";
    private const string PersonAdmin1Id = "person-admin1";

    // Area polygon for A1 (simple square around coordinates 51.5, -0.1)
    private static readonly List<RoutePoint> AreaA1Polygon =
    [
        new RoutePoint(51.4, -0.2),
        new RoutePoint(51.4, 0.0),
        new RoutePoint(51.6, 0.0),
        new RoutePoint(51.6, -0.2)
    ];

    // Coordinate inside A1 polygon
    private const double InsideA1Lat = 51.5;
    private const double InsideA1Lng = -0.1;

    // Coordinate outside A1 polygon
    private const double OutsideA1Lat = 52.0;
    private const double OutsideA1Lng = 0.5;

    private List<LocationEntity> _checkpoints = null!;
    private List<AreaEntity> _areas = null!;
    private List<AssignmentEntity> _assignments = null!;
    private List<MarshalEntity> _marshals = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<IncidentFunctions>>();
        _mockIncidentRepository = new Mock<IIncidentRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockClaimsService = new Mock<ClaimsService>(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object
        );

        _functions = new IncidentFunctions(
            _mockLogger.Object,
            _mockIncidentRepository.Object,
            _mockLocationRepository.Object,
            _mockMarshalRepository.Object,
            _mockAssignmentRepository.Object,
            _mockAreaRepository.Object,
            _mockClaimsService.Object
        );

        SetupTestData();
        SetupRepositoryMocks();
    }

    private void SetupTestData()
    {
        // Create checkpoints
        // C1 is NOT in A1, C2 and C3 ARE in A1
        _checkpoints =
        [
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = CheckpointC1Id,
                EventId = EventId,
                Name = "Checkpoint 1",
                AreaIdsJson = "[]" // Not in any area
            },
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = CheckpointC2Id,
                EventId = EventId,
                Name = "Checkpoint 2",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaA1Id })
            },
            new LocationEntity
            {
                PartitionKey = EventId,
                RowKey = CheckpointC3Id,
                EventId = EventId,
                Name = "Checkpoint 3",
                AreaIdsJson = JsonSerializer.Serialize(new List<string> { AreaA1Id })
            }
        ];

        // Create area A1
        _areas =
        [
            new AreaEntity
            {
                PartitionKey = EventId,
                RowKey = AreaA1Id,
                EventId = EventId,
                Name = "Area 1",
                PolygonJson = JsonSerializer.Serialize(AreaA1Polygon)
            }
        ];

        // Create assignments
        _assignments =
        [
            // M1 at C1
            new AssignmentEntity { PartitionKey = EventId, RowKey = "assign-1", EventId = EventId, LocationId = CheckpointC1Id, MarshalId = MarshalM1Id },
            // M2 at C2
            new AssignmentEntity { PartitionKey = EventId, RowKey = "assign-2", EventId = EventId, LocationId = CheckpointC2Id, MarshalId = MarshalM2Id },
            // M3a at C3
            new AssignmentEntity { PartitionKey = EventId, RowKey = "assign-3a", EventId = EventId, LocationId = CheckpointC3Id, MarshalId = MarshalM3aId },
            // M3b at C3
            new AssignmentEntity { PartitionKey = EventId, RowKey = "assign-3b", EventId = EventId, LocationId = CheckpointC3Id, MarshalId = MarshalM3bId }
            // M4 and M5 are NOT assigned to any checkpoint
        ];

        // Create marshals
        _marshals =
        [
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM1Id, MarshalId = MarshalM1Id, EventId = EventId, Name = "Marshal M1", PersonId = PersonM1Id },
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM2Id, MarshalId = MarshalM2Id, EventId = EventId, Name = "Marshal M2", PersonId = PersonM2Id },
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM3aId, MarshalId = MarshalM3aId, EventId = EventId, Name = "Marshal M3a", PersonId = PersonM3aId },
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM3bId, MarshalId = MarshalM3bId, EventId = EventId, Name = "Marshal M3b", PersonId = PersonM3bId },
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM4Id, MarshalId = MarshalM4Id, EventId = EventId, Name = "Marshal M4", PersonId = PersonM4Id },
            new MarshalEntity { PartitionKey = EventId, RowKey = MarshalM5Id, MarshalId = MarshalM5Id, EventId = EventId, Name = "Marshal M5", PersonId = PersonM5Id }
        ];
    }

    private void SetupRepositoryMocks()
    {
        // Location repository
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(_checkpoints);

        foreach (LocationEntity checkpoint in _checkpoints)
        {
            _mockLocationRepository
                .Setup(r => r.GetAsync(EventId, checkpoint.RowKey))
                .ReturnsAsync(checkpoint);
        }

        // Area repository
        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(_areas);

        // Assignment repository
        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(_assignments);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM1Id))
            .ReturnsAsync(_assignments.Where(a => a.MarshalId == MarshalM1Id));

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM2Id))
            .ReturnsAsync(_assignments.Where(a => a.MarshalId == MarshalM2Id));

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM3aId))
            .ReturnsAsync(_assignments.Where(a => a.MarshalId == MarshalM3aId));

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM3bId))
            .ReturnsAsync(_assignments.Where(a => a.MarshalId == MarshalM3bId));

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM4Id))
            .ReturnsAsync(Enumerable.Empty<AssignmentEntity>());

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalM5Id))
            .ReturnsAsync(Enumerable.Empty<AssignmentEntity>());

        _mockAssignmentRepository
            .Setup(r => r.GetByLocationAsync(EventId, CheckpointC1Id))
            .ReturnsAsync(_assignments.Where(a => a.LocationId == CheckpointC1Id));

        _mockAssignmentRepository
            .Setup(r => r.GetByLocationAsync(EventId, CheckpointC2Id))
            .ReturnsAsync(_assignments.Where(a => a.LocationId == CheckpointC2Id));

        _mockAssignmentRepository
            .Setup(r => r.GetByLocationAsync(EventId, CheckpointC3Id))
            .ReturnsAsync(_assignments.Where(a => a.LocationId == CheckpointC3Id));

        // Marshal repository
        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(_marshals);
    }

    private void SetupClaimsForMarshal(string marshalId, string personId, string personName)
    {
        UserClaims claims = new UserClaims(
            PersonId: personId,
            PersonName: personName,
            PersonEmail: $"{personName.ToLower()}@test.com",
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodMarshalMagicCode,
            MarshalId: marshalId,
            EventRoles: []
        );

        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), EventId))
            .ReturnsAsync(claims);
    }

    private void SetupClaimsForAreaLead(string personId, string personName, List<string> areaIds)
    {
        UserClaims claims = new UserClaims(
            PersonId: personId,
            PersonName: personName,
            PersonEmail: $"{personName.ToLower()}@test.com",
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null,
            EventRoles: [new EventRoleInfo(Constants.RoleEventAreaLead, areaIds)]
        );

        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), EventId))
            .ReturnsAsync(claims);
    }

    private void SetupClaimsForAdmin(string personId, string personName)
    {
        UserClaims claims = new UserClaims(
            PersonId: personId,
            PersonName: personName,
            PersonEmail: $"{personName.ToLower()}@test.com",
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null,
            EventRoles: [new EventRoleInfo(Constants.RoleEventAdmin, [])]
        );

        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), EventId))
            .ReturnsAsync(claims);
    }

    private static IncidentEntity CreateIncident(
        string incidentId,
        string reportedByPersonId,
        string reportedByMarshalId,
        string reportedByName,
        string? checkpointId = null,
        string? checkpointName = null,
        List<string>? checkpointAreaIds = null,
        double? latitude = null,
        double? longitude = null)
    {
        IncidentContextSnapshot context = new IncidentContextSnapshot();
        if (checkpointId != null)
        {
            context.Checkpoint = new IncidentCheckpointSnapshot
            {
                CheckpointId = checkpointId,
                Name = checkpointName ?? "Checkpoint",
                AreaIds = checkpointAreaIds ?? []
            };
        }

        return new IncidentEntity
        {
            PartitionKey = EventId,
            RowKey = incidentId,
            EventId = EventId,
            IncidentId = incidentId,
            Title = "Test Incident",
            Description = "Test description",
            Severity = "medium",
            IncidentTime = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ReportedByPersonId = reportedByPersonId,
            ReportedByMarshalId = reportedByMarshalId,
            ReportedByName = reportedByName,
            Status = "open",
            ContextSnapshotJson = JsonSerializer.Serialize(context),
            UpdatesJson = "[]",
            Latitude = latitude,
            Longitude = longitude
        };
    }

    #region Test 1: M3a logs incident without checkpoint tag

    /// <summary>
    /// M3a logs an incident, but it is not tagged to a location or checkpoint.
    /// M1 and M2 should NOT be able to see the incident.
    /// M3a, M3b, L1, and Admin1 should all be able to see it.
    /// </summary>
    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_M3a_CanSee()
    {
        // Arrange
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
            // No checkpoint tagged
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM3aId, PersonM3aId, "Marshal M3a");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m3a", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
        response.Incidents[0].IncidentId.ShouldBe("incident-1");
    }

    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_M3b_CanSee()
    {
        // Arrange - M3b is at same checkpoint as M3a
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM3bId, PersonM3bId, "Marshal M3b");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m3b", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_M1_CannotSee()
    {
        // Arrange - M1 is at different checkpoint
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM1Id, PersonM1Id, "Marshal M1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_M2_CannotSee()
    {
        // Arrange - M2 is at different checkpoint (C2, not C3)
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM2Id, PersonM2Id, "Marshal M2");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m2", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_L1_CanSee()
    {
        // Arrange - L1 is area lead for A1 which contains C3 where M3a is assigned
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAreaLead(PersonL1Id, "Lead L1", [AreaA1Id]);
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-l1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_NotTaggedToCheckpoint_Admin1_CanSee()
    {
        // Arrange
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-1",
            reportedByPersonId: PersonM3aId,
            reportedByMarshalId: MarshalM3aId,
            reportedByName: "Marshal M3a"
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAdmin(PersonAdmin1Id, "Admin Admin1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-admin1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    #endregion

    #region Test 2: M4 logs incident tagged to C3

    /// <summary>
    /// M4 logs an incident and tags checkpoint C3.
    /// M3a, M3b, M4, Admin1 and L1 should be able to view it.
    /// M1, M2 and M5 cannot.
    /// </summary>
    [TestMethod]
    public async Task Incident_TaggedToC3_M3a_CanSee()
    {
        // Arrange - M4 reports incident tagged to C3, M3a is at C3
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM3aId, PersonM3aId, "Marshal M3a");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m3a", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_M3b_CanSee()
    {
        // Arrange
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM3bId, PersonM3bId, "Marshal M3b");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m3b", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_M4_CanSee()
    {
        // Arrange - M4 is the reporter
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM4Id, PersonM4Id, "Marshal M4");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m4", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_L1_CanSee()
    {
        // Arrange - L1 is area lead for A1 which contains C3
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAreaLead(PersonL1Id, "Lead L1", [AreaA1Id]);
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-l1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_Admin1_CanSee()
    {
        // Arrange
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAdmin(PersonAdmin1Id, "Admin Admin1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-admin1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_M1_CannotSee()
    {
        // Arrange - M1 is at C1, not related to C3
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM1Id, PersonM1Id, "Marshal M1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_M2_CannotSee()
    {
        // Arrange - M2 is at C2, not C3
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM2Id, PersonM2Id, "Marshal M2");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m2", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_TaggedToC3_M5_CannotSee()
    {
        // Arrange - M5 is unassigned
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-2",
            reportedByPersonId: PersonM4Id,
            reportedByMarshalId: MarshalM4Id,
            reportedByName: "Marshal M4",
            checkpointId: CheckpointC3Id,
            checkpointName: "Checkpoint 3",
            checkpointAreaIds: [AreaA1Id]
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM5Id, PersonM5Id, "Marshal M5");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m5", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    #endregion

    #region Test 3: M5 logs incident geographically inside A1

    /// <summary>
    /// M5 logs an incident, not attached to a checkpoint, but geographically inside A1.
    /// M5 and L1 and Admin1 can see the incident, no one else can.
    /// </summary>
    [TestMethod]
    public async Task Incident_GeographicallyInA1_M5_CanSee()
    {
        // Arrange - M5 reports incident, they are the reporter
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM5Id, PersonM5Id, "Marshal M5");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m5", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_GeographicallyInA1_L1_CanSee()
    {
        // Arrange - L1 is area lead for A1, incident is geographically in A1
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAreaLead(PersonL1Id, "Lead L1", [AreaA1Id]);
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-l1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_GeographicallyInA1_Admin1_CanSee()
    {
        // Arrange
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForAdmin(PersonAdmin1Id, "Admin Admin1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-admin1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Incident_GeographicallyInA1_M1_CannotSee()
    {
        // Arrange - M1 is at C1 which is NOT in A1, and not related to M5
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM1Id, PersonM1Id, "Marshal M1");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m1", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_GeographicallyInA1_M2_CannotSee()
    {
        // Arrange - M2 is at C2 (in A1), but not related to M5 or the incident location
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM2Id, PersonM2Id, "Marshal M2");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m2", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task Incident_GeographicallyInA1_M3a_CannotSee()
    {
        // Arrange - M3a is at C3 (in A1), but not related to M5 or the incident
        IncidentEntity incident = CreateIncident(
            incidentId: "incident-3",
            reportedByPersonId: PersonM5Id,
            reportedByMarshalId: MarshalM5Id,
            reportedByName: "Marshal M5",
            latitude: InsideA1Lat,
            longitude: InsideA1Lng
        );

        _mockIncidentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([incident]);

        SetupClaimsForMarshal(MarshalM3aId, PersonM3aId, "Marshal M3a");
        HttpRequest request = TestHelpers.CreateEmptyHttpRequestWithAuth("token-m3a", debug: true);

        // Act
        IActionResult result = await _functions.GetIncidents(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        IncidentsListResponse response = (IncidentsListResponse)okResult.Value!;

        response.Incidents.Count.ShouldBe(0);
    }

    #endregion
}
