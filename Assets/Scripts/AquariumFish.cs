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

    public string OwnerName { get; private set; } = "";
    public string FishRarity { get; private set; } = "Common";
    public string ColorVariant { get; private set; } = "blue";
    public string AccessoryId { get; private set; } = "";
    public bool IsCurrentPlayerFish { get; set; } = false;

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

        OwnerName = !string.IsNullOrEmpty(ownerName) ? ownerName : "Anonymous Donor";
        FishRarity = !string.IsNullOrEmpty(fishRarity) ? fishRarity : "Common";
        ColorVariant = !string.IsNullOrEmpty(colorVariant) ? colorVariant : "blue";
        AccessoryId = accessoryId ?? "";

        // 1. Set text (Tampilkan nama pemilik dan tier)
        if (ownerNameText != null)
        {
            if (!string.IsNullOrEmpty(FishRarity))
            {
                ownerNameText.text = $"{OwnerName}\n<size=75%><color=#8EE5FF>({FishRarity})</color></size>";
            }
            else
            {
                ownerNameText.text = OwnerName;
            }
        }

        // 2. Apply Custom Sprite & Size based on Rarity Configs
        ApplyRarityConfig(FishRarity, ColorVariant);

        // 3. Apply Accessory if provided
        ApplyAccessory(AccessoryId, FishRarity);
        Fish3DView view3D = GetComponent<Fish3DView>();
        if (view3D != null) view3D.ApplyCustomization(FishRarity, ColorVariant, AccessoryId);
    }

    public void SetOwnerNameVisible(bool visible)
    {
        if (ownerNameText != null)
        {
            ownerNameText.gameObject.SetActive(visible);
        }
    }

    private void ApplyRarityConfig(string rarity, string colorVariant)
    {
        if (rarityConfigs == null) return;
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
