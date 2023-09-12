using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using podloader;
using podloader.Services.KdhxHostedService;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml;
using System.Xml.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add hosted service and logging
builder.Services.AddLogging(builder => builder.AddConsole());
builder.Services.AddSingleton<KdhxHostedService>(); // Register as singleton
builder.Services.AddHostedService<KdhxHostedService>(provider => provider.GetRequiredService<KdhxHostedService>());

var app = builder.Build();

// Your existing endpoints
app.MapGet("/", () => "Fuck Off");

app.MapGet("/status", ([FromServices] KdhxHostedService service, [FromServices] ILogger<Program> logger) =>
{
    var result = new
    {
        IsJobRunning = service.IsJobRunning
    };

    logger.LogInformation("Status endpoint was accessed.");
    return JsonSerializer.Serialize(result);
});

var baseUrl = "http://podloader.kdhx.box.ca"; // Change this to your actual base URL

app.MapGet("/podcast/kdhx", async () =>
{
    // Create the RSS feed
    var rss = new Rss
    {
        Version = "2.0",
        Channel = new Channel
        {
            Title = "[Local] Test Podcast",
            Description = "Testing generating RSS feed from local MP3 files",
            Category = "test",
            Item = new List<Item>()
        }
    };

    var directory = Path.Combine("kdhxfiles");
    var audioFiles = Directory.GetFiles(directory, "*.mp3");

    foreach (var mp3File in audioFiles)
    {
        var fileInfo = new FileInfo(mp3File);
        var item = new Item
        {
            Description = fileInfo.Name,
            Title = fileInfo.Name,
            Enclosure = new Enclosure
            {
                Url = $"{baseUrl}/audio/{fileInfo.Name}",
                Type = "audio/mpeg",
                Length = fileInfo.Length.ToString()
            }
        };
        rss.Channel.Item.Add(item);
    }

    // Serialize the RSS feed to XML
    var serializer = new XmlSerializer(typeof(Rss));
    var xmlString = "";
    using (var writer = new StringWriter())
    {
        using (var xmlWriter = XmlWriter.Create(writer))
        {
            serializer.Serialize(xmlWriter, rss);
            xmlString = writer.ToString();
        }
    }

    // Set the response content type to XML
    var response = new ContentResult
    {
        Content = xmlString,
        ContentType = "application/rss+xml",
        StatusCode = 200
    };

    return response;
});

// New endpoint to serve MP3 files
app.MapGet("/audio/{fileName}", async (HttpContext context) =>
{
    var fileName = context.Request.RouteValues["fileName"] as string;
    var filePath = Path.Combine("kdhxfiles", fileName);

    if (File.Exists(filePath))
    {
        var fileStream = File.OpenRead(filePath);

        context.Response.ContentType = "audio/mpeg";
        await fileStream.CopyToAsync(context.Response.Body);
        fileStream.Close();
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("File not found");
    }
});



app.Run();
