using System;
using System.Collections.Generic;
using UnityEngine;

// Data Models

[Serializable]
public class BigAquariumFishData
{
    public string tier;
    public string colorVariant;
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

// Manager class

public class BigAquariumManager : MonoBehaviour
{
    [Header("UI & Prefab References")]
    [Tooltip("Prefab Ikan UI yang memiliki komponen AquariumFish dan FishMovement")]
    public GameObject fishPrefab;

    [Tooltip("Panel UI RectTransform tempat ikan berenang (Big Aquarium Area)")]
    public RectTransform aquariumArea;

    [Header("Dummy JSON Data")]
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

    void Start()
    {
        // 1. Load data player dari JSON
        List<PlayerFishData> playerList = LoadDataFromJSON();

        // 2. Pastikan minimal ada 15 ikan untuk keperluan prototype
        EnsureMinimumFishCount(playerList, 15);

        // 3. Spawn seluruh ikan ke dalam Big Aquarium
        SpawnAllFish(playerList);
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
                fish = new List<BigAquariumFishData> { new BigAquariumFishData { tier = randomRarity, colorVariant = randomColor } }
            });

            currentFishCount++;
            index++;
        }
    }

    /// <summary>
    /// Men-spawn setiap ikan milik player ke Canvas Akuarium
    /// </summary>
    private void SpawnAllFish(List<PlayerFishData> players)
    {
        if (fishPrefab == null || aquariumArea == null)
        {
            Debug.LogError("[BigAquariumManager] FishPrefab atau AquariumArea belum di-assign di Inspector!");
            return;
        }

        int totalSpawned = 0;
        Rect rect = aquariumArea.rect;

        foreach (PlayerFishData player in players)
        {
            if (player.fish == null) continue;

            foreach (BigAquariumFishData fishData in player.fish)
            {
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
                    fishScript.SetupFish(player.name, fishData.tier, fishData.colorVariant);
                }

                totalSpawned++;
            }
        }

        //Debug.Log($"<color=cyan>[BIG AQUARIUM]</color> Berhasil men-spawn {totalSpawned} ekor ikan di Big Aquarium!");
    }
}