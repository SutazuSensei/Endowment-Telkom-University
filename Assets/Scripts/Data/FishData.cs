/// <summary>
/// Data model representing a player's fish.
/// This class is serializable so it can be used with JsonUtility for future backend integration.
///
/// Current (LoFi):  Stored in memory via FishDataRepository.
/// Future (Backend): Deserialized from REST API response (GET /api/fish/me).
/// </summary>
[System.Serializable]
public class FishData
{
    /// <summary>Unique identifier for this fish.</summary>
    public string fishId;

    /// <summary>Fish species name (e.g. "Clownfish").</summary>
    public string species;

    /// <summary>Fish rarity tier: "Common", "Rare", "Epic", "Legendary".</summary>
    public string tier;

    /// <summary>
    /// The selected color variant key (e.g. "red", "blue", "yellow", "green").
    /// Maps to a sprite configured in the UI layer.
    /// </summary>
    public string colorVariant;

    /// <summary>
    /// The ID of the currently equipped accessory, or null/empty if none.
    /// </summary>
    public string selectedAccessoryId;
}
