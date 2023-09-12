using System.Runtime.CompilerServices;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class SearchingService
    {

        //lock property to prevent multiple threads from accessing the same list
        private static readonly object _lock = new object();
        private Dictionary<string, List<string>> _schedule;

        public SearchingService(Dictionary<string, List<string>> schedule)
        {
            _schedule = schedule;
        }


        public async IAsyncEnumerable<IAsyncEnumerable<(string url, long fileName)>> FindFiles(DateTime startDate, DateTime endDate)
        {
            // iterate through each day between startDate and endDate
            for (int index = 0; index <= (endDate - startDate).Days; index++)
            {
                yield return FindFileForDay(startDate.AddDays(index));
            }
        }

        private async IAsyncEnumerable<(string url, long fileName)> FindFileForDay(DateTime current)
        {
            // Check if the current day is present in the schedule
            var dayOfWeek = current.DayOfWeek.ToString().ToLower();
            if (_schedule.ContainsKey(dayOfWeek))
            {
                var httpClientHandler = new HttpClientHandler()
                {
                    UseProxy = false, // disable proxy to avoid unnecessary overhead
                    MaxConnectionsPerServer = 10, // maximum number of connections per server
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate // enable gzip and deflate compression
                };

                var httpClient = new HttpClient(httpClientHandler);
                httpClient.DefaultRequestHeaders.ConnectionClose = false;

                Console.WriteLine($"Searching Day {current.ToShortDateString()}");
                var secondsToSearch = GenerateFirstHourSearch(current);

                //check to find the first file in the first hour
                var firstFile = await CheckFileBySecondAsync(httpClient, secondsToSearch[0], secondsToSearch[secondsToSearch.Count - 1]);

                var matchingFiles = new List<(string url, long fileName)>();

                if (firstFile > 0)
                {
                    matchingFiles.Add(($"https://kdhx.org/archive/files/{firstFile}.mp3", firstFile));
                    yield return ($"https://kdhx.org/archive/files/{firstFile}.mp3", firstFile);
                }

                //for the next 23 hours, check to see if the file exists
                for (int i = 1; i < 24; i++)
                {
                    // StartSecond will be firstFile + 1 hour
                    long startSecond = firstFile + 3600;
                    var endSecond = startSecond + 3600;

                    // First, attempt to find the file on the hour
                    var foundSecond = await CheckFileByHourAsync(httpClient, startSecond, endSecond);

                    if (foundSecond > 0)
                    {
                        // If a file is found on the hour, add it to the matching files and return it
                        matchingFiles.Add(($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond));
                        yield return ($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond);

                        // Update firstFile to be the found file's time
                        firstFile = foundSecond;
                    }
                    else
                    {
                        // If no file is found on the hour, start checking every second until the next file is found
                        foundSecond = await CheckFileBySecondAsync(httpClient, startSecond, endSecond);

                        if (foundSecond > 0)
                        {
                            // If a file is found within the second, add it to the matching files and return it
                            matchingFiles.Add(($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond));
                            yield return ($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond);

                            // Update firstFile to be the found file's time
                            firstFile = foundSecond;

                            // Decrement the loop index to repeat the hourly check with the new file
                            i--;
                        }
                        else
                        {
                            lock (_lock)
                            {
                                Console.WriteLine($"Could not find file for {DateTimeOffset.FromUnixTimeSeconds(startSecond).DateTime.ToLocalTime()}");
                            }
                        }
                    }
                }

            }
            else
            {
                // If the current day is not in the schedule, skip the search for this day.
                Console.WriteLine($"Skipping search for day {current.ToShortDateString()} as it's not in the schedule.");
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
