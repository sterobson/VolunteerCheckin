using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Shouldly;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using VolunteerCheckin.Functions;
using VolunteerCheckin.Functions.Functions;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Tests;

[TestClass]
public class ChecklistQueryFunctionsTests
{
    private Mock<ILogger<ChecklistQueryFunctions>> _mockLogger = null!;
    private Mock<IChecklistItemRepository> _mockChecklistItemRepository = null!;
    private Mock<IChecklistCompletionRepository> _mockChecklistCompletionRepository = null!;
    private Mock<IMarshalRepository> _mockMarshalRepository = null!;
    private Mock<IAssignmentRepository> _mockAssignmentRepository = null!;
    private Mock<IAreaRepository> _mockAreaRepository = null!;
    private Mock<ILocationRepository> _mockLocationRepository = null!;
    private ChecklistQueryFunctions _functions = null!;

    private const string EventId = "event123";
    private const string ItemId = "item456";
    private const string MarshalId = "marshal789";
    private const string AreaId = "area1";
    private const string LocationId = "location1";

    [TestInitialize]
    public void Setup()
    {
        _mockLogger = new Mock<ILogger<ChecklistQueryFunctions>>();
        _mockChecklistItemRepository = new Mock<IChecklistItemRepository>();
        _mockChecklistCompletionRepository = new Mock<IChecklistCompletionRepository>();
        _mockMarshalRepository = new Mock<IMarshalRepository>();
        _mockAssignmentRepository = new Mock<IAssignmentRepository>();
        _mockAreaRepository = new Mock<IAreaRepository>();
        _mockLocationRepository = new Mock<ILocationRepository>();

        _functions = new ChecklistQueryFunctions(
            _mockLogger.Object,
            _mockChecklistItemRepository.Object,
            _mockChecklistCompletionRepository.Object,
            _mockMarshalRepository.Object,
            _mockLocationRepository.Object,
            _mockAssignmentRepository.Object,
            _mockAreaRepository.Object
        );
    }

    #region GetMarshalChecklist Tests

    [TestMethod]
    public async Task GetMarshalChecklist_MarshalExists_ReturnsRelevantItems()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        // Setup assignments
        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, MarshalId))
            .ReturnsAsync([new AssignmentEntity { LocationId = LocationId }]);

        // Setup locations
        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            AreaIdsJson = $"[\"{AreaId}\"]"
        };
        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        // Setup areas
        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId, AreaLeadMarshalIdsJson = "[]" }]);

        // Setup checklist items
        ChecklistItemEntity item = new()
        {
            PartitionKey = EventId,
            RowKey = ItemId,
            ItemId = ItemId,
            EventId = EventId,
            Text = "Test Item",
            ScopeConfigurationsJson = JsonSerializer.Serialize(new List<ScopeConfiguration>
            {
                new() { Scope = "EveryoneInAreas", ItemType = "Area", Ids = [AreaId] }
            }),
            DisplayOrder = 1
        };
        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([item]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, MarshalId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> items = (List<ChecklistItemWithStatus>)okResult.Value!;
        items.Count.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task GetMarshalChecklist_MarshalNotFound_ReturnsEmptyList()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        _mockAssignmentRepository
            .Setup(r => r.GetByMarshalAsync(EventId, "nonexistent"))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
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

        // Act
        IActionResult result = await _functions.GetMarshalChecklist(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<OkObjectResult>();
        OkObjectResult okResult = (OkObjectResult)result;
        List<ChecklistItemWithStatus> items = (List<ChecklistItemWithStatus>)okResult.Value!;
        items.Count.ShouldBe(0);
    }

    #endregion

    #region GetCheckpointChecklist Tests

    [TestMethod]
    public async Task GetCheckpointChecklist_UsesPreloadedData_NoNPlusOneQueries()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            AreaIdsJson = $"[\"{AreaId}\"]"
        };

        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AssignmentEntity { MarshalId = MarshalId, LocationId = LocationId }]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId, AreaLeadMarshalIdsJson = "[]" }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new MarshalEntity { RowKey = MarshalId, Name = "Test Marshal" }]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.GetCheckpointChecklist(httpRequest, EventId, LocationId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Verify GetByEventAsync was called once for each repo (batch loading)
        _mockAssignmentRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
        _mockLocationRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
        _mockAreaRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
    }

    [TestMethod]
    public async Task GetCheckpointChecklist_LocationNotFound_ReturnsNotFound()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.GetCheckpointChecklist(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetAreaChecklist Tests

    [TestMethod]
    public async Task GetAreaChecklist_UsesPreloadedData_NoNPlusOneQueries()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        LocationEntity location = new()
        {
            PartitionKey = EventId,
            RowKey = LocationId,
            AreaIdsJson = $"[\"{AreaId}\"]"
        };

        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AssignmentEntity { MarshalId = MarshalId, LocationId = LocationId }]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([location]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new AreaEntity { RowKey = AreaId, AreaLeadMarshalIdsJson = "[]" }]);

        _mockMarshalRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([new MarshalEntity { RowKey = MarshalId, Name = "Test Marshal" }]);

        _mockChecklistItemRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockChecklistCompletionRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.GetAreaChecklist(httpRequest, EventId, AreaId);

        // Assert
        result.ShouldBeOfType<OkObjectResult>();

        // Verify GetByEventAsync was called once for each repo (batch loading)
        _mockAssignmentRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
        _mockLocationRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
        _mockAreaRepository.Verify(r => r.GetByEventAsync(EventId), Times.Once);
    }

    [TestMethod]
    public async Task GetAreaChecklist_AreaNotFound_ReturnsNotFound()
    {
        // Arrange
        HttpRequest httpRequest = TestHelpers.CreateHttpRequest(new { });

        _mockAssignmentRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockLocationRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        _mockAreaRepository
            .Setup(r => r.GetByEventAsync(EventId))
            .ReturnsAsync([]);

        // Act
        IActionResult result = await _functions.GetAreaChecklist(httpRequest, EventId, "nonexistent");

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion
}
