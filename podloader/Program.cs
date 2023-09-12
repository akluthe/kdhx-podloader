using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using podloader;
using podloader.Services.KdhxHostedService;
using System.Text.Json;
using System.Xml.Serialization;

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


string serverIPAddress = "192.168.1.20";
int serverPort = 9876;
var feedFullPath = "/Temp/webroot/feed.rss";
var audioRootUrl = $"http://{serverIPAddress}:{serverPort}";

// Your existing podcast logic
app.MapGet("/podcast/kdhx", async () =>
{
    // return podcast rss feed for path.combine("kdhxfiles", "*.mp3"), use a nuget to create the podcast response

    var directory = Path.Combine("kdhxfiles");
    var files = Directory.GetFiles(directory, "*.mp3");

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
    var allMp3s = new DirectoryInfo(directory)
        .GetFiles("*.mp3", SearchOption.AllDirectories)
        .OrderBy(x => x.Name);

    foreach (var mp3 in allMp3s)
    {
        rss.Channel.Item.Add(new Item
        {
            Description = mp3.Name,
            Title = mp3.Name,
            Enclosure = new Enclosure
            {
                Url = $"{audioRootUrl}/{mp3.Name}",
                Type = "audio/mpeg"
            }
        });
    }
    var serializer = new XmlSerializer(typeof(Rss));
    using (var writer = new StreamWriter(feedFullPath))
    {
        serializer.Serialize(writer, rss);
    }

});

app.Run();
