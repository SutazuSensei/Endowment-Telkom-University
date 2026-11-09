using UnityEngine;
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
        }
    }

    public void SetupFish(string ownerName, string fishRarity, string colorVariant = "", string accessoryId = "")
    {
        OwnerName = !string.IsNullOrEmpty(ownerName) ? ownerName : "Anonymous Donor";
        FishRarity = !string.IsNullOrEmpty(fishRarity) ? fishRarity : "Common";
        ColorVariant = !string.IsNullOrEmpty(colorVariant) ? colorVariant : "blue";
        AccessoryId = accessoryId ?? "";

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

        if (fish3DView == null)
        {
            fish3DView = GetComponent<Fish3DView>();
        }

        if (fish3DView != null)
        {
            fish3DView.ApplyCustomization(FishRarity, ColorVariant, AccessoryId);
        }
    }

    public void SetOwnerNameVisible(bool visible)
    {
        if (ownerNameText != null)
        {
            ownerNameText.gameObject.SetActive(visible);
        }
    }
}
