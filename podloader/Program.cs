using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using podloader.Services.KdhxHostedService;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add hosted service and logging
builder.Services.AddLogging(builder => builder.AddConsole());
builder.Services.AddSingleton<KdhxHostedService>(); // Register as singleton
builder.Services.AddHostedService<KdhxHostedService>(provider => provider.GetRequiredService<KdhxHostedService>());

var app = builder.Build();

// Your existing endpoint
app.MapGet("/", () => "Hello World!");

// Additional endpoint for showing job status
app.MapGet("/status", ([FromServices] KdhxHostedService service, [FromServices] ILogger<Program> logger) =>
{
    var result = new
    {
        IsJobRunning = service.IsJobRunning
    };

    logger.LogInformation("Status endpoint was accessed.");
    return JsonSerializer.Serialize(result);
});

// Your existing podcast logic
app.MapGet("/podcast/kdhx", async () =>
{
    // Your logic here
});

app.Run();
