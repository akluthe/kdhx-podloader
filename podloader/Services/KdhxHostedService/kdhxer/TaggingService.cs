using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class TaggingService
    {
        private readonly JsonSerializerOptions jsonOptions;
        private readonly TimeZoneInfo cstTimeZone;

        public TaggingService()
        {
            // Configure JsonSerializerOptions to handle Enum as strings during deserialization
            jsonOptions = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };

            cstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        }

        public void TagMp3Files(string directory)
        {
            var files = Directory.GetFiles(directory, "*.mp3").OrderBy(f => f).ToList();

            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateTimeOffset.TryParseExact(fileName, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                {
                    var dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeOffset.UtcDateTime, cstTimeZone);

                    SetTitle(file, dateTime.ToString("yyyy-MM-dd h:mm:ss tt"));
                    SetAlbum(file, $"KDHX {dateTime:yyyy-MM-dd ddd}");
                    SetArtist(file, "KDHX DJ");
                }
                else
                {
                    Console.WriteLine($"Could not parse the date-time from the filename {fileName}.");
                }
            }
        }

        public async Task TagMp3File(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (DateTimeOffset.TryParseExact(fileName, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                {
                    var dateTime = TimeZoneInfo.ConvertTimeFromUtc(dateTimeOffset.UtcDateTime, cstTimeZone);

                    using (var tagFile = TagLib.File.Create(filePath))
                    {
                        tagFile.Tag.Album = $"KDHX {dateTime:yyyy-MM-dd ddd}";
                        tagFile.Tag.Performers = new[] { "KDHX DJ" };
                        tagFile.Tag.Title = dateTime.ToString("yyyy-MM-dd h:mm:ss tt");
                        tagFile.Tag.Year = (uint)dateTime.Year;

                        await Task.Run(() => tagFile.Save());
                    }
                }
                else
                {
                    Console.WriteLine($"Could not parse the date-time from the filename {fileName}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tagging file {filePath}: {ex.Message}");
            }
        }

        public void SetTitle(string filePath, string title)
        {
            using var mp3File = TagLib.File.Create(filePath);
            mp3File.Tag.Title = title;
            mp3File.Save();
        }

        public void SetArtist(string filePath, string artist)
        {
            using var mp3File = TagLib.File.Create(filePath);
            mp3File.Tag.Performers = new[] { artist };
            mp3File.Save();
        }

        public void SetAlbum(string filePath, string album)
        {
            using var mp3File = TagLib.File.Create(filePath);
            mp3File.Tag.Album = album;
            mp3File.Save();
        }
    }
}
