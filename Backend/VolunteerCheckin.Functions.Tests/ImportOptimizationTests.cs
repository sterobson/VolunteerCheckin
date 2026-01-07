using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests verifying that CSV imports use preloaded caches instead of per-row queries.
/// These tests ensure that:
/// 1. Marshal/Location data is loaded once at the start of import
/// 2. FindByNameAsync is not called during import (uses cache instead)
/// 3. New entities created during import are added to cache for reuse
/// </summary>
[TestClass]
public class ImportOptimizationTests
{
    private const string EventId = "test-event-123";

    #region LocationFunctions.ImportLocations Tests

    [TestMethod]
    public async Task ImportLocations_ShouldPreloadMarshals_AndNotCallFindByNameAsync()
    {
        // Arrange
        Mock<ILocationRepository> mockLocationRepo = new();
        Mock<IMarshalRepository> mockMarshalRepo = new();
        Mock<IAssignmentRepository> mockAssignmentRepo = new();
        Mock<IAreaRepository> mockAreaRepo = new();
        Mock<IChecklistItemRepository> mockChecklistItemRepo = new();
        Mock<INoteRepository> mockNoteRepo = new();
        Mock<ILogger<LocationFunctions>> mockLogger = new();
        Mock<ClaimsService> mockClaimsService = new(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object);

        // Setup existing marshal that should be found via cache
        MarshalEntity existingMarshal = new()
        {
            PartitionKey = EventId,
            RowKey = "marshal-1",
            EventId = EventId,
            MarshalId = "marshal-1",
            Name = "John Smith"
        };

        mockLocationRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<LocationEntity>());

        mockMarshalRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity> { existingMarshal });

        mockMarshalRepo
            .Setup(r => r.AddAsync(It.IsAny<MarshalEntity>()))
            .ReturnsAsync((MarshalEntity m) => m);

        mockLocationRepo
            .Setup(r => r.AddAsync(It.IsAny<LocationEntity>()))
            .ReturnsAsync((LocationEntity l) => l);

        mockAssignmentRepo
            .Setup(r => r.AddAsync(It.IsAny<AssignmentEntity>()))
            .ReturnsAsync((AssignmentEntity a) => a);

        Mock<IEventRoleRepository> mockEventRoleRepo = new();

        LocationFunctions functions = new(
            mockLogger.Object,
            mockLocationRepo.Object,
            mockMarshalRepo.Object,
            mockAssignmentRepo.Object,
            mockChecklistItemRepo.Object,
            mockNoteRepo.Object,
            mockAreaRepo.Object,
            mockEventRoleRepo.Object,
            mockClaimsService.Object);

        // Create CSV with two locations, both referencing the same marshal
        string csvContent = "Name,Latitude,Longitude,Marshal1\nLocation A,51.5,-0.1,John Smith\nLocation B,51.6,-0.2,John Smith";
        HttpRequest request = CreateCsvRequest(csvContent, EventId);

        // Act
        IActionResult result = await functions.ImportLocations(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Verify GetByEventAsync was called exactly once for marshals (preload)
        mockMarshalRepo.Verify(r => r.GetByEventAsync(EventId), Times.Once);

        // Verify FindByNameAsync was NEVER called (should use cache)
        mockMarshalRepo.Verify(r => r.FindByNameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Verify only two assignments were created (one per location, same marshal)
        mockAssignmentRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentEntity>()), Times.Exactly(2));

        // Verify no new marshal was created (should have found existing via cache)
        mockMarshalRepo.Verify(r => r.AddAsync(It.IsAny<MarshalEntity>()), Times.Never);
    }

    [TestMethod]
    public async Task ImportLocations_ShouldCreateNewMarshal_AndAddToCache()
    {
        // Arrange
        Mock<ILocationRepository> mockLocationRepo = new();
        Mock<IMarshalRepository> mockMarshalRepo = new();
        Mock<IAssignmentRepository> mockAssignmentRepo = new();
        Mock<IAreaRepository> mockAreaRepo = new();
        Mock<IChecklistItemRepository> mockChecklistItemRepo = new();
        Mock<INoteRepository> mockNoteRepo = new();
        Mock<ILogger<LocationFunctions>> mockLogger = new();
        Mock<ClaimsService> mockClaimsService = new(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object);

        mockLocationRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<LocationEntity>());

        // No existing marshals - all will be created
        mockMarshalRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        mockMarshalRepo
            .Setup(r => r.AddAsync(It.IsAny<MarshalEntity>()))
            .ReturnsAsync((MarshalEntity m) => m);

        mockLocationRepo
            .Setup(r => r.AddAsync(It.IsAny<LocationEntity>()))
            .ReturnsAsync((LocationEntity l) => l);

        mockAssignmentRepo
            .Setup(r => r.AddAsync(It.IsAny<AssignmentEntity>()))
            .ReturnsAsync((AssignmentEntity a) => a);

        Mock<IEventRoleRepository> mockEventRoleRepo = new();

        LocationFunctions functions = new(
            mockLogger.Object,
            mockLocationRepo.Object,
            mockMarshalRepo.Object,
            mockAssignmentRepo.Object,
            mockChecklistItemRepo.Object,
            mockNoteRepo.Object,
            mockAreaRepo.Object,
            mockEventRoleRepo.Object,
            mockClaimsService.Object);

        // Create CSV with three locations, two referencing "New Marshal"
        // The cache should prevent creating duplicate marshals
        string csvContent = "Name,Latitude,Longitude,Marshal1\nLocation A,51.5,-0.1,New Marshal\nLocation B,51.6,-0.2,New Marshal\nLocation C,51.7,-0.3,Another Marshal";
        HttpRequest request = CreateCsvRequest(csvContent, EventId);

        // Act
        IActionResult result = await functions.ImportLocations(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Verify FindByNameAsync was NEVER called
        mockMarshalRepo.Verify(r => r.FindByNameAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

        // Verify only 2 marshals were created (not 3), because "New Marshal" should be cached
        mockMarshalRepo.Verify(r => r.AddAsync(It.IsAny<MarshalEntity>()), Times.Exactly(2));

        // Verify 3 assignments were created (one per location)
        mockAssignmentRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentEntity>()), Times.Exactly(3));
    }

    #endregion

    #region MarshalFunctions.ImportMarshals Tests

    [TestMethod]
    public async Task ImportMarshals_ShouldPreloadLocations_AndNotCallGetByEventForEachRow()
    {
        // Arrange
        Mock<IMarshalRepository> mockMarshalRepo = new();
        Mock<ILocationRepository> mockLocationRepo = new();
        Mock<IAssignmentRepository> mockAssignmentRepo = new();
        Mock<IEventRepository> mockEventRepo = new();
        Mock<IChecklistItemRepository> mockChecklistItemRepo = new();
        Mock<INoteRepository> mockNoteRepo = new();
        Mock<ILogger<MarshalFunctions>> mockLogger = new();

        Mock<ClaimsService> mockClaimsService = new(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object);

        Mock<ContactPermissionService> mockContactPermission = new(
            MockBehavior.Loose,
            new Mock<ILocationRepository>().Object,
            new Mock<IAssignmentRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object);

        // Setup existing location that should be found via cache
        LocationEntity existingLocation = new()
        {
            PartitionKey = EventId,
            RowKey = "location-1",
            EventId = EventId,
            Name = "Checkpoint Alpha"
        };

        mockMarshalRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        mockLocationRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<LocationEntity> { existingLocation });

        mockMarshalRepo
            .Setup(r => r.AddAsync(It.IsAny<MarshalEntity>()))
            .ReturnsAsync((MarshalEntity m) => m);

        mockAssignmentRepo
            .Setup(r => r.ExistsAsync(EventId, It.IsAny<string>(), "location-1"))
            .ReturnsAsync(false);

        mockAssignmentRepo
            .Setup(r => r.AddAsync(It.IsAny<AssignmentEntity>()))
            .ReturnsAsync((AssignmentEntity a) => a);

        // Setup auth
        mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), EventId))
            .ReturnsAsync(new UserClaims(
                PersonId: "person-1",
                PersonName: "Admin",
                PersonEmail: "admin@test.com",
                IsSystemAdmin: true,
                EventId: EventId,
                AuthMethod: "SecureEmailLink",
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>()));

        MarshalFunctions functions = new(
            mockLogger.Object,
            mockMarshalRepo.Object,
            mockLocationRepo.Object,
            mockAssignmentRepo.Object,
            mockEventRepo.Object,
            mockChecklistItemRepo.Object,
            mockNoteRepo.Object,
            mockClaimsService.Object,
            mockContactPermission.Object);

        // Create CSV with three marshals, all assigned to the same checkpoint
        string csvContent = "Name,Email,Phone,Checkpoint\nMarshal A,a@test.com,123,Checkpoint Alpha\nMarshal B,b@test.com,456,Checkpoint Alpha\nMarshal C,c@test.com,789,Checkpoint Alpha";
        HttpRequest request = CreateCsvRequest(csvContent, EventId, includeAuth: true);

        // Act
        IActionResult result = await functions.ImportMarshals(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Verify GetByEventAsync for locations was called exactly once (preload)
        mockLocationRepo.Verify(r => r.GetByEventAsync(EventId), Times.Once);

        // Verify 3 marshals were created
        mockMarshalRepo.Verify(r => r.AddAsync(It.IsAny<MarshalEntity>()), Times.Exactly(3));

        // Verify 3 assignments were created
        mockAssignmentRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentEntity>()), Times.Exactly(3));
    }

    [TestMethod]
    public async Task ImportMarshals_ShouldReportMissingCheckpoints_UsingCache()
    {
        // Arrange
        Mock<IMarshalRepository> mockMarshalRepo = new();
        Mock<ILocationRepository> mockLocationRepo = new();
        Mock<IAssignmentRepository> mockAssignmentRepo = new();
        Mock<IEventRepository> mockEventRepo = new();
        Mock<IChecklistItemRepository> mockChecklistItemRepo = new();
        Mock<INoteRepository> mockNoteRepo = new();
        Mock<ILogger<MarshalFunctions>> mockLogger = new();

        Mock<ClaimsService> mockClaimsService = new(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object);

        Mock<ContactPermissionService> mockContactPermission = new(
            MockBehavior.Loose,
            new Mock<ILocationRepository>().Object,
            new Mock<IAssignmentRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object);

        // No locations exist
        mockMarshalRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        mockLocationRepo
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<LocationEntity>());

        mockMarshalRepo
            .Setup(r => r.AddAsync(It.IsAny<MarshalEntity>()))
            .ReturnsAsync((MarshalEntity m) => m);

        // Setup auth
        mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), EventId))
            .ReturnsAsync(new UserClaims(
                PersonId: "person-1",
                PersonName: "Admin",
                PersonEmail: "admin@test.com",
                IsSystemAdmin: true,
                EventId: EventId,
                AuthMethod: "SecureEmailLink",
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>()));

        MarshalFunctions functions = new(
            mockLogger.Object,
            mockMarshalRepo.Object,
            mockLocationRepo.Object,
            mockAssignmentRepo.Object,
            mockEventRepo.Object,
            mockChecklistItemRepo.Object,
            mockNoteRepo.Object,
            mockClaimsService.Object,
            mockContactPermission.Object);

        // Create CSV with marshals assigned to non-existent checkpoints
        string csvContent = "Name,Email,Phone,Checkpoint\nMarshal A,a@test.com,123,Missing Checkpoint";
        HttpRequest request = CreateCsvRequest(csvContent, EventId, includeAuth: true);

        // Act
        IActionResult result = await functions.ImportMarshals(request, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        ImportMarshalsResponse response = (ImportMarshalsResponse)okResult.Value!;

        // Verify error was reported for missing checkpoint
        response.Errors.ShouldContain(e => e.Contains("Missing Checkpoint") && e.Contains("not found"));

        // Verify location repo was only called once (preload), not per-row
        mockLocationRepo.Verify(r => r.GetByEventAsync(EventId), Times.Once);

        // No assignments should be created since checkpoint doesn't exist
        mockAssignmentRepo.Verify(r => r.AddAsync(It.IsAny<AssignmentEntity>()), Times.Never);
    }

    #endregion

    #region Helper Methods

    private static HttpRequest CreateCsvRequest(string csvContent, string eventId, bool includeAuth = false)
    {
        DefaultHttpContext httpContext = new();
        HttpRequest request = httpContext.Request;

        // Create form with CSV file
        byte[] csvBytes = Encoding.UTF8.GetBytes(csvContent);
        MemoryStream stream = new(csvBytes);
        FormFile file = new(stream, 0, csvBytes.Length, "file", "import.csv");

        request.Form = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(),
            new FormFileCollection { file });

        if (includeAuth)
        {
            request.Headers.Append("Authorization", "Bearer test-token");
        }

        return request;
    }

    #endregion
}
