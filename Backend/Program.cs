using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VolunteerCheckin.Functions.Services;
using VolunteerCheckin.Functions.Repositories;

FunctionsApplicationBuilder builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Register services
builder.Services.AddSingleton(_ =>
{
    string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "AzureWebJobsStorage environment variable is required. " +
            "Set it to your Azure Storage connection string or 'UseDevelopmentStorage=true' for local development.");
    }
    return new TableStorageService(connectionString);
});

builder.Services.AddSingleton(_ =>
{
    string? smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST");
    string? smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT");
    string? smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME");
    string? smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
    string? fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL");
    string? fromName = Environment.GetEnvironmentVariable("FROM_NAME");

    // Validate required email configuration
    if (string.IsNullOrWhiteSpace(smtpHost))
    {
        throw new InvalidOperationException("SMTP_HOST environment variable is required for email functionality.");
    }
    if (string.IsNullOrWhiteSpace(smtpUsername))
    {
        throw new InvalidOperationException("SMTP_USERNAME environment variable is required for email functionality.");
    }
    if (string.IsNullOrWhiteSpace(smtpPassword))
    {
        throw new InvalidOperationException("SMTP_PASSWORD environment variable is required for email functionality.");
    }
    if (string.IsNullOrWhiteSpace(fromEmail))
    {
        throw new InvalidOperationException("FROM_EMAIL environment variable is required for email functionality.");
    }

    int smtpPort = int.TryParse(smtpPortStr, out int port) ? port : 587;
    string senderName = string.IsNullOrWhiteSpace(fromName) ? "Volunteer Check-in" : fromName;

    return new EmailService(smtpHost, smtpPort, smtpUsername, smtpPassword, fromEmail, senderName);
});

builder.Services.AddSingleton(_ =>
{
    string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("AzureWebJobsStorage environment variable is required for blob storage.");
    }
    return new BlobStorageService(connectionString);
});

builder.Services.AddSingleton<GpsService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<ClaimsService>();
builder.Services.AddSingleton<ContactPermissionService>();
builder.Services.AddSingleton<RateLimitService>();

// Register repositories
builder.Services.AddSingleton<ILocationRepository, TableStorageLocationRepository>();
builder.Services.AddSingleton<IMarshalRepository, TableStorageMarshalRepository>();
builder.Services.AddSingleton<IAssignmentRepository, TableStorageAssignmentRepository>();
builder.Services.AddSingleton<IEventRepository, TableStorageEventRepository>();
builder.Services.AddSingleton<IAdminUserRepository, TableStorageAdminUserRepository>();
builder.Services.AddSingleton<IUserEventMappingRepository, TableStorageUserEventMappingRepository>();
builder.Services.AddSingleton<IAreaRepository, TableStorageAreaRepository>();
builder.Services.AddSingleton<IChecklistItemRepository, TableStorageChecklistItemRepository>();
builder.Services.AddSingleton<IChecklistCompletionRepository, TableStorageChecklistCompletionRepository>();
builder.Services.AddSingleton<IPersonRepository, TableStoragePersonRepository>();
builder.Services.AddSingleton<IEventRoleRepository, TableStorageEventRoleRepository>();
builder.Services.AddSingleton<IAuthTokenRepository, TableStorageAuthTokenRepository>();
builder.Services.AddSingleton<IAuthSessionRepository, TableStorageAuthSessionRepository>();
builder.Services.AddSingleton<INoteRepository, TableStorageNoteRepository>();
builder.Services.AddSingleton<IEventContactRepository, TableStorageEventContactRepository>();
builder.Services.AddSingleton<IIncidentRepository, TableStorageIncidentRepository>();
builder.Services.AddSingleton<IEventRoleDefinitionRepository, TableStorageEventRoleDefinitionRepository>();

await builder.Build().RunAsync();
