using System.Collections.Generic;

/// <summary>
/// Data access layer for fish and accessory data.
/// Acts as the single source of truth for all fish-related data in the game.
///
/// Current (LoFi):  Returns hardcoded mock data stored in static memory.
///                   Data resets every time the game restarts.
///
/// Future (Backend): Replace method implementations with REST API calls.
///                   Example:
///                     public static async Task<FishData> GetPlayerFish()
///                     {
///                         var response = await httpClient.GetAsync("/api/fish/me");
///                         return JsonUtility.FromJson<FishData>(response.body);
///                     }
///
/// No other scripts need to change when migrating to backend —
/// only the internals of this class.
/// </summary>
public static class FishDataRepository
{
    // ───────────────────────────── Mock Data ─────────────────────────────

    private static FishData playerFish = new FishData
    {
        fishId = "player_fish_01",
        species = "Clownfish",
        tier = "Common",
        colorVariant = "red",
        selectedAccessoryId = null
    };

    private static List<AccessoryData> accessories = new List<AccessoryData>
    {
        new AccessoryData { accessoryId = "hat_01",     displayName = "Party Hat",    isOwned = true },
        new AccessoryData { accessoryId = "glasses_01", displayName = "Cool Glasses", isOwned = true },
        new AccessoryData { accessoryId = "bow_01",     displayName = "Bow Tie",      isOwned = false },
        new AccessoryData { accessoryId = "crown_01",   displayName = "Royal Crown",  isOwned = false }
    };

    // ───────────────────────────── Fish Queries ─────────────────────────────

    /// <summary>
    /// Returns the player's fish data.
    /// Future: GET /api/fish/me
    /// </summary>
    public static FishData GetPlayerFish()
    {
        return playerFish;
    }

    // ───────────────────────────── Fish Mutations ─────────────────────────────

    /// <summary>
    /// Saves the selected color variant for the player's fish.
    /// Future: PUT /api/fish/color { colorVariant: "blue" }
    /// </summary>
    public static void SaveFishColor(string colorVariant)
    {
        playerFish.colorVariant = colorVariant;
    }

    /// <summary>
    /// Saves the selected accessory for the player's fish. Pass null or empty to unequip.
    /// Future: PUT /api/fish/accessory { accessoryId: "hat_01" }
    /// </summary>
    public static void SaveSelectedAccessory(string accessoryId)
    {
        playerFish.selectedAccessoryId = accessoryId;
    }

    // ───────────────────────────── Accessory Queries ─────────────────────────────

    /// <summary>
    /// Returns all accessories with their ownership status.
    /// Future: GET /api/accessories
    /// </summary>
    public static List<AccessoryData> GetAccessories()
    {
        return accessories;
    }
}
