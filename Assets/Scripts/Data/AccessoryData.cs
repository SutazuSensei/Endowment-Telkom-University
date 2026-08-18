/// <summary>
/// Data model representing an accessory item in the game catalog.
///
/// Current (LoFi):  Catalog defined in PlayerDataRepository. Ownership persisted via PlayerPrefs.
/// Future (Backend): Catalog from GET /api/v1/accessories, ownership from GET /api/v1/me/accessories.
/// </summary>
[System.Serializable]
public class AccessoryData
{
    /// <summary>Unique identifier (e.g. "hat_party", "glasses_cool").</summary>
    public string accessoryId;

    /// <summary>Human-readable display name shown in UI.</summary>
    public string displayName;

    /// <summary>Price in points to purchase this accessory.</summary>
    public int price;

    /// <summary>Category for store filtering (e.g. "topi", "kalung", "kacamata", "dasi", "aksesoris_lain").</summary>
    public string category;

    /// <summary>Whether the player currently owns this accessory.</summary>
    public bool isOwned;
}
