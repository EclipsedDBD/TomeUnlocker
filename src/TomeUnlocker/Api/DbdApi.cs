using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace TomeUnlocker.Api
{
    public static class DbdApi
    {
        private static readonly Dictionary<string, string> UserAgents = new()
        {
            { "egs",  "DeadByDaylight/DBD_Quiche_REL_EGS_Shipping_8_3172922 (http-legacy) EGS/10.0.19045.1.256.64bit" },
            { "steam","DeadByDaylight/DBD_Quiche_REL_Steam_Shipping_8_3172922 (http-legacy) Windows/10.0.19045.1.256.64bit" },
            { "grdk", "DeadByDaylight/DBD_Quiche_REL_WinGDK_Shipping_8_3172922 (http-legacy) WinGDK/10.0.19045.1.256.64bit" }
        };

        private const string DefaultUa = "DeadByDaylight/DBD_Quiche_REL_Steam_Shipping_8_3172922 (http-legacy) Windows/10.0.19045.1.256.64bit";
        private const string ClientVersion = "10.0.1";

        private static readonly Dictionary<string, string> BaseUrls = new()
        {
            { "egs",   "https://egs.live.bhvrdbd.com/" },
            { "steam", "https://steam.live.bhvrdbd.com/" },
            { "grdk",  "https://grdk.live.bhvrdbd.com/" }
        };

        private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null
        });

        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey, string platform)
        {
            var request = new HttpRequestMessage(method, url);

            request.Headers.TryAddWithoutValidation("Connection", "Keep-Alive");
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            if (UserAgents.TryGetValue(platform, out var agent))
                request.Headers.TryAddWithoutValidation("User-Agent", agent);
            else
                request.Headers.TryAddWithoutValidation("User-Agent", DefaultUa);

            request.Headers.TryAddWithoutValidation("x-kraken-analytics-session-id", Guid.NewGuid().ToString());
            request.Headers.TryAddWithoutValidation("x-kraken-client-platform", platform);
            request.Headers.TryAddWithoutValidation("x-kraken-client-provider", platform);
            request.Headers.TryAddWithoutValidation("x-kraken-client-resolution", "1920x1080");
            request.Headers.TryAddWithoutValidation("x-kraken-client-timezone-offset", "-120");
            request.Headers.TryAddWithoutValidation("x-kraken-client-os", "10.0.19045.1.256.64bit");
            request.Headers.TryAddWithoutValidation("x-kraken-client-version", ClientVersion);
            request.Headers.TryAddWithoutValidation("api-key", apiKey);

            return request;
        }

        public static async Task<(int StatusCode, bool IsSuccessful, string Content)> UpdateQuest(string json, string apiKey, string platform)
        {
            if (!BaseUrls.TryGetValue(platform, out var baseUrl))
                baseUrl = BaseUrls["steam"];

            var request = CreateRequest(HttpMethod.Post, baseUrl + "api/v1/archives/stories/update/quest-progress-v3", apiKey, platform);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return ((int)response.StatusCode, response.IsSuccessStatusCode, content);
            }
            catch (Exception ex)
            {
                return (0, false, ex.Message);
            }
        }

        public static async Task<bool> ValidateSession(string apiKey, string platform)
        {
            if (!BaseUrls.TryGetValue(platform, out var baseUrl))
                baseUrl = BaseUrls["steam"];

            var request = CreateRequest(HttpMethod.Get, baseUrl + "api/v1/config", apiKey, platform);

            try
            {
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
