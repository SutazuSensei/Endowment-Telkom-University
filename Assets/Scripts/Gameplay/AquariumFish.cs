using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Represents a donor's fish in the Big Aquarium.
/// Stores donor metadata (OwnerName, FishRarity, ColorVariant, AccessoryId, IsCurrentPlayerFish),
/// manages the floating owner name label, and delegates 3D fish + 3D accessory rendering to Fish3DView.
/// </summary>
[RequireComponent(typeof(FishMovement))]
[RequireComponent(typeof(Fish3DView))]
public class AquariumFish : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI ownerNameText;

    public string OwnerName { get; private set; } = "";
    public string FishRarity { get; private set; } = "Common";
    public string ColorVariant { get; private set; } = "blue";
    public string AccessoryId { get; private set; } = "";
    public bool IsCurrentPlayerFish { get; set; } = false;

    private Fish3DView fish3DView;
    private RectTransform badgeRect;
    private Image badgeImage;
    private string normalizedTierKey = "common";

    private static Sprite cachedCrownSprite;
    private static Sprite cachedStarSprite;
    private static Sprite cachedDiamondSprite;

    private void Awake()
    {
        fish3DView = GetComponent<Fish3DView>();
    }

    private void Update()
    {
        if (ownerNameText != null)
        {
            Vector3 textScale = ownerNameText.transform.localScale;
            float parentScaleX = transform.localScale.x < 0 ? -1f : 1f;
            textScale.x = Mathf.Abs(textScale.x) * parentScaleX;
            ownerNameText.transform.localScale = textScale;

            if (badgeRect != null && badgeRect.gameObject.activeSelf && normalizedTierKey == "legendary")
            {
                float s = 1f + 0.08f * Mathf.Sin(Time.time * 3.0f);
                badgeRect.localScale = new Vector3(s, s, 1f);
            }
        }
    }

    public void SetupFish(string ownerName, string fishRarity, string colorVariant = "", string accessoryId = "")
    {
        OwnerName = !string.IsNullOrEmpty(ownerName) ? ownerName : "Anonymous Donor";
        FishRarity = !string.IsNullOrEmpty(fishRarity) ? fishRarity : "Common";
        ColorVariant = !string.IsNullOrEmpty(colorVariant) ? colorVariant : "blue";
        AccessoryId = accessoryId ?? "";
        normalizedTierKey = NormalizeTierKey(FishRarity);

        ApplyTierNameplateStyle();

        if (fish3DView == null)
        {
            fish3DView = GetComponent<Fish3DView>();
        }

        if (fish3DView != null)
        {
            fish3DView.ApplyCustomization(FishRarity, ColorVariant, AccessoryId);
        }
    }

    public void PlayInspectBurst()
    {
        if (fish3DView == null) fish3DView = GetComponent<Fish3DView>();
        if (fish3DView != null)
        {
            fish3DView.PlaySpecialBurst(false);
        }
    }

    public void SetOwnerNameVisible(bool visible)
    {
        if (ownerNameText != null)
        {
            ownerNameText.gameObject.SetActive(visible);
        }
    }

    private void ApplyTierNameplateStyle()
    {
        if (ownerNameText == null) return;

        string nameColor = "#FFFFFF";
        string tierLabel = $"({FishRarity})";
        string tierColor = "#BCE6FF";

        ownerNameText.textWrappingMode = TextWrappingModes.NoWrap;
        ownerNameText.overflowMode = TextOverflowModes.Overflow;
        ownerNameText.alignment = TextAlignmentOptions.Center;
        ownerNameText.rectTransform.sizeDelta = new Vector2(220f, 44f);

        switch (normalizedTierKey)
        {
            case "rare":
                nameColor = "#A3F0FF";
                tierColor = "#4DD2FF";
                tierLabel = "• Rare •";
                break;
            case "epic":
                nameColor = "#ECC7FF";
                tierColor = "#D07AFF";
                tierLabel = "• Epic •";
                break;
            case "legendary":
                nameColor = "#FFE670";
                tierColor = "#FFAE19";
                tierLabel = "• Legendary •";
                break;
            default:
                nameColor = "#FFFFFF";
                tierColor = "#BCE6FF";
                tierLabel = $"({FishRarity})";
                break;
        }

        if (normalizedTierKey == "common")
        {
            ownerNameText.text = $"<color={nameColor}>{OwnerName}</color>\n<size=72%><color={tierColor}>{tierLabel}</color></size>";
        }
        else
        {
            ownerNameText.text = $"<color={nameColor}><b>{OwnerName}</b></color>\n<size=76%><color={tierColor}><b>{tierLabel}</b></color></size>";
        }

        EnsureTierBadgeIcon();
    }

    private void EnsureTierBadgeIcon()
    {
        if (ownerNameText == null) return;

        if (normalizedTierKey == "common")
        {
            if (badgeRect != null) badgeRect.gameObject.SetActive(false);
            return;
        }

        EnsureBadgeSprites();

        if (badgeRect == null)
        {
            Transform existing = ownerNameText.transform.Find("TierBadgeIcon");
            if (existing != null)
            {
                badgeRect = existing.GetComponent<RectTransform>();
                badgeImage = existing.GetComponent<Image>();
            }
            else
            {
                GameObject badgeObj = new GameObject("TierBadgeIcon", typeof(RectTransform), typeof(Image));
                badgeObj.layer = ownerNameText.gameObject.layer;
                badgeObj.transform.SetParent(ownerNameText.transform, false);
                badgeRect = badgeObj.GetComponent<RectTransform>();
                badgeImage = badgeObj.GetComponent<Image>();
                badgeImage.raycastTarget = false;
                badgeRect.anchorMin = new Vector2(0.5f, 1f);
                badgeRect.anchorMax = new Vector2(0.5f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 0f);
                badgeRect.anchoredPosition = new Vector2(0f, 6f);
            }
        }

        if (badgeRect == null || badgeImage == null) return;

        badgeRect.gameObject.SetActive(true);
        badgeRect.localScale = Vector3.one;

        if (normalizedTierKey == "legendary")
        {
            badgeImage.sprite = cachedCrownSprite;
            badgeImage.color = new Color(1.0f, 0.86f, 0.25f, 1f);
            badgeRect.sizeDelta = new Vector2(20f, 16f);
        }
        else if (normalizedTierKey == "epic")
        {
            badgeImage.sprite = cachedStarSprite;
            badgeImage.color = new Color(0.80f, 0.50f, 1.0f, 1f);
            badgeRect.sizeDelta = new Vector2(15f, 15f);
        }
        else // rare
        {
            badgeImage.sprite = cachedDiamondSprite;
            badgeImage.color = new Color(0.35f, 0.88f, 1.0f, 1f);
            badgeRect.sizeDelta = new Vector2(12f, 12f);
        }
    }

    private static string NormalizeTierKey(string tier)
    {
        if (string.IsNullOrEmpty(tier)) return "common";
        string lower = tier.Trim().ToLowerInvariant();
        if (lower == "rare" || lower == "tier2" || lower == "tier 2") return "rare";
        if (lower == "epic" || lower == "tier3" || lower == "tier 3") return "epic";
        if (lower == "legendary" || lower == "tier4" || lower == "tier 4") return "legendary";
        return "common";
    }

    private static void EnsureBadgeSprites()
    {
        if (cachedCrownSprite == null) cachedCrownSprite = CreateCrownSprite(48, 38);
        if (cachedStarSprite == null) cachedStarSprite = CreateStarSprite(40);
        if (cachedDiamondSprite == null) cachedDiamondSprite = CreateDiamondSprite(32);
    }

    private static Sprite CreateCrownSprite(int w, int h)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            float ny = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float nx = (float)x / (w - 1);
                float alpha = 0f;

                // Base band
                if (ny >= 0.08f && ny <= 0.30f && nx >= 0.10f && nx <= 0.90f) alpha = 1f;

                // Left peak, Center peak, Right peak
                float topEdge = 0.28f;
                if (nx >= 0.08f && nx <= 0.92f)
                {
                    float p1 = Mathf.Max(0f, 1f - Mathf.Abs(nx - 0.16f) / 0.16f) * 0.50f;
                    float p2 = Mathf.Max(0f, 1f - Mathf.Abs(nx - 0.50f) / 0.22f) * 0.62f;
                    float p3 = Mathf.Max(0f, 1f - Mathf.Abs(nx - 0.84f) / 0.16f) * 0.50f;
                    topEdge = 0.28f + Mathf.Max(p1, Mathf.Max(p2, p3));
                    if (ny >= 0.14f && ny <= topEdge) alpha = 1f;
                }

                // Jewels on crown peaks
                float d1 = Vector2.Distance(new Vector2(nx * w, ny * h), new Vector2(0.16f * w, 0.82f * h));
                float d2 = Vector2.Distance(new Vector2(nx * w, ny * h), new Vector2(0.50f * w, 0.91f * h));
                float d3 = Vector2.Distance(new Vector2(nx * w, ny * h), new Vector2(0.84f * w, 0.82f * h));
                if (d1 < 3.2f || d2 < 3.8f || d3 < 3.2f) alpha = 1f;

                px[y * w + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateStarSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - half) / half);
                float ny = Mathf.Abs((y - half) / half);
                float starDist = Mathf.Pow(nx, 0.55f) + Mathf.Pow(ny, 0.55f);
                float a = Mathf.Clamp01((0.92f - starDist) * 6f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateDiamondSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = Mathf.Abs((x - half) / half);
                float ny = Mathf.Abs((y - half) / half);
                float a = Mathf.Clamp01((0.88f - (nx + ny)) * 7f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
