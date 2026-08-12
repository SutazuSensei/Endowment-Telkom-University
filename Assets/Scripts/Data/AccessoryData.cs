/// <summary>
/// Data model representing an accessory item in the player's collection.
///
/// Current (LoFi):  Stored in memory via FishDataRepository.
/// Future (Backend): Deserialized from REST API response (GET /api/accessories/owned).
/// </summary>
[System.Serializable]
public class AccessoryData
{
    /// <summary>Unique identifier (e.g. "hat_01", "glasses_01").</summary>
    public string accessoryId;

    /// <summary>Human-readable display name shown in UI.</summary>
    public string displayName;

    /// <summary>Whether the player currently owns this accessory.</summary>
    public bool isOwned;
}
