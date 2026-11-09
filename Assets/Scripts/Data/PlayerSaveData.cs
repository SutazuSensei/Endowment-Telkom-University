using System.Collections.Generic;

/// <summary>
/// Serializable container for persistent player data stored in PlayerPrefs.
/// Used both as local cache when online and as primary persistence when running in Offline Mode.
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    public int points;
    public string tier;
    public string colorVariant;
    public string selectedAccessoryId;
    public List<string> ownedAccessoryIds;
}
