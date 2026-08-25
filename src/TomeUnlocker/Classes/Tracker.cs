using Newtonsoft.Json.Linq;
using System;

namespace TomeUnlocker.Classes
{
    public static class Tracker
    {
        private static bool _hasActiveTome = false;
        private static string _activeRequestBody = null;
        private static string _activeResponseBody = null;

        public static bool HasActiveTome => _hasActiveTome;
        public static string ActiveRequestBody => _activeRequestBody;
        public static string ActiveResponseBody => _activeResponseBody;

        public static bool SetActiveTome(string requestBody, string responseBody)
        {
            try
            {
                var responseJson = JObject.Parse(responseBody);
                var questEvents = responseJson.SelectToken("activeNodesFull[0].objectives[0].questEvent") as JArray;
                var neededProgression = responseJson.SelectToken("activeNodesFull[0].objectives[0].neededProgression")?.Value<int>() ?? 0;

                if (questEvents == null || questEvents.Count == 0 || neededProgression == 0)
                    return false;

                _hasActiveTome = true;
                _activeRequestBody = requestBody;
                _activeResponseBody = responseBody;

                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[TOME] [{timestamp}] Active tome set");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearActiveTome()
        {
            _hasActiveTome = false;
            _activeRequestBody = null;
            _activeResponseBody = null;

            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.WriteLine($"[TOME] [{timestamp}] Active tome cleared");
            }
            catch { }
        }
    }
}
