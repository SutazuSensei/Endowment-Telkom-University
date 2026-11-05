using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Endowment.Network;

// Data Models (Legacy / Offline Fallback)

[Serializable]
public class BigAquariumFishData
{
    public string tier;
    public string colorVariant;
    public string accessoryId;
}

[Serializable]
public class PlayerFishData
{
    public string name;
    public List<BigAquariumFishData> fish;
}

[Serializable]
public class AquariumDataWrapper
{
    public List<PlayerFishData> players;
}

/// <summary>
/// Manages the Big Aquarium exhibition mode.
/// Fetches live public donor fish from GET /api/v1/big-aquarium?limit=15,
/// automatically refreshes every 60 seconds (or server-specified refreshAfterSeconds),
/// and gracefully falls back to local dummy JSON if the backend server is offline.
/// </summary>
public class BigAquariumManager : MonoBehaviour
{
    [Header("UI & Prefab References")]
    [Tooltip("Prefab Ikan UI yang memiliki komponen AquariumFish dan FishMovement")]
    public GameObject fishPrefab;

    [Tooltip("Panel UI RectTransform tempat ikan berenang (Big Aquarium Area)")]
    public RectTransform aquariumArea;

    [Header("Backend Exhibition Settings")]
    [Tooltip("Maksimal jumlah ikan yang diminta dari endpoint /api/v1/big-aquarium (1 - 50)")]
    [Range(1, 50)]
    public int maxFishLimit = 15;

    [Tooltip("Jika aktif, tambahkan ikan dummy lokal apabila jumlah ikan dari server kurang dari target minimum.")]
    public bool padWithDummyIfUnderMinimum = false;

    [Header("Offline Fallback / Dummy JSON Data")]
    [TextArea(10, 15)]
    public string dummyJson = @"[
  {
    ""name"":""Andi"",
    ""fish"":[{""tier"":""Common"", ""colorVariant"":""red""}]
  },
  {
    ""name"":""Siti"",
    ""fish"":[{""tier"":""Common"", ""colorVariant"":""blue""}]
  },
  {
    ""name"":""Budi"",
    ""fish"":[{""tier"":""Epic"", ""colorVariant"":""green""}]
  }
]";

    private readonly Dictionary<string, AquariumFish> activeServerFishById = new Dictionary<string, AquariumFish>();
    private readonly List<GameObject> spawnedFallbackFish = new List<GameObject>();
    private Coroutine refreshLoopCoroutine;
    private float currentRefreshInterval = 60f;

    private void Start()
    {
        refreshLoopCoroutine = StartCoroutine(ExhibitionRefreshLoop());
    }

    private void OnDestroy()
    {
        if (refreshLoopCoroutine != null)
        {
            StopCoroutine(refreshLoopCoroutine);
        }
    }

    /// <summary>
    /// Dapat dipanggil dari UI atau script lain untuk memaksa refresh data Big Aquarium sekarang juga.
    /// </summary>
    public void RefreshNow()
    {
        FetchBigAquariumFromBackend();
    }

    private IEnumerator ExhibitionRefreshLoop()
    {
        while (true)
        {
            bool requestDone = false;
            FetchBigAquariumFromBackend(() => requestDone = true);

            while (!requestDone)
            {
                yield return null;
            }

            yield return new WaitForSeconds(Mathf.Max(10f, currentRefreshInterval));
        }
    }

    private void FetchBigAquariumFromBackend(Action onFinished = null)
    {
        BackendApiClient.Instance.GetBigAquarium(maxFishLimit, (data, error) =>
        {
            if (data != null && data.fish != null)
            {
                if (data.refreshAfterSeconds > 0)
                {
                    currentRefreshInterval = data.refreshAfterSeconds;
                }

                ClearFallbackFish();
                SyncServerFish(data.fish);

                if (padWithDummyIfUnderMinimum && data.fish.Count < 15)
                {
                    List<PlayerFishData> extra = new List<PlayerFishData>();
                    EnsureMinimumFishCount(extra, 15 - data.fish.Count);
                    SpawnAllFish(extra);
                }

                Debug.Log($"[BigAquariumManager] Live sync sukses ({data.fish.Count} ikan aktif, cursor={data.syncCursor}, refresh={currentRefreshInterval}s).");
            }
            else
            {
                // Offline fallback: jika belum ada ikan sama sekali di layar, gunakan data lokal
                if (activeServerFishById.Count == 0 && spawnedFallbackFish.Count == 0)
                {
                    string reason = error != null ? $"{error.code}: {error.message}" : "Unknown";
                    Debug.LogWarning($"[BigAquariumManager] Backend tidak terjangkau ({reason}). Menggunakan data fallback lokal.");

                    List<PlayerFishData> playerList = LoadDataFromJSON();
                    EnsureMinimumFishCount(playerList, 15);
                    SpawnAllFish(playerList);
                }
            }

            onFinished?.Invoke();
        });
    }

    /// <summary>
    /// Menyinkronkan daftar PublicFishDTO dari backend tanpa menghancurkan ikan yang masih aktif berenang.
    /// Ikan baru di-spawn, ikan yang di-refund/dihapus di-despawn, dan ikan yang berganti warna/aksesoris di-update.
    /// </summary>
    private void SyncServerFish(List<PublicFishDTO> serverFishList)
    {
        if (fishPrefab == null || aquariumArea == null)
        {
            Debug.LogError("[BigAquariumManager] FishPrefab atau AquariumArea belum di-assign di Inspector!");
            return;
        }

        HashSet<string> incomingIds = new HashSet<string>();

        foreach (PublicFishDTO dto in serverFishList)
        {
            if (dto == null || string.IsNullOrEmpty(dto.id)) continue;
            incomingIds.Add(dto.id);

            string ownerName = !string.IsNullOrEmpty(dto.donorDisplayName) ? dto.donorDisplayName : "Anonymous Donor";
            string clientTier = BackendContractMapper.ToClientTier(dto.tierCode);
            string colorCode = dto.appearance != null ? dto.appearance.colorCode : "DEFAULT";
            string accessoryCode = dto.appearance != null ? dto.appearance.accessoryCode : "NONE";

            string clientColor = BackendContractMapper.ToClientColorVariant(colorCode);
            string clientAccessory = BackendContractMapper.ToClientAccessoryId(accessoryCode);

            if (activeServerFishById.TryGetValue(dto.id, out AquariumFish existingFish) && existingFish != null)
            {
                // Update tampilan ikan yang sudah ada tanpa mereset posisinya
                existingFish.SetupFish(ownerName, clientTier, clientColor, clientAccessory);
                Fish3DView view3D = existingFish.GetComponent<Fish3DView>();
                if (view3D != null)
                {
                    view3D.ApplyCustomization(clientTier, colorCode, accessoryCode != "NONE" ? accessoryCode : clientAccessory);
                }
            }
            else
            {
                // Spawn ikan baru dari server
                GameObject newFishObj = SpawnSingleFishObject(ownerName, clientTier, clientColor, clientAccessory);
                if (newFishObj != null)
                {
                    newFishObj.name = $"Fish_{ownerName}_{dto.id.Substring(0, Mathf.Min(6, dto.id.Length))}";
                    AquariumFish fishComp = newFishObj.GetComponent<AquariumFish>();
                    if (fishComp != null)
                    {
                        activeServerFishById[dto.id] = fishComp;
                    }

                    Fish3DView view3D = newFishObj.GetComponent<Fish3DView>();
                    if (view3D != null)
                    {
                        view3D.ApplyCustomization(clientTier, colorCode, accessoryCode != "NONE" ? accessoryCode : clientAccessory);
                    }
                }
            }
        }

        // Hapus ikan yang sudah tidak aktif (misalnya donasi di-refund)
        List<string> toRemove = new List<string>();
        foreach (var kvp in activeServerFishById)
        {
            if (!incomingIds.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
                toRemove.Add(kvp.Key);
            }
        }

        foreach (string key in toRemove)
        {
            activeServerFishById.Remove(key);
        }
    }

    private void ClearFallbackFish()
    {
        for (int i = spawnedFallbackFish.Count - 1; i >= 0; i--)
        {
            if (spawnedFallbackFish[i] != null)
            {
                Destroy(spawnedFallbackFish[i]);
            }
        }
        spawnedFallbackFish.Clear();
    }

    /// <summary>
    /// Membaca dan mem-parse string JSON ke dalam C# Object List
    /// </summary>
    private List<PlayerFishData> LoadDataFromJSON()
    {
        try
        {
            // Bungkus array JSON ke dalam JSON Wrapper agar bisa dibaca JsonUtility Unity
            string wrappedJson = "{\"players\":" + dummyJson + "}";
            AquariumDataWrapper wrapper = JsonUtility.FromJson<AquariumDataWrapper>(wrappedJson);

            return (wrapper != null && wrapper.players != null) ? wrapper.players : new List<PlayerFishData>();
        }
        catch (Exception e)
        {
            Debug.LogError($"[BigAquariumManager] Error parsing JSON: {e.Message}");
            return new List<PlayerFishData>();
        }
    }

    /// <summary>
    /// Menambahkan data player dummy otomatis jika data bawaan JSON kurang dari target minimum
    /// </summary>
    private void EnsureMinimumFishCount(List<PlayerFishData> list, int minTargetCount)
    {
        string[] sampleNames = { "Rudi", "Eka", "Dewi", "Fajar", "Gita", "Hana", "Irfan", "Joko", "Kiki", "Lani", "Maya", "Nico", "Rian", "Sari" };
        string[] sampleRarities = { "Common", "Rare", "Epic", "Legendary" };
        string[] sampleColors = { "red", "blue", "green", "yellow" };
        string[] sampleAccessories = { "", "", "hat_bigtop", "hat_cowboy", "glasses_fancy", "glasses_redsand", "" };

        // Hitung total ikan saat ini
        int currentFishCount = 0;
        foreach (var p in list)
        {
            if (p.fish != null)
                currentFishCount += p.fish.Count;
        }

        int index = 0;
        // Tambahkan ikan dummy baru jika masih kurang dari target (15)
        while (currentFishCount < minTargetCount)
        {
            string randomName = sampleNames[index % sampleNames.Length];
            string randomRarity = sampleRarities[UnityEngine.Random.Range(0, sampleRarities.Length)];
            string randomColor = sampleColors[UnityEngine.Random.Range(0, sampleColors.Length)];

            list.Add(new PlayerFishData
            {
                name = randomName,
                fish = new List<BigAquariumFishData> { new BigAquariumFishData { tier = randomRarity, colorVariant = randomColor, accessoryId = sampleAccessories[index % sampleAccessories.Length] } }
            });

            currentFishCount++;
            index++;
        }
    }

    /// <summary>
    /// Men-spawn setiap ikan milik player ke Canvas Akuarium (Fallback / Offline Mode)
    /// </summary>
    private void SpawnAllFish(List<PlayerFishData> players)
    {
        if (fishPrefab == null || aquariumArea == null)
        {
            Debug.LogError("[BigAquariumManager] FishPrefab atau AquariumArea belum di-assign di Inspector!");
            return;
        }

        foreach (PlayerFishData player in players)
        {
            if (player.fish == null) continue;

            foreach (BigAquariumFishData fishData in player.fish)
            {
                GameObject newFish = SpawnSingleFishObject(player.name, fishData.tier, fishData.colorVariant, fishData.accessoryId);
                if (newFish != null)
                {
                    spawnedFallbackFish.Add(newFish);
                }
            }
        }
    }

    private GameObject SpawnSingleFishObject(string ownerName, string tier, string colorVariant, string accessoryId)
    {
        Rect rect = aquariumArea.rect;

        // 1. Instantiate Prefab Ikan sebagai child dari aquariumArea
        GameObject newFish = Instantiate(fishPrefab, aquariumArea);
        newFish.transform.SetAsLastSibling(); // Agar ikan tampil di lapisan paling depan

        RectTransform fishRect = newFish.GetComponent<RectTransform>();
        if (fishRect != null)
        {
            // Reset Scale & Z Position
            fishRect.localScale = Vector3.one;

            // Set Posisi Awal Acak di dalam batas aquariumArea
            float randomX = UnityEngine.Random.Range(rect.xMin + 50f, rect.xMax - 50f);
            float randomY = UnityEngine.Random.Range(rect.yMin + 50f, rect.yMax - 50f);

            fishRect.anchoredPosition = new Vector2(randomX, randomY);
            fishRect.localPosition = new Vector3(fishRect.localPosition.x, fishRect.localPosition.y, 0f);
        }

        // 2. Pass data Nama Pemilik, Tier & Color ke script AquariumFish
        AquariumFish fishScript = newFish.GetComponent<AquariumFish>();
        if (fishScript != null)
        {
            fishScript.SetupFish(ownerName, tier, colorVariant, accessoryId);
        }

        return newFish;
    }
}
