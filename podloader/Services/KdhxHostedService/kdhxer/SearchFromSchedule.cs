using System.Globalization;
using System.Text.Json;

namespace podloader.Services.KdhxHostedService.kdhxer
{

    public class ScheduleReader
    {
        private readonly string scheduleFilePath;

        public ScheduleReader(string scheduleFilePath)
        {
            this.scheduleFilePath = scheduleFilePath;
        }

        public Dictionary<string, List<string>> ReadSchedule()
        {
            if (!File.Exists(scheduleFilePath))
            {
                throw new FileNotFoundException("Schedule file not found.", scheduleFilePath);
            }

            var scheduleJson = File.ReadAllText(scheduleFilePath);
            var schedule = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(scheduleJson);

            return schedule;
        }
    }


    public class SearchFromSchedule
    {

        //lock property to prevent multiple threads from accessing the same list
        private static readonly object _lock = new object();


        private readonly HttpClient httpClient;
        private readonly Dictionary<string, List<string>> schedule;
        private readonly DateTime startDate;
        private readonly DateTime endDate;

        public SearchFromSchedule(string scheduleFilePath, DateTime startDate, DateTime endDate)
        {

            httpClient = HttpClientHelper.GetHttpClient();

            // Read the schedule from the JSON file using ScheduleReader
            var scheduleReader = new ScheduleReader(scheduleFilePath);
            schedule = scheduleReader.ReadSchedule();
            this.startDate = startDate;
            this.endDate = endDate;
        }

        public async IAsyncEnumerable<(string url, long fileName)> FindFiles()
        {
            // Get the list of days of the week from the schedule
            var daysOfWeek = schedule.Keys.ToList();

            foreach (var dayOfWeek in daysOfWeek)
            {
                var timeSlots = schedule[dayOfWeek];
                foreach (var timeSlot in timeSlots)
                {
                    var startTime = DateTime.ParseExact(timeSlot, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;
                    var endTime = startTime.Add(TimeSpan.FromHours(1));
                    var currentDateTime = startDate.Date;

                    while (currentDateTime <= endDate.Date)
                    {
                        var currentTime = currentDateTime.Date.Add(startTime);

                        if (currentTime <= endDate)
                        {
                            // Check if the current day of the week is in the list of input days
                            if (currentTime.DayOfWeek.ToString().ToLower() == dayOfWeek)
                            {
                                Console.WriteLine($"Searching Day {currentTime.ToShortDateString()} at {currentTime.ToShortTimeString()}");
                                var secondsToSearch = GenerateTimeSlotSearch(currentTime, endTime);


                                await foreach (var fileFound in SearchFilesWithinTimeSlotAsync(secondsToSearch, currentTime))
                                {
                                    yield return fileFound;
                                }
                            }
                        }

                        currentDateTime = currentDateTime.AddDays(1);
                    }
                }
            }
        }

        private List<long> GenerateTimeSlotSearch(DateTimeOffset startDate, TimeSpan endTime)
        {
            TimeZoneInfo cdt = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

            // Convert the start and end times to the CDT time zone
            startDate = TimeZoneInfo.ConvertTime(startDate, cdt);
            var endDateTime = startDate.Date.Add(endTime);
            endDateTime = TimeZoneInfo.ConvertTime(endDateTime, cdt);

            // Generate the list of Unix seconds
            List<long> unixSeconds = new List<long>();
            for (DateTimeOffset current = startDate; current < endDateTime; current = current.AddSeconds(1))
            {
                unixSeconds.Add(current.ToUnixTimeSeconds());
            }

            return unixSeconds;
        }

        private async IAsyncEnumerable<(string url, long fileName)> SearchFilesWithinTimeSlotAsync(List<long> secondsToSearch, DateTime currentDate)
        {
            var timeSlots = schedule[currentDate.DayOfWeek.ToString().ToLower()];
            foreach (var timeSlot in timeSlots)
            {
                var startTime = DateTime.ParseExact(timeSlot, "h:mm tt", CultureInfo.InvariantCulture).TimeOfDay;
                var endTime = startTime.Add(TimeSpan.FromHours(1));
                var currentTime = currentDate.Date.Add(startTime);

                if (currentTime <= endDate)
                {
                    Console.WriteLine($"Searching Day {currentTime.ToShortDateString()} at {currentTime.ToShortTimeString()}");

                    await foreach (var fileFound in SearchFilesWithinSingleTimeSlotAsync(secondsToSearch, currentTime))
                    {
                        yield return fileFound;
                    }
                }
            }
        }

        private async IAsyncEnumerable<(string url, long fileName)> SearchFilesWithinSingleTimeSlotAsync(List<long> secondsToSearch, DateTime startTime)
        {
            foreach (var second in secondsToSearch)
            {
                var url = $"https://kdhx.org/archive/files/{second}.mp3";
                var foundSecond = await CheckFileBySecondAsync(httpClient, second, second);

                if (foundSecond > 0)
                {
                    yield return (url, foundSecond);
                    Console.WriteLine($"Found file at {url} - {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
                    break;
                }
                else
                {
                    // Search within the hour for the current second
                    var startSecond = second;
                    var endSecond = second + 3600;
                    foundSecond = await CheckFileByHourAsync(httpClient, startSecond, endSecond);

                    if (foundSecond > 0)
                    {
                        yield return (url, foundSecond);
                        Console.WriteLine($"Found file at {url} - {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"Could not find file for {DateTimeOffset.FromUnixTimeSeconds(second).DateTime.ToLocalTime()}");
                    }
                }
            }
        }






        public void AddFileToList(List<(string url, long fileName)> matchingFiles, long foundSecond)
        {
            if (foundSecond > 0)
            {
                // update the progress
                lock (_lock)
                {
                    Console.WriteLine($"Found file at {foundSecond} - {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
                }
                matchingFiles.Add(($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond));

            }
            else
            {
                // update the progress
                lock (_lock)
                {
                    Console.WriteLine($"Could not find file for {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
                }
            }
        }

        //Function To Search by Second
        public async Task<long> CheckFileBySecondAsync(HttpClient httpClient, long startSeconds, long endSeconds)
        {
            for (long current = startSeconds; current <= endSeconds; current++)
            {
                var url = $"https://kdhx.org/archive/files/{current}.mp3";
                if (await CheckFileAsync(httpClient, url, current))
                {
                    return current;
                }
            }
            return 0;
        }

        //Function to Search by Hour
        public async Task<long> CheckFileByHourAsync(HttpClient httpClient, long startSeconds, long endSeconds)
        {
            for (long current = startSeconds; current <= endSeconds; current += 3600)
            {
                var url = $"https://kdhx.org/archive/files/{current}.mp3";
                if (await CheckFileAsync(httpClient, url, current))
                {
                    return current;
                }
                else
                {
                    //check by the second for 10 minutes before current until you find it
                    var startSecond = current - 3;
                    var endSecond = current + 600;
                    var foundSecond = await CheckFileBySecondAsync(httpClient, startSecond, endSecond);
                    if (foundSecond > 0)
                    {
                        return foundSecond;
                    }
                }
            }

            return 0;
        }

        public List<long> GenerateFirstHourSearch(DateTime startDate)
        {
            // Generate a list of all unix Seconds between 00:00:00 and 1:00:00 CDT
            // Set the time zone to Central Daylight Time (CDT)
            TimeZoneInfo cdt = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

            // Set the start and end times
            DateTime start = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
            DateTime end = new DateTime(startDate.Year, startDate.Month, startDate.Day, 1, 0, 0, DateTimeKind.Unspecified);

            // Convert the start and end times to the CDT time zone
            start = TimeZoneInfo.ConvertTimeToUtc(start, cdt);
            end = TimeZoneInfo.ConvertTimeToUtc(end, cdt);

            // Generate the list of Unix seconds
            List<long> unixSeconds = new List<long>();
            for (DateTime current = start; current < end; current = current.AddSeconds(1))
            {
                unixSeconds.Add((long)(current - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            }

            return unixSeconds;
        }

        public async Task<bool> CheckFileAsync(HttpClient httpClient, string url, long fileName)
        {
            try
            {
                var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

                if (response.IsSuccessStatusCode)
                {
                    // update the progress
                    lock (_lock)
                    {
                        // turn filename into datetime local, output to console
                        Console.WriteLine($"Found file at {url} - {DateTimeOffset.FromUnixTimeSeconds(fileName).DateTime.ToLocalTime()}");
                        // update the progress
                    }
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                // update the progress
                lock (_lock)
                {
                    Console.WriteLine($"Error checking file at {url}: {ex.Message}");
                }
                return false;
            }
        }

    }


}
