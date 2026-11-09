using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Endowment.Network
{
    /// <summary>
    /// Central HTTP client for communicating with the Tel-U Endowment Aquarium Game API (:3000)
    /// and Mock Endowment API (:4000).
    /// 
    /// Features Fast Offline Detection:
    /// If the backend server is not active (e.g., running on a developer/teammate PC without Node.js),
    /// it logs a single clean info message and switches to silent Offline Fallback mode without
    /// spamming console errors or blocking gameplay.
    /// </summary>
    public class BackendApiClient : MonoBehaviour
    {
        private const string PREF_BEARER_TOKEN = "endowment_bearer_token";
        private const string PREF_ACTIVE_FISH_ID = "endowment_active_fish_id";

        private static BackendApiClient instance;

        public static BackendApiClient Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<BackendApiClient>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("[BackendApiClient]");
                        instance = go.AddComponent<BackendApiClient>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }

        [Header("Server Endpoints")]
        [Tooltip("Base URL of the Fastify Game API (default: http://localhost:3000)")]
        [SerializeField] private string gameApiBaseUrl = "http://localhost:3000";

        [Tooltip("Base URL of the Mock Endowment API for testing scenarios (default: http://localhost:4000)")]
        [SerializeField] private string mockApiBaseUrl = "http://localhost:4000";

        [Header("Authentication & Testing")]
        [Tooltip("Optional developer Bearer token for Personal Aquarium routes (/api/v1/me/*).")]
        [SerializeField] private string devBearerToken = "klSIM1rLCNw0Ard-8o3r6_R38yNazVsffAk7ZIc3Ces";

        [Tooltip("External ID of the developer donor for testing mock donations.")]
        [SerializeField] private string devDonorExternalId = "donor-pandu-dev";

        [Tooltip("Display name for the developer donor.")]
        [SerializeField] private string devDonorDisplayName = "Pandu (Tel-U)";

        [Tooltip("Request timeout in seconds before falling back to offline mode.")]
        [SerializeField] private int requestTimeoutSeconds = 3;

        private string runtimeBearerToken;
        private string cachedActiveFishId;
        private bool isServerReachable = true;
        private bool hasLoggedOfflineNotice = false;
        private float lastOfflineTimestamp = -999f;
        private const float OFFLINE_RETRY_COOLDOWN = 15f;

        public string GameApiBaseUrl => gameApiBaseUrl.TrimEnd('/');
        public string MockApiBaseUrl => mockApiBaseUrl.TrimEnd('/');
        public bool IsServerReachable => isServerReachable;

        public string DonorExternalId => !string.IsNullOrEmpty(devDonorExternalId) ? devDonorExternalId : "donor-pandu-dev";
        public string DonorDisplayName => !string.IsNullOrEmpty(devDonorDisplayName) ? devDonorDisplayName : OfflineDummyData.DEFAULT_PLAYER_NAME;

        public bool HasBearerToken => !string.IsNullOrEmpty(GetBearerToken());

        public string ActiveFishId
        {
            get
            {
                if (!string.IsNullOrEmpty(cachedActiveFishId)) return cachedActiveFishId;
                cachedActiveFishId = PlayerPrefs.GetString(PREF_ACTIVE_FISH_ID, "");
                return cachedActiveFishId;
            }
            set
            {
                cachedActiveFishId = value ?? "";
                PlayerPrefs.SetString(PREF_ACTIVE_FISH_ID, cachedActiveFishId);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAuthFromEnvironment();
        }

        private void InitializeAuthFromEnvironment()
        {
            string urlToken = ExtractQueryParameter(Application.absoluteURL, "token");
            if (!string.IsNullOrEmpty(urlToken))
            {
                SetBearerToken(urlToken);
                return;
            }

            if (!string.IsNullOrEmpty(devBearerToken))
            {
                runtimeBearerToken = devBearerToken.Trim();
                PlayerPrefs.SetString(PREF_BEARER_TOKEN, runtimeBearerToken);
                PlayerPrefs.Save();
                return;
            }

            if (PlayerPrefs.HasKey(PREF_BEARER_TOKEN))
            {
                runtimeBearerToken = PlayerPrefs.GetString(PREF_BEARER_TOKEN, "");
            }
        }

        public string GetBearerToken()
        {
            if (!string.IsNullOrEmpty(devBearerToken))
                return devBearerToken.Trim();

            if (!string.IsNullOrEmpty(runtimeBearerToken))
                return runtimeBearerToken.Trim();

            if (PlayerPrefs.HasKey(PREF_BEARER_TOKEN))
            {
                runtimeBearerToken = PlayerPrefs.GetString(PREF_BEARER_TOKEN, "");
                return runtimeBearerToken;
            }

            return "";
        }

        public void SetBearerToken(string token)
        {
            runtimeBearerToken = (token ?? "").Trim();
            devBearerToken = runtimeBearerToken;
            PlayerPrefs.SetString(PREF_BEARER_TOKEN, runtimeBearerToken);
            PlayerPrefs.Save();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API Methods
        // ─────────────────────────────────────────────────────────────────────

        public void CheckHealth(Action<bool> onComplete)
        {
            string url = $"{GameApiBaseUrl}/api/v1/health";
            StartCoroutine(SendJsonRequest(url, "GET", null, false, (status, body, netErr) =>
            {
                bool ok = !netErr && status == 200;
                isServerReachable = ok;
                onComplete?.Invoke(ok);
            }));
        }

        public void ExchangeToken(string rawToken, Action<AuthExchangeDataDTO, ApiErrorDTO> onComplete)
        {
            string url = $"{GameApiBaseUrl}/api/v1/auth/exchange";
            string jsonBody = JsonUtility.ToJson(new AuthExchangeRequestDTO { token = rawToken });

            StartCoroutine(SendJsonRequest(url, "POST", jsonBody, false, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<AuthExchangeEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        SetBearerToken(env.data.accessToken);
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void GetBigAquarium(int limit, Action<BigAquariumDataDTO, ApiErrorDTO> onComplete)
        {
            int clampedLimit = Mathf.Clamp(limit, 1, 50);
            string url = $"{GameApiBaseUrl}/api/v1/big-aquarium?limit={clampedLimit}";

            StartCoroutine(SendJsonRequest(url, "GET", null, false, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<BigAquariumEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void GetPersonalAquarium(Action<PersonalAquariumDataDTO, ApiErrorDTO> onComplete)
        {
            string url = $"{GameApiBaseUrl}/api/v1/me/aquarium";

            StartCoroutine(SendJsonRequest(url, "GET", null, true, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<PersonalAquariumEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        if (env.data.fish != null && env.data.fish.Count > 0)
                        {
                            PersonalFishDTO active = env.data.fish.Find(f => f.state != null && f.state.isActive)
                                                     ?? env.data.fish[0];
                            if (active != null && !string.IsNullOrEmpty(active.id))
                            {
                                ActiveFishId = active.id;
                            }
                        }
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void GetPublicAquarium(string publicSlug, Action<PersonalAquariumDataDTO, ApiErrorDTO> onComplete)
        {
            string url = $"{GameApiBaseUrl}/api/v1/aquariums/{UnityWebRequest.EscapeURL(publicSlug)}";

            StartCoroutine(SendJsonRequest(url, "GET", null, false, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<PersonalAquariumEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void FeedFish(string fishId, Action<FeedFishDataDTO, ApiErrorDTO, long> onComplete)
        {
            string targetId = !string.IsNullOrEmpty(fishId) ? fishId : ActiveFishId;
            if (string.IsNullOrEmpty(targetId) || !isServerReachable)
            {
                onComplete?.Invoke(null, CreateNetworkError(), 0);
                return;
            }

            string url = $"{GameApiBaseUrl}/api/v1/me/fish/{targetId}/feed";

            StartCoroutine(SendJsonRequest(url, "POST", "{}", true, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError(), 0);
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<FeedFishEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data, null, status);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status), status);
            }));
        }

        public void UpdateFishAppearance(
            string fishId,
            string clientColorVariant,
            string clientAccessoryId,
            Action<PersonalFishDTO, ApiErrorDTO> onComplete = null)
        {
            string targetId = !string.IsNullOrEmpty(fishId) ? fishId : ActiveFishId;
            if (string.IsNullOrEmpty(targetId) || !HasBearerToken || !isServerReachable)
            {
                onComplete?.Invoke(null, CreateNetworkError());
                return;
            }

            string url = $"{GameApiBaseUrl}/api/v1/me/fish/{targetId}/appearance";
            var payload = new AppearanceRequestDTO
            {
                colorCode = BackendContractMapper.ToBackendColorCode(clientColorVariant),
                accessoryCode = BackendContractMapper.ToBackendAccessoryCode(clientAccessoryId)
            };
            string jsonBody = JsonUtility.ToJson(payload);

            StartCoroutine(SendJsonRequest(url, "PATCH", jsonBody, true, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<AppearanceEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data.fish, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void SimulateTierDonation(string tier, Action<bool, string> onComplete = null)
        {
            if (!isServerReachable && (Time.unscaledTime - lastOfflineTimestamp) < OFFLINE_RETRY_COOLDOWN)
            {
                onComplete?.Invoke(false, "Offline Mode");
                return;
            }

            int amount = BackendContractMapper.GetAmountForTier(tier);
            string donorId = DonorExternalId;
            string donorName = DonorDisplayName;

            var req = new MockDonationRequestDTO
            {
                donorExternalId = donorId,
                donorName = donorName,
                donorEmail = $"{donorId.ToLowerInvariant()}@telkomuniversity.ac.id",
                showName = true,
                amount = amount,
                currency = "IDR"
            };

            CreateMockDonation(req, (mockData, err) =>
            {
                if (mockData != null)
                {
                    onComplete?.Invoke(true, tier);
                }
                else
                {
                    onComplete?.Invoke(false, err != null ? err.message : "Offline Mode");
                }
            });
        }

        public void TriggerMockScenario(string scenarioOrTier, Action<MockDonationDataDTO, ApiErrorDTO> onComplete = null)
        {
            string slug = BackendContractMapper.ToScenarioSlug(scenarioOrTier);
            string url = $"{MockApiBaseUrl}/mock/v1/scenarios/{slug}";

            StartCoroutine(SendJsonRequest(url, "POST", "{}", false, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<MockDonationEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        public void CreateMockDonation(MockDonationRequestDTO input, Action<MockDonationDataDTO, ApiErrorDTO> onComplete = null)
        {
            string url = $"{MockApiBaseUrl}/mock/v1/donations";
            string jsonBody = JsonUtility.ToJson(input);

            StartCoroutine(SendJsonRequest(url, "POST", jsonBody, false, (status, body, netErr) =>
            {
                if (netErr)
                {
                    onComplete?.Invoke(null, CreateNetworkError());
                    return;
                }

                if (status >= 200 && status < 300)
                {
                    var env = SafeFromJson<MockDonationEnvelopeDTO>(body);
                    if (env != null && env.data != null)
                    {
                        onComplete?.Invoke(env.data, null);
                        return;
                    }
                }

                onComplete?.Invoke(null, ParseApiError(body, status));
            }));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Low-Level UnityWebRequest Dispatcher with Fast Offline Short-Circuit
        // ─────────────────────────────────────────────────────────────────────

        private IEnumerator SendJsonRequest(
            string url,
            string method,
            string jsonBody,
            bool requireAuth,
            Action<long, string, bool> callback)
        {
            // Fast short-circuit if server was recently detected offline so we don't stall
            if (!isServerReachable && (Time.unscaledTime - lastOfflineTimestamp) < OFFLINE_RETRY_COOLDOWN)
            {
                callback?.Invoke(0, "", true);
                yield break;
            }

            using (UnityWebRequest req = new UnityWebRequest(url, method))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = Mathf.Max(2, requestTimeoutSeconds);

                req.SetRequestHeader("X-Request-ID", Guid.NewGuid().ToString());
                req.SetRequestHeader("Accept", "application/json");

                if (!string.IsNullOrEmpty(jsonBody))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    req.SetRequestHeader("Content-Type", "application/json");
                }

                if (requireAuth)
                {
                    string token = GetBearerToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        req.SetRequestHeader("Authorization", $"Bearer {token}");
                    }
                }

                yield return req.SendWebRequest();

                bool isNetworkFailure = req.result == UnityWebRequest.Result.ConnectionError ||
                                        req.result == UnityWebRequest.Result.DataProcessingError;

                if (isNetworkFailure)
                {
                    isServerReachable = false;
                    lastOfflineTimestamp = Time.unscaledTime;
                    if (!hasLoggedOfflineNotice)
                    {
                        hasLoggedOfflineNotice = true;
                        Debug.Log("[BackendApiClient] Server backend tidak terdeteksi — berjalan otomatis dalam Mode Offline (Dummy Data Aktif).");
                    }
                }
                else
                {
                    isServerReachable = true;
                    hasLoggedOfflineNotice = false;
                }

                string responseText = req.downloadHandler != null ? req.downloadHandler.text : "";
                callback?.Invoke(req.responseCode, responseText, isNetworkFailure);
            }
        }

        private static T SafeFromJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static ApiErrorDTO ParseApiError(string body, long statusCode)
        {
            if (!string.IsNullOrEmpty(body))
            {
                var errEnv = SafeFromJson<ErrorEnvelopeDTO>(body);
                if (errEnv != null && errEnv.error != null && !string.IsNullOrEmpty(errEnv.error.code))
                {
                    return errEnv.error;
                }
            }

            return new ApiErrorDTO
            {
                code = $"HTTP_{statusCode}",
                message = $"Server mengembalikan status {statusCode}."
            };
        }

        private static ApiErrorDTO CreateNetworkError()
        {
            return new ApiErrorDTO
            {
                code = "NETWORK_UNREACHABLE",
                message = "Mode Offline Fallback Aktif."
            };
        }

        private static string ExtractQueryParameter(string url, string paramName)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(paramName)) return "";
            int qIndex = url.IndexOf('?');
            if (qIndex < 0 || qIndex >= url.Length - 1) return "";

            string query = url.Substring(qIndex + 1);
            int hashIndex = query.IndexOf('#');
            if (hashIndex >= 0) query = query.Substring(0, hashIndex);

            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                string key = pair.Substring(0, eq);
                if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return UnityWebRequest.UnEscapeURL(pair.Substring(eq + 1));
                }
            }

            return "";
        }
    }
}
