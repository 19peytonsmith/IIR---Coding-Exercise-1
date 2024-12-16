using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
    // Use dependency injection in .NET Azure Functions to log using ILogger
    // https://learn.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection
    public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", Route = "events/{id}")] HttpRequest req, ILogger log, string id)
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
                    // Had an issue at first where it wasn't being deserialized properly (due to case-sensitivity comparisons) found this option to add
                    // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/character-casing
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    events = JsonSerializer.Deserialize<List<Event>>(json, options);

                    // If the data is not empty, break the retry loop
                    if (events != null && events.Count > 0)
                    {
                        break;
                    }
                }
                else
                {
                    // Get request to the IIR api was unsuccessful, log accordingly
                    log.LogInformation($"GET request to the IIR API failed with status code {response.StatusCode}. Retrying...");
                }
            }
            catch(Exception ex)
            {
                log.LogInformation($"An exception occurred while making the GET request: {ex.Message}");
            }

            attempt++;
            if (attempt >= 5)
            {
                events = null; // After max retries, return null --  Which then returns a 500 afterwards 
            }
        }

        // If the data is empty or all retries fail, return a 500 error
        if (events == null || events.Count == 0)
        {
            log.LogInformation("All retries failed or data returned is empty, 500 error returned");
            return new StatusCodeResult(500);
        }

        // Find the event by ID 
        var matchedEvent = events.FirstOrDefault(e => e.Id.ToString() == id);
        if (matchedEvent == null)
        {
            log.LogInformation($"There was no event that matches the input id: {id}");
            // https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.notfoundobjectresult?view=aspnetcore-9.0
            // To be shown to the end-user
            return new NotFoundObjectResult($"There was no event that matches the input id: {id}");
        }

        // Take the matchedEvent that is found and return the specified data as per the requirements
        var result = new
        {
            name = matchedEvent.Name,
            days = (matchedEvent.DateEnd - matchedEvent.DateStart).Days,
            websiteUrl = matchedEvent.Url
        };

        // Return specified json data if all is successful
        return new OkObjectResult(result);
    }

    // Class used to deserialize API json data
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
