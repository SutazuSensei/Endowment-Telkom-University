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

    [System.Serializable]
    public struct AccessoryPlacement
    {
        public string tier;
        public Vector2 offset;
        public Vector2 scale;
        public float rotation;
    }

    [System.Serializable]
    public struct AccessoryConfig
    {
        public string accessoryId;
        public Sprite accessorySprite;
        public AccessoryPlacement[] placements;
    }

    [Header("UI References")]
    public Image fishImage;
    public Image accessoryImage;
    public TextMeshProUGUI ownerNameText;

    [Header("Rarity Settings")]
    public FishRarityConfig[] rarityConfigs;

    [Header("Accessory Settings")]
    public AccessoryConfig[] accessoryConfigs;

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

    public void SetupFish(string ownerName, string fishRarity, string colorVariant = "", string accessoryId = "")
    {
        movementScript = GetComponent<FishMovement>();

        // 1. Set text
        if (ownerNameText != null)
        {
            ownerNameText.text = $"{ownerName}\n<size=70%>({fishRarity})</size>";
        }

        // 2. Apply Custom Sprite & Size based on Rarity Configs
        ApplyRarityConfig(fishRarity, colorVariant);

        // 3. Apply Accessory if provided
        ApplyAccessory(accessoryId, fishRarity);
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
            fishImage.preserveAspect = true;
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

    private void ApplyAccessory(string accessoryId, string tier)
    {
        if (accessoryImage == null) return;

        if (string.IsNullOrEmpty(accessoryId) || accessoryConfigs == null)
        {
            accessoryImage.enabled = false;
            return;
        }

        foreach (var acc in accessoryConfigs)
        {
            if (string.Equals(acc.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                if (acc.accessorySprite != null)
                {
                    accessoryImage.sprite = acc.accessorySprite;
                    accessoryImage.enabled = true;

                    // Apply per-tier placement
                    if (acc.placements != null && acc.placements.Length > 0)
                    {
                        AccessoryPlacement placement = acc.placements[0];
                        foreach (var p in acc.placements)
                        {
                            if (string.Equals(p.tier, tier, System.StringComparison.OrdinalIgnoreCase))
                            {
                                placement = p;
                                break;
                            }
                        }

                        RectTransform rt = accessoryImage.rectTransform;
                        rt.anchoredPosition = placement.offset;
                        if (placement.scale != Vector2.zero)
                            rt.localScale = new Vector3(placement.scale.x, placement.scale.y, 1f);
                        rt.localRotation = Quaternion.Euler(0f, 0f, placement.rotation);
                    }
                }
                return;
            }
        }

        accessoryImage.enabled = false;
    }
}
