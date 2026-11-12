using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the UI in the Collection Scene (3D Architecture).
/// Handles:
/// 1. Displaying the Player's 3D Fish & 3D Accessories in the Showcase view via Fish3DView.
/// 2. Switching fish colors via Color Palette buttons.
/// 3. Equipping 3D Accessories from the player's collection.
/// 4. Navigating back to the Personal Aquarium.
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
    public struct AccessoryItem
    {
        [Tooltip("Unique accessory ID (e.g. 'hat_bigtop', 'hat_cowboy', 'glasses_fancy').")]
        public string accessoryId;

        [Tooltip("Display name shown in UI.")]
        public string displayName;

        [Tooltip("UI Button in the accessory grid to equip this accessory.")]
        public Button accessoryButton;
    }

    [Header("Showcase 3D & UI References")]
    [Tooltip("The Fish3DView component displaying the 3D showcase fish.")]
    [SerializeField] private Fish3DView showcaseFish3D;

    [Tooltip("Text element displaying fish information (species, tier, current color, etc.).")]
    [SerializeField] private TextMeshProUGUI fishInfoText;

    [Header("Full Custom ColorPicker")]
    [Tooltip("Reference to the full custom ColorPicker.")]
    [SerializeField] private Endowment.UI.ColorPicker colorPicker;

    [Header("Color Palette Configuration (Optional fallback/presets)")]
    [Tooltip("List of UI buttons for selecting color variants.")]
    [SerializeField] private ColorPaletteItem[] colorPalette;

    [Header("Accessory Configuration")]
    [Tooltip("List of available accessories and their matching UI buttons.")]
    [SerializeField] private AccessoryItem[] accessoryItems;

    [Header("Navigation")]
    [Tooltip("Name of the scene to return to when Back button is pressed.")]
    [SerializeField] private string backSceneName = "Aquarium";
    [SerializeField] private Button backButton;

    private void Start()
    {
        if (showcaseFish3D == null)
        {
            showcaseFish3D = FindFirstObjectByType<Fish3DView>();
        }

        SetupListeners();
        RefreshShowcase();

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
    /// Refreshes the 3D Showcase display according to current data in PlayerDataRepository.
    /// </summary>
    public void RefreshShowcase()
    {
        FishData data = PlayerDataRepository.GetPlayerFish();
        if (data == null) return;

        if (showcaseFish3D == null)
        {
            showcaseFish3D = FindFirstObjectByType<Fish3DView>();
        }

        if (showcaseFish3D != null)
        {
            showcaseFish3D.ApplyCustomization(data.tier, data.colorVariant, data.selectedAccessoryId);
        }

        if (colorPicker == null)
        {
            colorPicker = FindFirstObjectByType<Endowment.UI.ColorPicker>();
        }

        if (colorPicker != null && !string.IsNullOrEmpty(data.colorVariant))
        {
            if (colorPicker.CurrentHex != data.colorVariant)
            {
                colorPicker.SetColorFromHex(data.colorVariant, notifySave: false);
            }
        }

        if (fishInfoText != null)
        {
            string accessoryText = string.IsNullOrEmpty(data.selectedAccessoryId) ? "None" : data.selectedAccessoryId;
            fishInfoText.text = $"Species: {data.species} | Tier: {data.tier}\nColor: {data.colorVariant} | Accessory: {accessoryText}";
        }
    }

    public void SelectColor(string colorVariant)
    {
        PlayerDataRepository.SaveFishColor(colorVariant);
        RefreshShowcase();
    }

    public void SelectAccessory(string accessoryId)
    {
        PlayerDataRepository.SaveSelectedAccessory(accessoryId);
        RefreshShowcase();
    }

    public void UnequipAccessory()
    {
        PlayerDataRepository.SaveSelectedAccessory(null);
        RefreshShowcase();
    }

    public void OnBackButtonClicked()
    {
        if (!string.IsNullOrEmpty(backSceneName))
        {
            SceneManager.LoadScene(backSceneName);
        }
    }
}
