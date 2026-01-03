using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
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
    public class EventFunctionsTests
    {
        private Mock<ILogger<EventFunctions>> _mockLogger = null!;
        private Mock<IEventRepository> _mockEventRepository = null!;
        private Mock<IUserEventMappingRepository> _mockUserEventMappingRepository = null!;
        private Mock<IPersonRepository> _mockPersonRepository = null!;
        private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private EventFunctions _eventFunctions = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<EventFunctions>>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockUserEventMappingRepository = new Mock<IUserEventMappingRepository>();
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockEventRoleRepository = new Mock<IEventRoleRepository>();
            _mockClaimsService = new Mock<ClaimsService>(
                Mock.Of<IAuthSessionRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IEventRoleRepository>(),
                Mock.Of<IMarshalRepository>(),
                Mock.Of<IUserEventMappingRepository>()
            );

            _eventFunctions = new EventFunctions(
                _mockLogger.Object,
                _mockEventRepository.Object,
                _mockUserEventMappingRepository.Object,
                _mockPersonRepository.Object,
                _mockEventRoleRepository.Object,
                _mockClaimsService.Object
            );
        }

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

        #region CreateEvent Tests

        [TestMethod]
        public async Task CreateEvent_ValidRequest_CreatesEventAndMapping()
        {
            // Arrange
            CreateEventRequest request = new(
                "Test Event",
                "Description",
                new DateTime(2025, 6, 15, 10, 0, 0),
                "America/Los_Angeles",
                "admin@example.com",
                new List<EmergencyContact> { new("John", "555-1234", "Emergency contact") }
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _eventFunctions.CreateEvent(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify event was created
            _mockEventRepository.Verify(
                r => r.AddAsync(It.Is<EventEntity>(e =>
                    e.Name == "Test Event" &&
                    e.Description == "Description" &&
                    e.AdminEmail == "admin@example.com" &&
                    e.TimeZoneId == "America/Los_Angeles"
                )),
                Times.Once
            );

            // Verify user-event mapping was created
            _mockUserEventMappingRepository.Verify(
                r => r.AddAsync(It.Is<UserEventMappingEntity>(m =>
                    m.UserEmail == "admin@example.com" &&
                    m.Role == "Admin"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateEvent_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            CreateEventRequest request = new(
                "Test Event",
                "Description",
                DateTime.Now,
                "UTC",
                "invalid-email",
                new List<EmergencyContact>()
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _eventFunctions.CreateEvent(httpRequest);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();

            _mockEventRepository.Verify(
                r => r.AddAsync(It.IsAny<EventEntity>()),
                Times.Never
            );
        }

        #endregion

        #region GetEvent Tests

        [TestMethod]
        public async Task GetEvent_EventExists_ReturnsEvent()
        {
            // Arrange
            string eventId = "event-123";
            EventEntity eventEntity = new()
            {
                RowKey = eventId,
                Name = "Test Event",
                Description = "Description",
                EventDate = DateTime.UtcNow,
                TimeZoneId = "UTC",
                AdminEmail = "admin@example.com",
                EmergencyContactsJson = "[]",
                GpxRouteJson = "[]"
            };

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(eventEntity);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _eventFunctions.GetEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();
        }

        [TestMethod]
        public async Task GetEvent_EventNotFound_ReturnsNotFound()
        {
            // Arrange
            _mockEventRepository
                .Setup(r => r.GetAsync(It.IsAny<string>()))
                .ReturnsAsync((EventEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _eventFunctions.GetEvent(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region UpdateEvent Tests

        [TestMethod]
        public async Task UpdateEvent_ValidRequest_UpdatesEvent()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";

            EventEntity existingEvent = new()
            {
                RowKey = eventId,
                Name = "Old Name",
                Description = "Old Description",
                EventDate = DateTime.UtcNow,
                TimeZoneId = "UTC",
                AdminEmail = adminEmail,
                EmergencyContactsJson = "[]",
                GpxRouteJson = "[]"
            };

            CreateEventRequest updateRequest = new(
                "New Name",
                "New Description",
                new DateTime(2025, 7, 1, 12, 0, 0),
                "America/New_York",
                adminEmail,
                new List<EmergencyContact>()
            );

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(existingEvent);

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity
                {
                    EventId = eventId,
                    UserEmail = adminEmail,
                    Role = "Admin"
                });

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(updateRequest, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.UpdateEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockEventRepository.Verify(
                r => r.UpdateAsync(It.Is<EventEntity>(e =>
                    e.Name == "New Name" &&
                    e.Description == "New Description" &&
                    e.TimeZoneId == "America/New_York"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task UpdateEvent_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "unauthorized@example.com";

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(new EventEntity { RowKey = eventId });

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync((UserEventMappingEntity?)null);

            CreateEventRequest updateRequest = new(
                "New Name",
                "Description",
                DateTime.UtcNow,
                "UTC",
                adminEmail,
                new List<EmergencyContact>()
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(updateRequest, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.UpdateEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task UpdateEvent_EventNotFound_ReturnsNotFound()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync((EventEntity?)null);

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail });

            CreateEventRequest updateRequest = new(
                "New Name",
                "Description",
                DateTime.UtcNow,
                "UTC",
                adminEmail,
                new List<EmergencyContact>()
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(updateRequest, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.UpdateEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region DeleteEvent Tests

        [TestMethod]
        public async Task DeleteEvent_AuthorizedUser_DeletesEvent()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity
                {
                    EventId = eventId,
                    UserEmail = adminEmail,
                    Role = "Admin"
                });

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(adminEmail);

            // Act
            IActionResult result = await _eventFunctions.DeleteEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockEventRepository.Verify(
                r => r.DeleteAsync(eventId),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteEvent_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "unauthorized@example.com";

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync((UserEventMappingEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(adminEmail);

            // Act
            IActionResult result = await _eventFunctions.DeleteEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task DeleteEvent_MissingHeader_ReturnsBadRequest()
        {
            // Arrange
            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _eventFunctions.DeleteEvent(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetAllEvents Tests

        [TestMethod]
        public async Task GetAllEvents_ReturnsUserEvents()
        {
            // Arrange
            string adminEmail = "admin@example.com";

            List<UserEventMappingEntity> userMappings = new()
            {
                new UserEventMappingEntity { EventId = "event-1", UserEmail = adminEmail },
                new UserEventMappingEntity { EventId = "event-2", UserEmail = adminEmail }
            };

            List<EventEntity> allEvents = new()
            {
                new EventEntity
                {
                    RowKey = "event-1",
                    Name = "Event 1",
                    EmergencyContactsJson = "[]",
                    GpxRouteJson = "[]"
                },
                new EventEntity
                {
                    RowKey = "event-2",
                    Name = "Event 2",
                    EmergencyContactsJson = "[]",
                    GpxRouteJson = "[]"
                },
                new EventEntity
                {
                    RowKey = "event-3",
                    Name = "Event 3",
                    EmergencyContactsJson = "[]",
                    GpxRouteJson = "[]"
                }
            };

            _mockUserEventMappingRepository
                .Setup(r => r.GetByUserAsync(adminEmail))
                .ReturnsAsync(userMappings);

            _mockEventRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(allEvents);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(adminEmail);

            // Act
            IActionResult result = await _eventFunctions.GetAllEvents(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<EventResponse>? events = okResult.Value as List<EventResponse>;
            events.ShouldNotBeNull();
            events.Count.ShouldBe(2); // Only event-1 and event-2
        }

        #endregion

        #region AddEventAdmin Tests

        [TestMethod]
        public async Task AddEventAdmin_ValidRequest_AddsAdmin()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "existing-admin@example.com";
            string newAdminEmail = "new-admin@example.com";

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail });

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, newAdminEmail))
                .ReturnsAsync((UserEventMappingEntity?)null);

            AddEventAdminRequest request = new(newAdminEmail);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.AddEventAdmin(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockUserEventMappingRepository.Verify(
                r => r.AddAsync(It.Is<UserEventMappingEntity>(m =>
                    m.EventId == eventId &&
                    m.UserEmail == newAdminEmail &&
                    m.Role == "Admin"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task AddEventAdmin_AlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";
            string existingAdminEmail = "existing@example.com";

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail });

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, existingAdminEmail))
                .ReturnsAsync(new UserEventMappingEntity { EventId = eventId, UserEmail = existingAdminEmail });

            AddEventAdminRequest request = new(existingAdminEmail);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.AddEventAdmin(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        [TestMethod]
        public async Task AddEventAdmin_InvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail });

            AddEventAdminRequest request = new("invalid-email");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, adminEmail);

            // Act
            IActionResult result = await _eventFunctions.AddEventAdmin(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region RemoveEventAdmin Tests

        [TestMethod]
        public async Task RemoveEventAdmin_MoreThanOneAdmin_RemovesAdmin()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";
            string removeAdminEmail = "remove@example.com";

            List<UserEventMappingEntity> admins = new()
            {
                new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail },
                new UserEventMappingEntity { EventId = eventId, UserEmail = removeAdminEmail }
            };

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(admins[0]);

            _mockUserEventMappingRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(admins);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(adminEmail);

            // Act
            IActionResult result = await _eventFunctions.RemoveEventAdmin(httpRequest, eventId, removeAdminEmail);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockUserEventMappingRepository.Verify(
                r => r.DeleteAsync(eventId, removeAdminEmail),
                Times.Once
            );
        }

        [TestMethod]
        public async Task RemoveEventAdmin_LastAdmin_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string adminEmail = "admin@example.com";

            List<UserEventMappingEntity> admins = new()
            {
                new UserEventMappingEntity { EventId = eventId, UserEmail = adminEmail }
            };

            _mockUserEventMappingRepository
                .Setup(r => r.GetAsync(eventId, adminEmail))
                .ReturnsAsync(admins[0]);

            _mockUserEventMappingRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(admins);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(adminEmail);

            // Act
            IActionResult result = await _eventFunctions.RemoveEventAdmin(httpRequest, eventId, adminEmail);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();

            _mockUserEventMappingRepository.Verify(
                r => r.DeleteAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Never
            );
        }

        #endregion

        #region GetEventAdmins Tests

        [TestMethod]
        public async Task GetEventAdmins_NoAuth_ReturnsUnauthorized()
        {
            // Arrange
            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task GetEventAdmins_InvalidSession_ReturnsUnauthorized()
        {
            // Arrange
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((UserClaims?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("invalid-token");

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        [TestMethod]
        public async Task GetEventAdmins_MarshalAuth_ReturnsForbid()
        {
            // Arrange - user logged in via magic code (not elevated)
            UserClaims marshalClaims = new(
                PersonId: "person-123",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                IsSystemAdmin: false,
                EventId: "event-123",
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: "marshal-123",
                EventRoles: new List<EventRoleInfo>()
            );

            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(marshalClaims);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("some-token");

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<ForbidResult>();
        }

        [TestMethod]
        public async Task GetEventAdmins_NotEventAdmin_ReturnsForbid()
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
                EventRoles: new List<EventRoleInfo>()
            );

            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(nonAdminClaims);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("some-token");

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, "event-123");

            // Assert
            result.ShouldBeOfType<ForbidResult>();
        }

        [TestMethod]
        public async Task GetEventAdmins_ReturnsAllAdmins()
        {
            // Arrange
            string eventId = "event-123";

            SetupAdminAuth(eventId);

            List<UserEventMappingEntity> admins = new()
            {
                new UserEventMappingEntity { EventId = eventId, UserEmail = "admin1@example.com" },
                new UserEventMappingEntity { EventId = eventId, UserEmail = "admin2@example.com" }
            };

            _mockUserEventMappingRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(admins);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<UserEventMappingResponse>? adminList = okResult.Value as List<UserEventMappingResponse>;
            adminList.ShouldNotBeNull();
            adminList.Count.ShouldBe(2);
        }

        #endregion
    }
}
