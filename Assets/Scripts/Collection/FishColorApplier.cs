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
    public struct AccessorySprite
    {
        [Tooltip("Unique accessory ID matching CollectionUIController (e.g. 'hat_01', 'glasses_01').")]
        public string accessoryId;

        [Tooltip("The accessory sprite overlay.")]
        public Sprite accessorySprite;
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
    [Tooltip("List of accessories and their matching sprites for the Personal Aquarium.")]
    [SerializeField] private AccessorySprite[] accessories;

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
                    fishImage.sprite = config.fishSprite;

                if (config.customSize != Vector2.zero)
                    fishImage.rectTransform.sizeDelta = config.customSize;
            }
        }

        // Apply Accessory Overlay
        if (accessoryOverlayImage != null)
        {
            if (!string.IsNullOrEmpty(data.selectedAccessoryId))
            {
                Sprite accSprite = GetSpriteForAccessory(data.selectedAccessoryId);
                if (accSprite != null)
                {
                    accessoryOverlayImage.sprite = accSprite;
                    accessoryOverlayImage.enabled = true;
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

    private Sprite GetSpriteForAccessory(string accessoryId)
    {
        if (accessories == null) return null;
        foreach (var item in accessories)
        {
            if (string.Equals(item.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                return item.accessorySprite;
            }
        }
        return null;
    }
}
