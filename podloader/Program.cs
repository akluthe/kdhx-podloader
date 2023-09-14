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
using System.Text;
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

app.MapGet("/podcast/kdhx", async (HttpContext context) =>
{
    // Create the RSS feed
    var rss = new Rss
    {
        Version = "2.0",
        Channel = new Channel
        {
            Title = "Your Podcast Title",
            Link = "https://podloader.kdhx.box.ca/podcast/kdhx", // Set the link to your podcast's website
            Description = "Your Podcast Description",
            Language = "en-us", // Set the language code
            PubDate = DateTime.Now.ToString("R"), // Set the publication date
            LastBuildDate = DateTime.Now.ToString("R"), // Set the last build date
            iTunesAuthor = "Your Podcast Author", // Set the author
            iTunesKeywords = "Keywords, Separated, By, Commas", // Set keywords
            iTunesExplicit = "no", // Set explicit content status
            iTunesImage = new iTunesImage { Href = "https://placehold.co/600x400" }, // Set the podcast image URL
            iTunesOwner = new iTunesOwner
            {
                Name = "Your Name",
                Email = "Your Email"
            },
            iTunesBlock = new iTunesBlock { Value = "no" }, // Set iTunes block status
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
            Title = fileInfo.Name,
            Description = fileInfo.Name, // Set episode description
            PubDate = DateTime.Now.ToString("R"), // Set episode publication date
            Enclosure = new Enclosure
            {
                Url = $"{baseUrl}/audio/{fileInfo.Name}",
                Type = "audio/mpeg",
                Length = fileInfo.Length
            },
            Guid = $"{baseUrl}/audio/{fileInfo.Name}", // Generate a unique GUID for the episode
            iTunesAuthor = "Your Episode Author", // Set episode author
            iTunesSubtitle = "Your Episode Subtitle", // Set episode subtitle
            iTunesSummary = "Your Episode Summary", // Set episode summary
            iTunesExplicit = "no", // Set episode explicit content status
            iTunesDuration = "00:00:00" // Set episode duration (in the format HH:mm:ss)
        };

        rss.Channel.Item.Add(item);
    }

    // Serialize the RSS feed to XML with UTF-8 encoding
    var serializer = new XmlSerializer(typeof(Rss));
    var xmlString = "";
    using (var writer = new StringWriter())
    {
        var settings = new XmlWriterSettings
        {
            Encoding = Encoding.UTF8 // Set the encoding to UTF-8
        };

        using (var xmlWriter = XmlWriter.Create(writer, settings))
        {
            // Add the iTunes namespace declaration
            var xmlns = new XmlSerializerNamespaces();
            xmlns.Add("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            serializer.Serialize(xmlWriter, rss, xmlns);
            xmlString = writer.ToString();
        }
    }

    // Set the response content type to RSS
    context.Response.ContentType = "application/rss+xml";
    await context.Response.WriteAsync(xmlString);
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
        context.Response.ContentLength = fileStream.Length; // Set the content length
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
