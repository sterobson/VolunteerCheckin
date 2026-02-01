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
    public class CheckInFunctionsTests
    {
        private Mock<ILogger<CheckInFunctions>> _mockLogger = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<GpsService> _mockGpsService = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
        private Mock<IChecklistCompletionRepository> _mockChecklistCompletionRepository = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private Mock<IAreaRepository> _mockAreaRepository = null!;
        private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
        private CheckInFunctions _checkInFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<CheckInFunctions>>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockGpsService = new Mock<GpsService>();
            _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
            _mockChecklistCompletionRepository = new Mock<IChecklistCompletionRepository>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();
            _mockAreaRepository = new Mock<IAreaRepository>();
            _mockEventRoleRepository = new Mock<IEventRoleRepository>();
            _mockClaimsService = new Mock<ClaimsService>(
                Mock.Of<IAuthSessionRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IEventRoleRepository>(),
                Mock.Of<IMarshalRepository>(),
                Mock.Of<ISampleEventService>(),
                Mock.Of<IEventDeletionRepository>()
            );

            // Setup default GPS service behavior - close enough (within 100m)
            _mockGpsService
                .Setup(g => g.CalculateDistance(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(50.0); // Default: 50 meters away (within range)

            // Setup default checklist item repository to return empty list (no linked tasks)
            _mockChecklistItemRepository
                .Setup(r => r.GetByEventAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<ChecklistItemEntity>());

            _checkInFunctions = new CheckInFunctions(
                _mockLogger.Object,
                _mockAssignmentRepository.Object,
                _mockLocationRepository.Object,
                _mockGpsService.Object,
                _mockClaimsService.Object,
                _mockChecklistItemRepository.Object,
                _mockChecklistCompletionRepository.Object,
                _mockMarshalRepository.Object,
                _mockAreaRepository.Object,
                _mockEventRoleRepository.Object
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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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

            // Mock GPS service to return distance > 100m
            _mockGpsService
                .Setup(g => g.CalculateDistance(locationLat, locationLon, checkInLat, checkInLon))
                .Returns(500.0); // 500 meters away (too far)

            CheckInRequest request = new(eventId, assignmentId, checkInLat, checkInLon, false);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

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
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _checkInFunctions.CheckIn(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region AdminCheckIn Tests

        private UserClaims CreateAdminClaims(string eventId)
        {
            return new UserClaims(
                PersonId: "person-123",
                PersonName: "Admin User",
                PersonEmail: "admin@example.com",
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo> { new(Constants.RoleEventAdmin, new List<string>()) }
            );
        }

        private void SetupAdminAuth(string eventId)
        {
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), eventId))
                .ReturnsAsync(CreateAdminClaims(eventId));
        }

        [TestMethod]
        public async Task AdminCheckIn_NoAuth_ReturnsUnauthorized()
        {
            // Arrange
            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_InvalidSession_ReturnsUnauthorized()
        {
            // Arrange
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((UserClaims?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("invalid-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_MarshalAuth_ReturnsForbid()
        {
            // Arrange - user logged in via magic code (not elevated)
            UserClaims marshalClaims = new(
                PersonId: "person-123",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                IsSystemAdmin: false,
                EventId: "event-123",
                AuthMethod: Constants.AuthMethodMarshalMagicCode, // Not elevated!
                MarshalId: "marshal-123",
                EventRoles: new List<EventRoleInfo>()
            );

            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(marshalClaims);

            // Need to set up assignment and location mocks since auth check happens after retrieval
            AssignmentEntity assignment = new()
            {
                PartitionKey = "event-123",
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                MarshalId = "marshal-123",
                IsCheckedIn = false
            };
            LocationEntity location = new()
            {
                PartitionKey = "event-123",
                RowKey = "location-789",
                EventId = "event-123",
                Name = "Test Location",
                AreaIdsJson = "[]"
            };
            _mockAssignmentRepository
                .Setup(r => r.GetAsync("event-123", "assignment-456"))
                .ReturnsAsync(assignment);
            _mockLocationRepository
                .Setup(r => r.GetAsync("event-123", "location-789"))
                .ReturnsAsync(location);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("some-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<ForbidResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_NotEventAdmin_ReturnsForbid()
        {
            // Arrange - user is authenticated via email but not an event admin
            UserClaims nonAdminClaims = new(
                PersonId: "person-123",
                PersonName: "Regular User",
                PersonEmail: "user@example.com",
                IsSystemAdmin: false,
                EventId: "event-123",
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>() // No admin role
            );

            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(nonAdminClaims);

            // Need to set up assignment and location mocks since auth check happens after retrieval
            AssignmentEntity assignment = new()
            {
                PartitionKey = "event-123",
                RowKey = "assignment-456",
                EventId = "event-123",
                LocationId = "location-789",
                MarshalId = "marshal-123",
                IsCheckedIn = false
            };
            LocationEntity location = new()
            {
                PartitionKey = "event-123",
                RowKey = "location-789",
                EventId = "event-123",
                Name = "Test Location",
                AreaIdsJson = "[]"
            };
            _mockAssignmentRepository
                .Setup(r => r.GetAsync("event-123", "assignment-456"))
                .ReturnsAsync(assignment);
            _mockLocationRepository
                .Setup(r => r.GetAsync("event-123", "location-789"))
                .ReturnsAsync(location);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("some-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<ForbidResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_NotCheckedIn_ChecksIn()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assignment-456";

            SetupAdminAuth(eventId);

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

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

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

            SetupAdminAuth(eventId);

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

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

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
            SetupAdminAuth("event-123");

            _mockAssignmentRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AssignmentEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_LocationNotFound_ReturnsNotFound()
        {
            // Arrange
            SetupAdminAuth("event-123");

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

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, "event-123", "assignment-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        [TestMethod]
        public async Task AdminCheckIn_SystemAdmin_Succeeds()
        {
            // Arrange - system admin can check in for any event
            string eventId = "event-123";
            string assignmentId = "assignment-456";

            UserClaims systemAdminClaims = new(
                PersonId: "person-123",
                PersonName: "System Admin",
                PersonEmail: "sysadmin@example.com",
                IsSystemAdmin: true,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>() // No explicit event admin role needed
            );

            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), eventId))
                .ReturnsAsync(systemAdminClaims);

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

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _checkInFunctions.AdminCheckIn(httpRequest, eventId, assignmentId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        #endregion
    }
}
