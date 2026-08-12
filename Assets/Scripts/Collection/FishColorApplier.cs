using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies the customized fish color variant and equipped accessory from FishDataRepository
/// to the fish GameObject in the Personal Aquarium scene (Aquarium.unity).
///
/// Attach this script to the fish GameObject ('fih') in the Personal Aquarium.
/// When the scene starts, it automatically applies the color variant selected by the player
/// in the Collection Scene.
/// </summary>
public class FishColorApplier : MonoBehaviour
{
    [System.Serializable]
    public struct ColorVariantSprite
    {
        [Tooltip("Unique color variant ID matching CollectionUIController (e.g. 'red', 'blue', 'yellow', 'green').")]
        public string colorVariant;

        [Tooltip("The fish sprite for this color variant.")]
        public Sprite fishSprite;
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
    [Tooltip("List of color variants and their matching sprites for the Personal Aquarium.")]
    [SerializeField] private ColorVariantSprite[] colorVariants;

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
        FishData data = FishDataRepository.GetPlayerFish();
        if (data == null) return;

        // Apply Color Variant Sprite
        if (fishImage != null && colorVariants != null)
        {
            Sprite matchedSprite = GetSpriteForColorVariant(data.colorVariant);
            if (matchedSprite != null)
            {
                fishImage.sprite = matchedSprite;
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

    private Sprite GetSpriteForColorVariant(string variant)
    {
        if (colorVariants == null) return null;
        foreach (var item in colorVariants)
        {
            if (string.Equals(item.colorVariant, variant, System.StringComparison.OrdinalIgnoreCase))
            {
                return item.fishSprite;
            }
        }
        return null;
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
