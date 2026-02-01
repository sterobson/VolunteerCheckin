using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System.Text.Json;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class EventContactFunctionsTests
{
    private const string EventId = "test-event-123";
    private const string SessionToken = "test-session-token";

    private Mock<ILogger<EventContactFunctions>> _mockLogger = null!;
    private Mock<IEventContactRepository> _mockContactRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<IEventRoleRepository> _mockEventRoleRepository = null!;
    private Mock<IEventRoleDefinitionRepository> _mockRoleDefinitionRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private EventContactFunctions _functions = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<EventContactFunctions>>();
        _mockContactRepository = new Mock<IEventContactRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockEventRoleRepository = new Mock<IEventRoleRepository>();
        _mockRoleDefinitionRepository = new Mock<IEventRoleDefinitionRepository>();

        _mockClaimsService = new Mock<ClaimsService>(
            MockBehavior.Loose,
            new Mock<IAuthSessionRepository>().Object,
            new Mock<IPersonRepository>().Object,
            new Mock<IEventRoleRepository>().Object,
            new Mock<IMarshalRepository>().Object,
            Mock.Of<ISampleEventService>(),
            Mock.Of<IEventDeletionRepository>());

        _functions = new EventContactFunctions(
            _mockLogger.Object,
            _mockContactRepository.Object,
            _mockLocationRepository.Object,
            _mockMarshalRepository.Object,
            _mockAssignmentRepository.Object,
            _mockAreaRepository.Object,
            _mockEventRoleRepository.Object,
            _mockRoleDefinitionRepository.Object,
            _mockClaimsService.Object);
    }

    #region CreateContact Tests

    [TestMethod]
    public async Task CreateContact_WithValidRequest_CreatesContact()
    {
        // Arrange
        SetupEventAdminClaims();

        CreateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEmergency],
            Name: "John Smith",
            Phone: "123-456-7890",
            Email: "john@example.com",
            Notes: "Primary emergency contact"
        );

        _mockContactRepository
            .Setup(r => r.AddAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateContact(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        EventContactResponse response = (EventContactResponse)((OkObjectResult)result).Value!;
        response.Name.ShouldBe("John Smith");
        response.Roles.ShouldContain(Constants.ContactRoleEmergency);
        response.Phone.ShouldBe("123-456-7890");

        _mockContactRepository.Verify(r => r.AddAsync(It.Is<EventContactEntity>(c =>
            c.EventId == EventId &&
            c.Name == "John Smith" &&
            c.RolesJson.Contains(Constants.ContactRoleEmergency)
        )), Times.Once);
    }

    [TestMethod]
    public async Task CreateContact_WithLinkedMarshal_IncludesMarshalId()
    {
        // Arrange
        SetupEventAdminClaims();
        string marshalId = "marshal-123";

        MarshalEntity marshal = new()
        {
            MarshalId = marshalId,
            EventId = EventId,
            Name = "Jane Doe"
        };

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, marshalId))
            .ReturnsAsync(marshal);

        CreateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEventDirector],
            Name: "Jane Doe",
            Phone: "987-654-3210",
            MarshalId: marshalId
        );

        _mockContactRepository
            .Setup(r => r.AddAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateContact(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        EventContactResponse response = (EventContactResponse)((OkObjectResult)result).Value!;
        response.MarshalId.ShouldBe(marshalId);
    }

    [TestMethod]
    public async Task CreateContact_WithInvalidMarshalId_ReturnsBadRequest()
    {
        // Arrange
        SetupEventAdminClaims();

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, It.IsAny<string>()))
            .ReturnsAsync((MarshalEntity?)null);

        CreateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEmergency],
            Name: "Invalid",
            Phone: "123",
            MarshalId: "nonexistent-marshal"
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateContact(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task CreateContact_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange - no auth setup

        CreateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEmergency],
            Name: "Test",
            Phone: "123"
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CreateContact(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task CreateContact_WithCustomScope_UsesProvidedScope()
    {
        // Arrange
        SetupEventAdminClaims();

        List<ScopeConfiguration> scopes =
        [
            new ScopeConfiguration
            {
                Scope = Constants.ChecklistScopeEveryoneAtCheckpoints,
                ItemType = "Checkpoint",
                Ids = ["checkpoint-1", "checkpoint-2"]
            }
        ];

        CreateEventContactRequest request = new(
            Roles: [Constants.ContactRoleMedicalLead],
            Name: "Medical Lead",
            Phone: "555-1234",
            ScopeConfigurations: scopes
        );

        _mockContactRepository
            .Setup(r => r.AddAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.CreateContact(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        EventContactResponse response = (EventContactResponse)((OkObjectResult)result).Value!;
        response.ScopeConfigurations.Count.ShouldBe(1);
        response.ScopeConfigurations[0].Scope.ShouldBe(Constants.ChecklistScopeEveryoneAtCheckpoints);
    }

    #endregion

    #region GetContacts Tests

    [TestMethod]
    public async Task GetContacts_AsAdmin_ReturnsAllContacts()
    {
        // Arrange
        SetupEventAdminClaims();

        List<EventContactEntity> contacts =
        [
            new EventContactEntity
            {
                ContactId = "contact-1",
                EventId = EventId,
                Name = "Contact 1",
                Role = Constants.ContactRoleEmergency,
                Phone = "111",
                ScopeConfigurationsJson = "[]"
            },
            new EventContactEntity
            {
                ContactId = "contact-2",
                EventId = EventId,
                Name = "Contact 2",
                Role = Constants.ContactRoleEventDirector,
                Phone = "222",
                ScopeConfigurationsJson = "[]"
            }
        ];

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(contacts);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<MarshalEntity>());

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetContacts(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        List<EventContactResponse> responses = (List<EventContactResponse>)((OkObjectResult)result).Value!;
        responses.Count.ShouldBe(2);
    }

    #endregion

    #region UpdateContact Tests

    [TestMethod]
    public async Task UpdateContact_WithValidRequest_UpdatesContact()
    {
        // Arrange
        SetupEventAdminClaims();
        string contactId = "contact-123";

        EventContactEntity existingContact = new()
        {
            ContactId = contactId,
            EventId = EventId,
            Name = "Old Name",
            Role = Constants.ContactRoleEmergency,
            Phone = "111",
            ScopeConfigurationsJson = "[]"
        };

        _mockContactRepository
            .Setup(r => r.GetAsync(EventId, contactId))
            .ReturnsAsync(existingContact);

        _mockContactRepository
            .Setup(r => r.UpdateAsync(It.IsAny<EventContactEntity>()))
            .Returns(Task.CompletedTask);

        UpdateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEventDirector],
            Name: "New Name",
            Phone: "999"
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateContact(httpRequest, EventId, contactId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        EventContactResponse response = (EventContactResponse)((OkObjectResult)result).Value!;
        response.Name.ShouldBe("New Name");
        response.Roles.ShouldContain(Constants.ContactRoleEventDirector);

        _mockContactRepository.Verify(r => r.UpdateAsync(It.Is<EventContactEntity>(c =>
            c.Name == "New Name" &&
            c.RolesJson.Contains(Constants.ContactRoleEventDirector)
        )), Times.Once);
    }

    [TestMethod]
    public async Task UpdateContact_NonExistent_ReturnsNotFound()
    {
        // Arrange
        SetupEventAdminClaims();

        _mockContactRepository
            .Setup(r => r.GetAsync(EventId, It.IsAny<string>()))
            .ReturnsAsync((EventContactEntity?)null);

        UpdateEventContactRequest request = new(
            Roles: [Constants.ContactRoleEmergency],
            Name: "Test",
            Phone: "123"
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdateContact(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteContact Tests

    [TestMethod]
    public async Task DeleteContact_ExistingContact_DeletesSuccessfully()
    {
        // Arrange
        SetupEventAdminClaims();
        string contactId = "contact-123";

        EventContactEntity existingContact = new()
        {
            ContactId = contactId,
            EventId = EventId,
            Name = "To Delete",
            Role = Constants.ContactRoleEmergency,
            Phone = "123",
            ScopeConfigurationsJson = "[]"
        };

        _mockContactRepository
            .Setup(r => r.GetAsync(EventId, contactId))
            .ReturnsAsync(existingContact);

        _mockContactRepository
            .Setup(r => r.DeleteAsync(EventId, contactId))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.DeleteContact(httpRequest, EventId, contactId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockContactRepository.Verify(r => r.DeleteAsync(EventId, contactId), Times.Once);
    }

    #endregion

    #region GetContactsForMarshal Tests

    [TestMethod]
    public async Task GetContactsForMarshal_WithMatchingScope_ReturnsRelevantContacts()
    {
        // Arrange
        string marshalId = "marshal-123";
        string locationId = "location-456";
        string areaId = "area-789";

        MarshalEntity marshal = new()
        {
            MarshalId = marshalId,
            EventId = EventId,
            Name = "Test Marshal"
        };

        LocationEntity location = new()
        {
            RowKey = locationId,
            EventId = EventId,
            Name = "Test Location",
            AreaIdsJson = $"[\"{areaId}\"]"
        };

        AssignmentEntity assignment = new()
        {
            EventId = EventId,
            MarshalId = marshalId,
            LocationId = locationId
        };

        // Contact visible to everyone
        EventContactEntity contactForAll = new()
        {
            ContactId = "contact-all",
            EventId = EventId,
            Name = "Emergency",
            Role = Constants.ContactRoleEmergency,
            Phone = "911",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new ScopeConfiguration
                {
                    Scope = Constants.ChecklistScopeEveryoneInAreas,
                    ItemType = "Area",
                    Ids = [Constants.AllAreas]
                }
            })
        };

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, marshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, marshalId))
            .ReturnsAsync(new List<AssignmentEntity> { assignment });

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<LocationEntity> { location });

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<AreaEntity>());

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(new List<EventContactEntity> { contactForAll });

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.GetContactsForMarshal(httpRequest, EventId, marshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        List<EventContactForMarshalResponse> responses = (List<EventContactForMarshalResponse>)((OkObjectResult)result).Value!;
        responses.Count.ShouldBe(1);
        responses[0].Name.ShouldBe("Emergency");
    }

    [TestMethod]
    public async Task GetContactsForMarshal_NonExistentMarshal_ReturnsNotFound()
    {
        // Arrange
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, It.IsAny<string>()))
            .ReturnsAsync((MarshalEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequest();

        // Act
        IActionResult result = await _functions.GetContactsForMarshal(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetContactRoles Tests

    [TestMethod]
    public async Task GetContactRoles_ReturnsBuiltInAndCustomRoles()
    {
        // Arrange
        SetupEventAdminClaims();

        List<EventContactEntity> contacts =
        [
            new EventContactEntity
            {
                ContactId = "contact-1",
                EventId = EventId,
                Name = "Emergency",
                Role = Constants.ContactRoleEmergency,
                Phone = "111",
                ScopeConfigurationsJson = "[]"
            },
            new EventContactEntity
            {
                ContactId = "contact-2",
                EventId = EventId,
                Name = "Custom Role Person",
                Role = "CustomRole",
                Phone = "222",
                ScopeConfigurationsJson = "[]"
            }
        ];

        _mockContactRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(contacts);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetContactRoles(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        ContactRolesResponse response = (ContactRolesResponse)((OkObjectResult)result).Value!;
        response.BuiltInRoles.ShouldContain(Constants.ContactRoleEmergency);
        response.BuiltInRoles.ShouldContain(Constants.ContactRoleEventDirector);
        response.CustomRoles.ShouldContain("CustomRole");
        response.CustomRoles.ShouldNotContain(Constants.ContactRoleEmergency);
    }

    #endregion

    #region Helper Methods

    private void SetupEventAdminClaims()
    {
        UserClaims claims = new UserClaims(
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
        );
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(SessionToken, EventId))
            .ReturnsAsync(claims);
        _mockClaimsService
            .Setup(c => c.GetClaimsWithSampleSupportAsync(It.IsAny<string?>(), It.IsAny<string?>(), EventId))
            .ReturnsAsync(claims);
    }

    #endregion
}
