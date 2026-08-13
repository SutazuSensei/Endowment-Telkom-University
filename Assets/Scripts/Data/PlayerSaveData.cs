using System.Collections.Generic;

/// <summary>
/// Serializable container for persistent player data stored in PlayerPrefs.
/// Only stores mutable player state — the accessory catalog (names, prices, categories)
/// is always defined in code via PlayerDataRepository.
///
/// Current (LoFi):  Serialized to JSON → PlayerPrefs (WebGL → IndexedDB).
/// Future (Backend): This class becomes unnecessary; data comes from REST API.
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    public int points;
    public string colorVariant;
    public string selectedAccessoryId;
    public List<string> ownedAccessoryIds;
}
