using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Endowment.Network;

/// <summary>
/// Handles the Feed Button UI event, validates server-side 4-hour cooldown via POST /api/v1/me/fish/:id/feed,
/// displays remaining cooldown status, and bridges food spawning with the fish movement system.
/// Gracefully falls back to local feeding when running in offline mode.
/// </summary>
public class FeedController : MonoBehaviour
{
    private const double SERVER_COOLDOWN_HOURS = 4.0;

    [Header("References")]
    [Tooltip("Reference to the FishMovement component in the aquarium.")]
    [SerializeField] private FishMovement fishMovement;

    [Tooltip("The UI prefab for the food object (must have FoodController attached).")]
    [SerializeField] private GameObject foodPrefab;

    [Tooltip("The aquarium RectTransform where food will spawn and fall.")]
    [SerializeField] private RectTransform aquariumArea;

    [Tooltip("The Feed UI Button.")]
    [SerializeField] private Button feedButton;

    [Tooltip("Optional TextMeshProUGUI to display cooldown countdown or status messages.")]
    [SerializeField] private TextMeshProUGUI cooldownStatusText;

    [Header("Backend Settings")]
    [Tooltip("If true, enforces the backend 4-hour cooldown when connected with a valid Bearer token.")]
    [SerializeField] private bool enforceServerCooldown = true;

    private FoodController currentActiveFood;
    private TextMeshProUGUI buttonLabelTmp;
    private string defaultButtonLabel = "Feed";
    private DateTime? nextAllowedFeedUtc;
    private bool isRequestInFlight = false;

    private void Start()
    {
        if (fishMovement == null)
        {
            fishMovement = FindFirstObjectByType<FishMovement>();
        }

        if (aquariumArea == null && fishMovement != null && fishMovement.transform.parent != null)
        {
            aquariumArea = fishMovement.transform.parent as RectTransform;
        }

        if (feedButton != null)
        {
            feedButton.onClick.AddListener(OnFeedButtonClicked);
            buttonLabelTmp = feedButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonLabelTmp != null && !string.IsNullOrEmpty(buttonLabelTmp.text))
            {
                defaultButtonLabel = buttonLabelTmp.text;
            }
        }

        SyncInitialCooldownFromServer();
    }

    private void OnDestroy()
    {
        if (feedButton != null)
        {
            feedButton.onClick.RemoveListener(OnFeedButtonClicked);
        }
    }

    private void Update()
    {
        if (!nextAllowedFeedUtc.HasValue) return;

        TimeSpan remaining = nextAllowedFeedUtc.Value - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0)
        {
            ClearCooldownState();
        }
        else
        {
            UpdateCooldownUI(remaining);
        }
    }

    /// <summary>
    /// Checks the personal aquarium state on scene load so if the fish is already in cooldown,
    /// the UI reflects the remaining time immediately.
    /// </summary>
    private void SyncInitialCooldownFromServer()
    {
        if (!enforceServerCooldown || !BackendApiClient.Instance.HasBearerToken) return;

        BackendApiClient.Instance.GetPersonalAquarium((aquarium, err) =>
        {
            if (aquarium == null || aquarium.fish == null || aquarium.fish.Count == 0) return;

            PersonalFishDTO activeFish = aquarium.fish.Find(f => f.state != null && f.state.isActive)
                                         ?? aquarium.fish[0];

            if (activeFish?.state != null && !string.IsNullOrEmpty(activeFish.state.lastFedAt))
            {
                if (TryParseIsoUtc(activeFish.state.lastFedAt, out DateTime lastFedUtc))
                {
                    DateTime calculatedNext = lastFedUtc.AddHours(SERVER_COOLDOWN_HOURS);
                    if (calculatedNext > DateTime.UtcNow)
                    {
                        ApplyCooldownUntil(calculatedNext);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Triggered when the Feed button is clicked.
    /// Validates with the backend API (if online and authenticated) before executing SpawnAndFeed().
    /// </summary>
    public void OnFeedButtonClicked()
    {
        if (isRequestInFlight) return;

        if (currentActiveFood != null && currentActiveFood.IsActive)
        {
            Debug.Log("[FeedController] Food is already active in the aquarium. Wait until the fish eats it.");
            return;
        }

        if (enforceServerCooldown && nextAllowedFeedUtc.HasValue)
        {
            TimeSpan rem = nextAllowedFeedUtc.Value - DateTime.UtcNow;
            if (rem.TotalSeconds > 0)
            {
                Debug.Log($"[FeedController] Feeding masih dalam cooldown ({FormatRemainingTime(rem)} tersisa).");
                UpdateCooldownUI(rem);
                return;
            }
        }

        // If authenticated with backend, validate via POST /api/v1/me/fish/:fishId/feed
        if (enforceServerCooldown &&
            BackendApiClient.Instance.HasBearerToken &&
            !string.IsNullOrEmpty(BackendApiClient.Instance.ActiveFishId))
        {
            isRequestInFlight = true;
            BackendApiClient.Instance.FeedFish(BackendApiClient.Instance.ActiveFishId, (feedData, error, statusCode) =>
            {
                isRequestInFlight = false;

                // 1. Success (HTTP 200)
                if (feedData != null)
                {
                    SpawnAndFeed();

                    if (!string.IsNullOrEmpty(feedData.nextFeedAt) &&
                        TryParseIsoUtc(feedData.nextFeedAt, out DateTime nextUtc))
                    {
                        ApplyCooldownUntil(nextUtc);
                    }
                    else
                    {
                        ApplyCooldownUntil(DateTime.UtcNow.AddHours(SERVER_COOLDOWN_HOURS));
                    }

                    Debug.Log($"[FeedController] Backend feed berhasil! Hunger={feedData.fish?.state?.hunger}, Mood={feedData.fish?.state?.mood}.");
                    return;
                }

                // 2. Cooldown Active (HTTP 409 FEEDING_COOLDOWN_ACTIVE)
                if (statusCode == 409 && error != null && error.code == "FEEDING_COOLDOWN_ACTIVE")
                {
                    if (error.details != null &&
                        !string.IsNullOrEmpty(error.details.nextFeedAt) &&
                        TryParseIsoUtc(error.details.nextFeedAt, out DateTime cooldownUtc))
                    {
                        ApplyCooldownUntil(cooldownUtc);
                    }
                    else
                    {
                        ApplyCooldownUntil(DateTime.UtcNow.AddHours(SERVER_COOLDOWN_HOURS));
                    }

                    Debug.LogWarning($"[FeedController] Server menolak feed: {error.message}");
                    return;
                }

                // 3. Offline / Network Unreachable fallback -> allow local feeding for smooth offline demo
                if (statusCode == 0 || (error != null && error.code == "NETWORK_UNREACHABLE"))
                {
                    Debug.LogWarning("[FeedController] Server offline, menjalankan feed dalam mode lokal.");
                    SpawnAndFeed();
                    return;
                }

                // 4. Other errors (e.g., 401/404) -> fallback to local feed with log
                Debug.LogWarning($"[FeedController] Response {statusCode} ({error?.code}): menggunakan fallback lokal.");
                SpawnAndFeed();
            });

            return;
        }

        // Offline / Local mode
        SpawnAndFeed();
    }

    /// <summary>
    /// Spawns the food item and instructs the fish to target it.
    /// </summary>
    private void SpawnAndFeed()
    {
        if (foodPrefab == null || aquariumArea == null || fishMovement == null)
        {
            Debug.LogWarning("[FeedController] Missing reference(s). Check Inspector for FoodPrefab, AquariumArea, or FishMovement.");
            return;
        }

        GameObject foodObj = Instantiate(foodPrefab, aquariumArea);
        RectTransform foodRect = foodObj.GetComponent<RectTransform>();
        FoodController foodCtrl = foodObj.GetComponent<FoodController>();

        if (foodRect == null || foodCtrl == null)
        {
            Debug.LogError("[FeedController] The assigned FoodPrefab must have both a RectTransform and a FoodController component!");
            Destroy(foodObj);
            return;
        }

        foodRect.localScale = Vector3.one;
        foodCtrl.Spawn(aquariumArea);

        currentActiveFood = foodCtrl;

        // Command the fish to swim toward the food and eat it
        fishMovement.SetFoodTarget(foodCtrl);

        Debug.Log("[FeedController] Food spawned and fish notified.");
    }

    private void ApplyCooldownUntil(DateTime utcTarget)
    {
        nextAllowedFeedUtc = utcTarget;
        TimeSpan remaining = utcTarget - DateTime.UtcNow;
        if (remaining.TotalSeconds > 0)
        {
            UpdateCooldownUI(remaining);
        }
    }

    private void ClearCooldownState()
    {
        nextAllowedFeedUtc = null;
        if (feedButton != null)
        {
            feedButton.interactable = true;
        }
        if (buttonLabelTmp != null)
        {
            buttonLabelTmp.text = defaultButtonLabel;
        }
        if (cooldownStatusText != null)
        {
            cooldownStatusText.text = "";
        }
    }

    private void UpdateCooldownUI(TimeSpan remaining)
    {
        string formatted = FormatRemainingTime(remaining);

        if (buttonLabelTmp != null)
        {
            buttonLabelTmp.text = $"Cooldown ({formatted})";
        }

        if (cooldownStatusText != null)
        {
            cooldownStatusText.text = $"Ikan masih kenyang! Bisa diberi makan lagi dalam {formatted}.";
        }
    }

    private static string FormatRemainingTime(TimeSpan span)
    {
        if (span.TotalHours >= 1.0)
        {
            return $"{(int)span.TotalHours}j {span.Minutes:D2}m";
        }
        if (span.TotalMinutes >= 1.0)
        {
            return $"{span.Minutes}m {span.Seconds:D2}d";
        }
        return $"{Mathf.Max(1, span.Seconds)}d";
    }

    private static bool TryParseIsoUtc(string isoString, out DateTime utcResult)
    {
        return DateTime.TryParse(
            isoString,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out utcResult);
    }
}
