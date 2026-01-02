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
    public class MarshalFunctionsTests
    {
        private const string TestSessionToken = "test-session-token";
        private Mock<ILogger<MarshalFunctions>> _mockLogger = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<IEventRepository> _mockEventRepository = null!;
        private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
        private Mock<INoteRepository> _mockNoteRepository = null!;
        private Mock<CsvParserService> _mockCsvParser = null!;
        private Mock<ClaimsService> _mockClaimsService = null!;
        private Mock<ContactPermissionService> _mockContactPermissionService = null!;
        private MarshalFunctions _marshalFunctions = null!;
        private UserClaims _adminClaims = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger<MarshalFunctions>>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockEventRepository = new Mock<IEventRepository>();
            _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
            _mockNoteRepository = new Mock<INoteRepository>();
            _mockCsvParser = new Mock<CsvParserService>();
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

            // Create admin claims for tests
            _adminClaims = new UserClaims(
                PersonId: "person-1",
                PersonName: "Admin User",
                PersonEmail: "admin@example.com",
                IsSystemAdmin: false,
                EventId: "event-123",
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>
                {
                    new EventRoleInfo(Constants.RoleEventAdmin, new List<string>())
                }
            );

            // Setup claims service to return admin claims for any event
            _mockClaimsService
                .Setup(c => c.GetClaimsAsync(TestSessionToken, It.IsAny<string>()))
                .ReturnsAsync(_adminClaims);

            // Setup contact permissions for admin (can view/modify all)
            _mockContactPermissionService
                .Setup(c => c.GetContactPermissionsAsync(It.IsAny<UserClaims>(), It.IsAny<string>()))
                .ReturnsAsync(new ContactPermissions(
                    CanViewAll: true,
                    ViewableMarshalIds: new HashSet<string>(),
                    CanModifyAll: true,
                    ModifiableMarshalIds: new HashSet<string>()
                ));

            _mockContactPermissionService
                .Setup(c => c.CanViewContactDetails(It.IsAny<ContactPermissions>(), It.IsAny<string>()))
                .Returns(true);

            _mockContactPermissionService
                .Setup(c => c.CanModifyMarshal(It.IsAny<ContactPermissions>(), It.IsAny<string>()))
                .Returns(true);

            _marshalFunctions = new MarshalFunctions(
                _mockLogger.Object,
                _mockMarshalRepository.Object,
                _mockLocationRepository.Object,
                _mockAssignmentRepository.Object,
                _mockEventRepository.Object,
                _mockChecklistItemRepository.Object,
                _mockNoteRepository.Object,
                _mockCsvParser.Object,
                _mockClaimsService.Object,
                _mockContactPermissionService.Object
            );
        }

        #region CreateMarshal Tests

        [TestMethod]
        public async Task CreateMarshal_ValidRequest_CreatesMarshal()
        {
            // Arrange
            CreateMarshalRequest request = new(
                "event-123",
                "John Doe",
                "john@example.com",
                "555-1234",
                "Test notes"
            );

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.CreateMarshal(httpRequest);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockMarshalRepository.Verify(
                r => r.AddAsync(It.Is<MarshalEntity>(m =>
                    m.EventId == "event-123" &&
                    m.Name == "John Doe" &&
                    m.Email == "john@example.com" &&
                    m.PhoneNumber == "555-1234" &&
                    m.Notes == "Test notes"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task CreateMarshal_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth("invalid json", TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.CreateMarshal(httpRequest);

            // Assert
            result.ShouldBeOfType<StatusCodeResult>();
            StatusCodeResult statusResult = (StatusCodeResult)result;
            statusResult.StatusCode.ShouldBe(500);
        }

        #endregion

        #region GetMarshalsByEvent Tests

        [TestMethod]
        public async Task GetMarshalsByEvent_ReturnsMarshalsWithAssignments()
        {
            // Arrange
            string eventId = "event-123";

            List<MarshalEntity> marshals = new()
            {
                new MarshalEntity
                {
                    MarshalId = "marshal-1",
                    EventId = eventId,
                    Name = "John Doe",
                    Email = "john@example.com",
                    PhoneNumber = "555-1234",
                    Notes = "Notes"
                },
                new MarshalEntity
                {
                    MarshalId = "marshal-2",
                    EventId = eventId,
                    Name = "Jane Smith",
                    Email = "jane@example.com",
                    PhoneNumber = "555-5678",
                    Notes = ""
                }
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    EventId = eventId,
                    MarshalId = "marshal-1",
                    LocationId = "loc-1",
                    IsCheckedIn = true
                },
                new AssignmentEntity
                {
                    EventId = eventId,
                    MarshalId = "marshal-2",
                    LocationId = "loc-2",
                    IsCheckedIn = false
                }
            };

            _mockMarshalRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(marshals);

            // Now uses GetByEventAsync for batch loading (N+1 fix)
            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.GetMarshalsByEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
            OkObjectResult okResult = (OkObjectResult)result;
            okResult.Value.ShouldNotBeNull();

            List<MarshalWithPermissionsResponse>? marshalList = okResult.Value as List<MarshalWithPermissionsResponse>;
            marshalList.ShouldNotBeNull();
            marshalList.Count.ShouldBe(2);
            marshalList[0].IsCheckedIn.ShouldBeTrue();
            marshalList[1].IsCheckedIn.ShouldBeFalse();
        }

        [TestMethod]
        public async Task GetMarshalsByEvent_UsesPreloadedAssignments_NoNPlusOneQueries()
        {
            // Arrange
            string eventId = "event-123";

            List<MarshalEntity> marshals = new()
            {
                new MarshalEntity { MarshalId = "marshal-1", EventId = eventId, Name = "Marshal 1" },
                new MarshalEntity { MarshalId = "marshal-2", EventId = eventId, Name = "Marshal 2" },
                new MarshalEntity { MarshalId = "marshal-3", EventId = eventId, Name = "Marshal 3" }
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity { EventId = eventId, MarshalId = "marshal-1", LocationId = "loc-1" },
                new AssignmentEntity { EventId = eventId, MarshalId = "marshal-2", LocationId = "loc-2" },
                new AssignmentEntity { EventId = eventId, MarshalId = "marshal-3", LocationId = "loc-3" }
            };

            _mockMarshalRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(marshals);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.GetMarshalsByEvent(httpRequest, eventId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            // Verify GetByEventAsync was called exactly once (batch load)
            _mockAssignmentRepository.Verify(r => r.GetByEventAsync(eventId), Times.Once);

            // Verify GetByMarshalAsync was never called (no N+1 queries)
            _mockAssignmentRepository.Verify(r => r.GetByMarshalAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        #endregion

        #region GetMarshal Tests

        [TestMethod]
        public async Task GetMarshal_Exists_ReturnsMarshal()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            MarshalEntity marshal = new()
            {
                MarshalId = marshalId,
                EventId = eventId,
                Name = "John Doe",
                Email = "john@example.com",
                PhoneNumber = "555-1234",
                Notes = "Notes"
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity { LocationId = "loc-1", IsCheckedIn = false }
            };

            _mockMarshalRepository
                .Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(marshal);

            _mockAssignmentRepository
                .Setup(r => r.GetByMarshalAsync(eventId, marshalId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.GetMarshal(httpRequest, eventId, marshalId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();
        }

        [TestMethod]
        public async Task GetMarshal_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockMarshalRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((MarshalEntity?)null);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.GetMarshal(httpRequest, "event-123", "marshal-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region UpdateMarshal Tests

        [TestMethod]
        public async Task UpdateMarshal_ValidRequest_UpdatesMarshalAndAssignments()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            MarshalEntity existingMarshal = new()
            {
                MarshalId = marshalId,
                EventId = eventId,
                Name = "Old Name",
                Email = "old@example.com",
                PhoneNumber = "555-0000",
                Notes = "Old notes"
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    RowKey = "assign-1",
                    EventId = eventId,
                    MarshalId = marshalId,
                    MarshalName = "Old Name",
                    LocationId = "loc-1"
                }
            };

            UpdateMarshalRequest request = new(
                "New Name",
                "new@example.com",
                "555-9999",
                "New notes"
            );

            _mockMarshalRepository
                .Setup(r => r.GetAsync(eventId, marshalId))
                .ReturnsAsync(existingMarshal);

            _mockAssignmentRepository
                .Setup(r => r.GetByMarshalAsync(eventId, marshalId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.UpdateMarshal(httpRequest, eventId, marshalId);

            // Assert
            result.ShouldBeOfType<OkObjectResult>();

            _mockMarshalRepository.Verify(
                r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
                    m.Name == "New Name" &&
                    m.Email == "new@example.com" &&
                    m.PhoneNumber == "555-9999"
                )),
                Times.Once
            );

            // Verify assignment name was updated
            _mockAssignmentRepository.Verify(
                r => r.UpdateAsync(It.Is<AssignmentEntity>(a =>
                    a.MarshalName == "New Name"
                )),
                Times.Once
            );
        }

        [TestMethod]
        public async Task UpdateMarshal_NotFound_ReturnsNotFound()
        {
            // Arrange
            _mockMarshalRepository
                .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((MarshalEntity?)null);

            UpdateMarshalRequest request = new("Name", "email@test.com", "555-1234", "Notes");
            HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.UpdateMarshal(httpRequest, "event-123", "marshal-456");

            // Assert
            result.ShouldBeOfType<NotFoundObjectResult>();
        }

        #endregion

        #region DeleteMarshal Tests

        [TestMethod]
        public async Task DeleteMarshal_DeletesMarshalAndAssignments()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity { RowKey = "assign-1", EventId = eventId, MarshalId = marshalId },
                new AssignmentEntity { RowKey = "assign-2", EventId = eventId, MarshalId = marshalId }
            };

            _mockAssignmentRepository
                .Setup(r => r.GetByMarshalAsync(eventId, marshalId))
                .ReturnsAsync(assignments);

            HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(TestSessionToken);

            // Act
            IActionResult result = await _marshalFunctions.DeleteMarshal(httpRequest, eventId, marshalId);

            // Assert
            result.ShouldBeOfType<NoContentResult>();

            // Verify all assignments deleted
            _mockAssignmentRepository.Verify(
                r => r.DeleteAsync(eventId, "assign-1"),
                Times.Once
            );
            _mockAssignmentRepository.Verify(
                r => r.DeleteAsync(eventId, "assign-2"),
                Times.Once
            );

            // Verify marshal deleted
            _mockMarshalRepository.Verify(
                r => r.DeleteAsync(eventId, marshalId),
                Times.Once
            );
        }

        #endregion
    }
}
