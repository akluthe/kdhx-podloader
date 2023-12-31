﻿using System;
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
                    var dateTime = dateTimeOffset.DateTime;

                    SetTitle(file, dateTime.ToString("yyyy-MM-dd h:mm:ss tt ddd"));
                    SetAlbum(file, $"KDHX {dateTime:yyyy-MM-dd ddd}");
                    SetArtist(file, "KDHX DJ");
                    //Console.WriteLine($"Tagged {file} with {dateTime:yyyy-MM-dd h:mm:ss tt}");
                }
                else
                {
                    Console.WriteLine($"Could not parse the date-time from the filename {fileName}.");
                }
            }
        }

        public void TagMp3File(string file)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (DateTimeOffset.TryParseExact(fileName, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset))
                {
                    var dateTime = dateTimeOffset.DateTime;

                    SetTitle(file, dateTime.ToString("yyyy-MM-dd h:mm:ss tt ddd"));
                    SetAlbum(file, $"KDHX {dateTime:yyyy-MM-dd ddd}");
                    SetArtist(file, "KDHX DJ");
                    //Console.WriteLine($"Tagged {file} with {dateTime:yyyy-MM-dd h:mm:ss tt}");
                }
                else
                {
                    Console.WriteLine($"Could not parse the date-time from the filename {fileName}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tagging file {file}: {ex.Message}");
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
