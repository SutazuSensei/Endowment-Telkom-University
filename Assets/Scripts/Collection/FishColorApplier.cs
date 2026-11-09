using UnityEngine;

/// <summary>
/// Synchronizes the player's customized fish data (Tier, Color Variant, and Equipped 3D Accessory)
/// from PlayerDataRepository directly onto the 3D Fish view (Fish3DView) in the Personal Aquarium.
/// </summary>
[RequireComponent(typeof(Fish3DView))]
public class FishColorApplier : MonoBehaviour
{
    private Fish3DView fish3DView;

    private void Awake()
    {
        fish3DView = GetComponent<Fish3DView>();
    }

    private void Start()
    {
        ApplyCustomization();
        PlayerDataRepository.SyncFromBackend(() =>
        {
            if (this != null && isActiveAndEnabled)
            {
                ApplyCustomization();
            }
        });
    }

    /// <summary>
    /// Reads the current player fish data from PlayerDataRepository and applies 3D visual changes.
    /// </summary>
    public void ApplyCustomization()
    {
        FishData data = PlayerDataRepository.GetPlayerFish();
        if (data == null) return;

        if (fish3DView == null)
        {
            fish3DView = GetComponent<Fish3DView>();
        }

        if (fish3DView != null)
        {
            fish3DView.ApplyCustomization(data.tier, data.colorVariant, data.selectedAccessoryId);
        }
    }
}
