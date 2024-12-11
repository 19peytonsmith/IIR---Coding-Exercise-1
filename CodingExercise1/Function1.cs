using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public static class GetEventById
{
    private static HttpClient httpClient = new HttpClient();

    [FunctionName("GetEventById")]
    public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/{id}")] HttpRequest req, string id)
    {
        // Edge case where the provided id is null or empty
        if (string.IsNullOrEmpty(id))
        {
            return new BadRequestObjectResult("Please provide a valid ID");
        }

        List<Event> events = null;
        int attempt = 0;

        // Loop 5 times and make the API call 
        while (attempt < 5)
        {
            try
            {
                var response = await httpClient.GetAsync("https://iir-interview-homework-ddbrefhkdkcgdpbs.eastus2-01.azurewebsites.net/api/v1.0/event-data");

                // If response is successful, deserialize data
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    events = JsonSerializer.Deserialize<List<Event>>(json, options);

                    // If the data is not empty, break the retry loop
                    if (events != null && events.Count > 0)
                    {
                        break;
                    }
                }
            }
            catch
            {
            }

            attempt++;
            if (attempt >= 5)
            {
                events = null; // After max retries, return null
            }
        }

        // If the data is empty or all retries fail, return a 500 error
        if (events == null || events.Count == 0)
        {
            return new StatusCodeResult(500); // Return 500 if data is empty or all retries fail
        }

        // Find the event by ID 
        var matchedEvent = events.FirstOrDefault(e => e.Id.ToString() == id);
        if (matchedEvent == null)
        {
            return new NotFoundObjectResult("Event not found.");
        }

        // Take the matchedEvent that it found and return the specified data as per the requirements
        var result = new
        {
            name = matchedEvent.Name,
            days = (matchedEvent.DateEnd - matchedEvent.DateStart).Days,
            websiteUrl = matchedEvent.Url
        };

        return new OkObjectResult(result);
    }

    private class Event
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Program { get; set; }
        public DateTime DateStart { get; set; }
        public DateTime DateEnd { get; set; }
        public string Url { get; set; }
        public string Owner { get; set; }
    }
}
