using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class CheckInFunctionsTests
    {
        private Mock<ILogger<CheckInFunctions>> _mockLogger = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private CheckInFunctions _checkInFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckInFunctions>>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();

            _checkInFunctions = new CheckInFunctions(
                _mockLogger.Object,
                _mockAssignmentRepository.Object,
                _mockLocationRepository.Object
            );
        }

        #region CheckIn Tests

        [TestMethod]
        public async Task CheckIn_WithValidGpsCoordinates_Succeeds()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";
            double locationLat = 47.6062;
            double locationLon = -122.3321;
            double checkInLat = 47.6063; // Very close
            double checkInLon = -122.3320;

            AssignmentEntity assignment = new()
            {
                RowKey = assignmentId,
                EventId = eventId,
                LocationId = "location-789",
                MarshalName = "John Doe",
                IsCheckedIn = false
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = eventId,
                Name = "Checkpoint 1",
                Latitude = locationLat,
                Longitude = locationLon,
                CheckedInCount = 0
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(eventId, assignmentId))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(eventId, "location-789"))
                .ReturnsAsync(location);

            CheckInRequest request = new(eventId, assignmentId, checkInLat, checkInLon, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify assignment was updated
            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.Is<AssignmentEntity>(a =>
                    a.IsCheckedIn == true &&
                    a.CheckInLatitude == checkInLat &&
                    a.CheckInLongitude == checkInLon &&
                    a.CheckInMethod == "GPS"
                )),
                Times.Once
            );

            // Verify location count was incremented
            _mockLocationRepository.Verify(
                r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                    l.CheckedInCount == 1
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CheckIn_WithGpsTooFarAway_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";
            double locationLat = 47.6062;
            double locationLon = -122.3321;
            double checkInLat = 47.6100; // Far away (>100m)
            double checkInLon = -122.3400;

            AssignmentEntity assignment = new()
            {
                RowKey = assignmentId,
                EventId = eventId,
                LocationId = "location-789",
                IsCheckedIn = false
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = eventId,
                Latitude = locationLat,
                Longitude = locationLon
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(eventId, assignmentId))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(eventId, "location-789"))
                .ReturnsAsync(location);

            CheckInRequest request = new(eventId, assignmentId, checkInLat, checkInLon, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();

            // Verify assignment was NOT updated
            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.IsAny<AssignmentEntity>()),
                Times.Never
            );
        }

        [TestMethod]
        public async Task CheckIn_WithManualCheckIn_Succeeds()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";

            AssignmentEntity assignment = new()
            {
                RowKey = assignmentId,
                EventId = eventId,
                LocationId = "location-789",
                MarshalName = "John Doe",
                IsCheckedIn = false
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = eventId,
                Name = "Checkpoint 1",
                CheckedInCount = 0
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(eventId, assignmentId))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(eventId, "location-789"))
                .ReturnsAsync(location);

            CheckInRequest request = new(eventId, assignmentId, null, null, true);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.Is<AssignmentEntity>(a =>
                    a.IsCheckedIn == true &&
                    a.CheckInMethod == "Manual"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CheckIn_AssignmentNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AssignmentEntity?)null);

            CheckInRequest request = new("event-123", "assignment-456", 47.6062, -122.3321, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        [TestMethod]
        public async Task CheckIn_AlreadyCheckedIn_ReturnsBadRequest()
        {
            // Arrange
            AssignmentEntity assignment = new()
            {
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                IsCheckedIn = true
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(assignment);

            CheckInRequest request = new("event-123", "assignment-456", 47.6062, -122.3321, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task CheckIn_LocationNotFound_ReturnsNotFound()
        {
            // Arrange
            AssignmentEntity assignment = new()
            {
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                IsCheckedIn = false
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((LocationEntity?)null);

            CheckInRequest request = new("event-123", "assignment-456", 47.6062, -122.3321, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        [TestMethod]
        public async Task CheckIn_NoGpsAndNotManual_ReturnsBadRequest()
        {
            // Arrange
            AssignmentEntity assignment = new()
            {
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                IsCheckedIn = false
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = "event-123"
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(location);

            CheckInRequest request = new("event-123", "assignment-456", null, null, false);
            HttpRequest httpRequest = CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region AdminCheckIn Tests

        [TestMethod]
        public async Task AdminCheckIn_NotCheckedIn_ChecksIn()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";

            AssignmentEntity assignment = new()
            {
                RowKey = assignmentId,
                EventId = eventId,
                LocationId = "location-789",
                IsCheckedIn = false
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = eventId,
                CheckedInCount = 0
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(eventId, assignmentId))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(eventId, "location-789"))
                .ReturnsAsync(location);

            DefaultHttpContext context = new();
            HttpRequest httpRequest = context.Request;

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, eventId, assignmentId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.Is<AssignmentEntity>(a =>
                    a.IsCheckedIn == true &&
                    a.CheckInMethod == "Admin"
                )),
                Times.Once
            );

            _mockLocationRepository.Verify(
                r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                    l.CheckedInCount == 1
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task AdminCheckIn_AlreadyCheckedIn_ChecksOut()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";

            AssignmentEntity assignment = new()
            {
                RowKey = assignmentId,
                EventId = eventId,
                LocationId = "location-789",
                IsCheckedIn = true,
                CheckInTime = DateTime.UtcNow,
                CheckInMethod = "GPS"
            };

            LocationEntity location = new()
            {
                RowKey = "location-789",
                EventId = eventId,
                CheckedInCount = 1
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(eventId, assignmentId))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(eventId, "location-789"))
                .ReturnsAsync(location);

            DefaultHttpContext context = new();
            HttpRequest httpRequest = context.Request;

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, eventId, assignmentId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.Is<AssignmentEntity>(a =>
                    a.IsCheckedIn == false &&
                    a.CheckInTime == null &&
                    a.CheckInMethod == string.Empty
                )),
                Times.Once
            );

            _mockLocationRepository.Verify(
                r => r.UpdateAsync(It.Is<LocationEntity>(l =>
                    l.CheckedInCount == 0
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task AdminCheckIn_AssignmentNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AssignmentEntity?)null);

            DefaultHttpContext context = new();
            HttpRequest httpRequest = context.Request;

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_LocationNotFound_ReturnsNotFound()
        {
            // Arrange
            AssignmentEntity assignment = new()
            {
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                IsCheckedIn = false
            };

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(assignment);

            _mockLocationRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((LocationEntity?)null);

            DefaultHttpContext context = new();
            HttpRequest httpRequest = context.Request;

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        private static HttpRequest CreateHttpRequest(object body)
        {
            DefaultHttpContext context = new();
            HttpRequest request = context.Request;

            string json = JsonSerializer.Serialize(body);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            request.Body = new MemoryStream(bytes);

            return request;
        }
    }
}
