using UnityEngine;

/// <summary>
/// Single Source of Truth ("Satu Sumber") for 3D Fish and 3D Accessory visual configurations.
/// Loaded automatically from Resources/FishVisualConfig.asset by Fish3DView across all scenes
/// (Aquarium, Collection, and BigAquarium) so changing the fish model or accessories in one place
/// updates every scene simultaneously.
/// </summary>
[CreateAssetMenu(fileName = "FishVisualConfig", menuName = "Endowment/Fish Visual Config")]
public class FishVisualConfig : ScriptableObject
{
    private const string RESOURCE_PATH = "FishVisualConfig";
    private static FishVisualConfig instance;

    public static FishVisualConfig Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<FishVisualConfig>(RESOURCE_PATH);
            }
            return instance;
        }
    }

    [Header("Single Source 3D Prefabs (Satu Sumber)")]
    [Tooltip("Prefab fallback model ikan 3D utama jika tier tidak terdaftar.")]
    public GameObject activeFish3DPrefab;

    [Tooltip("Prefab model ikan 3D Tier 1 (Common - Nemo).")]
    public GameObject tier1FishPrefab;

    [Tooltip("Prefab model ikan 3D Tier 2 (Rare - Tuna).")]
    public GameObject tier2FishPrefab;

    [Tooltip("Prefab model ikan 3D Tier 3 (Epic).")]
    public GameObject tier3FishPrefab;

    [Tooltip("Prefab model ikan 3D Tier 4 (Legendary).")]
    public GameObject tier4FishPrefab;

    [Tooltip("Prefab aksesoris 3D Topi yang dipasang pada HatSocket ikan.")]
    public GameObject hat3DPrefab;

    [Tooltip("Prefab aksesoris 3D Kacamata yang dipasang pada GlassesSocket ikan.")]
    public GameObject glasses3DPrefab;

    [Header("Tier Scale Multipliers (Calibrated per Prefab Native Size)")]
    [Tooltip("Tier 1 Common (Nemo - native bounds length ~125u).")]
    public float commonScale = 0.60f;
    [Tooltip("Tier 2 Rare (Dori - native bounds length ~95u, calibrated larger than Common).")]
    public float rareScale = 0.90f;
    [Tooltip("Tier 3 Epic (Lionfish - native bounds length ~121u with wide fins).")]
    public float epicScale = 0.92f;
    [Tooltip("Tier 4 Legendary (Marlin - native bounds length ~243u, already ~2x longer natively).")]
    public float legendaryScale = 0.68f;

    [Header("3D Material Color Palette")]
    public Color redColor = new Color(0.92f, 0.20f, 0.20f, 1f);
    public Color blueColor = new Color(0.15f, 0.45f, 0.95f, 1f);
    public Color yellowColor = new Color(0.95f, 0.80f, 0.12f, 1f);
    public Color greenColor = new Color(0.18f, 0.82f, 0.35f, 1f);

    public GameObject GetFishPrefabForTier(string tier)
    {
        if (string.IsNullOrEmpty(tier))
            return tier1FishPrefab != null ? tier1FishPrefab : activeFish3DPrefab;

        switch (tier.Trim().ToLowerInvariant())
        {
            case "common":
            case "tier1":
            case "tier 1":
                return tier1FishPrefab != null ? tier1FishPrefab : activeFish3DPrefab;

            case "rare":
            case "tier2":
            case "tier 2":
                return tier2FishPrefab != null ? tier2FishPrefab : (tier1FishPrefab != null ? tier1FishPrefab : activeFish3DPrefab);

            case "epic":
            case "tier3":
            case "tier 3":
                return tier3FishPrefab != null ? tier3FishPrefab : (tier2FishPrefab != null ? tier2FishPrefab : activeFish3DPrefab);

            case "legendary":
            case "tier4":
            case "tier 4":
                return tier4FishPrefab != null ? tier4FishPrefab : (tier3FishPrefab != null ? tier3FishPrefab : activeFish3DPrefab);

            default:
                return tier1FishPrefab != null ? tier1FishPrefab : activeFish3DPrefab;
        }
    }

    public float GetScaleForTier(string tier)
    {
        if (string.IsNullOrEmpty(tier)) return commonScale;

        switch (tier.Trim().ToLowerInvariant())
        {
            case "common":
            case "tier1":
            case "tier 1":
                return commonScale;
            case "rare":
            case "tier2":
            case "tier 2":
                return rareScale;
            case "epic":
            case "tier3":
            case "tier 3":
                return epicScale;
            case "legendary":
            case "tier4":
            case "tier 4":
                return legendaryScale;
            default:
                return commonScale;
        }
    }

    public Color ResolveColor(string variant)
    {
        if (string.IsNullOrEmpty(variant)) return blueColor;

        switch (variant.Trim().ToLowerInvariant())
        {
            case "red":
            case "coral_pink":
                return redColor;
            case "blue":
            case "default":
            case "ocean_blue":
                return blueColor;
            case "yellow":
            case "gold":
            case "golden":
                return yellowColor;
            case "green":
                return greenColor;
            default:
                if (ColorUtility.TryParseHtmlString(variant, out Color parsed))
                    return parsed;
                return blueColor;
        }
    }
}
