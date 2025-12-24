using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VolunteerCheckin.Functions.Models;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;

namespace VolunteerCheckin.Functions.Functions;

public class MarshalFunctions
{
    private readonly ILogger<MarshalFunctions> _logger;
    private readonly IMarshalRepository _marshalRepository;
    private readonly ILocationRepository _locationRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly CsvParserService _csvParser;

    public MarshalFunctions(
        ILogger<MarshalFunctions> logger,
        IMarshalRepository marshalRepository,
        ILocationRepository locationRepository,
        IAssignmentRepository assignmentRepository,
        CsvParserService csvParser)
    {
        _logger = logger;
        _marshalRepository = marshalRepository;
        _locationRepository = locationRepository;
        _assignmentRepository = assignmentRepository;
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

            await _marshalRepository.AddAsync(marshalEntity);

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
            IEnumerable<MarshalEntity> marshalEntities = await _marshalRepository.GetByEventAsync(eventId);
            List<MarshalResponse> marshals = [];

            foreach (MarshalEntity marshalEntity in marshalEntities)
            {
                // Get assignments for this marshal
                IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalEntity.MarshalId);

                List<string> assignedLocationIds = [];
                bool isCheckedIn = false;

                foreach (AssignmentEntity assignment in assignments)
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
            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);

            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            // Get assignments for this marshal
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);

            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            foreach (AssignmentEntity assignment in assignments)
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            MarshalResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                marshalEntity.Email,
                marshalEntity.PhoneNumber,
                marshalEntity.Notes,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.CreatedDate
            );

            return new OkObjectResult(response);
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

            MarshalEntity? marshalEntity = await _marshalRepository.GetAsync(eventId, marshalId);

            if (marshalEntity == null)
            {
                return new NotFoundObjectResult(new { message = "Marshal not found" });
            }

            marshalEntity.Name = request.Name;
            marshalEntity.Email = request.Email ?? string.Empty;
            marshalEntity.PhoneNumber = request.PhoneNumber ?? string.Empty;
            marshalEntity.Notes = request.Notes ?? string.Empty;

            await _marshalRepository.UpdateAsync(marshalEntity);

            // Update denormalized name in all assignments
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in assignments)
            {
                assignment.MarshalName = request.Name;
                await _assignmentRepository.UpdateAsync(assignment);
            }

            // Get updated assignments
            List<string> assignedLocationIds = [];
            bool isCheckedIn = false;

            IEnumerable<AssignmentEntity> updatedAssignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in updatedAssignments)
            {
                assignedLocationIds.Add(assignment.LocationId);
                if (assignment.IsCheckedIn)
                {
                    isCheckedIn = true;
                }
            }

            MarshalResponse response = new(
                marshalEntity.MarshalId,
                marshalEntity.EventId,
                marshalEntity.Name,
                marshalEntity.Email,
                marshalEntity.PhoneNumber,
                marshalEntity.Notes,
                assignedLocationIds,
                isCheckedIn,
                marshalEntity.CreatedDate
            );

            _logger.LogInformation($"Marshal updated: {marshalId}");

            return new OkObjectResult(response);
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
            // Delete all assignments for this marshal
            IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalId);
            foreach (AssignmentEntity assignment in assignments)
            {
                await _assignmentRepository.DeleteAsync(eventId, assignment.RowKey);
            }

            // Delete the marshal
            await _marshalRepository.DeleteAsync(eventId, marshalId);

            _logger.LogInformation($"Marshal deleted: {marshalId}");

            return new NoContentResult();
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

            // Load existing marshals to check for updates
            IEnumerable<MarshalEntity> existingMarshalsList = await _marshalRepository.GetByEventAsync(eventId);
            Dictionary<string, MarshalEntity> existingMarshals = existingMarshalsList
                .ToDictionary(m => m.Name.ToLower(), m => m);

            int marshalsCreated = 0;
            int assignmentsCreated = 0;

            foreach (CsvParserService.MarshalCsvRow row in parseResult.Marshals)
            {
                try
                {
                    bool isUpdate = existingMarshals.TryGetValue(row.Name.ToLower(), out MarshalEntity? existing);

                    MarshalEntity marshalEntity;
                    if (isUpdate && existing != null)
                    {
                        marshalEntity = await UpdateExistingMarshal(existing, row, eventId);
                    }
                    else
                    {
                        marshalEntity = await CreateNewMarshal(row, eventId);
                        marshalsCreated++;
                    }

                    // Create assignment if checkpoint is specified
                    if (!string.IsNullOrWhiteSpace(row.Checkpoint))
                    {
                        bool assignmentCreated = await CreateAssignmentForCheckpoint(
                            marshalEntity, row.Checkpoint, eventId, parseResult.Errors);

                        if (assignmentCreated)
                        {
                            assignmentsCreated++;
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

    private async Task<MarshalEntity> UpdateExistingMarshal(
        MarshalEntity marshalEntity,
        CsvParserService.MarshalCsvRow row,
        string eventId)
    {
        // Update existing marshal - only update fields that are provided
        if (!string.IsNullOrWhiteSpace(row.Email))
        {
            marshalEntity.Email = row.Email;
        }

        if (!string.IsNullOrWhiteSpace(row.Phone))
        {
            marshalEntity.PhoneNumber = row.Phone;
        }

        await _marshalRepository.UpdateAsync(marshalEntity);

        // Update denormalized name in all assignments
        IEnumerable<AssignmentEntity> assignments = await _assignmentRepository.GetByMarshalAsync(eventId, marshalEntity.MarshalId);
        foreach (AssignmentEntity assignment in assignments)
        {
            assignment.MarshalName = marshalEntity.Name;
            await _assignmentRepository.UpdateAsync(assignment);
        }

        return marshalEntity;
    }

    private async Task<MarshalEntity> CreateNewMarshal(
        CsvParserService.MarshalCsvRow row,
        string eventId)
    {
        string newMarshalId = Guid.NewGuid().ToString();
        MarshalEntity marshalEntity = new()
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

        await _marshalRepository.AddAsync(marshalEntity);
        _logger.LogInformation($"Created new marshal from CSV: {newMarshalId} - {row.Name}");

        return marshalEntity;
    }

    private async Task<bool> CreateAssignmentForCheckpoint(
        MarshalEntity marshalEntity,
        string checkpointName,
        string eventId,
        List<string> errors)
    {
        // Find the location by name
        LocationEntity? location = await FindLocationByName(checkpointName, eventId);

        if (location == null)
        {
            errors.Add($"Checkpoint '{checkpointName}' not found for marshal '{marshalEntity.Name}'");
            return false;
        }

        // Check if assignment already exists
        bool assignmentExists = await _assignmentRepository.ExistsAsync(eventId, marshalEntity.MarshalId, location.RowKey);

        if (assignmentExists)
        {
            return false;
        }

        AssignmentEntity assignmentEntity = new()
        {
            PartitionKey = eventId,
            RowKey = Guid.NewGuid().ToString(),
            EventId = eventId,
            LocationId = location.RowKey,
            MarshalId = marshalEntity.MarshalId,
            MarshalName = marshalEntity.Name
        };

        await _assignmentRepository.AddAsync(assignmentEntity);
        return true;
    }

    private async Task<LocationEntity?> FindLocationByName(string locationName, string eventId)
    {
        IEnumerable<LocationEntity> locations = await _locationRepository.GetByEventAsync(eventId);
        foreach (LocationEntity loc in locations)
        {
            if (loc.Name.Equals(locationName, StringComparison.OrdinalIgnoreCase))
            {
                return loc;
            }
        }
        return null;
    }
}
