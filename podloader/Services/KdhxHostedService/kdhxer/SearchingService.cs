using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace podloader.Services.KdhxHostedService.kdhxer
{
    public class SearchingService
    {
        private static readonly object _lock = new object();
        private Dictionary<string, List<string>> _schedule;

        public SearchingService(Dictionary<string, List<string>> schedule)
        {
            _schedule = schedule;
        }

        public DateTime ConvertUnixToCST(long unixTimestamp)
        {
            TimeZoneInfo cst = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
            DateTime utcTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, cst);
        }

        public async IAsyncEnumerable<IAsyncEnumerable<(string url, long fileName)>> FindFiles(DateTime startDate, DateTime endDate)
        {
            for (int index = 0; index <= (endDate - startDate).Days; index++)
            {
                yield return FindFileForDay(startDate.AddDays(index));
            }
        }

        private async IAsyncEnumerable<(string url, long fileName)> FindFileForDay(DateTime current)
        {
            var dayOfWeek = current.DayOfWeek.ToString().ToLower();
            if (_schedule.ContainsKey(dayOfWeek))
            {
                var httpClientHandler = new HttpClientHandler()
                {
                    UseProxy = false,
                    MaxConnectionsPerServer = 10,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };

                var httpClient = new HttpClient(httpClientHandler);
                httpClient.DefaultRequestHeaders.ConnectionClose = false;

                Console.WriteLine($"Searching Day {current}");

                var secondsToSearch = GenerateFirstHourSearch(current);
                var firstFile = await CheckFileBySecondAsync(httpClient, secondsToSearch[0], secondsToSearch[secondsToSearch.Count - 1]);

                if (firstFile > 0)
                {
                    yield return ($"https://kdhx.org/archive/files/{firstFile}.mp3", firstFile);
                }

                for (int i = 1; i < 24; i++)
                {
                    long startSecond = firstFile + 3600;
                    var endSecond = startSecond + 3600;
                    var foundSecond = await CheckFileByHourAsync(httpClient, startSecond, endSecond);

                    if (foundSecond > 0)
                    {
                        yield return ($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond);
                        firstFile = foundSecond;
                    }
                    else
                    {
                        foundSecond = await CheckFileBySecondAsync(httpClient, startSecond, endSecond);

                        if (foundSecond > 0)
                        {
                            yield return ($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond);
                            firstFile = foundSecond;
                            i--;
                        }
                        else
                        {
                            lock (_lock)
                            {
                                Console.WriteLine($"Could not find file for {ConvertUnixToCST(startSecond)}");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine($"Skipping search for day {ConvertUnixToCST((long)(current - new DateTime(1970, 1, 1)).TotalSeconds)} as it's not in the schedule.");
            }
        }

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
            // Generate a list of all unix Seconds between 00:00:00 and 01:00:00 CST
            // Set the time zone to Central Standard Time (CST)
            TimeZoneInfo cst = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

            // Create the start and end times in CST
            DateTime start = TimeZoneInfo.ConvertTime(new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Utc), cst);
            DateTime end = start.AddHours(1);

            // Convert the start and end times to UTC
            start = TimeZoneInfo.ConvertTimeToUtc(start, cst);
            end = TimeZoneInfo.ConvertTimeToUtc(end, cst);

            // Generate the list of Unix seconds
            List<long> unixSeconds = new List<long>();
            for (DateTime current = start; current < end; current = current.AddSeconds(1))
            {
                unixSeconds.Add((long)(current - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds);
            }

            return unixSeconds;
        }

        public async Task<bool> CheckFileAsync(HttpClient httpClient, string url, long current)
        {
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
            if (response.IsSuccessStatusCode)
            {
                lock (_lock)
                {
                    Console.WriteLine($"Found file at {current} - CST: {ConvertUnixToCST(current)}");
                }
                return true;
            }
            return false;
        }
    }
}
