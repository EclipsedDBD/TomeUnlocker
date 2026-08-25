using Fiddler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using TomeUnlocker.Api;

namespace TomeUnlocker.Classes
{
    public static class Proxy
    {
        #region Events

        public static event Action<string, string>? OnApiKeyCaptured;
        public static event Action? OnTomeActivated;
        public static event Action? OnTomeCleared;
        public static event Action<string>? OnMatchCompleted;
        public static event Action<int>? OnProxyStarted;
        public static event Action? OnProxyStopped;
        public static event Action<string>? OnLog;

        #endregion

        #region Fields

        private static string _currentApiKey = string.Empty;
        private static string _currentPlatform = string.Empty;
        private static ushort _proxyPort = 0;
        private static bool _isRunning = false;
        private static readonly HashSet<string> _processedMatchIds = new();

        public static string CurrentApiKey => _currentApiKey;
        public static string CurrentPlatform => _currentPlatform;
        public static int ProxyPort => _proxyPort;

        #endregion

        #region Certificate

        public static void CheckCertificate()
        {
            try
            {
                Certificate.EnsureCertificate();
                Log("Certificate checked/installed");
            }
            catch (Exception ex)
            {
                Log($"Certificate error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Proxy Lifecycle

        public static void StartProxy()
        {
            if (_isRunning)
            {
                Log("StartProxy skipped: already running");
                return;
            }

            Log("StartProxy: generating random port");

            try
            {
                var rng = new Random();
                _proxyPort = (ushort)rng.Next(1111, 9999);
                Log($"StartProxy: selected port {_proxyPort}");

                Log("StartProxy: building FiddlerCore settings (system proxy, decrypt SSL)");
                var settings = new FiddlerCoreStartupSettingsBuilder()
                    .ListenOnPort(_proxyPort)
                    .RegisterAsSystemProxy()
                    .ChainToUpstreamGateway()
                    .DecryptSSL()
                    .Build();

                CONFIG.IgnoreServerCertErrors = true;
                Log("StartProxy: IgnoreServerCertErrors enabled");

                Log("StartProxy: calling FiddlerApplication.Startup");
                FiddlerApplication.Startup(settings);
                Log("StartProxy: FiddlerApplication started successfully");

                Log("StartProxy: registering BeforeRequest/BeforeResponse handlers");
                FiddlerApplication.BeforeRequest += OnBeforeRequest;
                FiddlerApplication.BeforeResponse += OnBeforeResponse;

                _isRunning = true;
                Log("StartProxy: proxy is now running");

                OnProxyStarted?.Invoke(_proxyPort);
            }
            catch (Exception ex)
            {
                Log($"StartProxy failed: {ex.Message}");
                throw;
            }
        }

        public static void StopProxy()
        {
            if (!_isRunning)
            {
                Log("StopProxy skipped: not running");
                return;
            }

            Log("StopProxy: removing BeforeRequest/BeforeResponse handlers");

            try
            {
                FiddlerApplication.BeforeRequest -= OnBeforeRequest;
                FiddlerApplication.BeforeResponse -= OnBeforeResponse;
                Log("StopProxy: handlers removed");

                Log("StopProxy: calling FiddlerApplication.Shutdown");
                FiddlerApplication.Shutdown();
                _isRunning = false;
                _processedMatchIds.Clear();
                Log("StopProxy: shutdown complete");

                OnProxyStopped?.Invoke();
                Log("Proxy stopped");
            }
            catch (Exception ex)
            {
                Log($"StopProxy error: {ex.Message}");
            }
        }

        #endregion

        #region BeforeRequest

        private static void OnBeforeRequest(Session session)
        {
            try
            {
                if (!session.fullUrl.Contains("bhvrdbd"))
                    return;

                Log($"BeforeRequest: {session.fullUrl}");

                if (session.uriContains("/api/v1/config"))
                {
                    var apiKey = session.oRequest["api-key"];
                    Log($"BeforeRequest: intercepted /api/v1/config, api-key header present: {!string.IsNullOrEmpty(apiKey)}");

                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        _currentApiKey = apiKey;
                        _currentPlatform = GetPlatform(session);
                        OnApiKeyCaptured?.Invoke(apiKey, _currentPlatform);
                        Log($"API key captured: platform={_currentPlatform}, key={apiKey[..Math.Min(16, apiKey.Length)]}...");
                    }
                }

                if (session.uriContains("/api/v1/me/logout"))
                {
                    Log("BeforeRequest: blocking logout request");
                    session.utilCreateResponseAndBypassServerAsync().GetAwaiter().GetResult();
                    var json = new JObject
                    {
                        { "status", "ok" },
                        { "message", "Logout blocked" }
                    };
                    session.utilSetResponseBody(json.ToString(Formatting.None));
                    Log("Logout blocked");
                }

                if (session.uriContains("api/v1/clientVersion/check"))
                {
                    Log("BeforeRequest: bypassing version check");
                    session.utilCreateResponseAndBypassServerAsync().GetAwaiter().GetResult();
                    var json = new JObject { { "isValid", true } };
                    session.utilSetResponseBody(json.ToString());
                    Log("Version check bypassed");
                }
            }
            catch (Exception ex)
            {
                Log($"BeforeRequest error: {ex.Message}");
            }
        }

        #endregion

        #region BeforeResponse

        private static async void OnBeforeResponse(Session session)
        {
            try
            {
                if (!session.fullUrl.Contains("bhvrdbd"))
                    return;

                Log($"BeforeResponse: {session.fullUrl}");

                if (session.fullUrl.Contains("api/v1/archives/stories/update/active-node"))
                {
                    Log("BeforeResponse: intercepted active-node (tome selection)");
                    session.utilDecodeResponse();

                    var requestBody = session.GetRequestBodyAsString();
                    var responseBody = session.GetResponseBodyAsString();

                    Log($"BeforeResponse: active-node request body length={requestBody?.Length ?? 0}, response body length={responseBody?.Length ?? 0}");

                    if (Tracker.SetActiveTome(requestBody, responseBody))
                    {
                        OnTomeActivated?.Invoke();
                        Log("BeforeResponse: tome activated");
                    }
                    else
                    {
                        Log("BeforeResponse: active-node response has no quest data (claim/redeem, ignoring)");
                    }
                }

                if (session.fullUrl.Contains("api/v1/match") && !session.fullUrl.Contains("matchIncentives"))
                {
                    Log("BeforeResponse: intercepted match endpoint");
                    session.utilDecodeResponse();
                    var matchBody = session.GetResponseBodyAsString();

                    if (!string.IsNullOrEmpty(matchBody) && TryParseJson(matchBody, out var match))
                    {
                        var matchStatus = match["status"]?.ToString();
                        var matchId = match["matchId"]?.Value<string>();

                        Log($"BeforeResponse: match status={matchStatus}, matchId={matchId}");

                        if (matchStatus == "CLOSED" && !string.IsNullOrEmpty(matchId))
                        {
                            if (!_processedMatchIds.Add(matchId))
                            {
                                Log($"BeforeResponse: match {matchId} already processed, skipping duplicate");
                                return;
                            }

                            Log($"BeforeResponse: match CLOSED detected, firing OnMatchCompleted");
                            OnMatchCompleted?.Invoke(matchId);
                            var options = MainWindow.Instance?.Options;

                            if (options != null && options.TomeUnlocker && Tracker.HasActiveTome)

                            {
                                Log($"BeforeResponse: TomeUnlocker enabled, HasActiveTome={Tracker.HasActiveTome}");
                                var platform = _currentPlatform;
                                if (string.IsNullOrEmpty(platform))
                                {
                                    platform = GetPlatform(session);
                                    Log($"BeforeResponse: platform from session: {platform}");
                                }

                                Log($"BeforeResponse: starting tome completion for match {matchId}, platform={platform}");
                                await CompleteTomeAsync(matchId, platform);

                                Log("BeforeResponse: clearing active tome after completion");
                                Tracker.ClearActiveTome();
                            }
                            else
                            {
                                Log($"BeforeResponse: TomeUnlocker disabled or options null, skipping completion");
                            }
                        }
                        else
                        {
                            Log($"BeforeResponse: match not CLOSED or no matchId, ignoring");
                        }
                    }
                    else
                    {
                        Log("BeforeResponse: match body empty or not valid JSON");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"BeforeResponse error: {ex.Message}");
            }
        }

        #endregion

        #region Tome Completion

        private static async Task CompleteTomeAsync(string matchId, string platform)
        {
            Log($"CompleteTomeAsync: starting for matchId={matchId}, platform={platform}");

            if (string.IsNullOrEmpty(_currentApiKey))
            {
                Log("CompleteTomeAsync: aborting - no API key captured");
                return;
            }

            if (!Tracker.HasActiveTome || string.IsNullOrEmpty(Tracker.ActiveResponseBody))
            {
                Log("CompleteTomeAsync: aborting - no active tome data");
                return;
            }

            try
            {
                Log("CompleteTomeAsync: parsing active tome response and request bodies");

                var responseBody = Tracker.ActiveResponseBody;
                var requestBody = Tracker.ActiveRequestBody;

                var responseJson = JObject.Parse(responseBody);
                var requestJson = JObject.Parse(requestBody);

                var questEvents = responseJson.SelectToken("activeNodesFull[0].objectives[0].questEvent") as JArray;
                if (questEvents == null || questEvents.Count == 0)
                {
                    Log("CompleteTomeAsync: no quest events found in activeNodesFull[0].objectives[0].questEvent");
                    return;
                }

                Log($"CompleteTomeAsync: found {questEvents.Count} quest event(s)");

                string role = requestJson["role"]?.ToString() ?? "killer";
                if (role == "both")
                {
                    Log("CompleteTomeAsync: role is 'both', defaulting to 'killer'");
                    role = "killer";
                }
                Log($"CompleteTomeAsync: role={role}");

                var questEventObjects = new JArray();
                for (int i = 0; i < questEvents.Count; i++)
                {
                    int neededProgression = responseJson.SelectToken("activeNodesFull[0].objectives[0].neededProgression")?.Value<int>() ?? 0;
                    int repetition = responseJson.SelectToken($"activeNodesFull[0].objectives[0].questEvent[{i}].repetition")?.Value<int>() ?? 0;

                    int count = neededProgression > repetition ? neededProgression : repetition;
                    Log($"CompleteTomeAsync: event[{i}]: neededProgression={neededProgression}, original repetition={repetition}, inflated count={count}");

                    var questEventString = questEvents[i] as JObject;
                    if (questEventString != null)
                    {
                        questEventString["repetition"] = i > 0 ? repetition : count;
                        questEventObjects.Add(questEventString);
                    }
                }

                var payload = new JObject();
                payload.Add("krakenMatchId", matchId);
                payload.Add("matchId", matchId);
                payload.Add("questEvents", questEventObjects);
                payload.Add("role", role);

                var body = payload.ToString(Formatting.None);
                Log($"CompleteTomeAsync: payload built ({body.Length} chars), calling DbdApiClient.UpdateQuest");

                var (statusCode, isSuccessful, resContent) = await DbdApi.UpdateQuest(body, _currentApiKey, platform);
                Log($"CompleteTomeAsync: API response status={statusCode}, isSuccessful={isSuccessful}");

                if (isSuccessful)
                {
                    var resJson = JObject.Parse(resContent);
                    Log($"CompleteTomeAsync: response body parsed ({resContent?.Length ?? 0} chars)");

                    var afterMatch = resJson["afterMatch"] as JArray;

                    var objectiveId = responseJson["activeNodesFull"]?[0]?["objectives"]?[0]?["objectiveId"]?.ToString();
                    Log($"CompleteTomeAsync: objectiveId={objectiveId}");

                    int? progressAfter = afterMatch?.SelectMany(m => m["objectives"])
                        .Where(obj => (string)obj["objectiveId"] == objectiveId)
                        .Select(obj => (int?)obj["currentProgress"])
                        .FirstOrDefault();

                    int needed = responseJson.SelectToken("activeNodesFull[0].objectives[0].neededProgression")?.Value<int>() ?? 0;
                    bool completed = progressAfter.HasValue && progressAfter.Value >= needed;

                    Log($"CompleteTomeAsync: progressAfter={progressAfter}, needed={needed}, completed={completed}");

                    if (completed)
                    {
                        Log($"Challenge completed: {objectiveId}");
                        OnTomeCleared?.Invoke();
                    }
                    else
                    {
                        Log($"Challenge progress: {progressAfter}/{needed}");
                    }
                }
                else
                {
                    Log($"Quest API failed: HTTP {statusCode}");
                    if (!string.IsNullOrEmpty(resContent))
                    {
                        Log($"Response: {resContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"CompleteTomeAsync exception: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private static string GetPlatform(Session session)
        {
            try
            {
                var host = session.host ?? string.Empty;
                var dotIndex = host.IndexOf('.');
                if (dotIndex > 0)
                {
                    var platform = host.Substring(0, dotIndex);
                    Log($"GetPlatform: extracted '{platform}' from host '{host}'");
                    return platform;
                }
                Log($"GetPlatform: no dot found in host '{host}', defaulting to 'steam'");
            }
            catch (Exception ex)
            {
                Log($"GetPlatform error: {ex.Message}");
            }
            return "steam";
        }

        private static bool TryParseJson(string json, out JObject? result)
        {
            result = null;
            try
            {
                result = JObject.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void Log(string message)
        {
            OnLog?.Invoke(message);
        }

        #endregion
    }
}
