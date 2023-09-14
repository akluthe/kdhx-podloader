using Microsoft.AspNetCore.Mvc;
using podloader;
using podloader.Services.KdhxHostedService;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add hosted service and logging
builder.Services.AddLogging(builder => builder.AddConsole());
builder.Services.AddSingleton<KdhxHostedService>(); // Register as singleton
builder.Services.AddHostedService(provider => provider.GetRequiredService<KdhxHostedService>());

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
            Title = "KDHX Radio",
            Link = "https://podloader.kdhx.box.ca/podcast/kdhx", // Set the link to your podcast's website
            Description = "Radio Syndication",
            Language = "en-us", // Set the language code
            PubDate = DateTime.Now.ToString("R"), // Set the publication date
            LastBuildDate = DateTime.Now.ToString("R"), // Set the last build date
            iTunesAuthor = "KDHX", // Set the author
            iTunesKeywords = "Radio, KDHX", // Set keywords
            iTunesExplicit = "no", // Set explicit content status
            iTunesImage = new iTunesImage { Href = "https://placehold.co/600x400" }, // Set the podcast image URL
            iTunesOwner = new iTunesOwner
            {
                Name = "KDHX",

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

        //read tags from mp3 file
        var tagFile = TagLib.File.Create(mp3File);
        var tag = tagFile.Tag;

        //parse the title into a date (inferring the file is CST)
        var date = DateTime.Parse(tag.Title).ToUniversalTime();
        


        var item = new Item
        {
            Title = tag.Title,
            Description = tag.Album, // Set episode description
            PubDate = date.ToString("R"), // Set episode publication date from file name to "R" format
            Enclosure = new Enclosure
            {
                Url = $"{baseUrl}/audio/{fileInfo.Name}",
                Type = "audio/mpeg",
                Length = fileInfo.Length
            },
            Guid = $"{baseUrl}/audio/{fileInfo.Name}",
            //iTunesAuthor = "Your Episode Author", // Set episode author
            //iTunesSubtitle = "Your Episode Subtitle", // Set episode subtitle
            //iTunesSummary = "Your Episode Summary", // Set episode summary
            iTunesExplicit = "no", // Set episode explicit content status
            iTunesDuration = "01:00:00" // Set episode duration (in the format HH:mm:ss)
        };

        rss.Channel.Item.Add(item);
    }

    // Serialize the RSS feed to XML
    var serializer = new XmlSerializer(typeof(Rss));
    var xmlString = "";

    using (var memoryStream = new MemoryStream())
    {
        using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8))
        {
            var xmlns = new XmlSerializerNamespaces();
            xmlns.Add("itunes", "http://www.itunes.com/dtds/podcast-1.0.dtd");

            serializer.Serialize(streamWriter, rss, xmlns);
        }

        xmlString = Encoding.UTF8.GetString(memoryStream.ToArray());
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
