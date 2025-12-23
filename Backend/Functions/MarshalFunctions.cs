using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;

namespace VolunteerCheckin.Functions.Functions;

public class MarshalFunctions
{
    private readonly ILogger<MarshalFunctions> _logger;
    private readonly TableStorageService _tableStorage;
    private readonly CsvParserService _csvParser;

    public MarshalFunctions(ILogger<MarshalFunctions> logger, TableStorageService tableStorage, CsvParserService csvParser)
    {
        _logger = logger;
        _tableStorage = tableStorage;
        _csvParser = csvParser;
    }

    [Function("CreateMarshal")]
    public async Task<IActionResult> CreateMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals")] HttpRequest req)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            CreateMarshalRequest? request = JsonSerializer.Deserialize<CreateMarshalRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            string marshalId = Guid.NewGuid().ToString();
            MarshalEntity marshalEntity = new()
            {
                PartitionKey = request.EventId,
                RowKey = marshalId,
                EventId = request.EventId,
                MarshalId = marshalId,
                Name = request.Name,
                Email = request.Email ?? string.Empty,
                PhoneNumber = request.PhoneNumber ?? string.Empty,
                Notes = request.Notes ?? string.Empty
            };

            TableClient table = _tableStorage.GetMarshalsTable();
            await table.AddEntityAsync(marshalEntity);

            MarshalResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                marshalEntity.Email,
                marshalEntity.PhoneNumber,
                marshalEntity.Notes,
                [],
                false,
                marshalEntity.CreatedDate
            );

            _logger.LogInformation($"Marshal created: {marshalId}");

            return new OkObjectResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating marshal");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetMarshalsByEvent")]
    public async Task<IActionResult> GetMarshalsByEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}/marshals")] HttpRequest req,
        string eventId)
    {
        try
        {
            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            List<MarshalResponse> marshals = [];

            // Get all marshals for the event
            await foreach (MarshalEntity marshalEntity in marshalsTable.QueryAsync<MarshalEntity>(m => m.PartitionKey == eventId))
            {
                // Get assignments for this marshal
                List<string> assignedLocationIds = [];
                bool isCheckedIn = false;

                await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                    a => a.PartitionKey == eventId && a.MarshalId == marshalEntity.MarshalId))
                {
                    assignedLocationIds.Add(assignment.LocationId);
                    if (assignment.IsCheckedIn)
                    {
                        isCheckedIn = true;
                    }
                }

                marshals.Add(new MarshalResponse(
                    marshalEntity.MarshalId,
                    marshalEntity.EventId,
                    marshalEntity.Name,
                    marshalEntity.Email,
                    marshalEntity.PhoneNumber,
                    marshalEntity.Notes,
                    assignedLocationIds,
                    isCheckedIn,
                    marshalEntity.CreatedDate
                ));
            }

            return new OkObjectResult(marshals);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshals");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetMarshal")]
    public async Task<IActionResult> GetMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            Response<MarshalEntity> marshalEntity = await marshalsTable.GetEntityAsync<MarshalEntity>(eventId, marshalId);

            // Get assignments for this marshal
            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                a => a.PartitionKey == eventId && a.MarshalId == marshalId))
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            MarshalResponse response = new(
                marshalEntity.Value.MarshalId,
                marshalEntity.Value.EventId,
                marshalEntity.Value.Name,
                marshalEntity.Value.Email,
                marshalEntity.Value.PhoneNumber,
                marshalEntity.Value.Notes,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.Value.CreatedDate
            );

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Marshal not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting marshal");
            return new StatusCodeResult(500);
        }
    }

    [Function("UpdateMarshal")]
    public async Task<IActionResult> UpdateMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            string body = await new StreamReader(req.Body).ReadToEndAsync();
            UpdateMarshalRequest? request = JsonSerializer.Deserialize<UpdateMarshalRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null)
            {
                return new BadRequestObjectResult(new { message = "Invalid request" });
            }

            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            Response<MarshalEntity> marshalEntity = await marshalsTable.GetEntityAsync<MarshalEntity>(eventId, marshalId);

            marshalEntity.Value.Name = request.Name;
            marshalEntity.Value.Email = request.Email ?? string.Empty;
            marshalEntity.Value.PhoneNumber = request.PhoneNumber ?? string.Empty;
            marshalEntity.Value.Notes = request.Notes ?? string.Empty;

            await marshalsTable.UpdateEntityAsync(marshalEntity.Value, marshalEntity.Value.ETag);

            // Update denormalized name in all assignments
            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                a => a.PartitionKey == eventId && a.MarshalId == marshalId))
            {
                assignment.MarshalName = request.Name;
                await assignmentsTable.UpdateEntityAsync(assignment, assignment.ETag);
            }

            // Get updated assignments
            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                a => a.PartitionKey == eventId && a.MarshalId == marshalId))
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            MarshalResponse response = new(
                marshalEntity.Value.MarshalId,
                marshalEntity.Value.EventId,
                marshalEntity.Value.Name,
                marshalEntity.Value.Email,
                marshalEntity.Value.PhoneNumber,
                marshalEntity.Value.Notes,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.Value.CreatedDate
            );

            _logger.LogInformation($"Marshal updated: {marshalId}");

            return new OkObjectResult(response);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Marshal not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating marshal");
            return new StatusCodeResult(500);
        }
    }

    [Function("DeleteMarshal")]
    public async Task<IActionResult> DeleteMarshal(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "marshals/{eventId}/{marshalId}")] HttpRequest req,
        string eventId,
        string marshalId)
    {
        try
        {
            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Delete all assignments for this marshal
            await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                a => a.PartitionKey == eventId && a.MarshalId == marshalId))
            {
                await assignmentsTable.DeleteEntityAsync(eventId, assignment.RowKey);
            }

            // Delete the marshal
            await marshalsTable.DeleteEntityAsync(eventId, marshalId);

            _logger.LogInformation($"Marshal deleted: {marshalId}");

            return new NoContentResult();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new NotFoundObjectResult(new { message = "Marshal not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting marshal");
            return new StatusCodeResult(500);
        }
    }

    [Function("ImportMarshals")]
    public async Task<IActionResult> ImportMarshals(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "marshals/import/{eventId}")] HttpRequest req,
        string eventId)
    {
        try
        {
            // Get the uploaded file
            if (req.Form.Files.Count == 0)
            {
                return new BadRequestObjectResult(new { message = "No file uploaded" });
            }

            IFormFile csvFile = req.Form.Files[0];
            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return new BadRequestObjectResult(new { message = "File must be a .csv file" });
            }

            // Parse the CSV file
            CsvParserService.MarshalCsvParseResult parseResult;
            using (Stream stream = csvFile.OpenReadStream())
            {
                parseResult = CsvParserService.ParseMarshalsCsv(stream);
            }

            if (parseResult.Marshals.Count == 0 && parseResult.Errors.Count > 0)
            {
                return new BadRequestObjectResult(new ImportMarshalsResponse(0, 0, parseResult.Errors));
            }

            TableClient marshalsTable = _tableStorage.GetMarshalsTable();
            TableClient locationsTable = _tableStorage.GetLocationsTable();
            TableClient assignmentsTable = _tableStorage.GetAssignmentsTable();

            // Load existing marshals to check for updates
            Dictionary<string, MarshalEntity> existingMarshals = [];
            await foreach (MarshalEntity marshal in marshalsTable.QueryAsync<MarshalEntity>(m => m.PartitionKey == eventId))
            {
                existingMarshals[marshal.Name.ToLower()] = marshal;
            }

            int marshalsCreated = 0;
            int assignmentsCreated = 0;

            foreach (CsvParserService.MarshalCsvRow row in parseResult.Marshals)
            {
                try
                {
                    MarshalEntity marshalEntity;
                    bool isUpdate = existingMarshals.TryGetValue(row.Name.ToLower(), out MarshalEntity? existing);

                    if (isUpdate && existing != null)
                    {
                        // Update existing marshal - only update fields that are provided
                        marshalEntity = existing;

                        if (!string.IsNullOrWhiteSpace(row.Email))
                        {
                            marshalEntity.Email = row.Email;
                        }

                        if (!string.IsNullOrWhiteSpace(row.Phone))
                        {
                            marshalEntity.PhoneNumber = row.Phone;
                        }

                        await marshalsTable.UpdateEntityAsync(marshalEntity, marshalEntity.ETag);

                        // Update denormalized name in all assignments
                        await foreach (AssignmentEntity assignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                            a => a.PartitionKey == eventId && a.MarshalId == marshalEntity.MarshalId))
                        {
                            assignment.MarshalName = marshalEntity.Name;
                            await assignmentsTable.UpdateEntityAsync(assignment, assignment.ETag);
                        }
                    }
                    else
                    {
                        // Create new marshal
                        string newMarshalId = Guid.NewGuid().ToString();
                        marshalEntity = new MarshalEntity
                        {
                            PartitionKey = eventId,
                            RowKey = newMarshalId,
                            EventId = eventId,
                            MarshalId = newMarshalId,
                            Name = row.Name,
                            Email = row.Email,
                            PhoneNumber = row.Phone,
                            Notes = string.Empty
                        };

                        await marshalsTable.AddEntityAsync(marshalEntity);
                        marshalsCreated++;
                        _logger.LogInformation($"Created new marshal from CSV: {newMarshalId} - {row.Name}");
                    }

                    // Create assignment if checkpoint is specified
                    if (!string.IsNullOrWhiteSpace(row.Checkpoint))
                    {
                        // Find the location by name
                        LocationEntity? location = null;
                        await foreach (LocationEntity loc in locationsTable.QueryAsync<LocationEntity>(l => l.PartitionKey == eventId))
                        {
                            if (loc.Name.Equals(row.Checkpoint, StringComparison.OrdinalIgnoreCase))
                            {
                                location = loc;
                                break;
                            }
                        }

                        if (location != null)
                        {
                            // Check if assignment already exists
                            bool assignmentExists = false;
                            await foreach (AssignmentEntity existingAssignment in assignmentsTable.QueryAsync<AssignmentEntity>(
                                a => a.PartitionKey == eventId && a.LocationId == location.RowKey && a.MarshalId == marshalEntity.MarshalId))
                            {
                                assignmentExists = true;
                                break;
                            }

                            if (!assignmentExists)
                            {
                                AssignmentEntity assignmentEntity = new()
                                {
                                    PartitionKey = eventId,
                                    RowKey = Guid.NewGuid().ToString(),
                                    EventId = eventId,
                                    LocationId = location.RowKey,
                                    MarshalId = marshalEntity.MarshalId,
                                    MarshalName = marshalEntity.Name
                                };

                                await assignmentsTable.AddEntityAsync(assignmentEntity);
                                assignmentsCreated++;
                            }
                        }
                        else
                        {
                            parseResult.Errors.Add($"Checkpoint '{row.Checkpoint}' not found for marshal '{row.Name}'");
                        }
                    }
                }
                catch (Exception ex)
                {
                    parseResult.Errors.Add($"Failed to process marshal '{row.Name}': {ex.Message}");
                }
            }

            _logger.LogInformation($"Imported {marshalsCreated} new marshals and created {assignmentsCreated} assignments for event {eventId}");

            return new OkObjectResult(new ImportMarshalsResponse(marshalsCreated, assignmentsCreated, parseResult.Errors));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing marshals");
            return new StatusCodeResult(500);
        }
    }
}
