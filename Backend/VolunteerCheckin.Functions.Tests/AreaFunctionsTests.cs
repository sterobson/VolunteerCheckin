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
    public class AreaFunctionsTests
    {
        private Mock<ILogger<AreaFunctions>> _mockLogger = null!;
        private Mock<IAreaRepository> _mockAreaRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private Mock<ContactPermissionService> _mockContactPermissionService = null!;
        private AreaFunctions _areaFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<AreaFunctions>>();
            _mockAreaRepository = new Mock<IAreaRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockClaimsService = new Mock<ClaimsService>(
                MockBehavior.Loose,
                new Mock<IAuthSessionRepository>().Object,
                new Mock<IPersonRepository>().Object,
                new Mock<IEventRoleRepository>().Object,
                new Mock<IMarshalRepository>().Object,
                new Mock<IUserEventMappingRepository>().Object
            );
            _mockContactPermissionService = new Mock<ContactPermissionService>(
                MockBehavior.Loose,
                new Mock<IAreaRepository>().Object,
                new Mock<ILocationRepository>().Object,
                new Mock<IAssignmentRepository>().Object
            );

            _areaFunctions = new AreaFunctions(
                _mockLogger.Object,
                _mockAreaRepository.Object,
                _mockLocationRepository.Object,
                _mockMarshalRepository.Object,
                _mockClaimsService.Object,
                _mockContactPermissionService.Object
            );
        }

        #region CreateArea Tests

        [TestMethod]
        public async Task CreateArea_ValidRequest_CreatesArea()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            MarshalEntity marshal = new()
            {
                MarshalId = marshalId,
                EventId = eventId,
                Name = "John Doe"
            };

            CreateAreaRequest request = new(
                eventId,
                "Area 1",
                "Test area",
                "#FF5733",
                new List<AreaContact>
                {
                    new AreaContact(marshalId, "John Doe", "Leader")
                },
                new List<RoutePoint>
                {
                    new RoutePoint(47.6062, -122.3321),
                    new RoutePoint(47.6063, -122.3320),
                    new RoutePoint(47.6061, -122.3319)
                }
            );

            _mockMarshalRepository
                .Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(marshal);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(0);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _areaFunctions.CreateArea(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAreaRepository.Verify(
                r => r.AddAsync(It.Is<AreaEntity>(a =>
                    a.EventId == eventId &&
                    a.Name == "Area 1" &&
                    a.Description == "Test area" &&
                    a.Color == "#FF5733" &&
                    a.IsDefault == false
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateArea_InvalidMarshal_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";

            CreateAreaRequest request = new(
                eventId,
                "Area 1",
                "Description",
                "#FF5733",
                new List<AreaContact>
                {
                    new AreaContact("nonexistent-marshal", "Unknown", "Leader")
                },
                null
            );

            _mockMarshalRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((MarshalEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _areaFunctions.CreateArea(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetArea Tests

        [TestMethod]
        public async Task GetArea_Exists_ReturnsArea()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";

            AreaEntity area = new()
            {
                RowKey = areaId,
                EventId = eventId,
                Name = "Area 1",
                Description = "Test area",
                Color = "#FF5733",
                ContactsJson = "[]",
                PolygonJson = "[]",
                IsDefault = false,
                DisplayOrder = 0
            };

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(area);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(eventId, areaId))
                .ReturnsAsync(5);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.GetArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetArea_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAreaRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AreaEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.GetArea(httpRequest, "event-123", "area-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetAreasByEvent Tests

        [TestMethod]
        public async Task GetAreasByEvent_ReturnsAllAreas()
        {
            // Arrange
            string eventId = "event-123";

            List<AreaEntity> areas = new()
            {
                new AreaEntity
                {
                    RowKey = "area-1",
                    EventId = eventId,
                    Name = "Area 1",
                    ContactsJson = "[]",
                    PolygonJson = "[]"
                },
                new AreaEntity
                {
                    RowKey = "area-2",
                    EventId = eventId,
                    Name = "Area 2",
                    ContactsJson = "[]",
                    PolygonJson = "[]"
                }
            };

            _mockAreaRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(areas);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(eventId, "area-1"))
                .ReturnsAsync(3);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(eventId, "area-2"))
                .ReturnsAsync(5);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.GetAreasByEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<AreaResponse>? areaList = okResult.Value as List<AreaResponse>;
            areaList.ShouldNotBeNull();
            areaList.Count.ShouldBe(2);
        }

        #endregion

        #region UpdateArea Tests

        [TestMethod]
        public async Task UpdateArea_ValidRequest_UpdatesArea()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";
            string marshalId = "marshal-789";

            AreaEntity existingArea = new()
            {
                RowKey = areaId,
                EventId = eventId,
                Name = "Old Name",
                Description = "Old description",
                Color = "#000000",
                ContactsJson = "[]",
                PolygonJson = "[]",
                IsDefault = false,
                DisplayOrder = 0
            };

            MarshalEntity marshal = new()
            {
                MarshalId = marshalId,
                EventId = eventId,
                Name = "John Doe"
            };

            UpdateAreaRequest request = new(
                "New Name",
                "New description",
                "#FF5733",
                new List<AreaContact>
                {
                    new AreaContact(marshalId, "John Doe", "Leader")
                },
                new List<RoutePoint>(),
                1
            );

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(existingArea);

            _mockMarshalRepository
                .Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(marshal);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(eventId, areaId))
                .ReturnsAsync(0);

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<LocationEntity>());

            _mockAreaRepository
                .Setup(r => r.GetDefaultAreaAsync(eventId))
                .ReturnsAsync(new AreaEntity { IsDefault = true });

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _areaFunctions.UpdateArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockAreaRepository.Verify(
                r => r.UpdateAsync(It.Is<AreaEntity>(a =>
                    a.Name == "New Name" &&
                    a.Description == "New description" &&
                    a.Color == "#FF5733" &&
                    a.DisplayOrder == 1
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task UpdateArea_RenameDefaultArea_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";

            AreaEntity defaultArea = new()
            {
                RowKey = areaId,
                EventId = eventId,
                Name = Constants.DefaultAreaName,
                IsDefault = true,
                ContactsJson = "[]",
                PolygonJson = "[]"
            };

            UpdateAreaRequest request = new(
                "New Name",
                "Description",
                "#FF5733",
                new List<AreaContact>(),
                null,
                0
            );

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(defaultArea);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _areaFunctions.UpdateArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task UpdateArea_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAreaRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AreaEntity?)null);

            UpdateAreaRequest request = new("Name", "Desc", "#FF5733", new List<AreaContact>(), null, 0);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _areaFunctions.UpdateArea(httpRequest, "event-123", "area-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region DeleteArea Tests

        [TestMethod]
        public async Task DeleteArea_ValidArea_DeletesArea()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";

            AreaEntity area = new()
            {
                RowKey = areaId,
                EventId = eventId,
                IsDefault = false
            };

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(area);

            _mockLocationRepository
                .Setup(r => r.CountByAreaAsync(eventId, areaId))
                .ReturnsAsync(0);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.DeleteArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockAreaRepository.Verify(
                r => r.DeleteAsync(eventId, areaId),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteArea_DefaultArea_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";

            AreaEntity defaultArea = new()
            {
                RowKey = areaId,
                EventId = eventId,
                IsDefault = true
            };

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(defaultArea);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.DeleteArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task DeleteArea_HasCheckpoints_ReassignsToDefaultAndDeletes()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";
            string defaultAreaId = "default-area";

            AreaEntity area = new()
            {
                RowKey = areaId,
                EventId = eventId,
                IsDefault = false
            };

            AreaEntity defaultArea = new()
            {
                RowKey = defaultAreaId,
                EventId = eventId,
                IsDefault = true
            };

            List<LocationEntity> checkpoints = new()
            {
                new LocationEntity
                {
                    RowKey = "loc-1",
                    EventId = eventId,
                    Name = "Checkpoint 1",
                    AreaIdsJson = $"[\"{areaId}\"]"
                }
            };

            _mockAreaRepository
                .Setup(r => r.GetAsync(eventId, areaId))
                .ReturnsAsync(area);

            _mockAreaRepository
                .Setup(r => r.GetDefaultAreaAsync(eventId))
                .ReturnsAsync(defaultArea);

            _mockLocationRepository
                .Setup(r => r.GetByAreaAsync(eventId, areaId))
                .ReturnsAsync(checkpoints);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.DeleteArea(httpRequest, eventId, areaId);

            // Assert - Area is deleted after checkpoints are reassigned
            result.ShouldBeOfType<NoContentResult>();

            // Verify checkpoint was updated (reassigned to default area)
            _mockLocationRepository.Verify(
                r => r.UpdateAsync(It.Is<LocationEntity>(l => l.RowKey == "loc-1")),
                Times.Once
            );

            // Verify area was deleted
            _mockAreaRepository.Verify(
                r => r.DeleteAsync(eventId, areaId),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteArea_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockAreaRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((AreaEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.DeleteArea(httpRequest, "event-123", "area-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region GetLocationsByArea Tests

        [TestMethod]
        public async Task GetLocationsByArea_ReturnsLocations()
        {
            // Arrange
            string eventId = "event-123";
            string areaId = "area-456";

            List<LocationEntity> locations = new()
            {
                new LocationEntity
                {
                    RowKey = "loc-1",
                    EventId = eventId,
                    Name = "Checkpoint 1",
                    AreaIdsJson = "[\"area-456\"]"
                },
                new LocationEntity
                {
                    RowKey = "loc-2",
                    EventId = eventId,
                    Name = "Checkpoint 2",
                    AreaIdsJson = "[\"area-456\"]"
                }
            };

            _mockLocationRepository
                .Setup(r => r.GetByAreaAsync(eventId, areaId))
                .ReturnsAsync(locations);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _areaFunctions.GetLocationsByArea(httpRequest, eventId, areaId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<LocationResponse>? locationList = okResult.Value as List<LocationResponse>;
            locationList.ShouldNotBeNull();
            locationList.Count.ShouldBe(2);
        }

        #endregion
    }
}
