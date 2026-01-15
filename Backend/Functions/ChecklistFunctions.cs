using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Helpers;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

/// <summary>
/// CRUD operations for checklist items.
/// Query operations are in ChecklistQueryFunctions.
/// Completion operations are in ChecklistCompletionFunctions.
/// </summary>
public class ChecklistFunctions
{
    private readonly ILogger<ChecklistFunctions> _logger;
    private readonly IChecklistItemRepository _checklistItemRepository;
    private readonly IChecklistCompletionRepository _checklistCompletionRepository;

    public ChecklistFunctions(
        ILogger<ChecklistFunctions> logger,
        IChecklistItemRepository checklistItemRepository,
        IChecklistCompletionRepository checklistCompletionRepository)
    {
        _logger = logger;
        _checklistItemRepository = checklistItemRepository;
        _checklistCompletionRepository = checklistCompletionRepository;
    }

    /// <summary>
    /// Creates one or more checklist items for an event.
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <returns>
    /// If CreateSeparateItems is false: Returns a single ChecklistItemResponse
    /// If CreateSeparateItems is true: Returns { items: ChecklistItemResponse[], count: number }
    /// </returns>
    /// <remarks>
    /// When CreateSeparateItems is true, the Text field is split by line breaks, and each
    /// non-empty line creates a separate checklist item with the same scope, areas, checkpoints,
    /// marshals, and other settings. DisplayOrder is incremented for each item created.
    /// </remarks>
#pragma warning disable MA0051
    [Function("CreateChecklistItem")]
    public async Task<IActionResult> CreateChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/checklist-items")] HttpRequest req,
        string eventId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateChecklistItemRequest? request = JsonSerializer.Deserialize<CreateChecklistItemRequest>(body,
                FunctionHelpers.JsonOptions);

            if (request == null || string.IsNullOrWhiteSpace(request.Text))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            // Validate scope configurations
            if (request.ScopeConfigurations == null || request.ScopeConfigurations.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "At least one scope configuration is required" });
            }

            // Validate LinksToCheckIn scope compatibility
            string? linkValidationError = ChecklistCheckInLinkHelper.GetLinkToCheckInValidationError(
                request.ScopeConfigurations, request.LinksToCheckIn);
            if (linkValidationError != null)
            {
                return new BadRequestObjectResult(new { message = linkValidationError });
            }

            // Sanitize input
            string sanitizedText = InputSanitizer.SanitizeDescription(request.Text);

            // Serialize scope configurations once
            string scopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations);

            // Check if we should create separate items for each line
            if (request.CreateSeparateItems)
            {
                List<string> lines = [.. sanitizedText
                    .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))];

                if (lines.Count == 0)
                {
                    return new BadRequestObjectResult(new { message = "No valid lines found in text" });
                }

                List<ChecklistItemResponse> createdItems = [];
                int currentDisplayOrder = request.DisplayOrder;

                foreach (string line in lines)
                {
                    string itemId = Guid.NewGuid().ToString();
                    ChecklistItemEntity entity = new()
                    {
                        PartitionKey = eventId,
                        RowKey = itemId,
                        EventId = eventId,
                        ItemId = itemId,
                        Text = line,
                        ScopeConfigurationsJson = scopeConfigurationsJson,
                        DisplayOrder = currentDisplayOrder++,
                        IsRequired = request.IsRequired,
                        VisibleFrom = request.VisibleFrom,
                        VisibleUntil = request.VisibleUntil,
                        MustCompleteBy = request.MustCompleteBy,
                        CreatedByAdminEmail = adminEmail,
                        CreatedDate = DateTime.UtcNow,
                        LinksToCheckIn = request.LinksToCheckIn
                    };

                    await _checklistItemRepository.AddAsync(entity);
                    createdItems.Add(entity.ToResponse());

                    _logger.LogInformation("Checklist item created: {ItemId} by {AdminEmail}", itemId, adminEmail);
                }

                return new OkObjectResult(new { items = createdItems, count = createdItems.Count });
            }
            else
            {
                // Original behavior: create a single item
                string itemId = Guid.NewGuid().ToString();
                ChecklistItemEntity entity = new()
                {
                    PartitionKey = eventId,
                    RowKey = itemId,
                    EventId = eventId,
                    ItemId = itemId,
                    Text = sanitizedText,
                    ScopeConfigurationsJson = scopeConfigurationsJson,
                    DisplayOrder = request.DisplayOrder,
                    IsRequired = request.IsRequired,
                    VisibleFrom = request.VisibleFrom,
                    VisibleUntil = request.VisibleUntil,
                    MustCompleteBy = request.MustCompleteBy,
                    CreatedByAdminEmail = adminEmail,
                    CreatedDate = DateTime.UtcNow,
                    LinksToCheckIn = request.LinksToCheckIn
                };

                await _checklistItemRepository.AddAsync(entity);

                _logger.LogInformation("Checklist item created: {ItemId} by {AdminEmail}", itemId, adminEmail);

                return new OkObjectResult(entity.ToResponse());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating checklist item");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("GetChecklistItems")]
    public async Task<IActionResult> GetChecklistItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/checklist-items")] HttpRequest req,
        string eventId)
    {
        try
        {
            List<ChecklistItemEntity> items = (await _checklistItemRepository.GetByEventAsync(eventId)).ToList();

            // Lazy migration: normalize DisplayOrder if any items have DisplayOrder = 0
            await NormalizeChecklistDisplayOrdersAsync(items, eventId);

            List<ChecklistItemResponse> responses = [.. items.Select(i => i.ToResponse())];

            return new OkObjectResult(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist items");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetChecklistItem")]
    public async Task<IActionResult> GetChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            return new OkObjectResult(item.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting checklist item");
            return new StatusCodeResult(500);
        }
    }

#pragma warning disable MA0051
    [Function("UpdateChecklistItem")]
    public async Task<IActionResult> UpdateChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateChecklistItemRequest? request = JsonSerializer.Deserialize<UpdateChecklistItemRequest>(body,
                FunctionHelpers.JsonOptions);

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorInvalidRequest });
            }

            string adminEmail = req.Headers[Constants.AdminEmailHeader].ToString();
            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return new BadRequestObjectResult(new { message = Constants.ErrorEmailRequired });
            }

            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Validate scope configurations
            if (request.ScopeConfigurations == null || request.ScopeConfigurations.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "At least one scope configuration is required" });
            }

            // Validate LinksToCheckIn scope compatibility
            string? linkValidationError = ChecklistCheckInLinkHelper.GetLinkToCheckInValidationError(
                request.ScopeConfigurations, request.LinksToCheckIn);
            if (linkValidationError != null)
            {
                return new BadRequestObjectResult(new { message = linkValidationError });
            }

            // Sanitize input
            string sanitizedText = InputSanitizer.SanitizeDescription(request.Text);

            // Update fields
            item.Text = sanitizedText;
            item.ScopeConfigurationsJson = JsonSerializer.Serialize(request.ScopeConfigurations);
            item.DisplayOrder = request.DisplayOrder;
            item.IsRequired = request.IsRequired;
            item.VisibleFrom = request.VisibleFrom;
            item.VisibleUntil = request.VisibleUntil;
            item.MustCompleteBy = request.MustCompleteBy;
            item.LinksToCheckIn = request.LinksToCheckIn;
            item.LastModifiedDate = DateTime.UtcNow;
            item.LastModifiedByAdminEmail = adminEmail;

            await _checklistItemRepository.UpdateAsync(item);

            _logger.LogInformation("Checklist item updated: {ItemId} by {AdminEmail}", itemId, adminEmail);

            return new OkObjectResult(item.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checklist item");
            return new StatusCodeResult(500);
        }
    }
#pragma warning restore MA0051

    [Function("DeleteChecklistItem")]
    public async Task<IActionResult> DeleteChecklistItem(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "checklist-items/{eventId}/{itemId}")] HttpRequest req,
        string eventId,
        string itemId)
    {
        try
        {
            ChecklistItemEntity? item = await _checklistItemRepository.GetAsync(eventId, itemId);

            if (item == null)
            {
                return new NotFoundObjectResult(new { message = Constants.ErrorChecklistItemNotFound });
            }

            // Delete all completions for this item
            await _checklistCompletionRepository.DeleteAllByItemAsync(eventId, itemId);

            // Delete the item
            await _checklistItemRepository.DeleteAsync(eventId, itemId);

            _logger.LogInformation("Checklist item deleted: {ItemId}", itemId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting checklist item");
            return new StatusCodeResult(500);
        }
    }

    /// <summary>
    /// Reorder checklist items by updating their display order.
    /// </summary>
    [Function("ReorderChecklistItems")]
    public async Task<IActionResult> ReorderChecklistItems(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "events/{eventId}/checklist-items/reorder")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Parse request
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            ReorderChecklistItemsRequest? request = JsonSerializer.Deserialize<ReorderChecklistItemsRequest>(requestBody, FunctionHelpers.JsonOptions);

            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "Items array is required" });
            }

            // Build display order map
            Dictionary<string, int> displayOrders = request.Items.ToDictionary(i => i.Id, i => i.DisplayOrder);

            // Update all items
            await _checklistItemRepository.UpdateDisplayOrdersAsync(eventId, displayOrders);

            _logger.LogInformation("Checklist items reordered for event {EventId}", eventId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering checklist items for event {EventId}", eventId);
            return FunctionHelpers.CreateErrorResponse(ex, "Failed to reorder checklist items");
        }
    }

    /// <summary>
    /// Lazy migration: if any checklist items have DisplayOrder = 0, normalize all display orders.
    /// Sorts by text (alphabetically) and assigns sequential orders.
    /// </summary>
    private async Task NormalizeChecklistDisplayOrdersAsync(List<ChecklistItemEntity> items, string eventId)
    {
        // Check if any items need migration (DisplayOrder = 0)
        bool needsMigration = items.Any(i => i.DisplayOrder == 0);
        if (!needsMigration || items.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Normalizing DisplayOrder for {Count} checklist items in event {EventId}",
            items.Count, eventId);

        // Sort alphabetically by text
        List<ChecklistItemEntity> sortedItems = [.. items
            .OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase)];

        // Assign sequential display orders
        Dictionary<string, int> changes = [];
        for (int i = 0; i < sortedItems.Count; i++)
        {
            int newOrder = i + 1;
            if (sortedItems[i].DisplayOrder != newOrder)
            {
                sortedItems[i].DisplayOrder = newOrder;
                changes[sortedItems[i].ItemId] = newOrder;
            }
        }

        // Batch update if there are changes
        if (changes.Count > 0)
        {
            try
            {
                await _checklistItemRepository.UpdateDisplayOrdersAsync(eventId, changes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist DisplayOrder migration for checklist items");
                // Continue anyway - the in-memory list is already updated
            }
        }
    }
}
