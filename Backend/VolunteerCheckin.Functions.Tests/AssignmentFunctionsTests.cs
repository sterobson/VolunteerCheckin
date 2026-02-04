using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class AssignmentFunctionsTests
    {
        private Mock<ILogger<AssignmentFunctions>> _mockLogger = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<IAreaRepository> _mockAreaRepository = null!;
        private Mock<IEventRepository> _mockEventRepository = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private AssignmentFunctions _assignmentFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AssignmentFunctions>>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockAreaRepository = new Mock<IAreaRepository>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockClaimsService = new Mock<ClaimsService>(
                Mock.Of<IAuthSessionRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IEventRoleRepository>(),
                Mock.Of<IMarshalRepository>(),
                Mock.Of<ISampleEventService>(),
                Mock.Of<IEventDeletionRepository>()
            );

            // Setup default claims for authenticated requests
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string?>(), It.IsAny<string>()))
                .ReturnsAsync(new UserClaims(
                    PersonId: "test-person",
                    PersonName: "Test User",
                    PersonEmail: "test@example.com",
                    EventId: null,
                    AuthMethod: Constants.AuthMethodSecureEmailLink,
                    MarshalId: null,
                    EventRoles: new List<EventRoleInfo> { new(Constants.RoleEventAdmin, new List<string>()) }
                ));

            _assignmentFunctions = new AssignmentFunctions(
                _mockLogger.Object,
                _mockAssignmentRepository.Object,
                _mockMarshalRepository.Object,
                _mockLocationRepository.Object,
                _mockAreaRepository.Object,
                _mockEventRepository.Object,
                _mockClaimsService.Object
            );
        }

        #region CreateAssignment Tests

        [TestMethod]
        public async Task CreateAssignment_WithExistingMarshalId_CreatesAssignment()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";
            string locationId = "loc-789";

            MarshalEntity marshal = new()
            {
                MarshalId = marshalId,
                EventId = eventId,
                Name = "John Doe"
            };

            CreateAssignmentRequest request = new(eventId, locationId, marshalId, null);

            _mockMarshalRepository
                .Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(marshal);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _assignmentFunctions.CreateAssignment(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAssignmentRepository.Verify(
                r => r.AddAsync(It.Is<AssignmentEntity>(a =>
                    a.EventId == eventId &&
                    a.LocationId == locationId &&
                    a.MarshalId == marshalId &&
                    a.MarshalName == "John Doe"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateAssignment_WithNewMarshalName_CreatesMarshalAndAssignment()
        {
            // Arrange
            string eventId = "event-123";
            string locationId = "loc-789";
            string marshalName = "New Marshal";

            CreateAssignmentRequest request = new(eventId, locationId, null, marshalName);

            _mockMarshalRepository
                .Setup(r => r.FindByNameAsync(eventId, marshalName))
                .ReturnsAsync((MarshalEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _assignmentFunctions.CreateAssignment(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify new marshal was created
            _mockMarshalRepository.Verify(
                r => r.AddAsync(It.Is<MarshalEntity>(m =>
                    m.Name == marshalName &&
                    m.EventId == eventId
                )),
                Times.Once
            );

            // Verify assignment was created
            _mockAssignmentRepository.Verify(
                r => r.AddAsync(It.IsAny<AssignmentEntity>()),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateAssignment_WithExistingMarshalName_UsesExistingMarshal()
        {
            // Arrange
            string eventId = "event-123";
            string locationId = "loc-789";
            string marshalName = "Existing Marshal";

            MarshalEntity existingMarshal = new()
            {
                MarshalId = "existing-marshal-id",
                EventId = eventId,
                Name = marshalName
            };

            CreateAssignmentRequest request = new(eventId, locationId, null, marshalName);

            _mockMarshalRepository
                .Setup(r => r.FindByNameAsync(eventId, marshalName))
                .ReturnsAsync(existingMarshal);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _assignmentFunctions.CreateAssignment(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify no new marshal was created
            _mockMarshalRepository.Verify(
                r => r.AddAsync(It.IsAny<MarshalEntity>()),
                Times.Never
            );

            // Verify assignment uses existing marshal
            _mockAssignmentRepository.Verify(
                r => r.AddAsync(It.Is<AssignmentEntity>(a =>
                    a.MarshalId == "existing-marshal-id"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateAssignment_MarshalNotFound_ReturnsBadRequest()
        {
            // Arrange
            CreateAssignmentRequest request = new("event-123", "loc-789", "nonexistent-marshal", null);

            _mockMarshalRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((MarshalEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _assignmentFunctions.CreateAssignment(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task CreateAssignment_NoMarshalIdOrName_ReturnsBadRequest()
        {
            // Arrange
            CreateAssignmentRequest request = new("event-123", "loc-789", null, null);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _assignmentFunctions.CreateAssignment(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetAssignment Tests

        [TestMethod]
        public async Task GetAssignment_Exists_ReturnsAssignment()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assign-456";

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    RowKey = assignmentId,
                    EventId = eventId,
                    LocationId = "loc-1",
                    MarshalId = "marshal-1",
                    MarshalName = "John Doe",
                    IsCheckedIn = false
                }
            };

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _assignmentFunctions.GetAssignment(httpRequest, eventId, assignmentId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetAssignment_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<AssignmentEntity>());

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _assignmentFunctions.GetAssignment(httpRequest, "event-123", "assign-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetAssignmentsByEvent Tests

        [TestMethod]
        public async Task GetAssignmentsByEvent_ReturnsAllAssignments()
        {
            // Arrange
            string eventId = "event-123";

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    RowKey = "assign-1",
                    EventId = eventId,
                    LocationId = "loc-1",
                    MarshalId = "marshal-1",
                    MarshalName = "John Doe"
                },
                new AssignmentEntity
                {
                    RowKey = "assign-2",
                    EventId = eventId,
                    LocationId = "loc-2",
                    MarshalId = "marshal-2",
                    MarshalName = "Jane Smith"
                }
            };

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _assignmentFunctions.GetAssignmentsByEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<AssignmentResponse>? assignmentList = okResult.Value as List<AssignmentResponse>;
            assignmentList.ShouldNotBeNull();
            assignmentList.Count.ShouldBe(2);
        }

        #endregion

        #region DeleteAssignment Tests

        [TestMethod]
        public async Task DeleteAssignment_DeletesAssignment()
        {
            // Arrange
            string eventId = "event-123";
            string assignmentId = "assign-456";

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _assignmentFunctions.DeleteAssignment(httpRequest, eventId, assignmentId);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockAssignmentRepository.Verify(
                r => r.DeleteAsync(eventId, assignmentId),
                Times.Once
            );
        }

        #endregion

        #region GetEventStatus Tests

        [TestMethod]
        public async Task GetEventStatus_ReturnsLocationStatusesWithAssignments()
        {
            // Arrange
            string eventId = "event-123";

            EventEntity eventEntity = new()
            {
                RowKey = eventId,
                PartitionKey = eventId,
                Name = "Test Event",
                Description = "Test event description",
                DefaultCheckpointStyleType = "default",
                DefaultCheckpointStyleColor = ""
            };

            List<AreaEntity> areas = new()
            {
                new AreaEntity
                {
                    RowKey = "area-1",
                    PartitionKey = eventId,
                    EventId = eventId,
                    Name = "Default Area",
                    IsDefault = true,
                    PolygonJson = "[]",
                    ContactsJson = "[]"
                }
            };

            List<LocationEntity> locations = new()
            {
                new LocationEntity
                {
                    RowKey = "loc-1",
                    EventId = eventId,
                    Name = "Checkpoint 1",
                    Description = "First checkpoint",
                    Latitude = 47.6062,
                    Longitude = -122.3321,
                    RequiredMarshals = 2,
                    AreaIdsJson = "[\"area-1\"]"
                }
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    RowKey = "assign-1",
                    EventId = eventId,
                    LocationId = "loc-1",
                    MarshalId = "marshal-1",
                    MarshalName = "John Doe",
                    IsCheckedIn = true
                }
            };

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(eventEntity);

            _mockAreaRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(areas);

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(locations);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _assignmentFunctions.GetEventStatus(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();
        }

        #endregion
    }
}
