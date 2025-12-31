using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests
{
    [TestClass]
    public class ContactPermissionServiceTests
    {
        private Mock<IAreaRepository> _mockAreaRepository = null!;
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private ContactPermissionService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAreaRepository = new Mock<IAreaRepository>();
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();

            _service = new ContactPermissionService(
                _mockAreaRepository.Object,
                _mockLocationRepository.Object,
                _mockAssignmentRepository.Object
            );
        }

        [TestMethod]
        public async Task GetContactPermissions_EventAdmin_CanViewAndModifyAll()
        {
            // Arrange
            string eventId = "event-123";
            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Admin User",
                PersonEmail: "admin@example.com",
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>
                {
                    new EventRoleInfo(Constants.RoleEventAdmin, new List<string>())
                }
            );

            // Act
            ContactPermissions permissions = await _service.GetContactPermissionsAsync(claims, eventId);

            // Assert
            permissions.CanViewAll.ShouldBeTrue();
            permissions.CanModifyAll.ShouldBeTrue();
        }

        [TestMethod]
        public async Task GetContactPermissions_SystemAdmin_CanViewAndModifyAll()
        {
            // Arrange
            string eventId = "event-123";
            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "System Admin",
                PersonEmail: "sysadmin@example.com",
                IsSystemAdmin: true,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>()
            );

            // Act
            ContactPermissions permissions = await _service.GetContactPermissionsAsync(claims, eventId);

            // Assert
            permissions.CanViewAll.ShouldBeTrue();
            permissions.CanModifyAll.ShouldBeTrue();
        }

        [TestMethod]
        public async Task GetContactPermissions_Marshal_CanViewAndModifySelf()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: marshalId,
                EventRoles: new List<EventRoleInfo>()
            );

            _mockAreaRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<AreaEntity>());

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<LocationEntity>());

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<AssignmentEntity>());

            // Act
            ContactPermissions permissions = await _service.GetContactPermissionsAsync(claims, eventId);

            // Assert
            permissions.CanViewAll.ShouldBeFalse();
            permissions.CanModifyAll.ShouldBeFalse();
            permissions.ViewableMarshalIds.ShouldContain(marshalId);
            permissions.ModifiableMarshalIds.ShouldContain(marshalId);
        }

        [TestMethod]
        public async Task GetContactPermissions_Marshal_CanViewAreaLeads()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";
            string areaLeadMarshalId = "marshal-lead-789";
            string areaId = "area-1";
            string locationId = "loc-1";

            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: marshalId,
                EventRoles: new List<EventRoleInfo>()
            );

            // Marshal is assigned to a location in area-1
            List<AreaEntity> areas = new()
            {
                new AreaEntity
                {
                    RowKey = areaId,
                    EventId = eventId,
                    Name = "Area 1",
                    AreaLeadMarshalIdsJson = JsonSerializer.Serialize(new List<string> { areaLeadMarshalId }),
                    ContactsJson = "[]",
                    PolygonJson = "[]"
                }
            };

            List<LocationEntity> locations = new()
            {
                new LocationEntity
                {
                    RowKey = locationId,
                    EventId = eventId,
                    Name = "Location 1",
                    AreaIdsJson = JsonSerializer.Serialize(new List<string> { areaId })
                }
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    EventId = eventId,
                    MarshalId = marshalId,
                    LocationId = locationId
                }
            };

            _mockAreaRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(areas);

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(locations);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            // Act
            ContactPermissions permissions = await _service.GetContactPermissionsAsync(claims, eventId);

            // Assert
            permissions.CanViewAll.ShouldBeFalse();
            permissions.ViewableMarshalIds.ShouldContain(marshalId);
            permissions.ViewableMarshalIds.ShouldContain(areaLeadMarshalId);
        }

        [TestMethod]
        public async Task GetContactPermissions_AreaLead_CanViewMarshalsInArea()
        {
            // Arrange
            string eventId = "event-123";
            string areaLeadMarshalId = "marshal-lead-1";
            string marshalInAreaId = "marshal-2";
            string areaId = "area-1";
            string locationId = "loc-1";

            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Area Lead",
                PersonEmail: "lead@example.com",
                IsSystemAdmin: false,
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: areaLeadMarshalId,
                EventRoles: new List<EventRoleInfo>()
            );

            // Area lead is designated via AreaLeadMarshalIdsJson
            List<AreaEntity> areas = new()
            {
                new AreaEntity
                {
                    RowKey = areaId,
                    EventId = eventId,
                    Name = "Area 1",
                    AreaLeadMarshalIdsJson = JsonSerializer.Serialize(new List<string> { areaLeadMarshalId }),
                    ContactsJson = "[]",
                    PolygonJson = "[]"
                }
            };

            List<LocationEntity> locations = new()
            {
                new LocationEntity
                {
                    RowKey = locationId,
                    EventId = eventId,
                    Name = "Location 1",
                    AreaIdsJson = JsonSerializer.Serialize(new List<string> { areaId })
                }
            };

            List<AssignmentEntity> assignments = new()
            {
                new AssignmentEntity
                {
                    EventId = eventId,
                    MarshalId = marshalInAreaId,
                    LocationId = locationId
                }
            };

            _mockAreaRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(areas);

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(locations);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            // Act
            ContactPermissions permissions = await _service.GetContactPermissionsAsync(claims, eventId);

            // Assert
            permissions.CanViewAll.ShouldBeFalse();
            permissions.ViewableMarshalIds.ShouldContain(areaLeadMarshalId); // self
            permissions.ViewableMarshalIds.ShouldContain(marshalInAreaId); // marshal in their area
        }

        [TestMethod]
        public void CanViewContactDetails_WithPermission_ReturnsTrue()
        {
            // Arrange
            ContactPermissions permissions = new(
                CanViewAll: false,
                ViewableMarshalIds: new HashSet<string> { "marshal-1", "marshal-2" },
                CanModifyAll: false,
                ModifiableMarshalIds: new HashSet<string>()
            );

            // Act & Assert
            _service.CanViewContactDetails(permissions, "marshal-1").ShouldBeTrue();
            _service.CanViewContactDetails(permissions, "marshal-2").ShouldBeTrue();
            _service.CanViewContactDetails(permissions, "marshal-3").ShouldBeFalse();
        }

        [TestMethod]
        public void CanViewContactDetails_CanViewAll_ReturnsTrue()
        {
            // Arrange
            ContactPermissions permissions = new(
                CanViewAll: true,
                ViewableMarshalIds: new HashSet<string>(),
                CanModifyAll: false,
                ModifiableMarshalIds: new HashSet<string>()
            );

            // Act & Assert
            _service.CanViewContactDetails(permissions, "any-marshal").ShouldBeTrue();
        }

        [TestMethod]
        public void CanModifyMarshal_WithPermission_ReturnsTrue()
        {
            // Arrange
            ContactPermissions permissions = new(
                CanViewAll: false,
                ViewableMarshalIds: new HashSet<string>(),
                CanModifyAll: false,
                ModifiableMarshalIds: new HashSet<string> { "marshal-1" }
            );

            // Act & Assert
            _service.CanModifyMarshal(permissions, "marshal-1").ShouldBeTrue();
            _service.CanModifyMarshal(permissions, "marshal-2").ShouldBeFalse();
        }

        [TestMethod]
        public void CanModifyMarshal_CanModifyAll_ReturnsTrue()
        {
            // Arrange
            ContactPermissions permissions = new(
                CanViewAll: false,
                ViewableMarshalIds: new HashSet<string>(),
                CanModifyAll: true,
                ModifiableMarshalIds: new HashSet<string>()
            );

            // Act & Assert
            _service.CanModifyMarshal(permissions, "any-marshal").ShouldBeTrue();
        }
    }
}
