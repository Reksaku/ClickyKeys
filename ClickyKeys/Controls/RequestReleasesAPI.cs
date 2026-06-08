using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClickyKeys
{
    public class RequestReleasesAPI
    {
        private readonly HttpClient _httpClient;

        public RequestReleasesAPI()
        {
            _httpClient = new HttpClient();

            // Custom User-Agent. The distribution ID comes from BuildInfo
            // (compile-time) rather than from the JSON-deserialised
            // Configuration so the user can't spoof their channel by
            // editing config.json.
            Configuration parameters = new Configuration();

            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ClickyKeysApp", parameters.Version));

            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Distro", BuildInfo.Distribution.ToString()));

            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Type", "application"));

            // Launch trigger (auto_start / user_start). Travels as a User-Agent
            // token like the other flags above rather than as a query string,
            // keeping every request flag in one place.
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Trigger", App.LaunchTrigger));


        }

        public async Task<T?> GetJsonAsync<T>(string url)
        {
            using var response = await _httpClient.GetAsync(url);

            response.EnsureSuccessStatusCode(); // exception 

            var json = await response.Content.ReadAsStringAsync();

            // De-serialization of T object
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<T>(json, options);
        }
    }
}

