using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClickyKeys
{
    /// <summary>
    /// A single entry of the sponsorship feed returned by
    /// <c>clickykeys.fun/api/sponsorship.php</c>, e.g.
    /// <code>
    /// [{"id":1,"publication_date":"2026-06-10T00:00:00",
    ///   "service":"Buy Me a Coffee","link":"https://buymeacoffee.com/reksaku"}]
    /// </code>
    /// </summary>
    public class SponsorshipEntry
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("publication_date")]
        public string PublicationDate { get; set; } = string.Empty;

        [JsonPropertyName("service")]
        public string Service { get; set; } = string.Empty;

        [JsonPropertyName("link")]
        public string Link { get; set; } = string.Empty;
    }

    /// <summary>
    /// Retrieves the current sponsoring link from the API. Kept out of
    /// <c>MainWindow</c> so the window only deals with UI (opening the browser /
    /// showing the no-connection popup) and the fetch+parse logic lives here.
    /// </summary>
    public static class SponsorshipService
    {
        private const string Endpoint = "https://clickykeys.fun/api/sponsorship.php";

        /// <summary>
        /// Fetches the sponsorship feed and returns the first valid http(s)
        /// link from it. Returns <c>null</c> on any failure — no internet
        /// connection, an empty/invalid response, or no usable link — so the
        /// caller can surface a "no connection" message.
        /// </summary>
        public static async Task<string?> GetLinkAsync()
        {
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

                // Same User-Agent flags as the other API clients so the server
                // sees a consistent identity across endpoints.
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("ClickyKeysApp", BuildInfo.Version));
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("Distro", BuildInfo.Distribution.ToString()));
                http.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("Type", "application"));

                var json = await http.GetStringAsync(Endpoint);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entries = JsonSerializer.Deserialize<List<SponsorshipEntry>>(json, options);
                if (entries == null)
                    return null;

                // First entry whose link is a well-formed http(s) URL.
                foreach (var entry in entries)
                {
                    var link = entry.Link?.Trim();
                    if (!string.IsNullOrWhiteSpace(link)
                        && Uri.TryCreate(link, UriKind.Absolute, out var uri)
                        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        return link;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SponsorshipService.GetLinkAsync failed: {ex}");
                return null;
            }
        }
    }
}
