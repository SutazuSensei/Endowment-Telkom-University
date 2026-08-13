using System.Collections.Generic;
using UnityEngine;

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
    private const int DEFAULT_STARTING_POINTS = 40000;

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
            // ── Topi (3) ──
            new AccessoryData { accessoryId = "hat_party",    displayName = "Topi Pesta",      price = 5000,  category = "topi",  isOwned = false },
            new AccessoryData { accessoryId = "hat_wizard",   displayName = "Topi Penyihir",   price = 12000, category = "topi",  isOwned = false },
            new AccessoryData { accessoryId = "hat_crown",    displayName = "Mahkota Emas",    price = 35000, category = "topi",  isOwned = false },

            // ── Kalung (2) ──
            new AccessoryData { accessoryId = "necklace_pearl", displayName = "Kalung Mutiara", price = 8000,  category = "kalung", isOwned = false },
            new AccessoryData { accessoryId = "necklace_shell", displayName = "Kalung Kerang",  price = 15000, category = "kalung", isOwned = false },

            // ── Kacamata (3) ──
            new AccessoryData { accessoryId = "glasses_cool",  displayName = "Kacamata Hitam",   price = 6000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_round", displayName = "Kacamata Bulat",   price = 7000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_star",  displayName = "Kacamata Bintang", price = 10000, category = "kacamata", isOwned = false },

            // ── Dasi (2) ──
            new AccessoryData { accessoryId = "tie_bow",   displayName = "Dasi Kupu-kupu", price = 5000, category = "dasi", isOwned = false },
            new AccessoryData { accessoryId = "tie_polka", displayName = "Dasi Polkadot",  price = 8000, category = "dasi", isOwned = false },

            // ── Aksesoris Lain (3) ──
            new AccessoryData { accessoryId = "acc_scarf",  displayName = "Syal Rajut",    price = 9000, category = "aksesoris_lain", isOwned = false },
            new AccessoryData { accessoryId = "acc_flower", displayName = "Bunga Teratai",  price = 4000, category = "aksesoris_lain", isOwned = false },
            new AccessoryData { accessoryId = "acc_ribbon", displayName = "Pita Cantik",    price = 6000, category = "aksesoris_lain", isOwned = false }
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
                points = save.points;
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

    // ───────────────────────────── Fish Mutations ─────────────────────────────

    /// <summary>
    /// Saves the selected color variant for the player's fish.
    /// Future: PATCH /api/v1/me/fish/:fishId/appearance { colorCode: "BLUE" }
    /// </summary>
    public static void SaveFishColor(string colorVariant)
    {
        EnsureInitialized();
        playerFish.colorVariant = colorVariant;
        Save();
    }

    /// <summary>
    /// Saves the selected accessory for the player's fish. Pass null or empty to unequip.
    /// Future: PATCH /api/v1/me/fish/:fishId/appearance { accessoryCode: "HAT_CROWN" }
    /// </summary>
    public static void SaveSelectedAccessory(string accessoryId)
    {
        EnsureInitialized();
        playerFish.selectedAccessoryId = accessoryId;
        Save();
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
