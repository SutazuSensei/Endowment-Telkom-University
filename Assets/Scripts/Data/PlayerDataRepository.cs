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
/// Operates in Hybrid Online/Offline mode:
/// - Online: Syncs with BackendApiClient (/api/v1/me/*) and caches locally in PlayerPrefs.
/// - Offline: Seamlessly reads/writes to PlayerPrefs using OfflineDummyData defaults with zero errors.
/// </summary>
public static class PlayerDataRepository
{
    private const string SAVE_KEY = "player_save_data";

    private static bool initialized = false;

    private static int points;
    private static FishData playerFish;
    private static List<AccessoryData> accessoryCatalog;

    private static void EnsureInitialized()
    {
        if (initialized) return;

        BuildAccessoryCatalog();

        playerFish = new FishData
        {
            fishId = "player_fish_01",
            species = "Clownfish",
            tier = OfflineDummyData.DEFAULT_PLAYER_TIER,
            colorVariant = OfflineDummyData.DEFAULT_PLAYER_COLOR,
            selectedAccessoryId = null
        };

        LoadSavedState();

        initialized = true;
    }

    private static void BuildAccessoryCatalog()
    {
        accessoryCatalog = new List<AccessoryData>
        {
            // Hat (4)
            new AccessoryData { accessoryId = "hat_bigtop",  displayName = "Big Top Hat",   price = 10000, category = "topi",     isOwned = false },
            new AccessoryData { accessoryId = "hat_cowboy",  displayName = "Cowboy Honour", price = 8000,  category = "topi",     isOwned = false },
            new AccessoryData { accessoryId = "hat_sailor",  displayName = "Sailor Hat",    price = 7000,  category = "topi",     isOwned = false },
            new AccessoryData { accessoryId = "hat_unicorn", displayName = "Unicorn Hat",   price = 15000, category = "topi",     isOwned = false },

            // Glasses (3)
            new AccessoryData { accessoryId = "glasses_fancy",    displayName = "Fancy Glasses", price = 6000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_redsand",  displayName = "Evil Glasses",  price = 9000,  category = "kacamata", isOwned = false },
            new AccessoryData { accessoryId = "glasses_starfish", displayName = "Starfish Eye",  price = 12000, category = "kacamata", isOwned = false }
        };
    }

    private static void LoadSavedState()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY))
        {
            points = OfflineDummyData.DEFAULT_STARTING_POINTS;
            return;
        }

        try
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            PlayerSaveData save = JsonUtility.FromJson<PlayerSaveData>(json);

            if (save != null)
            {
                points = Mathf.Max(save.points, OfflineDummyData.DEFAULT_STARTING_POINTS);
                playerFish.tier = !string.IsNullOrEmpty(save.tier) ? save.tier : OfflineDummyData.DEFAULT_PLAYER_TIER;
                playerFish.colorVariant = !string.IsNullOrEmpty(save.colorVariant) ? save.colorVariant : OfflineDummyData.DEFAULT_PLAYER_COLOR;
                playerFish.selectedAccessoryId = save.selectedAccessoryId;

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
                points = OfflineDummyData.DEFAULT_STARTING_POINTS;
            }
        }
        catch (Exception)
        {
            points = OfflineDummyData.DEFAULT_STARTING_POINTS;
        }
    }

    private static void Save()
    {
        var save = new PlayerSaveData
        {
            points = points,
            tier = playerFish.tier,
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

    public static int GetPoints()
    {
        EnsureInitialized();
        return points;
    }

    public static void AddPoints(int amount)
    {
        EnsureInitialized();
        points += amount;
        Save();
    }

    public static void SetPoints(int amount)
    {
        EnsureInitialized();
        points = amount;
        Save();
    }

    public static FishData GetPlayerFish()
    {
        EnsureInitialized();
        return playerFish;
    }

    /// <summary>
    /// Synchronizes the local playerFish state from GET /api/v1/me/aquarium when online.
    /// Automatically falls back to local PlayerPrefs state when server is offline.
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

                        playerFish.selectedAccessoryId = BackendContractMapper.ToClientAccessoryId(remoteFish.appearance.accessoryCode);
                    }

                    Save();
                }
            }

            onSynced?.Invoke();
        });
    }

    /// <summary>
    /// Applies personal aquarium data received during login so playerFish is immediately up-to-date.
    /// </summary>
    public static void ApplyLoginAquariumData(PersonalAquariumDataDTO aquarium)
    {
        EnsureInitialized();
        if (aquarium == null || aquarium.fish == null || aquarium.fish.Count == 0) return;

        PersonalFishDTO active = aquarium.fish.Find(f => f.state != null && f.state.isActive)
                                 ?? aquarium.fish[0];

        if (active != null)
        {
            playerFish.fishId = active.id;
            if (!string.IsNullOrEmpty(active.speciesCode))
                playerFish.species = active.speciesCode;
            if (!string.IsNullOrEmpty(active.tierCode))
                playerFish.tier = BackendContractMapper.ToClientTier(active.tierCode);

            if (active.appearance != null)
            {
                if (!string.IsNullOrEmpty(active.appearance.colorCode))
                    playerFish.colorVariant = BackendContractMapper.ToClientColorVariant(active.appearance.colorCode);

                playerFish.selectedAccessoryId = BackendContractMapper.ToClientAccessoryId(active.appearance.accessoryCode);
            }

            Save();
        }
    }

    public static void SaveFishColor(string colorVariant)
    {
        EnsureInitialized();
        playerFish.colorVariant = colorVariant;
        Save();

        BackendApiClient.Instance.UpdateFishAppearance(
            playerFish.fishId,
            playerFish.colorVariant,
            playerFish.selectedAccessoryId);
    }

    public static void SaveSelectedAccessory(string accessoryId)
    {
        EnsureInitialized();
        playerFish.selectedAccessoryId = accessoryId;
        Save();

        BackendApiClient.Instance.UpdateFishAppearance(
            playerFish.fishId,
            playerFish.colorVariant,
            playerFish.selectedAccessoryId);
    }

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
                SyncFromBackend(() =>
                {
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

    public static List<AccessoryData> GetAccessories()
    {
        EnsureInitialized();
        return accessoryCatalog;
    }

    public static AccessoryData FindAccessory(string accessoryId)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(accessoryId)) return null;
        foreach (var acc in accessoryCatalog)
        {
            if (string.Equals(acc.accessoryId, accessoryId, StringComparison.OrdinalIgnoreCase))
            {
                return acc;
            }
        }
        return null;
    }

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

    public static void ResetAllData()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        initialized = false;
        EnsureInitialized();
    }
}
