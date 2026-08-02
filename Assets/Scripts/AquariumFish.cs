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

    public void SetupFish(string ownerName, string fishRarity)
    {
        movementScript = GetComponent<FishMovement>();

        // 1. Set text
        if (ownerNameText != null)
        {
            ownerNameText.text = $"{ownerName}\n<size=70%>({fishRarity})</size>";
        }

        // 2. Apply Custom Sprite & Size based on Rarity Configs
        ApplyRarityConfig(fishRarity);
    }

    private void ApplyRarityConfig(string rarity)
    {
        foreach (var config in rarityConfigs)
        {
            // Case-insensitive comparison
            if (config.rarityName.Equals(rarity, System.StringComparison.OrdinalIgnoreCase))
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

                return; // Found match
            }
        }
    }
}
