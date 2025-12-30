using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
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

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class ChecklistFunctionsTests
{
    private Mock<ILogger<ChecklistFunctions>> _mockLogger = null!;
    private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
    private Mock<IChecklistCompletionRepository> _mockChecklistCompletionRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private ChecklistFunctions _functions = null!;

    private const string EventId = "event123";
    private const string ItemId = "item456";
    private const string MarshalId = "marshal789";
    private const string AdminEmail = "admin@test.com";
    private const string AreaId = "area1";
    private const string LocationId = "location1";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ChecklistFunctions>>();
        _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
        _mockChecklistCompletionRepository = new Mock<IChecklistCompletionRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();

        _functions = new ChecklistFunctions(
            _mockLogger.Object,
            _mockChecklistItemRepository.Object,
            _mockChecklistCompletionRepository.Object,
            _mockMarshalRepository.Object,
            _mockAssignmentRepository.Object,
            _mockLocationRepository.Object,
            _mockAreaRepository.Object
        );
    }

    #region CreateChecklistItem Tests

    [TestMethod]
    public async Task CreateChecklistItem_ValidRequest_ReturnsOk()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Collect hi-viz vest",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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
        response.ScopeConfigurations[0].Scope.ShouldBe(Constants.ChecklistScopeEveryone);
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
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
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
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
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
                CompletedByMarshalId = MarshalId,
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

    #region GetMarshalChecklist Tests

    [TestMethod]
    public async Task GetMarshalChecklist_MarshalExists_ReturnsRelevantItems()
    {
        // Arrange
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        AssignmentEntity assignment = new()
        {
            PartitionKey = EventId,
            RowKey = "assignment1",
            MarshalId = MarshalId,
            LocationId = LocationId
        };

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            EventId = EventId,
            Name = "Test Location",
            Latitude = 51.5074,
            Longitude = -0.1278,
            AreaIdsJson = JsonSerializer.Serialize(new[] { AreaId })
        };

        List<ChecklistItemEntity> items =
        [
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item1",
                EventId = EventId,
                ItemId = "item1",
                Text = "Everyone item",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 1,
                IsRequired = true,
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            }
        ];

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([assignment]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(items);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> response = (List<ChecklistItemWithStatus>)okResult.Value!;

        response.Count.ShouldBe(1);
        response[0].ItemId.ShouldBe("item1");
        response[0].CanBeCompletedByMe.ShouldBeTrue();
        response[0].IsCompleted.ShouldBeFalse();
    }

    [TestMethod]
    public async Task GetMarshalChecklist_MarshalNotFound_ReturnsEmptyList()
    {
        // Arrange
        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync((MarshalEntity?)null);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> response = (List<ChecklistItemWithStatus>)okResult.Value!;
        response.Count.ShouldBe(0);
    }

    #endregion

    #region CompleteChecklistItem Tests

    [TestMethod]
    public async Task CompleteChecklistItem_ValidRequest_ReturnsOk()
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        ChecklistCompletionEntity? capturedCompletion = null;
        _mockChecklistCompletionRepository
            .Setup(r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()))
            .Callback<ChecklistCompletionEntity>(c => capturedCompletion = c)
            .ReturnsAsync((ChecklistCompletionEntity c) => c);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        capturedCompletion.ShouldNotBeNull();
        capturedCompletion.EventId.ShouldBe(EventId);
        capturedCompletion.ChecklistItemId.ShouldBe(ItemId);
        capturedCompletion.CompletedByMarshalId.ShouldBe(MarshalId);
        capturedCompletion.CompletedByMarshalName.ShouldBe("John Doe");
        capturedCompletion.IsDeleted.ShouldBeFalse();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_MarshalNotFound_ReturnsNotFound()
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync((MarshalEntity?)null);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task CompleteChecklistItem_AlreadyCompleted_ReturnsBadRequest()
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        List<ChecklistCompletionEntity> existingCompletions =
        [
            new ChecklistCompletionEntity
            {
                PartitionKey = EventId,
                RowKey = $"{ItemId}#completion1",
                EventId = EventId,
                ChecklistItemId = ItemId,
                CompletedByMarshalId = MarshalId,
                CompletionContextType = Constants.ChecklistContextPersonal,
                CompletionContextId = MarshalId,
                IsDeleted = false
            }
        ];

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync(existingCompletions);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region UncompleteChecklistItem Tests

    [TestMethod]
    public async Task UncompleteChecklistItem_ValidRequest_ReturnsNoContent()
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = EventId,
            RowKey = $"{ItemId}#completion1",
            EventId = EventId,
            ChecklistItemId = ItemId,
            CompletedByMarshalId = MarshalId,
            CompletionContextType = Constants.ChecklistContextPersonal,
            CompletionContextId = MarshalId,
            IsDeleted = false
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockChecklistCompletionRepository.Verify(r => r.UpdateAsync(It.IsAny<ChecklistCompletionEntity>()), Times.Once);
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_ItemNotFound_ReturnsNotFound()
    {
        // Arrange
        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync((ChecklistItemEntity?)null);

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_CompletionNotFound_ReturnsNotFound()
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
                new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([]);

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetChecklistCompletionReport Tests

    [TestMethod]
    public async Task GetChecklistCompletionReport_ReturnsReport()
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
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 1,
                IsRequired = true,
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            }
        ];

        List<ChecklistCompletionEntity> completions =
        [
            new ChecklistCompletionEntity
            {
                PartitionKey = EventId,
                RowKey = "item1#completion1",
                EventId = EventId,
                ChecklistItemId = "item1",
                CompletedByMarshalId = MarshalId,
                CompletedByMarshalName = "John Doe",
                CompletedAt = DateTime.UtcNow,
                CompletionContextType = Constants.ChecklistContextPersonal,
                CompletionContextId = MarshalId,
                IsDeleted = false
            }
        ];

        List<MarshalEntity> marshals =
        [
            new MarshalEntity
            {
                PartitionKey = EventId,
                RowKey = MarshalId,
                MarshalId = MarshalId,
                Name = "John Doe",
                Email = "john@test.com"
            }
        ];

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(items);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(completions);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(marshals);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetChecklistCompletionReport(httpRequest, EventId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        okResult.Value.ShouldNotBeNull();

        // Serialize and deserialize to verify structure
        string json = JsonSerializer.Serialize(okResult.Value);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        root.GetProperty("TotalItems").GetInt32().ShouldBe(1);
        root.GetProperty("TotalCompletions").GetInt32().ShouldBe(1);
    }

    #endregion

    #region CompleteChecklistItem Permission Tests

    [TestMethod]
    public async Task CompleteChecklistItem_MarshalLacksPermission_ReturnsForbidden()
    {
        // Arrange - Item only for area1, but marshal is assigned to area2
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Area 1 only item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeEveryoneInAreas, ItemType = "Area", Ids = ["area1"] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            EventId = EventId,
            Name = "Location in Area 2",
            Latitude = 51.5074,
            Longitude = -0.1278,
            AreaIdsJson = JsonSerializer.Serialize(new[] { "area2" })
        };

        AssignmentEntity assignment = new()
        {
            PartitionKey = EventId,
            RowKey = "assignment1",
            MarshalId = MarshalId,
            LocationId = LocationId
        };

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([assignment]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(request);

        // Act
        IActionResult result = await _functions.CompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<ForbidResult>();
        _mockChecklistCompletionRepository.Verify(
            r => r.AddAsync(It.IsAny<ChecklistCompletionEntity>()),
            Times.Never);
    }

    #endregion

    #region CreateSeparateItems Tests

    [TestMethod]
    public async Task CreateChecklistItem_WithCreateSeparateItems_CreatesMultipleItems()
    {
        // Arrange
        CreateChecklistItemRequest request = new(
            Text: "Item 1\nItem 2\nItem 3",
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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
            ScopeConfigurations: [new ScopeConfiguration { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }],
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

    #region Time-Based Visibility Tests

    [TestMethod]
    public async Task GetMarshalChecklist_ExcludesNotYetVisibleItems()
    {
        // Arrange
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        List<ChecklistItemEntity> items =
        [
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item1",
                EventId = EventId,
                ItemId = "item1",
                Text = "Visible now",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 1,
                IsRequired = true,
                VisibleFrom = DateTime.UtcNow.AddHours(-1),
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            },
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item2",
                EventId = EventId,
                ItemId = "item2",
                Text = "Not visible yet",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 2,
                IsRequired = true,
                VisibleFrom = DateTime.UtcNow.AddHours(1),
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            }
        ];

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(items);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> response = (List<ChecklistItemWithStatus>)okResult.Value!;

        response.Count.ShouldBe(1);
        response[0].ItemId.ShouldBe("item1");
    }

    [TestMethod]
    public async Task GetMarshalChecklist_ExcludesExpiredItems()
    {
        // Arrange
        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        List<ChecklistItemEntity> items =
        [
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item1",
                EventId = EventId,
                ItemId = "item1",
                Text = "Still visible",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 1,
                IsRequired = true,
                VisibleUntil = DateTime.UtcNow.AddHours(1),
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            },
            new ChecklistItemEntity
            {
                PartitionKey = EventId,
                RowKey = "item2",
                EventId = EventId,
                ItemId = "item2",
                Text = "Expired",
                ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
                {
                    new() { Scope = Constants.ChecklistScopeEveryone, ItemType = null, Ids = [] }
                }),
                DisplayOrder = 2,
                IsRequired = true,
                VisibleUntil = DateTime.UtcNow.AddHours(-1),
                CreatedByAdminEmail = AdminEmail,
                CreatedDate = DateTime.UtcNow
            }
        ];

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync(items);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> response = (List<ChecklistItemWithStatus>)okResult.Value!;

        response.Count.ShouldBe(1);
        response[0].ItemId.ShouldBe("item1");
    }

    #endregion

    #region Shared Context Uncomplete Tests

    [TestMethod]
    public async Task UncompleteChecklistItem_SharedContext_UncompletesByContext()
    {
        // Arrange - OnePerCheckpoint item completed by someone else
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Checkpoint item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeOnePerCheckpoint, ItemType = "Checkpoint", Ids = [LocationId] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        AssignmentEntity assignment = new()
        {
            PartitionKey = EventId,
            RowKey = "assignment1",
            MarshalId = MarshalId,
            LocationId = LocationId
        };

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            EventId = EventId,
            Name = "Test Location",
            Latitude = 51.5074,
            Longitude = -0.1278,
            AreaIdsJson = JsonSerializer.Serialize(new[] { AreaId })
        };

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = EventId,
            RowKey = $"{ItemId}#completion1",
            EventId = EventId,
            ChecklistItemId = ItemId,
            CompletedByMarshalId = "other-marshal",
            CompletedByMarshalName = "Jane Smith",
            CompletionContextType = Constants.ChecklistContextCheckpoint,
            CompletionContextId = LocationId,
            IsDeleted = false
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([assignment]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockChecklistCompletionRepository.Verify(
            r => r.UpdateAsync(It.Is<ChecklistCompletionEntity>(c =>
                c.IsDeleted == true &&
                c.UncompletedByAdminEmail == AdminEmail)),
            Times.Once);
    }

    [TestMethod]
    public async Task UncompleteChecklistItem_OnePerArea_UncompletesByAreaContext()
    {
        // Arrange - OnePerArea item
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            EventId = EventId,
            ItemId = ItemId,
            Text = "Area item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = Constants.ChecklistScopeOnePerArea, ItemType = "Area", Ids = [AreaId] }
            }),
            DisplayOrder = 1,
            IsRequired = true,
            CreatedByAdminEmail = AdminEmail,
            CreatedDate = DateTime.UtcNow
        };

        MarshalEntity marshal = new()
        {
            PartitionKey = EventId,
            RowKey = MarshalId,
            MarshalId = MarshalId,
            Name = "John Doe",
            Email = "john@test.com"
        };

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            EventId = EventId,
            Name = "Test Location",
            Latitude = 51.5074,
            Longitude = -0.1278,
            AreaIdsJson = JsonSerializer.Serialize(new[] { AreaId })
        };

        AssignmentEntity assignment = new()
        {
            PartitionKey = EventId,
            RowKey = "assignment1",
            MarshalId = MarshalId,
            LocationId = LocationId
        };

        ChecklistCompletionEntity completion = new()
        {
            PartitionKey = EventId,
            RowKey = $"{ItemId}#completion1",
            EventId = EventId,
            ChecklistItemId = ItemId,
            CompletedByMarshalId = MarshalId,
            CompletedByMarshalName = "John Doe",
            CompletionContextType = Constants.ChecklistContextArea,
            CompletionContextId = AreaId,
            IsDeleted = false
        };

        _mockChecklistItemRepository
            .Setup(r => r.GetAsync(EventId, ItemId))
            .ReturnsAsync(item);

        _mockMarshalRepository
            .Setup(r => r.GetAsync(EventId, MarshalId))
            .ReturnsAsync(marshal);

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([assignment]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByItemAsync(EventId, ItemId))
            .ReturnsAsync([completion]);

        CompleteChecklistItemRequest request = new(MarshalId: MarshalId);
        HttpRequest httpRequest = TestHelpers.CreateHttpRequestWithAdminHeader(request, AdminEmail);

        // Act
        IActionResult result = await _functions.UncompleteChecklistItem(httpRequest, EventId, ItemId);

        // Assert
        result.ShouldBeOfType<NoContentResult>();
        _mockChecklistCompletionRepository.Verify(
            r => r.UpdateAsync(It.Is<ChecklistCompletionEntity>(c => c.IsDeleted == true)),
            Times.Once);
    }

    #endregion
}
