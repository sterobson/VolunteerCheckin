using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class LocationFunctionsTests
    {
    private Mock<ILogger<LocationFunctions>> _mockLogger = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
    private Mock<INoteRepository> _mockNoteRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private LocationFunctions _locationFunctions = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<LocationFunctions>>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
        _mockNoteRepository = new Mock<INoteRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockEventRoleRepository = new Mock<IEventRoleRepository>();
        _mockClaimsService = new Mock<ClaimsService>(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object
        );

        _locationFunctions = new LocationFunctions(
            _mockLogger.Object,
            _mockLocationRepository.Object,
            _mockMarshalRepository.Object,
            _mockAssignmentRepository.Object,
            _mockChecklistItemRepository.Object,
            _mockNoteRepository.Object,
            _mockAreaRepository.Object,
            _mockEventRoleRepository.Object,
            _mockClaimsService.Object
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_WithPositiveTimeDelta_UpdatesAllCheckpointTimes()
    {
        // Arrange
        string eventId = "event-123";
        TimeSpan timeDelta = TimeSpan.FromHours(2); // Move forward 2 hours

        List<LocationEntity> locations = new()
        {
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-1",
                Name = "Checkpoint 1",
                StartTime = new DateTime(2025, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2025, 1, 15, 10, 0, 0)
            },
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-2",
                Name = "Checkpoint 2",
                StartTime = new DateTime(2025, 1, 15, 11, 0, 0),
                EndTime = new DateTime(2025, 1, 15, 12, 0, 0)
            }
        };

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;

        okResult.Value.ShouldNotBeNull();
        int updatedCount = (int)okResult.Value!.GetType().GetProperty("updatedCount")!.GetValue(okResult.Value!)!;
        updatedCount.ShouldBe(2);

        // Verify that each location's times were updated correctly
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-1" &&
                l.StartTime == new DateTime(2025, 1, 15, 11, 0, 0) &&
                l.EndTime == new DateTime(2025, 1, 15, 12, 0, 0)
            )),
            Times.Once
        );

        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-2" &&
                l.StartTime == new DateTime(2025, 1, 15, 13, 0, 0) &&
                l.EndTime == new DateTime(2025, 1, 15, 14, 0, 0)
            )),
            Times.Once
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_WithNegativeTimeDelta_ShiftsTimesBackward()
    {
        // Arrange
        string eventId = "event-456";
        TimeSpan timeDelta = TimeSpan.FromHours(-3); // Move backward 3 hours

        List<LocationEntity> locations = new()
        {
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-1",
                Name = "Checkpoint 1",
                StartTime = new DateTime(2025, 1, 15, 12, 0, 0),
                EndTime = new DateTime(2025, 1, 15, 13, 0, 0)
            }
        };

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-1" &&
                l.StartTime == new DateTime(2025, 1, 15, 9, 0, 0) &&
                l.EndTime == new DateTime(2025, 1, 15, 10, 0, 0)
            )),
            Times.Once
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_LocationsWithoutTimes_DoesNotUpdate()
    {
        // Arrange
        string eventId = "event-789";
        TimeSpan timeDelta = TimeSpan.FromHours(1);

        List<LocationEntity> locations = new()
        {
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-1",
                Name = "Checkpoint without times",
                StartTime = null,
                EndTime = null
            }
        };

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;

        okResult.Value.ShouldNotBeNull();
        int updatedCount = (int)okResult.Value!.GetType().GetProperty("updatedCount")!.GetValue(okResult.Value!)!;
        updatedCount.ShouldBe(0);

        // Verify UpdateAsync was never called for locations without times
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.IsAny<LocationEntity>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_MixedScenario_OnlyUpdatesLocationsWithTimes()
    {
        // Arrange
        string eventId = "event-mixed";
        TimeSpan timeDelta = TimeSpan.FromMinutes(30);

        List<LocationEntity> locations = new()
        {
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-1",
                Name = "Has both times",
                StartTime = new DateTime(2025, 1, 15, 9, 0, 0),
                EndTime = new DateTime(2025, 1, 15, 10, 0, 0)
            },
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-2",
                Name = "Has only start time",
                StartTime = new DateTime(2025, 1, 15, 11, 0, 0),
                EndTime = null
            },
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-3",
                Name = "Has only end time",
                StartTime = null,
                EndTime = new DateTime(2025, 1, 15, 12, 0, 0)
            },
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-4",
                Name = "Has no times",
                StartTime = null,
                EndTime = null
            }
        };

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;

        okResult.Value.ShouldNotBeNull();
        int updatedCount = (int)okResult.Value!.GetType().GetProperty("updatedCount")!.GetValue(okResult.Value!)!;
        updatedCount.ShouldBe(3); // loc-1, loc-2, loc-3

        // Verify loc-1: both times updated
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-1" &&
                l.StartTime == new DateTime(2025, 1, 15, 9, 30, 0) &&
                l.EndTime == new DateTime(2025, 1, 15, 10, 30, 0)
            )),
            Times.Once
        );

        // Verify loc-2: only start time updated
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-2" &&
                l.StartTime == new DateTime(2025, 1, 15, 11, 30, 0) &&
                l.EndTime == null
            )),
            Times.Once
        );

        // Verify loc-3: only end time updated
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-3" &&
                l.StartTime == null &&
                l.EndTime == new DateTime(2025, 1, 15, 12, 30, 0)
            )),
            Times.Once
        );

        // Verify loc-4 was not updated (no times)
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l => l.RowKey == "loc-4")),
            Times.Never
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_InvalidRequest_ReturnsError()
    {
        // Arrange
        string eventId = "event-invalid";
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest("invalid json content");

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert - Invalid JSON causes an exception which returns StatusCodeResult(500)
        result.ShouldBeOfType<StatusCodeResult>();
        StatusCodeResult statusResult = (StatusCodeResult)result;
        statusResult.StatusCode.ShouldBe(500);

        // Verify no updates were attempted
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.IsAny<LocationEntity>()),
            Times.Never
        );
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_EmptyEventId_ProcessesCorrectly()
    {
        // Arrange
        string eventId = "empty-event";
        TimeSpan timeDelta = TimeSpan.FromHours(1);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(new List<LocationEntity>());

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;

        okResult.Value.ShouldNotBeNull();
        int updatedCount = (int)okResult.Value!.GetType().GetProperty("updatedCount")!.GetValue(okResult.Value!)!;
        updatedCount.ShouldBe(0);
    }

    [TestMethod]
    public async Task BulkUpdateLocationTimes_ZeroTimeDelta_UpdatesWithoutChange()
    {
        // Arrange
        string eventId = "event-zero";
        TimeSpan timeDelta = TimeSpan.Zero;
        DateTime originalStartTime = new DateTime(2025, 1, 15, 9, 0, 0);
        DateTime originalEndTime = new DateTime(2025, 1, 15, 10, 0, 0);

        List<LocationEntity> locations = new()
        {
            new LocationEntity
            {
                EventId = eventId,
                RowKey = "loc-1",
                Name = "Checkpoint 1",
                StartTime = originalStartTime,
                EndTime = originalEndTime
            }
        };

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(eventId))
            .ReturnsAsync(locations);

        BulkUpdateLocationTimesRequest request = new(timeDelta);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _locationFunctions.BulkUpdateLocationTimes(httpRequest, eventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Times should remain the same
        _mockLocationRepository.Verify(
            r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                l.RowKey == "loc-1" &&
                l.StartTime == originalStartTime &&
                l.EndTime == originalEndTime
            )),
            Times.Once
        );
    }

    }
}
