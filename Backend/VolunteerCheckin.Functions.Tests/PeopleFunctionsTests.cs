using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for PeopleFunctions - person management operations.
/// </summary>
[TestClass]
public class PeopleFunctionsTests
{
    private Mock<ILogger<PeopleFunctions>> _mockLogger = null!;
    private Mock<IPersonRepository> _mockPersonRepository = null!;
    private Mock<IEventRoleRepository> _mockRoleRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private PeopleFunctions _functions = null!;

    private const string EventId = "event123";
    private const string PersonId = "person123";
    private const string AdminPersonId = "admin456";
    private const string PersonName = "Test User";
    private const string PersonEmail = "test@example.com";
    private const string AdminEmail = "admin@test.com";
    private const string SessionToken = "valid-session-token";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<PeopleFunctions>>();
        _mockPersonRepository = new Mock<IPersonRepository>();
        _mockRoleRepository = new Mock<IEventRoleRepository>();

        _mockClaimsService = new Mock<ClaimsService>(
            Mock.Of<IAuthSessionRepository>(),
            Mock.Of<IPersonRepository>(),
            Mock.Of<IEventRoleRepository>(),
            Mock.Of<IMarshalRepository>(),
            Mock.Of<IUserEventMappingRepository>()
        );

        _functions = new PeopleFunctions(
            _mockLogger.Object,
            _mockPersonRepository.Object,
            _mockRoleRepository.Object,
            _mockClaimsService.Object
        );
    }

    private UserClaims CreateAdminClaims()
    {
        return new UserClaims(
            PersonId: AdminPersonId,
            PersonName: "Admin User",
            PersonEmail: AdminEmail,
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodSecureEmailLink,
            MarshalId: null,
            EventRoles: [new EventRoleInfo(Constants.RoleEventAdmin, [])]
        );
    }

    private UserClaims CreateNonAdminClaims()
    {
        return new UserClaims(
            PersonId: PersonId,
            PersonName: PersonName,
            PersonEmail: PersonEmail,
            IsSystemAdmin: false,
            EventId: EventId,
            AuthMethod: Constants.AuthMethodMarshalMagicCode,
            MarshalId: "marshal123",
            EventRoles: []
        );
    }

    private void SetupClaimsService(UserClaims? claims)
    {
        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(claims);
    }

    private PersonEntity CreatePersonEntity(string personId, string name, string email)
    {
        return new PersonEntity
        {
            PartitionKey = "PERSON",
            RowKey = personId,
            PersonId = personId,
            Name = name,
            Email = email,
            Phone = "555-1234",
            IsSystemAdmin = false,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
    }

    #region GetPerson Tests

    [TestMethod]
    public async Task GetPerson_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity person = CreatePersonEntity(PersonId, PersonName, PersonEmail);

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuthAndQuery(
            SessionToken,
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        PersonDetailsResponse response = (PersonDetailsResponse)okResult.Value!;

        response.PersonId.ShouldBe(PersonId);
        response.Name.ShouldBe(PersonName);
        response.Email.ShouldBe(PersonEmail);
        response.Phone.ShouldBe("555-1234");
    }

    [TestMethod]
    public async Task GetPerson_WithRoles_ReturnsRolesInResponse()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity person = CreatePersonEntity(PersonId, PersonName, PersonEmail);

        List<EventRoleEntity> roles =
        [
            new EventRoleEntity
            {
                PartitionKey = PersonId,
                RowKey = $"{EventId}#role1",
                PersonId = PersonId,
                EventId = EventId,
                Role = Constants.RoleEventAreaLead,
                AreaIdsJson = "[\"area1\", \"area2\"]"
            }
        ];

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(person);

        _mockRoleRepository
            .Setup(r => r.GetByPersonAndEventAsync(PersonId, EventId))
            .ReturnsAsync(roles);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuthAndQuery(
            SessionToken,
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        PersonDetailsResponse response = (PersonDetailsResponse)okResult.Value!;

        response.EventRoles.Count.ShouldBe(1);
        response.EventRoles[0].Role.ShouldBe(Constants.RoleEventAreaLead);
        response.EventRoles[0].AreaIds.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task GetPerson_NoSessionToken_ReturnsUnauthorized()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithHeaders(
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task GetPerson_MissingEventId_ReturnsBadRequest()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuth(SessionToken);

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task GetPerson_InvalidSession_ReturnsUnauthorized()
    {
        // Arrange
        SetupClaimsService(null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuthAndQuery(
            SessionToken,
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task GetPerson_NonAdmin_ReturnsForbid()
    {
        // Arrange
        SetupClaimsService(CreateNonAdminClaims());

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuthAndQuery(
            SessionToken,
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<ForbidResult>();
    }

    [TestMethod]
    public async Task GetPerson_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync((PersonEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateEmptyHttpRequestWithAuthAndQuery(
            SessionToken,
            new Dictionary<string, string> { { "eventId", EventId } }
        );

        // Act
        IActionResult result = await _functions.GetPerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region UpdatePerson Tests

    [TestMethod]
    public async Task UpdatePerson_ValidRequest_ReturnsOk()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity existingPerson = CreatePersonEntity(PersonId, "Old Name", "old@example.com");

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "new@example.com",
            Phone: "555-9999"
        );

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(existingPerson);

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync("new@example.com"))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedEntity = null;
        _mockPersonRepository
            .Setup(r => r.UpdateAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        PersonInfo response = (PersonInfo)okResult.Value!;

        response.Name.ShouldBe("New Name");
        response.Email.ShouldBe("new@example.com");
        response.Phone.ShouldBe("555-9999");

        capturedEntity.ShouldNotBeNull();
        capturedEntity.Name.ShouldBe("New Name");
        capturedEntity.Email.ShouldBe("new@example.com");
    }

    [TestMethod]
    public async Task UpdatePerson_SameEmail_Succeeds()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity existingPerson = CreatePersonEntity(PersonId, "Old Name", PersonEmail);

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: PersonEmail, // Same email
            Phone: "555-9999"
        );

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(existingPerson);

        _mockPersonRepository
            .Setup(r => r.UpdateAsync(It.IsAny<PersonEntity>()))
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_EmailAlreadyInUse_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity existingPerson = CreatePersonEntity(PersonId, "Old Name", "old@example.com");
        PersonEntity otherPerson = CreatePersonEntity("other-person", "Other User", "taken@example.com");

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "taken@example.com",
            Phone: "555-9999"
        );

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(existingPerson);

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync("taken@example.com"))
            .ReturnsAsync(otherPerson);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_NoSessionToken_ReturnsUnauthorized()
    {
        // Arrange
        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "new@example.com",
            Phone: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_MissingEventId_ReturnsBadRequest()
    {
        // Arrange
        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "new@example.com",
            Phone: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuth(request, SessionToken);

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_NonAdmin_ReturnsForbid()
    {
        // Arrange
        SetupClaimsService(CreateNonAdminClaims());

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "new@example.com",
            Phone: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<ForbidResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_MissingName_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        UpdatePersonRequest request = new(
            Name: "",
            Email: "new@example.com",
            Phone: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_InvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "not-an-email",
            Phone: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_PersonNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "new@example.com",
            Phone: null
        );

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync((PersonEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UpdatePerson_NormalizesEmail()
    {
        // Arrange
        SetupClaimsService(CreateAdminClaims());

        PersonEntity existingPerson = CreatePersonEntity(PersonId, "Old Name", "old@example.com");

        UpdatePersonRequest request = new(
            Name: "New Name",
            Email: "NEW@EXAMPLE.COM", // Uppercase (will be normalized to lowercase)
            Phone: null
        );

        _mockPersonRepository
            .Setup(r => r.GetAsync(PersonId))
            .ReturnsAsync(existingPerson);

        _mockPersonRepository
            .Setup(r => r.GetByEmailAsync("new@example.com"))
            .ReturnsAsync((PersonEntity?)null);

        PersonEntity? capturedEntity = null;
        _mockPersonRepository
            .Setup(r => r.UpdateAsync(It.IsAny<PersonEntity>()))
            .Callback<PersonEntity>(e => capturedEntity = e)
            .Returns(Task.CompletedTask);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAuthAndHeaders(
            request,
            SessionToken,
            new Dictionary<string, string>()
        );
        httpRequest.QueryString = new QueryString($"?eventId={EventId}");

        // Act
        IActionResult result = await _functions.UpdatePerson(httpRequest, PersonId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        capturedEntity.ShouldNotBeNull();
        capturedEntity.Email.ShouldBe("new@example.com"); // Lowercase and trimmed
    }

    #endregion
}
