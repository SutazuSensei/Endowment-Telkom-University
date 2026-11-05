using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the UI in the Collection Scene.
/// Handles:
/// 1. Displaying the Player's Fish in a large Showcase view.
/// 2. Switching fish colors via Color Palette buttons.
/// 3. Displaying and equipping Accessories from the player's collection.
/// 4. Navigating back to Main Menu or Personal Aquarium.
///
/// Backend-oriented architecture:
/// Reads and writes all data through FishDataRepository. When a backend is connected,
/// this UI controller will continue to work without modification.
/// </summary>
public class CollectionUIController : MonoBehaviour
{
    [System.Serializable]
    public struct ColorPaletteItem
    {
        [Tooltip("Unique color variant ID (e.g. 'red', 'blue', 'yellow', 'green').")]
        public string colorVariant;

        [Tooltip("UI Button in the palette to select this color.")]
        public Button paletteButton;
    }

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
    public struct AccessoryItem
    {
        [Tooltip("Unique accessory ID (e.g. 'hat_bigtop', 'hat_cowboy').")]
        public string accessoryId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [Tooltip("Sprite overlay for this accessory on the fish.")]
        public Sprite accessorySprite;

        [Tooltip("UI Button in the accessory grid to equip this accessory.")]
        public Button accessoryButton;

        [Tooltip("Per-tier placement data (offset, scale, rotation). One entry per tier.")]
        public AccessoryPlacement[] placements;
    }

    [Header("Showcase UI References")]
    [Tooltip("The Image component displaying the large showcase fish.")]
    [SerializeField] private Image fishShowcaseImage;

    [Tooltip("The Image component displaying the equipped accessory overlay on top of the fish.")]
    [SerializeField] private Image accessoryOverlayImage;

    [Tooltip("Text element displaying fish information (species, tier, current color, etc.).")]
    [SerializeField] private TextMeshProUGUI fishInfoText;

    [Header("Color Palette Configuration")]
    [Tooltip("List of UI buttons for selecting color variants.")]
    [SerializeField] private ColorPaletteItem[] colorPalette;

    [Header("Fish Sprites")]
    [Tooltip("List of fish sprites combinations (Tier + Color Variant) for the Showcase.")]
    [SerializeField] private TierColorSprite[] fishSprites;

    [Header("Accessory Configuration")]
    [Tooltip("List of available accessories and their matching sprites/buttons.")]
    [SerializeField] private AccessoryItem[] accessoryItems;

    [Header("Navigation")]
    [Tooltip("Name of the scene to return to when Back button is pressed (default: 'MainMenu').")]
    [SerializeField] private string backSceneName = "MainMenu";
    [SerializeField] private Button backButton;

    private void Start()
    {
        // 1. Setup button listeners
        SetupListeners();

        // 2. Load and display initial fish state from Repository
        RefreshShowcase();

        // 3. Sync with Backend API if authenticated
        PlayerDataRepository.SyncFromBackend(() =>
        {
            if (this != null && isActiveAndEnabled)
            {
                RefreshShowcase();
            }
        });
    }

    private void SetupListeners()
    {
        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // Connect Color Palette buttons
        if (colorPalette != null)
        {
            foreach (var item in colorPalette)
            {
                if (item.paletteButton != null)
                {
                    string variant = item.colorVariant;
                    item.paletteButton.onClick.AddListener(() => SelectColor(variant));
                }
            }
        }

        // Connect Accessory buttons
        if (accessoryItems != null)
        {
            foreach (var item in accessoryItems)
            {
                if (item.accessoryButton != null)
                {
                    string accId = item.accessoryId;
                    item.accessoryButton.onClick.AddListener(() => SelectAccessory(accId));
                }
            }
        }
    }

    /// <summary>
    /// Refreshes the Showcase display according to current data in FishDataRepository.
    /// </summary>
    public void RefreshShowcase()
    {
        FishData data = PlayerDataRepository.GetPlayerFish();
        if (data == null) return;
        if (fishShowcaseImage != null) { Fish3DView v3d = fishShowcaseImage.GetComponent<Fish3DView>(); if (v3d != null) v3d.ApplyCustomization(data.tier, data.colorVariant, data.selectedAccessoryId); }

        // Update Fish Sprite based on Tier + Color Variant
        if (TryGetConfigForFish(data.tier, data.colorVariant, out TierColorSprite config))
        {
            if (fishShowcaseImage != null && config.fishSprite != null)
            {
                fishShowcaseImage.sprite = config.fishSprite;
                fishShowcaseImage.preserveAspect = true;
            }

            if (fishShowcaseImage != null && config.customSize != Vector2.zero)
            {
                fishShowcaseImage.rectTransform.sizeDelta = config.customSize;
            }
        }

        // Update Accessory Overlay with Placement
        if (accessoryOverlayImage != null)
        {
            if (!string.IsNullOrEmpty(data.selectedAccessoryId))
            {
                AccessoryItem? accItem = GetAccessoryItem(data.selectedAccessoryId);
                if (accItem.HasValue && accItem.Value.accessorySprite != null)
                {
                    accessoryOverlayImage.sprite = accItem.Value.accessorySprite;
                    accessoryOverlayImage.enabled = true;

                    // Apply per-tier placement
                    ApplyPlacement(accessoryOverlayImage, accItem.Value.placements, data.tier);
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

        // Update Info Text
        if (fishInfoText != null)
        {
            string accessoryText = string.IsNullOrEmpty(data.selectedAccessoryId) ? "None" : data.selectedAccessoryId;
            fishInfoText.text = $"Species: {data.species} | Tier: {data.tier}\nColor: {data.colorVariant} | Accessory: {accessoryText}";
        }
    }

    /// <summary>
    /// Selects a color variant, saves it to the Repository, and refreshes the Showcase.
    /// Can also be called via Unity Inspector OnClick events.
    /// </summary>
    public void SelectColor(string colorVariant)
    {
        PlayerDataRepository.SaveFishColor(colorVariant);
        RefreshShowcase();
    }

    /// <summary>
    /// Selects an accessory, saves it to the Repository, and refreshes the Showcase.
    /// Pass an empty string or null to unequip.
    /// Can also be called via Unity Inspector OnClick events.
    /// </summary>
    public void SelectAccessory(string accessoryId)
    {
        PlayerDataRepository.SaveSelectedAccessory(accessoryId);
        RefreshShowcase();
    }

    /// <summary>
    /// Unequips any currently equipped accessory.
    /// </summary>
    public void UnequipAccessory()
    {
        PlayerDataRepository.SaveSelectedAccessory(null);
        RefreshShowcase();
    }

    /// <summary>
    /// Navigates back to the specified previous scene (MainMenu or Aquarium).
    /// </summary>
    public void OnBackButtonClicked()
    {
        if (!string.IsNullOrEmpty(backSceneName))
        {
            SceneManager.LoadScene(backSceneName);
        }
    }

    // ───────────────────────────── Helper Lookups ─────────────────────────────

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
            
            // Fallback to any sprite of the same color if tier combo doesn't exist
            if (matchColor && !foundFallback)
            {
                result = item;
                foundFallback = true;
            }
        }
        
        return foundFallback;
    }

    private AccessoryItem? GetAccessoryItem(string accessoryId)
    {
        if (accessoryItems == null) return null;
        foreach (var item in accessoryItems)
        {
            if (string.Equals(item.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }
        return null;
    }

    private Sprite GetSpriteForAccessory(string accessoryId)
    {
        var item = GetAccessoryItem(accessoryId);
        return item?.accessorySprite;
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
