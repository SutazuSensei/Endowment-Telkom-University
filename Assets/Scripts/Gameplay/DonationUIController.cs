using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the LoFi Donation Panel in the Aquarium scene.
/// This panel is only for simulation testing. It replaces the player's fish
/// with a fish of the selected tier to verify visual mapping and behavior.
/// 
/// Future (Backend): This UI will be removed or replaced by a redirect to the official payment gateway.
/// </summary>
public class DonationUIController : MonoBehaviour
{
    [Header("UI Panel")]
    [Tooltip("The main container for the donation panel.")]
    [SerializeField] private GameObject donationPanel;

    [Tooltip("Button in Aquarium to open this panel.")]
    [SerializeField] private Button openDonationButton;

    [Tooltip("Button to close this panel.")]
    [SerializeField] private Button closeButton;

    [Header("Tier Simulation Buttons")]
    [SerializeField] private Button btnCommon;
    [SerializeField] private Button btnRare;
    [SerializeField] private Button btnEpic;
    [SerializeField] private Button btnLegendary;

    [Header("Dependencies")]
    [Tooltip("Reference to FishColorApplier in the scene to immediately refresh the fish visual after tier change.")]
    [SerializeField] private FishColorApplier fishColorApplier;

    private void Start()
    {
        // Hide panel by default
        if (donationPanel != null)
        {
            donationPanel.SetActive(false);
        }

        // Auto-find FishColorApplier if not assigned
        if (fishColorApplier == null)
        {
            fishColorApplier = FindFirstObjectByType<FishColorApplier>();
        }

        SetupListeners();
    }

    private void SetupListeners()
    {
        if (openDonationButton != null)
            openDonationButton.onClick.AddListener(ShowPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        // Bind tier buttons
        if (btnCommon != null) btnCommon.onClick.AddListener(() => SimulateDonation("Common"));
        if (btnRare != null) btnRare.onClick.AddListener(() => SimulateDonation("Rare"));
        if (btnEpic != null) btnEpic.onClick.AddListener(() => SimulateDonation("Epic"));
        if (btnLegendary != null) btnLegendary.onClick.AddListener(() => SimulateDonation("Legendary"));
    }

    public void ShowPanel()
    {
        if (donationPanel != null) donationPanel.SetActive(true);
    }

    public void HidePanel()
    {
        if (donationPanel != null) donationPanel.SetActive(false);
    }

    private void SimulateDonation(string tier)
    {
        // 1. Update data in the repository (LoFi overwrite behavior)
        PlayerDataRepository.SimulateDonation(tier);

        // 2. Refresh the fish visual immediately
        if (fishColorApplier != null)
        {
            fishColorApplier.ApplyCustomization();
        }
        else
        {
            Debug.LogWarning("[DonationUIController] FishColorApplier not found. Visual won't update until next load.");
        }

        // 3. Hide panel
        HidePanel();

        Debug.Log($"[DonationUIController] Simulated donation for tier: {tier}");
    }
}
