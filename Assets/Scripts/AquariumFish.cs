using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(FishMovement))]
public class AquariumFish : MonoBehaviour
{
    [System.Serializable]
    public struct FishRarityConfig
    {
        public string rarityName;
        public string colorVariant;
        public Sprite fishSprite;
        public Vector2 customSize;
        public Color textColor;
    }

    [Header("UI References")]
    public Image fishImage;
    public TextMeshProUGUI ownerNameText;

    [Header("Rarity Settings")]
    public FishRarityConfig[] rarityConfigs;

    private FishMovement movementScript;

    private void Update()
    {
        if (ownerNameText != null)
        {
            Vector3 textScale = ownerNameText.transform.localScale;

            float parentScaleX = transform.localScale.x < 0 ? -1f : 1f;
            textScale.x = Mathf.Abs(textScale.x) * parentScaleX;

            ownerNameText.transform.localScale = textScale;
        }
    }

    public void SetupFish(string ownerName, string fishRarity, string colorVariant = "")
    {
        movementScript = GetComponent<FishMovement>();

        // 1. Set text
        if (ownerNameText != null)
        {
            ownerNameText.text = $"{ownerName}\n<size=70%>({fishRarity})</size>";
        }

        // 2. Apply Custom Sprite & Size based on Rarity Configs
        ApplyRarityConfig(fishRarity, colorVariant);
    }

    private void ApplyRarityConfig(string rarity, string colorVariant)
    {
        bool foundFallback = false;

        foreach (var config in rarityConfigs)
        {
            bool matchRarity = string.Equals(config.rarityName, rarity, System.StringComparison.OrdinalIgnoreCase);
            bool matchColor = string.IsNullOrEmpty(colorVariant) || string.Equals(config.colorVariant, colorVariant, System.StringComparison.OrdinalIgnoreCase);

            if (matchRarity && matchColor)
            {
                ApplyConfig(config);
                return; // Perfect match
            }

            if (matchRarity && !foundFallback)
            {
                ApplyConfig(config);
                foundFallback = true;
            }
        }
    }

    private void ApplyConfig(FishRarityConfig config)
    {
        // Set Sprite
        if (fishImage != null && config.fishSprite != null)
        {
            fishImage.sprite = config.fishSprite;
        }

        // Set Custom Size
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null && config.customSize != Vector2.zero)
        {
            rect.sizeDelta = config.customSize;
        }

        // Optional: Custom Text Color
        if (ownerNameText != null && config.textColor != Color.clear)
        {
            ownerNameText.color = config.textColor;
        }
    }
}
