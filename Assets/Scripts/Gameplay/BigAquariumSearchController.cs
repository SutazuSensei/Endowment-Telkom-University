using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles the "Search User by Name" and "Opsi A: Camera Follow & Inspect" feature in Big Aquarium.
/// All UI elements exist permanently in the Scene Hierarchy (GearPanel & InspectFishPanel)
/// and are referenced via serialized fields in the Inspector.
/// </summary>
public class BigAquariumSearchController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private BigAquariumManager bigAquariumManager;
    [SerializeField] private RectTransform aquariumArea;
    [SerializeField] private Camera cam2D;

    [Header("Search UI (In Hierarchy)")]
    [SerializeField] private GameObject gearPanelRoot;
    [SerializeField] private GameObject gearMenuBackground;
    [SerializeField] private GameObject searchInputContainer;
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button searchSubmitButton;
    [SerializeField] private TextMeshProUGUI searchFeedbackText;

    [Header("Inspect / Follow UI (In Hierarchy)")]
    [SerializeField] private GameObject inspectPanelRoot;
    [SerializeField] private TextMeshProUGUI inspectOwnerNameText;
    [SerializeField] private TextMeshProUGUI inspectTierText;
    [SerializeField] private Button releaseFocusButton;

    [Header("Camera Follow Settings (Opsi A)")]
    [Tooltip("Orthographic size of 2D camera when zoomed out in full Big Aquarium mode.")]
    [SerializeField] private float fullAquariumOrthoSize = 129f;

    [Tooltip("Orthographic size of 2D camera when following/inspecting a searched fish.")]
    [SerializeField] private float inspectFollowOrthoSize = 72f;

    [Tooltip("Default world X/Y position of 2D camera in full Big Aquarium mode.")]
    [SerializeField] private Vector2 defaultCam2DPosition = new Vector2(-389f, 120f);

    [Tooltip("How smoothly the camera and viewport follow the target fish.")]
    [SerializeField] private float followSmoothSpeed = 5.5f;

    private AquariumFish inspectedFish;
    private RectTransform inspectedFishRect;
    private bool isInspecting = false;
    private bool isReturningToOverview = false;
    private Coroutine feedbackCoroutine;

    public bool IsInspecting => isInspecting;
    public AquariumFish InspectedFish => inspectedFish;

    private void Awake()
    {
        if (bigAquariumManager == null)
        {
            bigAquariumManager = FindFirstObjectByType<BigAquariumManager>();
        }

        if (aquariumArea == null && bigAquariumManager != null)
        {
            aquariumArea = bigAquariumManager.aquariumArea;
        }

        if (cam2D == null)
        {
            GameObject c2dObj = GameObject.Find("2d camera");
            if (c2dObj != null) cam2D = c2dObj.GetComponent<Camera>();
        }

        if (searchSubmitButton != null && searchSubmitButton.onClick.GetPersistentEventCount() == 0)
        {
            searchSubmitButton.onClick.AddListener(ExecuteSearch);
        }

        if (searchInputField != null)
        {
            searchInputField.onSubmit.AddListener(_ => ExecuteSearch());
        }

        if (releaseFocusButton != null && releaseFocusButton.onClick.GetPersistentEventCount() == 0)
        {
            releaseFocusButton.onClick.AddListener(ReleaseFocus);
        }

        if (inspectPanelRoot != null)
        {
            inspectPanelRoot.SetActive(false);
        }

        if (searchFeedbackText != null)
        {
            searchFeedbackText.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (inspectedFish != null)
        {
            inspectedFish.SetOwnerNameVisible(true);
        }

        inspectedFish = null;
        inspectedFishRect = null;
        isInspecting = false;
        isReturningToOverview = false;
        FishMovement.InspectedFish = null;

        if (aquariumArea != null)
        {
            aquariumArea.localScale = Vector3.one;
            aquariumArea.anchoredPosition = Vector2.zero;
        }

        if (inspectPanelRoot != null)
        {
            inspectPanelRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Triggered when the user clicks the Search button or presses Enter in the search input field.
    /// </summary>
    public void ExecuteSearch()
    {
        if (searchInputField == null || bigAquariumManager == null) return;

        string query = searchInputField.text != null ? searchInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(query))
        {
            ShowSearchFeedback("Masukkan nama pemilik ikan!");
            return;
        }

        AquariumFish foundFish = bigAquariumManager.FindOrBringInFishByName(query);
        if (foundFish == null)
        {
            ShowSearchFeedback($"User \"{query}\" tidak ditemukan");
            return;
        }

        if (searchFeedbackText != null)
        {
            searchFeedbackText.gameObject.SetActive(false);
        }

        StartFollowingFish(foundFish);
    }

    /// <summary>
    /// Enters Opsi A Follow Mode on the target fish:
    /// - Activates the repulsion boundary around the target fish so other fish do not block it.
    /// - Hides the search bar and displays only the Owner Name, Tier, and "Lepas Fokus" button.
    /// - Smoothly zooms in and follows the target fish continuously.
    /// </summary>
    public void StartFollowingFish(AquariumFish targetFish)
    {
        if (targetFish == null) return;

        // Restore previous fish overhead label if switching targets
        if (inspectedFish != null && inspectedFish != targetFish)
        {
            inspectedFish.SetOwnerNameVisible(true);
        }

        inspectedFish = targetFish;
        inspectedFishRect = targetFish.GetComponent<RectTransform>();
        isInspecting = true;
        isReturningToOverview = false;

        // Activate repulsion boundary so other fish steer away from this fish
        FishMovement movement = targetFish.GetComponent<FishMovement>();
        FishMovement.InspectedFish = movement;

        // Bring inspected fish to front of UI hierarchy
        targetFish.transform.SetAsLastSibling();

        // Hide overhead floating text on the target fish while the dedicated Inspect HUD is shown
        targetFish.SetOwnerNameVisible(false);

        // Hide Search / Gear UI while inspecting
        if (searchInputContainer != null) searchInputContainer.SetActive(false);
        if (gearMenuBackground != null) gearMenuBackground.SetActive(false);
        if (gearPanelRoot != null) gearPanelRoot.SetActive(false);

        // Show Inspect HUD (Owner Name + Tier + Lepas Fokus button)
        if (inspectPanelRoot != null)
        {
            inspectPanelRoot.SetActive(true);
        }

        UpdateInspectLabels();
    }

    private void UpdateInspectLabels()
    {
        if (inspectedFish == null) return;

        if (inspectOwnerNameText != null)
        {
            inspectOwnerNameText.text = inspectedFish.OwnerName;
        }

        if (inspectTierText != null)
        {
            inspectTierText.text = $"Tier: {inspectedFish.FishRarity}";
        }
    }

    /// <summary>
    /// Releases camera focus (called by "Lepas Fokus" button) and smoothly zooms back out to full Big Aquarium.
    /// </summary>
    public void ReleaseFocus()
    {
        if (inspectedFish != null)
        {
            inspectedFish.SetOwnerNameVisible(true);
        }

        inspectedFish = null;
        inspectedFishRect = null;
        isInspecting = false;
        isReturningToOverview = true;

        FishMovement.InspectedFish = null;

        if (inspectPanelRoot != null)
        {
            inspectPanelRoot.SetActive(false);
        }

        if (gearPanelRoot != null)
        {
            gearPanelRoot.SetActive(true);
        }
    }

    public void ReleaseFocusImmediate()
    {
        if (inspectedFish != null)
        {
            inspectedFish.SetOwnerNameVisible(true);
        }

        inspectedFish = null;
        inspectedFishRect = null;
        isInspecting = false;
        isReturningToOverview = false;
        FishMovement.InspectedFish = null;

        if (aquariumArea != null)
        {
            aquariumArea.localScale = Vector3.one;
            aquariumArea.anchoredPosition = Vector2.zero;
        }

        if (cam2D != null)
        {
            cam2D.orthographicSize = fullAquariumOrthoSize;
            Vector3 cPos = cam2D.transform.position;
            cPos.x = defaultCam2DPosition.x;
            cPos.y = defaultCam2DPosition.y;
            cam2D.transform.position = cPos;
        }

        if (inspectPanelRoot != null) inspectPanelRoot.SetActive(false);
        if (gearPanelRoot != null) gearPanelRoot.SetActive(true);
    }

    private void LateUpdate()
    {
        if (isInspecting)
        {
            if (inspectedFish == null || inspectedFishRect == null)
            {
                ReleaseFocus();
                return;
            }

            UpdateInspectLabels();

            float dt = Time.deltaTime * followSmoothSpeed;

            // 1. Zoom 2D Camera in smoothly
            if (cam2D != null)
            {
                cam2D.orthographicSize = Mathf.Lerp(cam2D.orthographicSize, inspectFollowOrthoSize, dt);
            }

            float currentOrtho = cam2D != null ? cam2D.orthographicSize : inspectFollowOrthoSize;
            float zoomFactor = Mathf.Clamp(fullAquariumOrthoSize / Mathf.Max(10f, currentOrtho), 1f, 2.5f);

            // 2. Pan and scale aquariumArea so the followed fish stays smoothly centered on screen
            if (aquariumArea != null)
            {
                Vector3 targetScale = new Vector3(zoomFactor, zoomFactor, 1f);
                aquariumArea.localScale = Vector3.Lerp(aquariumArea.localScale, targetScale, dt);

                Vector2 fishPos = inspectedFishRect.anchoredPosition;
                Vector2 targetAreaPos = -fishPos * aquariumArea.localScale.x;
                aquariumArea.anchoredPosition = Vector2.Lerp(aquariumArea.anchoredPosition, targetAreaPos, dt);
            }

            // 3. Pan 2D Camera within background bounds so underwater reef moves with the followed fish
            if (cam2D != null && aquariumArea != null)
            {
                Rect rect = aquariumArea.rect;
                float halfW = Mathf.Max(400f, rect.width * 0.5f);
                float halfH = Mathf.Max(250f, rect.height * 0.5f);

                Vector2 fishPos = inspectedFishRect.anchoredPosition;
                float normX = Mathf.Clamp(fishPos.x / halfW, -1f, 1f);
                float normY = Mathf.Clamp(fishPos.y / halfH, -1f, 1f);

                // Clamp camera pan inside background sprite limits
                float targetCamX = defaultCam2DPosition.x + normX * 75f;
                float targetCamY = defaultCam2DPosition.y + normY * 42f;

                Vector3 camPos = cam2D.transform.position;
                camPos.x = Mathf.Lerp(camPos.x, targetCamX, dt);
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, dt);
                cam2D.transform.position = camPos;
            }
        }
        else if (isReturningToOverview)
        {
            float dt = Time.deltaTime * followSmoothSpeed;
            bool done = true;

            if (cam2D != null)
            {
                cam2D.orthographicSize = Mathf.Lerp(cam2D.orthographicSize, fullAquariumOrthoSize, dt);
                Vector3 camPos = cam2D.transform.position;
                camPos.x = Mathf.Lerp(camPos.x, defaultCam2DPosition.x, dt);
                camPos.y = Mathf.Lerp(camPos.y, defaultCam2DPosition.y, dt);
                cam2D.transform.position = camPos;

                if (Mathf.Abs(cam2D.orthographicSize - fullAquariumOrthoSize) > 0.15f)
                    done = false;
            }

            if (aquariumArea != null)
            {
                aquariumArea.localScale = Vector3.Lerp(aquariumArea.localScale, Vector3.one, dt);
                aquariumArea.anchoredPosition = Vector2.Lerp(aquariumArea.anchoredPosition, Vector2.zero, dt);

                if (aquariumArea.anchoredPosition.sqrMagnitude > 1f ||
                    Mathf.Abs(aquariumArea.localScale.x - 1f) > 0.01f)
                {
                    done = false;
                }
            }

            if (done)
            {
                if (cam2D != null)
                {
                    cam2D.orthographicSize = fullAquariumOrthoSize;
                    Vector3 camPos = cam2D.transform.position;
                    camPos.x = defaultCam2DPosition.x;
                    camPos.y = defaultCam2DPosition.y;
                    cam2D.transform.position = camPos;
                }
                if (aquariumArea != null)
                {
                    aquariumArea.localScale = Vector3.one;
                    aquariumArea.anchoredPosition = Vector2.zero;
                }
                isReturningToOverview = false;
            }
        }
    }

    private void ShowSearchFeedback(string message)
    {
        if (searchFeedbackText == null)
        {
            Debug.LogWarning($"[BigAquariumSearch] {message}");
            return;
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
        }
        feedbackCoroutine = StartCoroutine(ShowFeedbackRoutine(message));
    }

    private IEnumerator ShowFeedbackRoutine(string message)
    {
        searchFeedbackText.text = message;
        searchFeedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(2.5f);
        searchFeedbackText.gameObject.SetActive(false);
        feedbackCoroutine = null;
    }
}
