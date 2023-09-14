using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class DownloadingService
    {
        private static readonly object _lock = new object();
        private Dictionary<string, List<string>> _schedule;

        public DownloadingService(Dictionary<string, List<string>> schedule)
        {
            _schedule = schedule;
        }

        public async Task DownloadFiles(IAsyncEnumerable<(string url, long fileName)> filesToDownload)
        {
            const int MaxConcurrentDownloads = 10;
            var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
            var downloadTasks = new List<Task>();

            var cstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

            await foreach (var file in filesToDownload)
            {
                var fileDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(file.fileName);
                var fileDateTime = TimeZoneInfo.ConvertTimeFromUtc(fileDateTimeOffset.UtcDateTime, cstTimeZone);
                var dayOfWeek = fileDateTime.DayOfWeek.ToString().ToLower();

                if (_schedule.ContainsKey(dayOfWeek))
                {
                    foreach (var scheduledTime in _schedule[dayOfWeek])
                    {
                        var scheduledTimeSpan = DateTime.ParseExact(scheduledTime, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;

                        var startRange = scheduledTimeSpan.Add(TimeSpan.FromMinutes(-59));
                        var endRange = scheduledTimeSpan.Add(TimeSpan.FromMinutes(59));

                        if (fileDateTime.TimeOfDay >= startRange && fileDateTime.TimeOfDay <= endRange)
                        {
                            await semaphore.WaitAsync();
                            var downloadTask = DownloadFile(file.url, file.fileName, semaphore);
                            downloadTasks.Add(downloadTask);
                            break;
                        }
                    }
                }
            }

            await Task.WhenAll(downloadTasks);
        }

        public async Task DownloadFile(string url, long fileName, SemaphoreSlim semaphore)
        {
            try
            {
                var tagging = new TaggingService();
                var cstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                var readableFileName = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(fileName).UtcDateTime, cstTimeZone).ToString("yyyy-MM-dd HH-mm-ss");

                var diskFileName = Path.Combine("kdhxfiles", $"{readableFileName}.mp3");
                if (File.Exists(diskFileName))
                {
                    return;
                }

                var httpClientHandler = new HttpClientHandler()
                {
                    UseProxy = false,
                    MaxConnectionsPerServer = 10,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                var httpClient = new HttpClient(httpClientHandler);
                httpClient.DefaultRequestHeaders.ConnectionClose = false;
                httpClient.Timeout = TimeSpan.FromMinutes(15);

                var response = await httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    lock (_lock)
                    {
                        Console.WriteLine($"Starting {url}");
                    }

                    var stream = await response.Content.ReadAsStreamAsync();
                    using (var fileStream = File.Create(diskFileName))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    tagging.TagMp3File(diskFileName);
                }
                else
                {
                    lock (_lock)
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        }
    }
}
