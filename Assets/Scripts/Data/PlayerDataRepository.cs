using System;
using System.Collections.Generic;
using UnityEngine;
using Endowment.Network;

/// <summary>
/// Result of a purchase attempt.
/// </summary>
public enum PurchaseResult
{
    Success,
    InsufficientPoints,
    AlreadyOwned,
    NotFound
}

/// <summary>
/// Single source of truth for all player data: points, fish customization, and accessories.
/// Replaces the former FishDataRepository with added points management and PlayerPrefs persistence.
///
/// Architecture:
///   - Accessory CATALOG (names, prices, categories) is always defined in code.
///   - Player STATE (points, owned accessories, selected color/accessory) is loaded/saved via PlayerPrefs.
///   - All UI scripts (Store, Collection, Aquarium) read/write through this class.
///
/// Current (LoFi):  PlayerPrefs persistence (WebGL → IndexedDB, survives browser close).
/// Future (Backend): Replace method internals with REST API calls.
///                   Only this class changes; all UI/gameplay scripts remain untouched.
/// </summary>
public static class PlayerDataRepository
{
    private const string SAVE_KEY = "player_save_data";
    private const int DEFAULT_STARTING_POINTS = 100000;

    private static bool initialized = false;

    // ───────────────────────────── Runtime State ─────────────────────────────

    private static int points;

    private static FishData playerFish;

    private static List<AccessoryData> accessoryCatalog;

    // ───────────────────────────── Initialization ─────────────────────────────

    /// <summary>
    /// Ensures data is loaded from PlayerPrefs on first access.
    /// Safe to call multiple times — only loads once.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (initialized) return;

        // 1. Build the full accessory catalog (always from code)
        BuildAccessoryCatalog();

        // 2. Initialize fish data with defaults
        playerFish = new FishData
        {
            fishId = "player_fish_01",
            species = "Clownfish",
            tier = "Common",
            colorVariant = "red",
            selectedAccessoryId = null
        };

        // 3. Load saved player state from PlayerPrefs (if exists)
        LoadSavedState();

        initialized = true;
    }

    private static void BuildAccessoryCatalog()
    {
        accessoryCatalog = new List<AccessoryData>
        {
            // ── Hat (4) ──
            new AccessoryData { accessoryId = "hat_bigtop",  displayName = "Big Top Hat",    price = 10000, category = "topi", isOwned = false },
            new AccessoryData { accessoryId = "hat_cowboy",  displayName = "Cowboy Honour",   price = 8000,  category = "topi", isOwned = false },
            new AccessoryData { accessoryId = "hat_sailor",  displayName = "Sailor Hat",      price = 7000,  category = "topi", isOwned = false },
            new AccessoryData { accessoryId = "hat_unicorn", displayName = "Unicorn Hat",     price = 15000, category = "topi", isOwned = false },

            // ── Glasses (3) ──
            new AccessoryData { accessoryId = "glasses_fancy",    displayName = "Fancy Glasses",        price = 6000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_redsand",  displayName = "Evil Glasses", price = 9000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_starfish", displayName = "Starfish Eye",   price = 12000, category = "kacamata", isOwned = false }
        };
    }

    private static void LoadSavedState()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            // First launch — use defaults
            points = DEFAULT_STARTING_POINTS;
            return;
        }

        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            PlayerSaveData save = JsonUtility.FromJson<PlayerSaveData>(json);

            if (save != null)
            {
                points = Mathf.Max(save.points, DEFAULT_STARTING_POINTS);
                playerFish.colorVariant = !string.IsNullOrEmpty(save.colorVariant) ? save.colorVariant : "red";
                playerFish.selectedAccessoryId = save.selectedAccessoryId;

                // Apply ownership from saved IDs
                if (save.ownedAccessoryIds != null)
                {
                    foreach (var acc in accessoryCatalog)
                    {
                        acc.isOwned = save.ownedAccessoryIds.Contains(acc.accessoryId);
                    }
                }
            }
            else
            {
                points = DEFAULT_STARTING_POINTS;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[PlayerDataRepository] Failed to load save data: {e.Message}. Using defaults.");
            points = DEFAULT_STARTING_POINTS;
        }
    }

    // ───────────────────────────── Persistence ─────────────────────────────

    private static void Save()
    {
        var save = new PlayerSaveData
        {
            points = points,
            colorVariant = playerFish.colorVariant,
            selectedAccessoryId = playerFish.selectedAccessoryId,
            ownedAccessoryIds = new List<string>()
        };

        foreach (var acc in accessoryCatalog)
        {
            if (acc.isOwned)
            {
                save.ownedAccessoryIds.Add(acc.accessoryId);
            }
        }

        string json = JsonUtility.ToJson(save);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    // ───────────────────────────── Points ─────────────────────────────

    /// <summary>
    /// Returns the current points balance.
    /// Future: GET /api/v1/me/aquarium → data.points
    /// </summary>
    public static int GetPoints()
    {
        EnsureInitialized();
        return points;
    }

    /// <summary>
    /// Adds points and persists immediately.
    /// Future: POST /api/v1/me/points/add
    /// </summary>
    public static void AddPoints(int amount)
    {
        EnsureInitialized();
        points += amount;
        Save();
    }

    /// <summary>
    /// Sets the exact points balance and persists immediately.
    /// </summary>
    public static void SetPoints(int amount)
    {
        EnsureInitialized();
        points = amount;
        Save();
    }

    // ───────────────────────────── Fish Queries ─────────────────────────────

    /// <summary>
    /// Returns the player's fish data.
    /// Future: GET /api/v1/me/aquarium → data.fish
    /// </summary>
    public static FishData GetPlayerFish()
    {
        EnsureInitialized();
        return playerFish;
    }

    /// <summary>
    /// Synchronizes the local playerFish state from GET /api/v1/me/aquarium when a Bearer token is configured.
    /// Safe to call on scene load; invokes onSynced if remote fish data was loaded.
    /// </summary>
    public static void SyncFromBackend(Action onSynced = null)
    {
        EnsureInitialized();

        if (!BackendApiClient.Instance.HasBearerToken)
        {
            onSynced?.Invoke();
            return;
        }

        BackendApiClient.Instance.GetPersonalAquarium((aquarium, error) =>
        {
            if (aquarium != null && aquarium.fish != null && aquarium.fish.Count > 0)
            {
                PersonalFishDTO remoteFish = aquarium.fish.Find(f => f.state != null && f.state.isActive)
                                             ?? aquarium.fish[0];

                if (remoteFish != null)
                {
                    playerFish.fishId = remoteFish.id;
                    if (!string.IsNullOrEmpty(remoteFish.speciesCode))
                        playerFish.species = remoteFish.speciesCode;
                    if (!string.IsNullOrEmpty(remoteFish.tierCode))
                        playerFish.tier = BackendContractMapper.ToClientTier(remoteFish.tierCode);

                    if (remoteFish.appearance != null)
                    {
                        if (!string.IsNullOrEmpty(remoteFish.appearance.colorCode))
                            playerFish.colorVariant = BackendContractMapper.ToClientColorVariant(remoteFish.appearance.colorCode);

                        string mappedAcc = BackendContractMapper.ToClientAccessoryId(remoteFish.appearance.accessoryCode);
                        if (!string.IsNullOrEmpty(mappedAcc) || string.IsNullOrEmpty(playerFish.selectedAccessoryId))
                        {
                            playerFish.selectedAccessoryId = mappedAcc;
                        }
                    }

                    Save();
                    Debug.Log($"[PlayerDataRepository] Synced personal fish from backend (id={playerFish.fishId}, tier={playerFish.tier}, color={playerFish.colorVariant}).");
                }
            }

            onSynced?.Invoke();
        });
    }

    // ───────────────────────────── Fish Mutations ─────────────────────────────

    /// <summary>
    /// Saves the selected color variant for the player's fish and syncs via PATCH /api/v1/me/fish/:fishId/appearance.
    /// </summary>
    public static void SaveFishColor(string colorVariant)
    {
        EnsureInitialized();
        playerFish.colorVariant = colorVariant;
        Save();

        BackendApiClient.Instance.UpdateFishAppearance(
            playerFish.fishId,
            playerFish.colorVariant,
            playerFish.selectedAccessoryId,
            (updatedFish, err) =>
            {
                if (updatedFish != null)
                {
                    Debug.Log($"[PlayerDataRepository] Backend color synced: {updatedFish.appearance?.colorCode}");
                }
            });
    }

    /// <summary>
    /// Saves the selected accessory for the player's fish and syncs via PATCH /api/v1/me/fish/:fishId/appearance.
    /// Pass null or empty to unequip.
    /// </summary>
    public static void SaveSelectedAccessory(string accessoryId)
    {
        EnsureInitialized();
        playerFish.selectedAccessoryId = accessoryId;
        Save();

        BackendApiClient.Instance.UpdateFishAppearance(
            playerFish.fishId,
            playerFish.colorVariant,
            playerFish.selectedAccessoryId,
            (updatedFish, err) =>
            {
                if (updatedFish != null)
                {
                    Debug.Log($"[PlayerDataRepository] Backend accessory synced: {updatedFish.appearance?.accessoryCode}");
                }
            });
    }

    /// <summary>
    /// Simulates a donation resulting in a new fish of the specified tier.
    /// Triggers a mock donation for the current donor on the Mock Endowment API (:4000),
    /// which generates the fish with the matching tierCode in the database.
    /// </summary>
    public static void SimulateDonation(string tier, Action onComplete = null)
    {
        EnsureInitialized();
        playerFish.tier = tier;
        Save();

        string preservedColor = playerFish.colorVariant;
        string preservedAccessory = playerFish.selectedAccessoryId;

        BackendApiClient.Instance.SimulateTierDonation(tier, (success, result) =>
        {
            if (success)
            {
                // Refresh personal aquarium from backend to get the newly created fish & its ID
                SyncFromBackend(() =>
                {
                    // Re-apply the current color and accessory so the new fish retains customizations
                    BackendApiClient.Instance.UpdateFishAppearance(
                        playerFish.fishId,
                        preservedColor,
                        preservedAccessory,
                        (updatedFish, err) =>
                        {
                            onComplete?.Invoke();
                        });
                });
            }
            else
            {
                onComplete?.Invoke();
            }
        });
    }

    // ───────────────────────────── Accessory Queries ─────────────────────────────

    /// <summary>
    /// Returns the full accessory catalog with current ownership status.
    /// Future: GET /api/v1/accessories (catalog) + GET /api/v1/me/accessories (ownership)
    /// </summary>
    public static List<AccessoryData> GetAccessories()
    {
        EnsureInitialized();
        return accessoryCatalog;
    }

    /// <summary>
    /// Finds a specific accessory by ID. Returns null if not found.
    /// </summary>
    public static AccessoryData FindAccessory(string accessoryId)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(accessoryId)) return null;
        foreach (var acc in accessoryCatalog)
        {
            if (string.Equals(acc.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                return acc;
            }
        }
        return null;
    }

    // ───────────────────────────── Purchase ─────────────────────────────

    /// <summary>
    /// Attempts to purchase an accessory. Deducts points and marks as owned on success.
    /// Future: POST /api/v1/me/accessories/purchase { accessoryCode: "HAT_CROWN" }
    /// </summary>
    public static PurchaseResult PurchaseAccessory(string accessoryId)
    {
        EnsureInitialized();

        AccessoryData acc = FindAccessory(accessoryId);
        if (acc == null) return PurchaseResult.NotFound;
        if (acc.isOwned) return PurchaseResult.AlreadyOwned;
        if (points < acc.price) return PurchaseResult.InsufficientPoints;

        points -= acc.price;
        acc.isOwned = true;
        Save();

        return PurchaseResult.Success;
    }

    // ───────────────────────────── Debug / Reset ─────────────────────────────

    /// <summary>
    /// Resets all player data to defaults. Useful for testing.
    /// </summary>
    public static void ResetAllData()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        initialized = false;
        EnsureInitialized();
    }
}
