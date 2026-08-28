using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the Store Scene UI.
/// Handles:
/// 1. Displaying the accessory catalog in a scrollable grid.
/// 2. Category sidebar filtering.
/// 3. Tab filtering (Semua / Terbaru / Terpopuler — placeholder for LoFi).
/// 4. Purchase flow with confirmation dialog.
/// 5. Points display.
/// 6. Navigation back to Aquarium.
///
/// All data flows through PlayerDataRepository.
/// When a backend is connected, this controller requires no modification.
/// </summary>
public class StoreUIController : MonoBehaviour
{
    // ───────────────────────────── Structs ─────────────────────────────

    [System.Serializable]
    public struct CategoryButtonConfig
    {
        [Tooltip("Category ID matching AccessoryData.category (e.g. 'topi', 'kalung'). Use 'semua' for all.")]
        public string categoryId;
        public Button button;
    }

    [System.Serializable]
    public struct TabButtonConfig
    {
        [Tooltip("Tab ID (e.g. 'semua', 'terbaru', 'terpopuler'). All show same data in LoFi.")]
        public string tabId;
        public Button button;
    }

    [System.Serializable]
    public struct AccessoryIconConfig
    {
        [Tooltip("Accessory ID matching catalog (e.g. 'hat_party').")]
        public string accessoryId;
        [Tooltip("Icon sprite to display in the Store grid.")]
        public Sprite icon;
    }

    // ───────────────────────────── Inspector References ─────────────────────────────

    [Header("Points Display")]
    [Tooltip("Text displaying the player's current points balance.")]
    [SerializeField] private TextMeshProUGUI pointsText;

    [Header("Item Grid")]
    [Tooltip("The Content RectTransform inside the ScrollView where item cards are spawned.")]
    [SerializeField] private RectTransform itemGridContent;

    [Tooltip("Prefab for a single store item card. Must have StoreItemCard component.")]
    [SerializeField] private GameObject itemCardPrefab;

    [Header("Category Sidebar")]
    [Tooltip("Category filter buttons. First entry should be 'semua' (all).")]
    [SerializeField] private CategoryButtonConfig[] categoryButtons;

    [Header("Tab Buttons")]
    [Tooltip("Tab filter buttons. All show the same data in LoFi.")]
    [SerializeField] private TabButtonConfig[] tabButtons;

    [Header("Accessory Icons")]
    [Tooltip("Map accessory IDs to their display sprites. Items without a mapped icon will use a default placeholder.")]
    [SerializeField] private AccessoryIconConfig[] accessoryIcons;

    [Header("Navigation")]
    [SerializeField] private Button closeButton;
    [Tooltip("Scene to load when closing the Store.")]
    [SerializeField] private string backSceneName = "Aquarium";

    [Header("Confirmation Dialog")]
    [Tooltip("The confirmation dialog panel (hidden by default).")]
    [SerializeField] private GameObject confirmationPanel;
    [Tooltip("Text in the confirmation dialog showing item name and price.")]
    [SerializeField] private TextMeshProUGUI confirmationText;
    [SerializeField] private Button btnConfirmYes;
    [SerializeField] private Button btnConfirmNo;

    [Header("Feedback")]
    [Tooltip("Text element for displaying purchase feedback messages.")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [Tooltip("How long feedback messages remain visible (seconds).")]
    [SerializeField] private float feedbackDuration = 2f;

    // ───────────────────────────── Runtime State ─────────────────────────────

    private string currentCategory = "semua";
    private string currentTab = "semua";
    private string pendingPurchaseId;
    private List<GameObject> spawnedCards = new List<GameObject>();
    private Coroutine feedbackCoroutine;

    // ───────────────────────────── Lifecycle ─────────────────────────────

    private void Start()
    {
        SetupListeners();
        HideConfirmation();
        HideFeedback();
        RefreshUI();
    }

    private void OnDestroy()
    {
        // Clean up listeners
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        if (btnConfirmYes != null) btnConfirmYes.onClick.RemoveListener(OnConfirmYes);
        if (btnConfirmNo != null) btnConfirmNo.onClick.RemoveListener(OnConfirmNo);
    }

    private void SetupListeners()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        if (btnConfirmYes != null)
            btnConfirmYes.onClick.AddListener(OnConfirmYes);

        if (btnConfirmNo != null)
            btnConfirmNo.onClick.AddListener(OnConfirmNo);

        // Category buttons
        if (categoryButtons != null)
        {
            foreach (var cat in categoryButtons)
            {
                if (cat.button != null)
                {
                    string catId = cat.categoryId;
                    cat.button.onClick.AddListener(() => OnCategorySelected(catId));
                }
            }
        }

        // Tab buttons
        if (tabButtons != null)
        {
            foreach (var tab in tabButtons)
            {
                if (tab.button != null)
                {
                    string tabId = tab.tabId;
                    tab.button.onClick.AddListener(() => OnTabSelected(tabId));
                }
            }
        }
    }

    // ───────────────────────────── UI Refresh ─────────────────────────────

    /// <summary>
    /// Refreshes the entire Store UI: points display and item grid.
    /// </summary>
    public void RefreshUI()
    {
        UpdatePointsDisplay();
        RebuildItemGrid();
    }

    private void UpdatePointsDisplay()
    {
        if (pointsText != null)
        {
            int pts = PlayerDataRepository.GetPoints();
            pointsText.text = pts.ToString("N0");
        }
    }

    private void RebuildItemGrid()
    {
        // Clear existing cards
        foreach (var card in spawnedCards)
        {
            if (card != null)
            {
                card.SetActive(false);
                card.transform.SetParent(null);
                Destroy(card);
            }
        }
        spawnedCards.Clear();

        if (itemCardPrefab == null || itemGridContent == null) return;

        // Get filtered accessories
        List<AccessoryData> accessories = PlayerDataRepository.GetAccessories();
        List<AccessoryData> filtered = FilterAccessories(accessories);

        // Spawn cards
        foreach (var acc in filtered)
        {
            GameObject cardObj = Instantiate(itemCardPrefab, itemGridContent);
            StoreItemCard card = cardObj.GetComponent<StoreItemCard>();

            if (card != null)
            {
                PopulateCard(card, acc);
            }

            spawnedCards.Add(cardObj);
        }
    }

    private List<AccessoryData> FilterAccessories(List<AccessoryData> all)
    {
        List<AccessoryData> result = new List<AccessoryData>();

        foreach (var acc in all)
        {
            // Category filter
            if (!string.Equals(currentCategory, "semua", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(acc.category, currentCategory, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Tab filter (LoFi: all tabs show same data)
            // Future: "terbaru" could sort by date, "terpopuler" by purchase count

            result.Add(acc);
        }

        return result;
    }

    private void PopulateCard(StoreItemCard card, AccessoryData acc)
    {
        card.accessoryId = acc.accessoryId;

        // Name
        if (card.nameText != null)
            card.nameText.text = acc.displayName;

        // Icon
        if (card.iconImage != null)
        {
            Sprite icon = GetIconForAccessory(acc.accessoryId);
            if (icon != null)
            {
                card.iconImage.sprite = icon;
                card.iconImage.color = Color.white;
            }
            else
            {
                // No icon configured — show a colored placeholder
                card.iconImage.sprite = null;
                card.iconImage.color = new Color(0.85f, 0.75f, 0.65f, 1f);
            }
        }

        // Price / Owned state
        if (acc.isOwned)
        {
            if (card.priceText != null) card.priceText.text = "";
            if (card.buyButtonText != null) card.buyButtonText.text = "Owned";
            if (card.buyButton != null) card.buyButton.interactable = false;
        }
        else
        {
            if (card.priceText != null) card.priceText.text = acc.price.ToString("N0");
            if (card.buyButtonText != null) card.buyButtonText.text = "Beli";
            if (card.buyButton != null)
            {
                card.buyButton.interactable = true;
                string accId = acc.accessoryId;
                card.buyButton.onClick.AddListener(() => OnBuyClicked(accId));
            }
        }
    }

    // ───────────────────────────── Category & Tab ─────────────────────────────

    private void OnCategorySelected(string categoryId)
    {
        currentCategory = categoryId != null ? categoryId.Trim() : "semua";
        RebuildItemGrid();
    }

    private void OnTabSelected(string tabId)
    {
        currentTab = tabId != null ? tabId.Trim() : "semua";
        RebuildItemGrid();
    }

    // ───────────────────────────── Purchase Flow ─────────────────────────────

    private void OnBuyClicked(string accessoryId)
    {
        AccessoryData acc = PlayerDataRepository.FindAccessory(accessoryId);
        if (acc == null) return;

        if (acc.isOwned)
        {
            ShowFeedback("Aksesoris ini sudah kamu miliki!");
            return;
        }

        if (PlayerDataRepository.GetPoints() < acc.price)
        {
            ShowFeedback("Points tidak cukup!");
            return;
        }

        // Show confirmation dialog
        pendingPurchaseId = accessoryId;
        ShowConfirmation(acc);
    }

    private void ShowConfirmation(AccessoryData acc)
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);

        if (confirmationText != null)
            confirmationText.text = $"Beli {acc.displayName} seharga {acc.price:N0} points?";
    }

    private void HideConfirmation()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        pendingPurchaseId = null;
    }

    private void OnConfirmYes()
    {
        if (string.IsNullOrEmpty(pendingPurchaseId))
        {
            HideConfirmation();
            return;
        }

        PurchaseResult result = PlayerDataRepository.PurchaseAccessory(pendingPurchaseId);
        HideConfirmation();

        switch (result)
        {
            case PurchaseResult.Success:
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayPurchaseSuccess();
                }
                ShowFeedback("Berhasil dibeli!");
                RefreshUI();
                break;

            case PurchaseResult.InsufficientPoints:
                ShowFeedback("Points tidak cukup!");
                break;

            case PurchaseResult.AlreadyOwned:
                ShowFeedback("Aksesoris ini sudah kamu miliki!");
                break;

            case PurchaseResult.NotFound:
                ShowFeedback("Aksesoris tidak ditemukan.");
                break;
        }
    }

    private void OnConfirmNo()
    {
        HideConfirmation();
    }

    // ───────────────────────────── Navigation ─────────────────────────────

    private void OnCloseClicked()
    {
        if (!string.IsNullOrEmpty(backSceneName))
        {
            SceneManager.LoadScene(backSceneName);
        }
    }

    // ───────────────────────────── Feedback ─────────────────────────────

    private void ShowFeedback(string message)
    {
        if (feedbackText == null) return;

        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        if (feedbackCoroutine != null)
            StopCoroutine(feedbackCoroutine);

        feedbackCoroutine = StartCoroutine(HideFeedbackAfterDelay());
    }

    private System.Collections.IEnumerator HideFeedbackAfterDelay()
    {
        yield return new WaitForSeconds(feedbackDuration);
        HideFeedback();
    }

    private void HideFeedback()
    {
        if (feedbackText != null)
            feedbackText.gameObject.SetActive(false);
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    private Sprite GetIconForAccessory(string accessoryId)
    {
        if (accessoryIcons == null) return null;
        foreach (var config in accessoryIcons)
        {
            if (string.Equals(config.accessoryId, accessoryId, System.StringComparison.OrdinalIgnoreCase))
            {
                return config.icon;
            }
        }
        return null;
    }
}
