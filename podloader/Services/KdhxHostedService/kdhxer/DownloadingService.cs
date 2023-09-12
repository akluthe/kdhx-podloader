
using System.Globalization;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class DownloadingService
    {
        //lockObj is used to lock the console output so that it doesn't get jumbled
        private static readonly object _lock = new object();
        private Dictionary<string, List<string>> _schedule;

        public DownloadingService(Dictionary<string, List<string>> schedule)
        {
            _schedule = schedule;
        }
        public async Task DownloadFiles(IAsyncEnumerable<(string url, long fileName)> filesToDownload)
        {
            // set the maximum number of concurrent downloads
            const int MaxConcurrentDownloads = 10;

            // create a semaphore to limit the number of concurrent downloads
            var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);

            // create a list to hold the download tasks
            var downloadTasks = new List<Task>();

            // asynchronously iterate through the files to download
            await foreach (var file in filesToDownload)
            {
                var fileDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(file.fileName);
                var fileDateTime = fileDateTimeOffset.LocalDateTime; // Get the local date and time from the DateTimeOffset
                var dayOfWeek = fileDateTime.DayOfWeek.ToString().ToLower();

                // Checking if the day of the week exists in the schedule
                if (_schedule.ContainsKey(dayOfWeek))
                {
                    // Loop through the times for the day in the schedule
                    foreach (var scheduledTime in _schedule[dayOfWeek])
                    {
                        // Convert the scheduled time string to a TimeSpan
                        var scheduledTimeSpan = DateTime.ParseExact(scheduledTime, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;

                        // Create a time range starting from 50 minutes before the scheduled time and ending 50 minutes after
                        var startRange = scheduledTimeSpan.Add(TimeSpan.FromMinutes(-59));
                        var endRange = scheduledTimeSpan.Add(TimeSpan.FromMinutes(59));

                        // Check if the file's time is within the range
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

            // wait for all download tasks to complete
            await Task.WhenAll(downloadTasks);
        }


        public async Task DownloadFile(string url, long fileName, SemaphoreSlim semaphore)
        {
            try
            {
                var readableFileName = DateTimeOffset.FromUnixTimeSeconds(fileName).DateTime.ToLocalTime().ToString("yyyy-MM-dd HH-mm-ss");
                //var diskFileName = $@"H:\KDHX\{readableFileName}.mp3";
                var diskFileName = Path.Combine("kdhxfiles", $"{readableFileName}.mp3");
                if (File.Exists(diskFileName))
                {
                    return;
                }

                var httpClientHandler = new HttpClientHandler()
                {
                    UseProxy = false, // disable proxy to avoid unnecessary overhead
                    MaxConnectionsPerServer = 10, // maximum number of connections per server
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate // enable gzip and deflate compression
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
                }
                else
                {
                    // update the progress
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
