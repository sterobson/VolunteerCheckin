using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Tests;

/// <summary>
/// Tests for ChecklistFunctions CRUD operations.
/// Query tests are in ChecklistQueryFunctionsTests.
/// Completion tests are in ChecklistCompletionFunctionsTests.
/// </summary>
[TestClass]
public class ChecklistFunctionsTests
{
    private Mock<ILogger<ChecklistFunctions>> _mockLogger = null!;
    private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
    private Mock<IChecklistCompletionRepository> _mockChecklistCompletionRepository = null!;
    private Mock<ClaimsService> _mockClaimsService = null!;
    private ChecklistFunctions _functions = null!;

    private const string EventId = "event123";
    private const string ItemId = "item456";
    private const string MarshalId = "marshal789";
    private const string AdminEmail = "admin@test.com";
    private const string AreaId = "area1";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ChecklistFunctions>>();
        _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
        _mockChecklistCompletionRepository = new Mock<IChecklistCompletionRepository>();
        _mockClaimsService = new Mock<ClaimsService>(null!, null!, null!, null!, null!, null!);

        SetupClaimsService();

        _functions = new ChecklistFunctions(
            _mockLogger.Object,
            _mockChecklistItemRepository.Object,
            _mockChecklistCompletionRepository.Object,
            _mockClaimsService.Object
        );
    }

    private void SetupClaimsService()
    {
        UserClaims adminClaims = new(
            PersonId: "person123",
            PersonName: "Admin User",
            PersonEmail: AdminEmail,
            EventId: EventId,
            AuthMethod: "SecureEmailLink",
            MarshalId: null,
            EventRoles: new List<EventRoleInfo> { new(Constants.RoleEventAdmin, new List<string>()) }
        );

        _mockClaimsService
            .Setup(c => c.GetClaimsAsync(It.IsAny<string?>(), It.IsAny<string>()))
            .ReturnsAsync(adminClaims);
    }

    #region CreateChecklistItem Tests

    [TestMethod]
    public async Task CreateChecklistItem_ValidRequest_ReturnsOk()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Collect hi-viz vest",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 1,
            IsRequired: true,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        ChecklistItemEntity? capturedEntity = null;
        _mockChecklistItemRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistItemEntity>()))
            .Callback<ChecklistItemEntity>(e => capturedEntity = e)
            .ReturnsAsync((ChecklistItemEntity e) => e);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        ChecklistItemResponse response = (ChecklistItemResponse)okResult.Value!;

        response.EventId.ShouldBe(EventId);
        response.Text.ShouldBe("Collect hi-viz vest");
        response.ScopeConfigurations.Count.ShouldBe(1);
        response.ScopeConfigurations[0].Scope.ShouldBe(Constants.ChecklistScopeSpecificPeople);
        response.DisplayOrder.ShouldBe(1);
        response.IsRequired.ShouldBeTrue();

        capturedEntity.ShouldNotBeNull();
        capturedEntity.CreatedByAdminEmail.ShouldBe(AdminEmail);
        capturedEntity.CreatedDate.ShouldBeInRange(DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [TestMethod]
    public async Task CreateChecklistItem_InvalidRequest_ReturnsServerError()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader("invalid json", AdminEmail);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<StatusCodeResult>();
        StatusCodeResult statusResult = (StatusCodeResult)result;
        statusResult.StatusCode.ShouldBe(500);
    }

    [TestMethod]
    public async Task CreateChecklistItem_SanitizesInput()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Test <script>alert('xss')</script> item",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 1,
            IsRequired: true,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        ChecklistItemEntity? capturedEntity = null;
        _mockChecklistItemRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistItemEntity>()))
            .Callback<ChecklistItemEntity>(e => capturedEntity = e)
            .ReturnsAsync((ChecklistItemEntity e) => e);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        capturedEntity.ShouldNotBeNull();
        capturedEntity.Text.ShouldNotContain("<script>");
        capturedEntity.Text.ShouldNotContain("</script>");
    }

    #endregion

    #region GetChecklistItems Tests

    [TestMethod]
    public async Task GetChecklistItems_ReturnsAllItems()
    {
        // Arrange
        List<ChecklistItemEntity> items =
        [
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item1",
                EventId = EventId,
                ItemId = "item1",
                Text = "Item 1",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
                }),
                DisplayOrder = 1,
                IsRequired = true,
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            },
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item2",
                EventId = EventId,
                ItemId = "item2",
                Text = "Item 2",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = ["area1"] }
                }),
                DisplayOrder = 2,
                IsRequired = false,
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            }
        ];

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(items);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetChecklistItems(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemResponse> response = (List<ChecklistItemResponse>)okResult.Value!;

        response.Count.ShouldBe(2);
        response[0].ItemId.ShouldBe("item1");
        response[1].ItemId.ShouldBe("item2");
    }

    #endregion

    #region GetChecklistItem Tests

    [TestMethod]
    public async Task GetChecklistItem_ItemExists_ReturnsOk()
    {
        // Arrange
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        ChecklistItemResponse response = (ChecklistItemResponse)okResult.Value!;

        response.ItemId.ShouldBe(ItemId);
        response.Text.ShouldBe("Test item");
    }

    [TestMethod]
    public async Task GetChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region UpdateChecklistItem Tests

    [TestMethod]
    public async Task UpdateChecklistItem_ValidRequest_ReturnsOk()
    {
        // Arrange
        ChecklistItemEntity existingItem = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Old text",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow.AddDays(-1)
        };

        UpdateChecklistItemRequest request = new(
            Text: "Updated text",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = [AreaId] }],
            DisplayOrder: 2,
            IsRequired: false,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null
        );

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(existingItem);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UpdateChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        ChecklistItemResponse response = (ChecklistItemResponse)okResult.Value!;

        response.Text.ShouldBe("Updated text");
        response.ScopeConfigurations.Count.ShouldBe(1);
        response.ScopeConfigurations[0].Scope.ShouldBe(Constants.ChecklistScopeEveryoneInAreas);
        response.DisplayOrder.ShouldBe(2);
        response.IsRequired.ShouldBeFalse();

        _mockChecklistItemRepository.Verify(r => r.UpdateAsync(It.IsAny<ChecklistItemEntity>()), Times.Once);
    }

    [TestMethod]
    public async Task UpdateChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        UpdateChecklistItemRequest request = new(
            Text: "Updated text",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 1,
            IsRequired: true,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null
        );

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UpdateChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteChecklistItem Tests

    [TestMethod]
    public async Task DeleteChecklistItem_ItemExists_ReturnsNoContent()
    {
        // Arrange
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.DeleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockChecklistItemRepository.Verify(r => r.DeleteAsync(EventId, ItemId), Times.Once);
    }

    [TestMethod]
    public async Task DeleteChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.DeleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task DeleteChecklistItem_DeletesAssociatedCompletions()
    {
        // Arrange
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Test item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                PartitionKey = EventId,
                RowKey = $"{ItemId}#completion1",
                EventId = EventId,
                ChecklistItemId = ItemId,
                ContextOwnerMarshalId = MarshalId,
                IsDeleted = false
            }
        ];

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync(completions);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.DeleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockChecklistCompletionRepository.Verify(
            r => r.DeleteAllByItemAsync(EventId, ItemId),
            Times.Once);
    }

    #endregion

    #region CreateSeparateItems Tests

    [TestMethod]
    public async Task CreateChecklistItem_WithCreateSeparateItems_CreatesMultipleItems()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Item 1\nItem 2\nItem 3",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 1,
            IsRequired: true,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null,
            CreateSeparateItems: true
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        List<ChecklistItemEntity> capturedEntities = [];
        _mockChecklistItemRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistItemEntity>()))
            .Callback<ChecklistItemEntity>(e => capturedEntities.Add(e))
            .ReturnsAsync((ChecklistItemEntity e) => e);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;

        // Deserialize the response
        string json = JsonSerializer.Serialize(okResult.Value);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("count").GetInt32().ShouldBe(3);
        capturedEntities.Count.ShouldBe(3);
        capturedEntities[0].Text.ShouldBe("Item 1");
        capturedEntities[1].Text.ShouldBe("Item 2");
        capturedEntities[2].Text.ShouldBe("Item 3");
        capturedEntities[0].DisplayOrder.ShouldBe(1);
        capturedEntities[1].DisplayOrder.ShouldBe(2);
        capturedEntities[2].DisplayOrder.ShouldBe(3);
    }

    [TestMethod]
    public async Task CreateChecklistItem_WithCreateSeparateItems_IgnoresEmptyLines()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Item 1\n\n\nItem 2\n  \nItem 3",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 10,
            IsRequired: false,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null,
            CreateSeparateItems: true
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        List<ChecklistItemEntity> capturedEntities = [];
        _mockChecklistItemRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistItemEntity>()))
            .Callback<ChecklistItemEntity>(e => capturedEntities.Add(e))
            .ReturnsAsync((ChecklistItemEntity e) => e);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        capturedEntities.Count.ShouldBe(3);
        capturedEntities[0].Text.ShouldBe("Item 1");
        capturedEntities[1].Text.ShouldBe("Item 2");
        capturedEntities[2].Text.ShouldBe("Item 3");
    }

    [TestMethod]
    public async Task CreateChecklistItem_WithCreateSeparateItems_EmptyText_ReturnsBadRequest()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "\n\n\n",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeSpecificPeople, ItemType = "Marshal", Ids = [Constants.AllMarshals] }],
            DisplayOrder: 1,
            IsRequired: true,
            VisibleFrom: null,
            VisibleUntil: null,
            MustCompleteBy: null,
            CreateSeparateItems: true
        );

        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.CreateChecklistItem(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
        BadRequestObjectResult badRequest = (BadRequestObjectResult)result;
        badRequest.Value.ShouldNotBeNull();
    }

    #endregion
}
