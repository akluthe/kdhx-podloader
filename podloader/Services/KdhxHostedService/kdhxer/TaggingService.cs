using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class TaggingService
    {
        private readonly JsonSerializerOptions jsonOptions;
        public TaggingService()
        {
            // Configure JsonSerializerOptions to handle Enum as strings during deserialization
            jsonOptions = new JsonSerializerOptions
            {
                Converters = { new JsonStringEnumConverter() },
                PropertyNameCaseInsensitive = true
            };
        }

        //TODO: Improve this so it can tag the files with the current show maybe against the radio schedule
        public void TagMp3Files(string directory)
        {

            //get all mp3 files in the directory
            var files = Directory.GetFiles(directory, "*.mp3").OrderBy(f => f).ToList();

            foreach (var file in files)
            {
                //set title to Human Readable DateTime using the Filename in this format: 2023-04-11 00-00-02.mp3
                var fileName = Path.GetFileNameWithoutExtension(file);
                var dateTime = DateTimeOffset.ParseExact(fileName, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture).LocalDateTime;

                //set title as 12 hour time
                SetTitle(file, dateTime.ToString("yyyy-MM-dd h:mm:ss tt"));

                // set the Album to Kdhx and short Day of the Week and 4/1/23
                SetAlbum(file, $"KDHX {dateTime:yyyy-MM-dd ddd}");

                // set the Artist to the show Name
                SetArtist(file, "KDHX DJ");

                //SetDateTime(file, dateTime);

            }

        }

        public async Task TagMp3File(string filePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var dateTime = DateTimeOffset.ParseExact(fileName, "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture).LocalDateTime;

                using (var tagFile = TagLib.File.Create(filePath))
                {
                    tagFile.Tag.Album = $"KDHX {dateTime:yyyy-MM-dd ddd}";
                    tagFile.Tag.Performers = new[] { "KDHX DJ" };
                    tagFile.Tag.Title = dateTime.ToString("yyyy-MM-dd h:mm:ss tt");
                    tagFile.Tag.Year = (uint)dateTime.Year;

                    await Task.Run(() => tagFile.Save());
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
