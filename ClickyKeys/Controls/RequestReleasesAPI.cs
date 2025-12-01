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

            // Custom User-Agent
            Configuration parameters = new Configuration();

            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("ClickyKeysApp", parameters.Version));

            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Distro", parameters.Distribution.ToString()));

            _httpClient.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Type", "application"));


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

