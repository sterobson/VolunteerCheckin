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
        private Mock<IPersonRepository> _mockPersonRepository = null!;
        private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private Mock<IEventService> _mockEventService = null!;
        private Mock<IEventDeletionRepository> _mockEventDeletionRepository = null!;
        private EventFunctions _eventFunctions = null!;

        private const string AdminPersonId = "person-123";
        private const string AdminEmail = "admin@example.com";

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<EventFunctions>>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockPersonRepository = new Mock<IPersonRepository>();
            _mockEventRoleRepository = new Mock<IEventRoleRepository>();
            _mockClaimsService = new Mock<ClaimsService>(
                Mock.Of<IAuthSessionRepository>(),
                Mock.Of<IPersonRepository>(),
                Mock.Of<IEventRoleRepository>(),
                Mock.Of<IMarshalRepository>(),
                Mock.Of<ISampleEventService>(),
                Mock.Of<IEventDeletionRepository>()
            );
            _mockEventService = new Mock<IEventService>();
            _mockEventDeletionRepository = new Mock<IEventDeletionRepository>();

            _eventFunctions = new EventFunctions(
                _mockLogger.Object,
                _mockEventRepository.Object,
                _mockPersonRepository.Object,
                _mockEventRoleRepository.Object,
                _mockClaimsService.Object,
                _mockEventService.Object,
                _mockEventDeletionRepository.Object
            );
        }

        private UserClaims CreateAdminClaims(string? eventId = null)
        {
            return new UserClaims(
                PersonId: AdminPersonId,
                PersonName: "Admin User",
                PersonEmail: AdminEmail,
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo> { new(Constants.RoleEventAdmin, new List<string>()) }
            );
        }

        private void SetupAdminAuth(string? eventId = null)
        {
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), eventId))
                .ReturnsAsync(CreateAdminClaims(eventId));

            // Also setup GetClaimsWithSampleSupportAsync for endpoints that support sample code auth
            _mockClaimsService
                .Setup(c => c.GetClaimsWithSampleSupportAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
                .ReturnsAsync(CreateAdminClaims(eventId));

            // Also setup for null eventId calls
            if (eventId != null)
            {
                _mockClaimsService
                    .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), null))
                    .ReturnsAsync(CreateAdminClaims(null));
            }
        }

        private void SetupPersonLookup(string email, string personId)
        {
            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(email))
                .ReturnsAsync(new PersonEntity { PersonId = personId, Email = email });
        }

        private void SetupEventAdminRole(string personId, string eventId)
        {
            _mockEventRoleRepository
                .Setup(r => r.GetByPersonAndEventAsync(personId, eventId))
                .ReturnsAsync(new List<EventRoleEntity>
                {
                    new EventRoleEntity
                    {
                        PersonId = personId,
                        EventId = eventId,
                        Role = Constants.RoleEventAdmin,
                        AreaIdsJson = "[]"
                    }
                });
        }

        #region CreateEvent Tests

        [TestMethod]
        public async Task CreateEvent_ValidRequest_CreatesEventAndRole()
        {
            // Arrange
            SetupAdminAuth();

            CreateEventRequest request = new(
                "Test Event",
                "Description",
                new DateTime(2025, 6, 15, 10, 0, 0),
                "America/Los_Angeles"
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, "valid-session-token");

            // Act
            IActionResult result = await _eventFunctions.CreateEvent(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify event was created
            _mockEventRepository.Verify(
                r => r.AddAsync(It.Is<EventEntity>(e =>
                    e.Name == "Test Event" &&
                    e.Description == "Description" &&
                    e.TimeZoneId == "America/Los_Angeles"
                )),
                Times.Once
            );

            // Verify EventAdmin role was created for the creator
            _mockEventRoleRepository.Verify(
                r => r.AddAsync(It.Is<EventRoleEntity>(role =>
                    role.PersonId == AdminPersonId &&
                    role.Role == Constants.RoleEventAdmin
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateEvent_NoAuth_ReturnsUnauthorized()
        {
            // Arrange
            CreateEventRequest request = new(
                "Test Event",
                "Description",
                DateTime.Now,
                "UTC"
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

            // Act
            IActionResult result = await _eventFunctions.CreateEvent(httpRequest);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();

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

            EventEntity existingEvent = new()
            {
                RowKey = eventId,
                Name = "Old Name",
                Description = "Old Description",
                EventDate = DateTime.UtcNow,
                TimeZoneId = "UTC",
                                GpxRouteJson = "[]"
            };

            UpdateEventRequest updateRequest = new(
                "New Name",
                "New Description",
                new DateTime(2025, 7, 1, 12, 0, 0),
                "America/New_York"
            );

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(existingEvent);

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(updateRequest, AdminEmail);

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
            string unauthorizedEmail = "unauthorized@example.com";

            _mockEventRepository
                .Setup(r => r.GetAsync(eventId))
                .ReturnsAsync(new EventEntity { RowKey = eventId });

            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(unauthorizedEmail))
                .ReturnsAsync((PersonEntity?)null);

            UpdateEventRequest updateRequest = new(
                "New Name",
                "Description",
                DateTime.UtcNow,
                "UTC"
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(updateRequest, unauthorizedEmail);

            // Act
            IActionResult result = await _eventFunctions.UpdateEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<UnauthorizedObjectResult>();
        }

        #endregion

        #region DeleteEvent Tests

        [TestMethod]
        public async Task DeleteEvent_AuthorizedUser_DeletesEvent()
        {
            // Arrange
            string eventId = "event-123";

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(AdminEmail);

            // Act
            IActionResult result = await _eventFunctions.DeleteEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockEventService.Verify(
                s => s.DeleteEventWithAllDataAsync(eventId),
                Times.Once
            );
        }

        [TestMethod]
        public async Task DeleteEvent_Unauthorized_ReturnsUnauthorized()
        {
            // Arrange
            string eventId = "event-123";
            string unauthorizedEmail = "unauthorized@example.com";

            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(unauthorizedEmail))
                .ReturnsAsync((PersonEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(unauthorizedEmail);

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
            SetupPersonLookup(AdminEmail, AdminPersonId);

            List<EventRoleEntity> userRoles = new()
            {
                new EventRoleEntity { PersonId = AdminPersonId, EventId = "event-1", Role = Constants.RoleEventAdmin },
                new EventRoleEntity { PersonId = AdminPersonId, EventId = "event-2", Role = Constants.RoleEventAdmin }
            };

            _mockEventRoleRepository
                .Setup(r => r.GetByPersonAsync(AdminPersonId))
                .ReturnsAsync(userRoles);

            List<EventEntity> allEvents = new()
            {
                new EventEntity { RowKey = "event-1", Name = "Event 1", GpxRouteJson = "[]" },
                new EventEntity { RowKey = "event-2", Name = "Event 2", GpxRouteJson = "[]" },
                new EventEntity { RowKey = "event-3", Name = "Event 3", GpxRouteJson = "[]" }
            };

            _mockEventRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(allEvents);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(AdminEmail);

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
            string newAdminEmail = "new-admin@example.com";

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            // New admin doesn't exist yet - will be created
            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(newAdminEmail))
                .ReturnsAsync((PersonEntity?)null);

            AddEventAdminRequest request = new(newAdminEmail);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

            // Act
            IActionResult result = await _eventFunctions.AddEventAdmin(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify person was created
            _mockPersonRepository.Verify(
                r => r.AddAsync(It.Is<PersonEntity>(p => p.Email == newAdminEmail)),
                Times.Once
            );

            // Verify EventAdmin role was created
            _mockEventRoleRepository.Verify(
                r => r.AddAsync(It.Is<EventRoleEntity>(role =>
                    role.EventId == eventId &&
                    role.Role == Constants.RoleEventAdmin
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task AddEventAdmin_AlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";
            string existingAdminEmail = "existing@example.com";
            string existingAdminPersonId = "existing-person-456";

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            // Existing admin already has the role
            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(existingAdminEmail))
                .ReturnsAsync(new PersonEntity { PersonId = existingAdminPersonId, Email = existingAdminEmail });

            _mockEventRoleRepository
                .Setup(r => r.GetByPersonAndEventAsync(existingAdminPersonId, eventId))
                .ReturnsAsync(new List<EventRoleEntity>
                {
                    new EventRoleEntity { PersonId = existingAdminPersonId, EventId = eventId, Role = Constants.RoleEventAdmin }
                });

            AddEventAdminRequest request = new(existingAdminEmail);
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

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

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            AddEventAdminRequest request = new("invalid-email");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

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
            string removeAdminEmail = "remove@example.com";
            string removeAdminPersonId = "remove-person-456";

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            _mockPersonRepository
                .Setup(r => r.GetByEmailAsync(removeAdminEmail))
                .ReturnsAsync(new PersonEntity { PersonId = removeAdminPersonId, Email = removeAdminEmail });

            // Two admins exist
            List<EventRoleEntity> allAdminRoles = new()
            {
                new EventRoleEntity { PersonId = AdminPersonId, EventId = eventId, Role = Constants.RoleEventAdmin, RowKey = $"{eventId}|role-1" },
                new EventRoleEntity { PersonId = removeAdminPersonId, EventId = eventId, Role = Constants.RoleEventAdmin, RowKey = $"{eventId}|role-2" }
            };

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(allAdminRoles);

            _mockEventRoleRepository
                .Setup(r => r.GetByPersonAndEventAsync(removeAdminPersonId, eventId))
                .ReturnsAsync(new List<EventRoleEntity> { allAdminRoles[1] });

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(AdminEmail);

            // Act
            IActionResult result = await _eventFunctions.RemoveEventAdmin(httpRequest, eventId, removeAdminEmail);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            _mockEventRoleRepository.Verify(
                r => r.DeleteAsync(removeAdminPersonId, It.IsAny<string>()),
                Times.Once
            );
        }

        [TestMethod]
        public async Task RemoveEventAdmin_LastAdmin_ReturnsBadRequest()
        {
            // Arrange
            string eventId = "event-123";

            SetupPersonLookup(AdminEmail, AdminPersonId);
            SetupEventAdminRole(AdminPersonId, eventId);

            // Only one admin exists
            List<EventRoleEntity> allAdminRoles = new()
            {
                new EventRoleEntity { PersonId = AdminPersonId, EventId = eventId, Role = Constants.RoleEventAdmin }
            };

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(allAdminRoles);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAdminHeader(AdminEmail);

            // Act
            IActionResult result = await _eventFunctions.RemoveEventAdmin(httpRequest, eventId, AdminEmail);

            // Assert
            result.ShouldBeOfType<BadRequestObjectResult>();

            _mockEventRoleRepository.Verify(
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
                .Setup(c => c.GetClaimsWithSampleSupportAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
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
                .Setup(c => c.GetClaimsWithSampleSupportAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string>()))
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

            List<EventRoleEntity> adminRoles = new()
            {
                new EventRoleEntity { PersonId = "person-1", EventId = eventId, Role = Constants.RoleEventAdmin, GrantedAt = DateTime.UtcNow },
                new EventRoleEntity { PersonId = "person-2", EventId = eventId, Role = Constants.RoleEventAdmin, GrantedAt = DateTime.UtcNow }
            };

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(adminRoles);

            _mockPersonRepository
                .Setup(r => r.GetAsync("person-1"))
                .ReturnsAsync(new PersonEntity { PersonId = "person-1", Email = "admin1@example.com" });
            _mockPersonRepository
                .Setup(r => r.GetAsync("person-2"))
                .ReturnsAsync(new PersonEntity { PersonId = "person-2", Email = "admin2@example.com" });

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth("valid-token");

            // Act
            IActionResult result = await _eventFunctions.GetEventAdmins(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<EventAdminResponse>? adminList = okResult.Value as List<EventAdminResponse>;
            adminList.ShouldNotBeNull();
            adminList.Count.ShouldBe(2);
        }

        #endregion
    }
}
