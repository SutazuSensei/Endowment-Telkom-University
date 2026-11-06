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
/// Manages the Big Aquarium exhibition mode:
/// - Always maintains exactly 15 fish in the aquarium:
///   * 1 Current Player Fish (permanent, NEVER rotated out).
///   * 14 Random Donor Fish (periodically rotated a few at a time).
/// - When rotating fish, outgoing fish swim out past the aquarium boundary collider,
///   and incoming replacement fish spawn outside the boundary and swim in naturally.
/// - Provides search lookup by donor name for BigAquariumSearchController.
/// </summary>
public class BigAquariumManager : MonoBehaviour
{
    [Serializable]
    public class DonorFishProfile
    {
        public string id;
        public string ownerName;
        public string tier;
        public string colorVariant;
        public string accessoryId;
    }

    [Header("UI & Prefab References")]
    [Tooltip("Prefab Ikan UI yang memiliki komponen AquariumFish dan FishMovement")]
    public GameObject fishPrefab;

    [Tooltip("Panel UI RectTransform tempat ikan berenang (Big Aquarium Area)")]
    public RectTransform aquariumArea;

    [Header("Population Rules (1 Player + 14 Random = 15 Total)")]
    [Tooltip("Total maksimal ikan di Big Aquarium (1 ikan player + 14 ikan random).")]
    [Range(2, 50)]
    public int maxFishLimit = 15;

    [Tooltip("Nama default untuk ikan Current Player jika nama dari BackendApiClient kosong.")]
    public string currentPlayerDefaultName = "Pandu (Tel-U)";

    [Header("Partial Rotation Settings")]
    [Tooltip("Interval waktu (detik) untuk merotasi sebagian ikan random.")]
    [Range(15f, 300f)]
    public float rotationIntervalSeconds = 90f;

    [Tooltip("Jumlah ikan random yang digantikan setiap siklus rotasi (jangan semua sekaligus).")]
    [Range(1, 6)]
    public int fishRotatedPerCycle = 3;

    [Tooltip("Jika aktif, tambahkan ikan dummy lokal apabila jumlah ikan dari server kurang dari target.")]
    public bool padWithDummyIfUnderMinimum = true;

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

    private AquariumFish currentPlayerFish;
    private readonly List<AquariumFish> activeRandomFish = new List<AquariumFish>();
    private readonly List<DonorFishProfile> donorCandidatePool = new List<DonorFishProfile>();

    private Coroutine syncLoopCoroutine;
    private Coroutine rotationLoopCoroutine;
    private int poolRotationCursor = 0;

    public AquariumFish CurrentPlayerFish => currentPlayerFish;
    public IReadOnlyList<AquariumFish> ActiveRandomFish => activeRandomFish;

    private void OnEnable()
    {
        InitializePopulationAndLoops();
    }

    private void OnDisable()
    {
        if (syncLoopCoroutine != null)
        {
            StopCoroutine(syncLoopCoroutine);
            syncLoopCoroutine = null;
        }
        if (rotationLoopCoroutine != null)
        {
            StopCoroutine(rotationLoopCoroutine);
            rotationLoopCoroutine = null;
        }
    }

    private void InitializePopulationAndLoops()
    {
        if (fishPrefab == null || aquariumArea == null)
        {
            Debug.LogError("[BigAquariumManager] FishPrefab atau AquariumArea belum di-assign di Inspector!");
            return;
        }

        // 1. Build fallback candidate pool so we always have >= 28 unique donors available for rotation
        BuildDefaultCandidatePool();

        // 2. Spawn or update the 1 Permanent Current Player Fish
        EnsureCurrentPlayerFish();

        // 3. Spawn initial 14 random fish inside the aquarium if not yet populated
        activeRandomFish.RemoveAll(f => f == null);
        int targetRandomCount = Mathf.Max(1, maxFishLimit - 1);
        while (activeRandomFish.Count < targetRandomCount)
        {
            DonorFishProfile profile = GetNextUnusedCandidate();
            AquariumFish spawned = SpawnFishInsideAquarium(profile, false);
            if (spawned != null)
            {
                activeRandomFish.Add(spawned);
            }
            else
            {
                break;
            }
        }

        // 4. Start backend sync & partial rotation coroutines
        if (syncLoopCoroutine != null) StopCoroutine(syncLoopCoroutine);
        syncLoopCoroutine = StartCoroutine(BackendSyncLoop());

        if (rotationLoopCoroutine != null) StopCoroutine(rotationLoopCoroutine);
        rotationLoopCoroutine = StartCoroutine(PartialRotationLoop());
    }

    /// <summary>
    /// Dapat dipanggil dari UI atau Inspector untuk memaksa refresh data server sekarang juga.
    /// </summary>
    public void RefreshNow()
    {
        FetchDonorPoolFromBackend();
    }

    /// <summary>
    /// Dapat dipanggil dari Inspector Context Menu untuk mengetes rotasi sebagian ikan secara langsung.
    /// </summary>
    [ContextMenu("Trigger Partial Rotation Now")]
    public void TriggerPartialRotationNow()
    {
        RotateSomeFish(fishRotatedPerCycle);
    }

    // ───────────────────────────── 1. Current Player Fish (Never Rotated) ─────────────────────────────

    public void EnsureCurrentPlayerFish()
    {
        FishData playerFishData = PlayerDataRepository.GetPlayerFish();
        string playerName = BackendApiClient.Instance != null && !string.IsNullOrEmpty(BackendApiClient.Instance.DonorDisplayName)
            ? BackendApiClient.Instance.DonorDisplayName
            : currentPlayerDefaultName;

        string tier = playerFishData != null && !string.IsNullOrEmpty(playerFishData.tier) ? playerFishData.tier : "Common";
        string color = playerFishData != null && !string.IsNullOrEmpty(playerFishData.colorVariant) ? playerFishData.colorVariant : "red";
        string accessory = playerFishData != null ? (playerFishData.selectedAccessoryId ?? "") : "";

        if (currentPlayerFish == null)
        {
            DonorFishProfile playerProfile = new DonorFishProfile
            {
                id = "current_player_fish",
                ownerName = playerName,
                tier = tier,
                colorVariant = color,
                accessoryId = accessory
            };

            currentPlayerFish = SpawnFishInsideAquarium(playerProfile, true);
            if (currentPlayerFish != null)
            {
                currentPlayerFish.gameObject.name = $"Fish_CurrentPlayer_{playerName}";
            }
        }
        else
        {
            currentPlayerFish.IsCurrentPlayerFish = true;
            currentPlayerFish.SetupFish(playerName, tier, color, accessory);
        }

        // Also sync from backend if token is present so tier/color updates reflect
        PlayerDataRepository.SyncFromBackend(() =>
        {
            if (currentPlayerFish != null)
            {
                FishData synced = PlayerDataRepository.GetPlayerFish();
                currentPlayerFish.SetupFish(playerName, synced.tier, synced.colorVariant, synced.selectedAccessoryId ?? "");
            }
        });
    }

    // ───────────────────────────── 2. Partial Fish Rotation (Swim Out & Swim In) ─────────────────────────────

    private IEnumerator PartialRotationLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(10f, rotationIntervalSeconds));
            RotateSomeFish(fishRotatedPerCycle);
        }
    }

    /// <summary>
    /// Replaces a small subset of activeRandomFish (never touching currentPlayerFish or the currently inspected fish).
    /// Outgoing fish swim out beyond the boundary collider; replacement fish swim in from outside the boundary collider.
    /// </summary>
    public void RotateSomeFish(int countToRotate)
    {
        activeRandomFish.RemoveAll(f => f == null);
        if (activeRandomFish.Count == 0) return;

        // Collect eligible random fish (exclude current player fish, inspected fish, and already exiting fish)
        List<AquariumFish> eligible = new List<AquariumFish>();
        foreach (AquariumFish fish in activeRandomFish)
        {
            if (fish == null || fish.IsCurrentPlayerFish) continue;
            FishMovement mov = fish.GetComponent<FishMovement>();
            if (mov != null && (mov == FishMovement.InspectedFish || mov.IsExiting || mov.IsEntering))
                continue;

            eligible.Add(fish);
        }

        if (eligible.Count == 0) return;

        int actualRotateCount = Mathf.Min(countToRotate, eligible.Count);

        for (int i = 0; i < actualRotateCount; i++)
        {
            int pickIdx = UnityEngine.Random.Range(0, eligible.Count);
            AquariumFish outgoingFish = eligible[pickIdx];
            eligible.RemoveAt(pickIdx);

            // Remove from active list and command it to swim out past the collider
            activeRandomFish.Remove(outgoingFish);
            FishMovement outMov = outgoingFish.GetComponent<FishMovement>();
            if (outMov != null)
            {
                outMov.SwimOutAndDestroy();
            }
            else
            {
                Destroy(outgoingFish.gameObject);
            }

            // Spawn a new replacement fish outside the boundary collider that swims into the aquarium
            DonorFishProfile replacementProfile = GetNextUnusedCandidate();
            AquariumFish incomingFish = SpawnFishEnteringFromOutside(replacementProfile);
            if (incomingFish != null)
            {
                activeRandomFish.Add(incomingFish);
            }
        }

        Debug.Log($"[BigAquariumManager] Rotasi bertahap: {actualRotateCount} ikan lama berenang keluar & {actualRotateCount} ikan baru berenang masuk. Ikan player tetap aman.");
    }

    // ───────────────────────────── 3. Search Lookup by Owner Name ─────────────────────────────

    /// <summary>
    /// Searches for a fish by owner name (case-insensitive).
    /// Checks the 15 active fish in the aquarium first (Current Player + 14 Random).
    /// If not currently in the tank but exists in the donor pool, rotates 1 fish out and swims the searched donor's fish in!
    /// </summary>
    public AquariumFish FindOrBringInFishByName(string searchName)
    {
        if (string.IsNullOrWhiteSpace(searchName)) return null;
        string query = searchName.Trim();

        activeRandomFish.RemoveAll(f => f == null);

        // 1. Exact match among currently swimming fish (Current Player + 14 Random)
        if (currentPlayerFish != null && MatchesNameExact(currentPlayerFish.OwnerName, query))
        {
            return currentPlayerFish;
        }
        foreach (AquariumFish fish in activeRandomFish)
        {
            if (fish != null && MatchesNameExact(fish.OwnerName, query))
            {
                return fish;
            }
        }

        // 2. Partial (contains) match among currently swimming fish
        if (currentPlayerFish != null && MatchesNamePartial(currentPlayerFish.OwnerName, query))
        {
            return currentPlayerFish;
        }
        foreach (AquariumFish fish in activeRandomFish)
        {
            if (fish != null && MatchesNamePartial(fish.OwnerName, query))
            {
                return fish;
            }
        }

        // 3. Check if the donor exists in the candidate pool (currently rotated out)
        DonorFishProfile poolMatch = donorCandidatePool.Find(p => MatchesNameExact(p.ownerName, query))
                                     ?? donorCandidatePool.Find(p => MatchesNamePartial(p.ownerName, query));

        if (poolMatch != null)
        {
            // Rotate 1 random fish out and bring the searched donor fish in from outside!
            if (activeRandomFish.Count >= Mathf.Max(1, maxFishLimit - 1))
            {
                AquariumFish outgoing = activeRandomFish[0];
                activeRandomFish.RemoveAt(0);
                if (outgoing != null)
                {
                    FishMovement outMov = outgoing.GetComponent<FishMovement>();
                    if (outMov != null) outMov.SwimOutAndDestroy();
                    else Destroy(outgoing.gameObject);
                }
            }

            AquariumFish broughtIn = SpawnFishEnteringFromOutside(poolMatch);
            if (broughtIn != null)
            {
                activeRandomFish.Add(broughtIn);
                return broughtIn;
            }
        }

        return null;
    }

    private bool MatchesNameExact(string ownerName, string query)
    {
        if (string.IsNullOrEmpty(ownerName)) return false;
        return string.Equals(ownerName.Trim(), query, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesNamePartial(string ownerName, string query)
    {
        if (string.IsNullOrEmpty(ownerName)) return false;
        return ownerName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ───────────────────────────── 4. Spawning Helpers (Inside vs Entering From Outside) ─────────────────────────────

    private AquariumFish SpawnFishInsideAquarium(DonorFishProfile profile, bool isCurrentPlayer)
    {
        if (fishPrefab == null || aquariumArea == null || profile == null) return null;

        Rect rect = aquariumArea.rect;
        if (rect.width <= 10f) rect = new Rect(-910f, -460f, 1820f, 920f);

        GameObject newFish = Instantiate(fishPrefab, aquariumArea);
        newFish.name = $"Fish_{profile.ownerName}";
        newFish.transform.SetAsLastSibling();

        RectTransform fishRect = newFish.GetComponent<RectTransform>();
        if (fishRect != null)
        {
            fishRect.localScale = Vector3.one;
            float randomX = UnityEngine.Random.Range(rect.xMin + 120f, rect.xMax - 120f);
            float randomY = UnityEngine.Random.Range(rect.yMin + 100f, rect.yMax - 100f);

            fishRect.anchoredPosition = new Vector2(randomX, randomY);
            fishRect.localPosition = new Vector3(fishRect.localPosition.x, fishRect.localPosition.y, 0f);
        }

        AquariumFish fishScript = newFish.GetComponent<AquariumFish>();
        if (fishScript != null)
        {
            fishScript.IsCurrentPlayerFish = isCurrentPlayer;
            fishScript.SetupFish(profile.ownerName, profile.tier, profile.colorVariant, profile.accessoryId);
        }

        return fishScript;
    }

    private AquariumFish SpawnFishEnteringFromOutside(DonorFishProfile profile)
    {
        if (fishPrefab == null || aquariumArea == null || profile == null) return null;

        Rect rect = aquariumArea.rect;
        if (rect.width <= 10f) rect = new Rect(-910f, -460f, 1820f, 920f);

        GameObject newFish = Instantiate(fishPrefab, aquariumArea);
        newFish.name = $"Fish_{profile.ownerName}";
        newFish.transform.SetAsLastSibling();

        // Spawn outside either the left or right collider boundary
        bool enterFromLeft = UnityEngine.Random.value < 0.5f;
        float outsideMargin = 320f;
        float startX = enterFromLeft ? (rect.xMin - outsideMargin) : (rect.xMax + outsideMargin);
        float startY = UnityEngine.Random.Range(rect.yMin + 120f, rect.yMax - 120f);
        Vector2 outsideStartPos = new Vector2(startX, startY);

        // Pick an entry target point well inside the aquarium
        float insideX = enterFromLeft
            ? UnityEngine.Random.Range(rect.xMin + 220f, rect.center.x)
            : UnityEngine.Random.Range(rect.center.x, rect.xMax - 220f);
        float insideY = Mathf.Clamp(startY + UnityEngine.Random.Range(-100f, 100f), rect.yMin + 120f, rect.yMax - 120f);
        Vector2 insideTargetPos = new Vector2(insideX, insideY);

        RectTransform fishRect = newFish.GetComponent<RectTransform>();
        if (fishRect != null)
        {
            fishRect.localScale = Vector3.one;
            fishRect.anchoredPosition = outsideStartPos;
            fishRect.localPosition = new Vector3(outsideStartPos.x, outsideStartPos.y, 0f);
        }

        AquariumFish fishScript = newFish.GetComponent<AquariumFish>();
        if (fishScript != null)
        {
            fishScript.IsCurrentPlayerFish = false;
            fishScript.SetupFish(profile.ownerName, profile.tier, profile.colorVariant, profile.accessoryId);
        }

        FishMovement movement = newFish.GetComponent<FishMovement>();
        if (movement != null)
        {
            movement.EnterFromOutside(outsideStartPos, insideTargetPos);
        }

        return fishScript;
    }

    // ───────────────────────────── 5. Backend Sync & Candidate Pool ─────────────────────────────

    private IEnumerator BackendSyncLoop()
    {
        while (true)
        {
            bool done = false;
            FetchDonorPoolFromBackend(() => done = true);
            while (!done) yield return null;

            yield return new WaitForSeconds(60f);
        }
    }

    private void FetchDonorPoolFromBackend(Action onFinished = null)
    {
        EnsureCurrentPlayerFish();

        BackendApiClient.Instance.GetBigAquarium(50, (data, error) =>
        {
            if (data != null && data.fish != null && data.fish.Count > 0)
            {
                string currentPlayerName = currentPlayerFish != null ? currentPlayerFish.OwnerName : currentPlayerDefaultName;

                foreach (PublicFishDTO dto in data.fish)
                {
                    if (dto == null || string.IsNullOrEmpty(dto.id)) continue;
                    string ownerName = !string.IsNullOrEmpty(dto.donorDisplayName) ? dto.donorDisplayName : "Anonymous Donor";

                    // Skip current player so they are never duplicated in the random pool
                    if (string.Equals(ownerName, currentPlayerName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string clientTier = BackendContractMapper.ToClientTier(dto.tierCode);
                    string colorCode = dto.appearance != null ? dto.appearance.colorCode : "DEFAULT";
                    string accessoryCode = dto.appearance != null ? dto.appearance.accessoryCode : "NONE";

                    string clientColor = BackendContractMapper.ToClientColorVariant(colorCode);
                    string clientAccessory = BackendContractMapper.ToClientAccessoryId(accessoryCode);

                    DonorFishProfile existing = donorCandidatePool.Find(p => p.id == dto.id || string.Equals(p.ownerName, ownerName, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        existing.tier = clientTier;
                        existing.colorVariant = clientColor;
                        existing.accessoryId = clientAccessory;
                    }
                    else
                    {
                        donorCandidatePool.Insert(0, new DonorFishProfile
                        {
                            id = dto.id,
                            ownerName = ownerName,
                            tier = clientTier,
                            colorVariant = clientColor,
                            accessoryId = clientAccessory
                        });
                    }
                }
            }

            onFinished?.Invoke();
        });
    }

    private void BuildDefaultCandidatePool()
    {
        if (donorCandidatePool.Count > 0) return;

        // Parse dummyJson first
        List<PlayerFishData> fromJson = LoadDataFromJSON();
        foreach (PlayerFishData p in fromJson)
        {
            if (p == null || string.IsNullOrEmpty(p.name) || p.fish == null || p.fish.Count == 0) continue;
            BigAquariumFishData f = p.fish[0];
            donorCandidatePool.Add(new DonorFishProfile
            {
                id = $"pool_{p.name.ToLowerInvariant()}",
                ownerName = p.name,
                tier = !string.IsNullOrEmpty(f.tier) ? f.tier : "Common",
                colorVariant = !string.IsNullOrEmpty(f.colorVariant) ? f.colorVariant : "blue",
                accessoryId = f.accessoryId ?? ""
            });
        }

        // Add diverse donor names so rotation always has fresh donors outside the 14 active ones
        string[] sampleNames = {
            "Rudi", "Eka", "Dewi", "Fajar", "Gita", "Hana", "Irfan", "Joko",
            "Kiki", "Lani", "Maya", "Nico", "Rian", "Sari", "Bayu", "Dina",
            "Reza", "Tono", "Putri", "Agus", "Nadia", "Dimas", "Rizky", "Ayu", "Bagas"
        };
        string[] sampleRarities = { "Common", "Rare", "Epic", "Legendary" };
        string[] sampleColors = { "red", "blue", "green", "yellow" };
        string[] sampleAccessories = { "", "hat_bigtop", "hat_cowboy", "glasses_fancy", "glasses_redsand", "hat_sailor", "glasses_starfish", "" };

        for (int i = 0; i < sampleNames.Length; i++)
        {
            string name = sampleNames[i];
            if (donorCandidatePool.Exists(p => string.Equals(p.ownerName, name, StringComparison.OrdinalIgnoreCase)))
                continue;

            donorCandidatePool.Add(new DonorFishProfile
            {
                id = $"pool_{name.ToLowerInvariant()}",
                ownerName = name,
                tier = sampleRarities[i % sampleRarities.Length],
                colorVariant = sampleColors[i % sampleColors.Length],
                accessoryId = sampleAccessories[i % sampleAccessories.Length]
            });
        }
    }

    private DonorFishProfile GetNextUnusedCandidate()
    {
        if (donorCandidatePool.Count == 0)
        {
            BuildDefaultCandidatePool();
        }

        // Try to pick a candidate from donorCandidatePool that is not currently swimming in activeRandomFish
        for (int attempt = 0; attempt < donorCandidatePool.Count; attempt++)
        {
            int idx = (poolRotationCursor + attempt) % donorCandidatePool.Count;
            DonorFishProfile candidate = donorCandidatePool[idx];

            bool alreadySwimming = activeRandomFish.Exists(f =>
                f != null && string.Equals(f.OwnerName, candidate.ownerName, StringComparison.OrdinalIgnoreCase));

            if (!alreadySwimming)
            {
                poolRotationCursor = (idx + 1) % donorCandidatePool.Count;
                return candidate;
            }
        }

        // Fallback if all pool candidates are already swimming
        DonorFishProfile fallback = donorCandidatePool[poolRotationCursor % donorCandidatePool.Count];
        poolRotationCursor = (poolRotationCursor + 1) % donorCandidatePool.Count;
        return fallback;
    }

    private List<PlayerFishData> LoadDataFromJSON()
    {
        try
        {
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
}
