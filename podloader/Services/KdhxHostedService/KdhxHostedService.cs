using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using podloader.Services.KdhxHostedService.kdhxer;

namespace podloader.Services.KdhxHostedService
{
    public class KdhxHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<KdhxHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer _timer;

        public bool IsJobRunning { get; private set; } = false;

        public KdhxHostedService(ILogger<KdhxHostedService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("KDHX Hosted Service running.");
            _timer = new Timer(ExecuteTask, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); // Check every minute
            return Task.CompletedTask;
        }

        private async void ExecuteTask(object state)
        {
            var now = DateTime.Now;
            // 5:00AM CDT and 10:00AM (UTC when it runs on the server)
            //var targetTime = new DateTime(now.Year, now.Month, now.Day, 10, 0, 0);

           var targetTime = now;
          
            _logger.LogDebug($"Current Time: {now} => Run Time: {targetTime}");

            // If the time is already past for today, look for 5 AM tomorrow
            if (now > targetTime)
            {
                targetTime = targetTime.AddDays(1);
            }

            // Check if it's time to run the task
            if ((targetTime - now).TotalMinutes <= 1 && !IsJobRunning)
            {
                await DoWork(CancellationToken.None);
            }
        }

        public async Task DoWork(CancellationToken cancellationToken)
        {
            IsJobRunning = true;
            _logger.LogInformation("KDHX Hosted Service is working.");

            // Get current time in CST
            TimeZoneInfo cstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            DateTime nowCst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, cstZone);

            // Create a DateTime object for the start date in CST, which is one day behind
            DateTime startDate = new DateTime(nowCst.Year, nowCst.Month, nowCst.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(-1);

            // Create a DateTime object for the end date in CST, which is current day till last second
            DateTime endDate = new DateTime(nowCst.Year, nowCst.Month, nowCst.Day, 23, 59, 59, DateTimeKind.Unspecified);

            _logger.LogInformation($"Start Date: {startDate} => End Date: {endDate}");

            // Read the schedule from a JSON file
            var scheduleReader = new ScheduleReader("schedule.json");
            var schedule = scheduleReader.ReadSchedule();

            var tagging = new TaggingService();
            var searching = new SearchingService(schedule);
            var downloading = new DownloadingService(schedule);

            // Create a semaphore to limit concurrent downloads
            var semaphore = new SemaphoreSlim(10); // Set the maximum number of concurrent downloads (e.g., 10)

            try
            {
                await foreach (var fileToDownload in searching.FindFiles(startDate, endDate))
                {
                    // Start the download task for each file
                    await downloading.DownloadFiles(fileToDownload);

                    // Tag the downloaded file
                    //tagging.TagMp3File($@"H:\KDHX\{fileToDownload.fileName}.mp3");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            tagging.TagMp3Files(@"kdhxfiles"); // Replace with the actual path of your downloaded files
            IsJobRunning = false;
        }



        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("KDHX Hosted Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
