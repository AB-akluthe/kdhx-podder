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


/*

Figure out how to iterate through each day, and find the first file that exists.

Then scan forward 1 hour + or - 5 minutes to see if the next file exists.

Check for the next file 1 hour + or - 5 minutes after that.

Proceed until you have 24 hours of files.

*/


var matchingFiles = new List<(string url, long fileName)>();
var httpClient = new HttpClient();

// $"https://kdhx.org/archive/files/{currentTimestamp}.mp3";

// iterate through each day between startDate and endDate
for (DateTime current = startDate; current <= endDate; current = current.AddDays(1))
{

    var secondsToSearch = await GenerateFirstHourSearch(current);



    // iterate through each unix second in the list
    foreach (var currentTimestamp in secondsToSearch)
    {
        var url = $"https://kdhx.org/archive/files/{currentTimestamp}.mp3";

        if (await CheckFileAsync(httpClient, url, currentTimestamp))
        {

            matchingFiles.Add((url, currentTimestamp));
            Console.WriteLine($"Found file at {url}");
            break;
        }

    }

    // once we have a file, see if there is 1 an hour after that
    if (matchingFiles.Count > 0)
    {
        var lastFile = matchingFiles[^1];
        var nextFile = lastFile.fileName + 3600;

        var url = $"https://kdhx.org/archive/files/{nextFile}.mp3";

        if (await CheckFileAsync(httpClient, url, nextFile))
        {
            matchingFiles.Add((url, nextFile));
            Console.WriteLine($"Found file at {url}");
        }else{
            // check every second 5 minutes before and after
            for (int i = 1; i <= 300; i++)
            {
                var urlBefore = $"https://kdhx.org/archive/files/{nextFile - i}.mp3";
                var urlAfter = $"https://kdhx.org/archive/files/{nextFile + i}.mp3";

                if (await CheckFileAsync(httpClient, urlBefore, nextFile - i))
                {
                    matchingFiles.Add((urlBefore, nextFile - i));
                    Console.WriteLine($"Found file at {urlBefore}");
                    break;
                }

                if (await CheckFileAsync(httpClient, urlAfter, nextFile + i))
                {
                    matchingFiles.Add((urlAfter, nextFile + i));
                    Console.WriteLine($"Found file at {urlAfter}");
                    break;
                }
            }
        }
    }



}


    async Task<List<long>> GenerateFirstHourSearch(DateTime startDate)
    {
        // Generate a list of all unix Seconds between 00:00:00 and 1:00:00 CDT
        // Set the time zone to Central Daylight Time (CDT)
        TimeZoneInfo cdt = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

        // Set the start and end times
        DateTime start = new DateTime(2023, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
        DateTime end = new DateTime(2023, 4, 14, 1, 0, 0, DateTimeKind.Unspecified);

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

               return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking file at {url}: {ex.Message}");
            return false;
        }
    }
