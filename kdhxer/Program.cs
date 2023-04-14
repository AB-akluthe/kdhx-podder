using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

if (args.Length < 2 || !DateTime.TryParse(args[0], out DateTime startDate) || !DateTime.TryParse(args[1], out DateTime endDate))
{
    Console.WriteLine("Usage: FileSearch <startDate> <endDate>");
    startDate = new DateTime(2023, 4, 3);
    endDate = new DateTime(2023, 4, 9);
}

var matchingFiles = new List<(string url, long fileName)>();
var httpClient = new HttpClient();
int totalSeconds = (int)(endDate - startDate).TotalSeconds;

var tasks = new List<Task>();
for (int i = 0; i < totalSeconds; i++)
{
    var currentDate = startDate.AddSeconds(i);
    var currentTimestamp = new DateTimeOffset(currentDate).ToUnixTimeSeconds();

    var url = $"https://kdhx.org/archive/files/{currentTimestamp}.mp3";
    var fileName = currentTimestamp;

    if (matchingFiles.Count > 0)
    {
        // check if the file 1 hour from the current time exists on the server
        var nextDate = matchingFiles[matchingFiles.Count - 1].fileName + 3600;
        if (currentTimestamp < nextDate)
        {
            continue;
        }
    }

    var task = CheckFileAsync(httpClient, url, fileName, matchingFiles);
    tasks.Add(task);
    await Task.Delay(25);
}

await Task.WhenAll(tasks);

Console.WriteLine("files found: " + matchingFiles.Count);
Console.WriteLine($"Files between {startDate} and {endDate}:");
foreach (var file in matchingFiles)
{
    Console.WriteLine($"{file.url} - {file.fileName}");
}

async Task CheckFileAsync(HttpClient httpClient, string url, long fileName, List<(string, long)> matchingFiles)
{
    try
    {
        var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

        if (response.IsSuccessStatusCode)
        {

            // check if the file 1 hour from the current time exists on the server

            var nextTimestamp = fileName + 3600;
            var nextUrl = $"https://kdhx.org/archive/files/{nextTimestamp}.mp3";
            var nextResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, nextUrl));

            if (nextResponse.IsSuccessStatusCode)
            {
                lock (matchingFiles)
                {
                    matchingFiles.Add((url, fileName));
                }
                // turn filename from unix seconds into CDT string
                var date = DateTimeOffset.FromUnixTimeSeconds(fileName).ToLocalTime();
                Console.WriteLine($"{url} - {fileName} - {date}");



            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error checking file at {url}: {ex.Message}");
    }
}
