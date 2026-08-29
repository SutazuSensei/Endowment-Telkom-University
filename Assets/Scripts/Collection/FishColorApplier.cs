using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the customized fish color variant and equipped accessory from PlayerDataRepository
/// to the fish GameObject in the Personal Aquarium scene (Aquarium.unity).
///
/// Attach this script to the fish GameObject ('fih') in the Personal Aquarium.
/// When the scene starts, it automatically applies the color variant selected by the player
/// in the Collection Scene.
/// </summary>
public class FishColorApplier : MonoBehaviour
{
    [System.Serializable]
    public struct TierColorSprite
    {
        [Tooltip("The fish tier (e.g. 'Common', 'Rare', 'Epic', 'Legendary').")]
        public string tier;

        [Tooltip("Unique color variant ID (e.g. 'red', 'blue', 'yellow', 'green').")]
        public string colorVariant;

        [Tooltip("The fish sprite corresponding to this tier and color.")]
        public Sprite fishSprite;

        [Tooltip("Custom size in pixels for this specific tier and color variant. Ignored if 0,0.")]
        public Vector2 customSize;
    }

    [System.Serializable]
    public struct AccessoryPlacement
    {
        [Tooltip("The fish tier this placement applies to (e.g. 'Common', 'Rare', 'Epic', 'Legendary').")] 
        public string tier;

        [Tooltip("Position offset relative to the fish center.")]
        public Vector2 offset;

        [Tooltip("Scale of the accessory (1,1 = default sprite size).")]
        public Vector2 scale;

        [Tooltip("Rotation in degrees.")]
        public float rotation;
    }

    [System.Serializable]
    public struct AccessoryConfig
    {
        [Tooltip("Unique accessory ID matching PlayerDataRepository (e.g. 'hat_bigtop', 'hat_cowboy').")]
        public string accessoryId;

        [Tooltip("The accessory sprite overlay.")]
        public Sprite accessorySprite;

        [Tooltip("Per-tier placement data (offset, scale, rotation). One entry per tier.")]
        public AccessoryPlacement[] placements;
    }

    [Header("UI References")]
    [Tooltip("The Image component of the fish. If unassigned, will attempt GetComponent<Image>().")]
    [SerializeField] private Image fishImage;

    [Tooltip("Optional child Image component to display the equipped accessory overlay.")]
    [SerializeField] private Image accessoryOverlayImage;

    [Header("Color Variants")]
    [Tooltip("List of fish sprites combinations (Tier + Color Variant) for the Personal Aquarium.")]
    [SerializeField] private TierColorSprite[] fishSprites;

    [Header("Accessories")]
    [Tooltip("List of accessories with per-tier placement data.")]
    [SerializeField] private AccessoryConfig[] accessories;

    private void Awake()
    {
        if (fishImage == null)
        {
            fishImage = GetComponent<Image>();
        }
    }

    private void Start()
    {
        ApplyCustomization();
    }

    /// <summary>
    /// Reads the current player fish data from FishDataRepository and applies visual changes.
    /// </summary>
    public void ApplyCustomization()
    {
        FishData data = PlayerDataRepository.GetPlayerFish();
        if (data == null) return;

        // Apply Fish Sprite based on Tier + Color Variant
        if (fishImage != null && fishSprites != null)
        {
            if (TryGetConfigForFish(data.tier, data.colorVariant, out TierColorSprite config))
            {
                if (config.fishSprite != null)
                {
                    fishImage.sprite = config.fishSprite;
                    fishImage.preserveAspect = true;
                }

                if (config.customSize != Vector2.zero)
                    fishImage.rectTransform.sizeDelta = config.customSize;
            }
        }

        // Apply Accessory Overlay with Placement
        if (accessoryOverlayImage != null)
        {
            if (!string.IsNullOrEmpty(data.selectedAccessoryId))
            {
                AccessoryConfig? accConfig = GetAccessoryConfig(data.selectedAccessoryId);
                if (accConfig.HasValue && accConfig.Value.accessorySprite != null)
                {
                    accessoryOverlayImage.sprite = accConfig.Value.accessorySprite;
                    accessoryOverlayImage.enabled = true;

                    // Apply per-tier placement
                    ApplyPlacement(accessoryOverlayImage, accConfig.Value.placements, data.tier);
                }
                else
                {
                    accessoryOverlayImage.enabled = false;
                }
            }
            else
            {
                accessoryOverlayImage.enabled = false;
            }
        }
    }

    private bool TryGetConfigForFish(string tier, string variant, out TierColorSprite result)
    {
        result = default;
        if (fishSprites == null) return false;
        
        bool foundFallback = false;

        foreach (var item in fishSprites)
        {
            bool matchTier = string.Equals(item.tier, tier, System.StringComparison.OrdinalIgnoreCase);
            bool matchColor = string.Equals(item.colorVariant, variant, System.StringComparison.OrdinalIgnoreCase);

            if (matchTier && matchColor)
            {
                result = item;
                return true; // Perfect match
            }
            
            // Store a fallback of the correct color just in case the specific tier+color combo doesn't exist
            if (matchColor && !foundFallback)
            {
                result = item;
                foundFallback = true;
            }
        }

        return foundFallback;
    }

    private AccessoryConfig? GetAccessoryConfig(string accessoryId)
    {
        if (accessories == null) return null;
        foreach (var item in accessories)
        {
            if (string.Equals(item.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }
        return null;
    }

    private void ApplyPlacement(Image overlayImage, AccessoryPlacement[] placements, string tier)
    {
        if (placements == null || placements.Length == 0) return;

        // Find placement for current tier, fallback to first entry
        AccessoryPlacement placement = placements[0];
        foreach (var p in placements)
        {
            if (string.Equals(p.tier, tier, System.StringComparison.OrdinalIgnoreCase))
            {
                placement = p;
                break;
            }
        }

        RectTransform rt = overlayImage.rectTransform;
        rt.anchoredPosition = placement.offset;

        if (placement.scale != Vector2.zero)
            rt.localScale = new Vector3(placement.scale.x, placement.scale.y, 1f);

        rt.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
    }
}
