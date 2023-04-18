﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

if (args.Length < 2 || !DateTime.TryParse(args[0], out DateTime startDate) || !DateTime.TryParse(args[1], out DateTime endDate))
{
    Console.WriteLine("Usage: FileSearch <startDate> <endDate>");

    //today at midnight if they didnt pass
    startDate = DateTime.Today;
    endDate = DateTime.Today.AddDays(1);
}

/*

Figure out how to iterate through each day, and find the first file that exists.

Then scan forward 1 hour + or - 5 minutes to see if the next file exists.

Check for the next file 1 hour + or - 5 minutes after that.

Proceed until you have 24 hours of files.

*/

// $"https://kdhx.org/archive/files/{currentTimestamp}.mp3";

var lockObj = new object();

var matchingFiles = new List<(string url, long fileName)>();

// iterate through each day between startDate and endDate
var tasks = Enumerable.Range(0, (endDate - startDate).Days + 1)
    .Select(index => Task.Run(async () =>
    {
        var current = startDate.AddDays(index);

        var httpClientHandler = new HttpClientHandler()
        {
            UseProxy = false, // disable proxy to avoid unnecessary overhead
            MaxConnectionsPerServer = 10, // maximum number of connections per server
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate // enable gzip and deflate compression
        };

        var httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.ConnectionClose = false;


        Console.WriteLine($"Searching Day {current.ToShortDateString()}");
        var secondsToSearch = await GenerateFirstHourSearch(current);

        //check to find the first file in the first hour
        var firstFile = await CheckFileBySecondAsync(httpClient, secondsToSearch[0], secondsToSearch[secondsToSearch.Count - 1]);

        if (firstFile > 0)
        {
            await AddFileToList(matchingFiles, firstFile);

        }

        //for the next 23 hours, check to see if the file exists
        for (int i = 1; i < 24; i++)
        {
            //startSecond will be firstFile + 1 hour * i if i=1 or else it will be the last file in MatchingFiles + 1 hour
            long startSecond = 0;
            if (i == 1)
            {
                startSecond = firstFile + 3600;
            }
            else
            {
                startSecond = matchingFiles[matchingFiles.Count - 1].fileName + 3600;
            }

            var endSecond = startSecond + 3600;
            var foundSecond = await CheckFileByHourAsync(httpClient, startSecond, endSecond);
            if (foundSecond > 0)
            {
                await AddFileToList(matchingFiles, foundSecond);
            }
            else
            {
                // update the progress
                lock (lockObj)
                {

                    Console.WriteLine($"Could not find file for {DateTimeOffset.FromUnixTimeSeconds(startSecond).DateTime.ToLocalTime()}");
                }
            }
        }

        // update the progress
        lock (lockObj)
        {
            Console.WriteLine($"Found {matchingFiles.Count} files");
        }

    }
));

await Task.WhenAll(tasks);

await DownloadFiles(matchingFiles);

async Task AddFileToList(List<(string url, long fileName)> matchingFiles, long foundSecond)
{
    if (foundSecond > 0)
    {
        // update the progress
        lock (lockObj)
        {
            Console.WriteLine($"Found file at {foundSecond} - {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
        }
        matchingFiles.Add(($"https://kdhx.org/archive/files/{foundSecond}.mp3", foundSecond));

    }
    else
    {
        // update the progress
        lock (lockObj)
        {
            Console.WriteLine($"Could not find file for {DateTimeOffset.FromUnixTimeSeconds(foundSecond).DateTime.ToLocalTime()}");
        }
    }
}

//Function To Search by Second
async Task<long> CheckFileBySecondAsync(HttpClient httpClient, long startSeconds, long endSeconds)
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
async Task<long> CheckFileByHourAsync(HttpClient httpClient, long startSeconds, long endSeconds)
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

async Task DownloadFiles(List<(string url, long fileName)> matchingFiles)
{



    // set the maximum number of concurrent downloads
    const int MaxConcurrentDownloads = 10;

    // create a semaphore to limit the number of concurrent downloads
    var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);

    // create a list to hold the download tasks
    var downloadTasks = new List<Task>();

    // for each file in matchingFiles, create a download task
    foreach (var file in matchingFiles)
    {
        // acquire a semaphore slot
        await semaphore.WaitAsync();

        // create a download task
        var task = Task.Run(async () =>
        {
            try
            {

                var httpClientHandler = new HttpClientHandler()
                {
                    UseProxy = false, // disable proxy to avoid unnecessary overhead
                    MaxConnectionsPerServer = 10, // maximum number of connections per server
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate // enable gzip and deflate compression


                };

                var httpClient = new HttpClient(httpClientHandler);
                httpClient.DefaultRequestHeaders.ConnectionClose = false;
                httpClient.Timeout = TimeSpan.FromMinutes(15);

                var response = await httpClient.GetAsync(file.url);
                if (response.IsSuccessStatusCode)
                {
                    lock (lockObj)
                    {
                        Console.WriteLine($"Starting {file.url}");
                    }
                    var readableFileName = DateTimeOffset.FromUnixTimeSeconds(file.fileName).DateTime.ToLocalTime().ToString("yyyy-MM-dd HH-mm-ss");
                    var stream = await response.Content.ReadAsStreamAsync();
                    using (var fileStream = File.Create($@"H:\KDHX\{readableFileName}.mp3"))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
                else
                {
                    // update the progress
                    lock (lockObj)
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
            }
            finally
            {
                // release the semaphore slot
                // update the progress
                lock (lockObj)
                {
                    Console.WriteLine($"Downloaded {file.url}");
                }
                semaphore.Release();
            }
        });

        // add the task to the list
        downloadTasks.Add(task);
    }

    // wait for all download tasks to complete
    await Task.WhenAll(downloadTasks);


}

async Task<List<long>> GenerateFirstHourSearch(DateTime startDate)
{
    // Generate a list of all unix Seconds between 00:00:00 and 1:00:00 CDT
    // Set the time zone to Central Daylight Time (CDT)
    TimeZoneInfo cdt = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

    // Set the start and end times
    DateTime start = new DateTime(2023, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
    DateTime end = new DateTime(2023, startDate.Month, startDate.Day, 1, 0, 0, DateTimeKind.Unspecified);

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

async Task<bool> CheckFileAsync(HttpClient httpClient, string url, long fileName)
{
    try
    {
        var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

        if (response.IsSuccessStatusCode)
        {
            // update the progress
            lock (lockObj)
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
        lock (lockObj)
        {
            Console.WriteLine($"Error checking file at {url}: {ex.Message}");
        }
        return false;
    }
}
