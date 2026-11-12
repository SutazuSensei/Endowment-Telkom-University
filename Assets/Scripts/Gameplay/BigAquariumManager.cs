using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Endowment.Network;

/// <summary>
/// Manages the Big Aquarium exhibition mode:
/// - Always maintains 15 fish in the aquarium:
///   * 1 Current Player Fish (permanent, NEVER rotated out).
///   * 14 Random Donor Fish (periodically rotated a few at a time).
/// - When rotating fish, outgoing fish swim out past the aquarium boundary collider,
///   and incoming replacement fish spawn outside the boundary and swim in naturally.
/// - Seamlessly merges live Backend API donor data with OfflineDummyData fallback so it
///   always works with zero errors whether the backend server is online or offline.
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
    [Tooltip("Prefab Ikan UI yang memiliki komponen AquariumFish, FishMovement, dan Fish3DView")]
    public GameObject fishPrefab;

    [Tooltip("Panel UI RectTransform tempat ikan berenang (Big Aquarium Area)")]
    public RectTransform aquariumArea;

    [Header("Population Rules (1 Player + 14 Random = 15 Total)")]
    [Tooltip("Total maksimal ikan di Big Aquarium (1 ikan player + 14 ikan random).")]
    [Range(2, 50)]
    public int maxFishLimit = 15;

    [Tooltip("Nama default untuk ikan Current Player jika nama dari BackendApiClient kosong.")]
    public string currentPlayerDefaultName = OfflineDummyData.DEFAULT_PLAYER_NAME;

    [Header("Partial Rotation Settings")]
    [Tooltip("Interval waktu (detik) untuk merotasi sebagian ikan random.")]
    [Range(15f, 300f)]
    public float rotationIntervalSeconds = 90f;

    [Tooltip("Jumlah ikan random yang digantikan setiap siklus rotasi (jangan semua sekaligus).")]
    [Range(1, 6)]
    public int fishRotatedPerCycle = 3;

    private AquariumFish currentPlayerFish;
    private readonly List<AquariumFish> activeRandomFish = new List<AquariumFish>();
    private readonly List<DonorFishProfile> donorCandidatePool = new List<DonorFishProfile>();

    private Coroutine syncLoopCoroutine;
    private Coroutine rotationLoopCoroutine;
    private int poolRotationCursor = 0;

    public AquariumFish CurrentPlayerFish => currentPlayerFish;
    public IReadOnlyList<AquariumFish> ActiveRandomFish => activeRandomFish;

    public bool IsGuestSession => BackendApiClient.Instance != null && BackendApiClient.Instance.IsGuest;
    public int TargetRandomFishCount => IsGuestSession ? maxFishLimit : Mathf.Max(1, maxFishLimit - 1);

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

        // 1. Build fallback candidate pool from centralized OfflineDummyData
        BuildDefaultCandidatePool();

        // 2. Spawn or update the 1 Permanent Current Player Fish (donors only)
        if (!IsGuestSession)
        {
            EnsureCurrentPlayerFish();
        }
        else if (currentPlayerFish != null)
        {
            Destroy(currentPlayerFish.gameObject);
            currentPlayerFish = null;
        }

        // 3. Spawn initial random fish inside the aquarium (15 for guest, 14 for donor)
        activeRandomFish.RemoveAll(f => f == null);
        int targetRandomCount = TargetRandomFishCount;
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

    public void RefreshNow()
    {
        FetchDonorPoolFromBackend();
    }

    [ContextMenu("Trigger Partial Rotation Now")]
    public void TriggerPartialRotationNow()
    {
        RotateSomeFish(fishRotatedPerCycle);
    }

    // ───────────────────────────── 1. Current Player Fish (Never Rotated) ─────────────────────────────

    public void EnsureCurrentPlayerFish()
    {
        if (IsGuestSession)
        {
            if (currentPlayerFish != null)
            {
                Destroy(currentPlayerFish.gameObject);
                currentPlayerFish = null;
            }
            return;
        }

        FishData playerFishData = PlayerDataRepository.GetPlayerFish();
        string playerName = BackendApiClient.Instance != null && !string.IsNullOrEmpty(BackendApiClient.Instance.DonorDisplayName)
            ? BackendApiClient.Instance.DonorDisplayName
            : currentPlayerDefaultName;

        string tier = playerFishData != null && !string.IsNullOrEmpty(playerFishData.tier) ? playerFishData.tier : OfflineDummyData.DEFAULT_PLAYER_TIER;
        string color = playerFishData != null && !string.IsNullOrEmpty(playerFishData.colorVariant) ? playerFishData.colorVariant : OfflineDummyData.DEFAULT_PLAYER_COLOR;
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

    public void RotateSomeFish(int countToRotate)
    {
        activeRandomFish.RemoveAll(f => f == null);
        if (activeRandomFish.Count == 0) return;

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

            DonorFishProfile replacementProfile = GetNextUnusedCandidate();
            AquariumFish incomingFish = SpawnFishEnteringFromOutside(replacementProfile);
            if (incomingFish != null)
            {
                activeRandomFish.Add(incomingFish);
            }
        }
    }

    // ───────────────────────────── 3. Search Lookup by Owner Name ─────────────────────────────

    public AquariumFish FindOrBringInFishByName(string searchName)
    {
        if (string.IsNullOrWhiteSpace(searchName)) return null;
        string query = searchName.Trim();

        activeRandomFish.RemoveAll(f => f == null);

        // 1. Exact match among currently swimming fish
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
            if (activeRandomFish.Count >= TargetRandomFishCount)
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

    // ───────────────────────────── 4. Spawning Helpers ─────────────────────────────

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

        bool enterFromLeft = UnityEngine.Random.value < 0.5f;
        float outsideMargin = 320f;
        float startX = enterFromLeft ? (rect.xMin - outsideMargin) : (rect.xMax + outsideMargin);
        float startY = UnityEngine.Random.Range(rect.yMin + 120f, rect.yMax - 120f);
        Vector2 outsideStartPos = new Vector2(startX, startY);

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
                string currentPlayerName = (!IsGuestSession && currentPlayerFish != null)
                    ? currentPlayerFish.OwnerName
                    : (!IsGuestSession ? currentPlayerDefaultName : null);

                foreach (PublicFishDTO dto in data.fish)
                {
                    if (dto == null || string.IsNullOrEmpty(dto.id)) continue;
                    string ownerName = !string.IsNullOrEmpty(dto.donorDisplayName) ? dto.donorDisplayName : "Anonymous Donor";

                    if (!string.IsNullOrEmpty(currentPlayerName) && string.Equals(ownerName, currentPlayerName, StringComparison.OrdinalIgnoreCase))
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

        List<OfflineDummyData.DummyDonorEntry> defaults = OfflineDummyData.GetDefaultDonorPool();
        foreach (var entry in defaults)
        {
            donorCandidatePool.Add(new DonorFishProfile
            {
                id = entry.id,
                ownerName = entry.ownerName,
                tier = entry.tier,
                colorVariant = entry.colorVariant,
                accessoryId = entry.accessoryId
            });
        }
    }

    private DonorFishProfile GetNextUnusedCandidate()
    {
        if (donorCandidatePool.Count == 0)
        {
            BuildDefaultCandidatePool();
        }

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

        DonorFishProfile fallback = donorCandidatePool[poolRotationCursor % donorCandidatePool.Count];
        poolRotationCursor = (poolRotationCursor + 1) % donorCandidatePool.Count;
        return fallback;
    }
}
