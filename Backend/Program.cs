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
builder.Services.AddSingleton(sp =>
{
    string connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage")
        ?? "UseDevelopmentStorage=true";
    return new TableStorageService(connectionString);
});

builder.Services.AddSingleton(sp =>
{
    string smtpHost = Environment.GetEnvironmentVariable("SMTP_HOST") ?? "smtp.gmail.com";
    int smtpPort = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
    string smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? "";
    string smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? "";
    string fromEmail = Environment.GetEnvironmentVariable("FROM_EMAIL") ?? "noreply@volunteercheckin.com";
    string fromName = Environment.GetEnvironmentVariable("FROM_NAME") ?? "Volunteer Check-in";

    return new EmailService(smtpHost, smtpPort, smtpUsername, smtpPassword, fromEmail, fromName);
});

builder.Services.AddSingleton<GpxParserService>();
builder.Services.AddSingleton<CsvParserService>();
builder.Services.AddSingleton<GpsService>();

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

builder.Build().Run();
