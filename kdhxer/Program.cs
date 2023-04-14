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

var matchingFiles = new List<(string url, string fileName)>();
var httpClient = new HttpClient();
int totalSeconds = (int)(endDate - startDate).TotalSeconds;

var tasks = new List<Task>();
for (int i = 0; i < totalSeconds; i++)
{
    var currentDate = startDate.AddSeconds(i);
    var currentTimestamp = new DateTimeOffset(currentDate).ToUnixTimeSeconds();

    var url = $"https://kdhx.org/archive/files/{currentTimestamp}.mp3";
    var fileName = $"{currentDate:yyyy-MM-dd HH.mm.ss}.mp3";
    var task = CheckFileAsync(httpClient, url, fileName, matchingFiles);
    tasks.Add(task);
    
    // Add a delay of 100 milliseconds between requests to avoid rate limits
    await Task.Delay(100);
}

Task.WaitAll(tasks.ToArray());

Console.WriteLine("files found: " + matchingFiles.Count);
Console.WriteLine($"Files between {startDate} and {endDate}:");
foreach (var file in matchingFiles)
{
    Console.WriteLine($"{file.url} - {file.fileName}");
}

async Task CheckFileAsync(HttpClient httpClient, string url, string fileName, List<(string, string)> matchingFiles)
{
    try
    {
        var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

        if (response.IsSuccessStatusCode)
        {
            matchingFiles.Add((url, fileName));
            Console.WriteLine($"{url} - {fileName}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error checking file at {url}: {ex.Message}");
    }
}
