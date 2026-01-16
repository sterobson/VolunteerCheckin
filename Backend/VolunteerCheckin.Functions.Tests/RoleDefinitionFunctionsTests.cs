using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class RoleDefinitionFunctionsTests
{
    private const string EventId = "test-event-123";
    private const string SessionToken = "test-session-token";

    private Mock<ILogger<RoleDefinitionFunctions>> _mockLogger = null!;
    private Mock<IEventRoleDefinitionRepository> _mockRoleDefinitionRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IEventContactRepository> _mockContactRepository = null!;
    private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private RoleDefinitionFunctions _functions = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<RoleDefinitionFunctions>>();
        _mockRoleDefinitionRepository = new Mock<IEventRoleDefinitionRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockContactRepository = new Mock<IEventContactRepository>();
        _mockEventRoleRepository = new Mock<IEventRoleRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();

        _mockClaimsService = new Mock<ClaimsService>(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            new Mock<IUserEventMappingRepository>().Object);

        _functions = new RoleDefinitionFunctions(
            _mockLogger.Object,
            _mockRoleDefinitionRepository.Object,
            _mockMarshalRepository.Object,
            _mockContactRepository.Object,
            _mockEventRoleRepository.Object,
            _mockAreaRepository.Object,
            _mockClaimsService.Object);
    }

    #region DeleteRoleDefinition Tests

    [TestMethod]
    public async Task DeleteRoleDefinition_WithNoAssignments_DeletesSuccessfully()
    {
        // Arrange
        SetupEventAdminClaims();
        string roleId = "role-123";

        EventRoleDefinitionEntity roleDefinition = new()
        {
            PartitionKey = EventId,
            RowKey = roleId,
            EventId = EventId,
            RoleId = roleId,
            Name = "Test Role",
            Notes = "Test notes"
        };

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, roleId))
            .ReturnsAsync(roleDefinition);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity>());

        _mockRoleDefinitionRepository
            .Setup(r => r.DeleteAsync(EventId, roleId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, roleId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockRoleDefinitionRepository.Verify(r => r.DeleteAsync(EventId, roleId), Times.Once);
        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.IsAny<MarshalEntity>()), Times.Never);
        _mockContactRepository.Verify(r => r.UpdateAsync(It.IsAny<EventContactEntity>()), Times.Never);
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_WithMarshalAssignments_UnassignsAndDeletes()
    {
        // Arrange
        SetupEventAdminClaims();
        string roleId = "role-123";

        EventRoleDefinitionEntity roleDefinition = new()
        {
            PartitionKey = EventId,
            RowKey = roleId,
            EventId = EventId,
            RoleId = roleId,
            Name = "Test Role",
            Notes = "Test notes"
        };

        MarshalEntity marshal1 = new()
        {
            MarshalId = "marshal-1",
            EventId = EventId,
            Name = "Marshal 1",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId, "other-role" })
        };

        MarshalEntity marshal2 = new()
        {
            MarshalId = "marshal-2",
            EventId = EventId,
            Name = "Marshal 2",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId })
        };

        MarshalEntity marshal3 = new()
        {
            MarshalId = "marshal-3",
            EventId = EventId,
            Name = "Marshal 3 - No Role",
            RolesJson = JsonSerializer.Serialize(new List<string> { "different-role" })
        };

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, roleId))
            .ReturnsAsync(roleDefinition);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity> { marshal1, marshal2, marshal3 });

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity>());

        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        _mockRoleDefinitionRepository
            .Setup(r => r.DeleteAsync(EventId, roleId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, roleId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockRoleDefinitionRepository.Verify(r => r.DeleteAsync(EventId, roleId), Times.Once);

        // Should update marshal1 and marshal2 (who had the role), not marshal3
        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == "marshal-1" &&
            !m.RolesJson.Contains(roleId) &&
            m.RolesJson.Contains("other-role")
        )), Times.Once);

        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == "marshal-2" &&
            !m.RolesJson.Contains(roleId)
        )), Times.Once);

        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == "marshal-3"
        )), Times.Never);
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_WithContactAssignments_UnassignsAndDeletes()
    {
        // Arrange
        SetupEventAdminClaims();
        string roleId = "role-123";

        EventRoleDefinitionEntity roleDefinition = new()
        {
            PartitionKey = EventId,
            RowKey = roleId,
            EventId = EventId,
            RoleId = roleId,
            Name = "Test Role",
            Notes = "Test notes"
        };

        EventContactEntity contact1 = new()
        {
            ContactId = "contact-1",
            EventId = EventId,
            Name = "Contact 1",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId, "other-role" }),
            ScopeConfigurationsJson = "[]"
        };

        EventContactEntity contact2 = new()
        {
            ContactId = "contact-2",
            EventId = EventId,
            Name = "Contact 2",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId }),
            ScopeConfigurationsJson = "[]"
        };

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, roleId))
            .ReturnsAsync(roleDefinition);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity> { contact1, contact2 });

        _mockContactRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        _mockRoleDefinitionRepository
            .Setup(r => r.DeleteAsync(EventId, roleId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, roleId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockRoleDefinitionRepository.Verify(r => r.DeleteAsync(EventId, roleId), Times.Once);

        // Both contacts should be updated
        _mockContactRepository.Verify(r => r.UpdateAsync(It.Is<EventContactEntity>(c =>
            c.ContactId == "contact-1" &&
            !c.RolesJson.Contains(roleId)
        )), Times.Once);

        _mockContactRepository.Verify(r => r.UpdateAsync(It.Is<EventContactEntity>(c =>
            c.ContactId == "contact-2" &&
            !c.RolesJson.Contains(roleId)
        )), Times.Once);
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_WithLinkedMarshalAndContact_UnassignsBoth()
    {
        // Arrange
        SetupEventAdminClaims();
        string roleId = "role-123";
        string marshalId = "marshal-linked";

        EventRoleDefinitionEntity roleDefinition = new()
        {
            PartitionKey = EventId,
            RowKey = roleId,
            EventId = EventId,
            RoleId = roleId,
            Name = "Test Role",
            Notes = "Test notes"
        };

        // Linked marshal-contact pair
        MarshalEntity linkedMarshal = new()
        {
            MarshalId = marshalId,
            EventId = EventId,
            Name = "Linked Person",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId })
        };

        EventContactEntity linkedContact = new()
        {
            ContactId = "contact-linked",
            EventId = EventId,
            Name = "Linked Person",
            MarshalId = marshalId, // Linked to marshal
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId }),
            ScopeConfigurationsJson = "[]"
        };

        // Unlinked contact
        EventContactEntity standaloneContact = new()
        {
            ContactId = "contact-standalone",
            EventId = EventId,
            Name = "Standalone Contact",
            MarshalId = null, // Not linked
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId }),
            ScopeConfigurationsJson = "[]"
        };

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, roleId))
            .ReturnsAsync(roleDefinition);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity> { linkedMarshal });

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity> { linkedContact, standaloneContact });

        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        _mockContactRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        _mockRoleDefinitionRepository
            .Setup(r => r.DeleteAsync(EventId, roleId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, roleId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockRoleDefinitionRepository.Verify(r => r.DeleteAsync(EventId, roleId), Times.Once);

        // Marshal should be updated
        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == marshalId &&
            !m.RolesJson.Contains(roleId)
        )), Times.Once);

        // Both contacts should be updated (role removed from both)
        _mockContactRepository.Verify(r => r.UpdateAsync(It.Is<EventContactEntity>(c =>
            c.ContactId == "contact-linked" &&
            !c.RolesJson.Contains(roleId)
        )), Times.Once);

        _mockContactRepository.Verify(r => r.UpdateAsync(It.Is<EventContactEntity>(c =>
            c.ContactId == "contact-standalone" &&
            !c.RolesJson.Contains(roleId)
        )), Times.Once);
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_WithLegacyRoleName_UnassignsByNameAndId()
    {
        // Arrange
        SetupEventAdminClaims();
        string roleId = "role-123";
        string roleName = "Area Lead";

        EventRoleDefinitionEntity roleDefinition = new()
        {
            PartitionKey = EventId,
            RowKey = roleId,
            EventId = EventId,
            RoleId = roleId,
            Name = roleName,
            Notes = "Test notes"
        };

        // Marshal with role by ID
        MarshalEntity marshalWithId = new()
        {
            MarshalId = "marshal-1",
            EventId = EventId,
            Name = "Marshal With ID",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleId })
        };

        // Marshal with role by name (legacy)
        MarshalEntity marshalWithName = new()
        {
            MarshalId = "marshal-2",
            EventId = EventId,
            Name = "Marshal With Name",
            RolesJson = JsonSerializer.Serialize(new List<string> { roleName })
        };

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, roleId))
            .ReturnsAsync(roleDefinition);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity> { marshalWithId, marshalWithName });

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity>());

        _mockMarshalRepository
            .Setup(r => r.UpdateAsync(It.IsAny<MarshalEntity>()))
            .Returns(Task.CompletedTask);

        _mockRoleDefinitionRepository
            .Setup(r => r.DeleteAsync(EventId, roleId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, roleId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockRoleDefinitionRepository.Verify(r => r.DeleteAsync(EventId, roleId), Times.Once);

        // Both marshals should be updated
        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == "marshal-1"
        )), Times.Once);

        _mockMarshalRepository.Verify(r => r.UpdateAsync(It.Is<MarshalEntity>(m =>
            m.MarshalId == "marshal-2"
        )), Times.Once);
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_NonExistentRole_ReturnsNotFound()
    {
        // Arrange
        SetupEventAdminClaims();

        _mockRoleDefinitionRepository
            .Setup(r => r.GetAsync(EventId, It.IsAny<string>()))
            .ReturnsAsync((EventRoleDefinitionEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task DeleteRoleDefinition_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange - no auth setup

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.DeleteRoleDefinition(httpRequest, EventId, "role-123");

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    #endregion

    #region Helper Methods

    private void SetupEventAdminClaims()
    {
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(SessionToken, EventId))
            .ReturnsAsync(new UserClaims(
                PersonId: "person-1",
                PersonName: "Admin User",
                PersonEmail: "admin@test.com",
                IsSystemAdmin: false,
                EventId: EventId,
                AuthMethod: Constants.AuthMethodSecureEmailLink,
                MarshalId: null,
                EventRoles: new List<EventRoleInfo>
                {
                    new EventRoleInfo(Constants.RoleEventAdmin, new List<string>())
                }
            ));
    }

    #endregion
}
