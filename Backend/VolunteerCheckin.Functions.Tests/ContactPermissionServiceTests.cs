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
        private Mock<ILocationRepository> _mockLocationRepository = null!;
        private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
        private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
        private Mock<IMarshalRepository> _mockMarshalRepository = null!;
        private ContactPermissionService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockLocationRepository = new Mock<ILocationRepository>();
            _mockAssignmentRepository = new Mock<IAssignmentRepository>();
            _mockEventRoleRepository = new Mock<IEventRoleRepository>();
            _mockMarshalRepository = new Mock<IMarshalRepository>();

            _service = new ContactPermissionService(
                _mockLocationRepository.Object,
                _mockAssignmentRepository.Object,
                _mockEventRoleRepository.Object,
                _mockMarshalRepository.Object
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
        public async Task GetContactPermissions_Marshal_CanViewAndModifySelf()
        {
            // Arrange
            string eventId = "event-123";
            string marshalId = "marshal-456";

            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: marshalId,
                EventRoles: new List<EventRoleInfo>()
            );

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<LocationEntity>());

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<AssignmentEntity>());

            _mockMarshalRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<MarshalEntity>());

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<EventRoleEntity>());

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
            string areaLeadPersonId = "person-lead-789";
            string areaId = "area-1";
            string locationId = "loc-1";

            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Marshal User",
                PersonEmail: "marshal@example.com",
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: marshalId,
                EventRoles: new List<EventRoleInfo>()
            );

            // Marshal is assigned to a location in area-1
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

            // Area lead is defined via EventRole
            List<MarshalEntity> marshals = new()
            {
                new MarshalEntity { RowKey = areaLeadMarshalId, MarshalId = areaLeadMarshalId, PersonId = areaLeadPersonId, EventId = eventId }
            };

            List<EventRoleEntity> eventRoles = new()
            {
                new EventRoleEntity
                {
                    PersonId = areaLeadPersonId,
                    EventId = eventId,
                    Role = Constants.RoleEventAreaLead,
                    AreaIdsJson = JsonSerializer.Serialize(new List<string> { areaId })
                }
            };

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(locations);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            _mockMarshalRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(marshals);

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(eventRoles);

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

            // Area lead has EventAreaLead role via EventRoles in claims
            UserClaims claims = new(
                PersonId: "person-1",
                PersonName: "Area Lead",
                PersonEmail: "lead@example.com",
                EventId: eventId,
                AuthMethod: Constants.AuthMethodMarshalMagicCode,
                MarshalId: areaLeadMarshalId,
                EventRoles: new List<EventRoleInfo>
                {
                    new EventRoleInfo(Constants.RoleEventAreaLead, new List<string> { areaId })
                }
            );

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

            _mockLocationRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(locations);

            _mockAssignmentRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(assignments);

            _mockMarshalRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<MarshalEntity>());

            _mockEventRoleRepository
                .Setup(r => r.GetByEventAsync(eventId))
                .ReturnsAsync(new List<EventRoleEntity>());

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
