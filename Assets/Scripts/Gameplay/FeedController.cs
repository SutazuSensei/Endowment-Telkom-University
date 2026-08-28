using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the Feed Button UI event, validates cooldown / spam prevention, and bridges food spawning
/// with the fish movement system.
///
/// Designed with separation of responsibilities so a backend service can later be inserted
/// before SpawnAndFeed() without refactoring FishMovement or FoodController.
/// </summary>
public class FeedController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Reference to the FishMovement component in the aquarium.")]
    [SerializeField] private FishMovement fishMovement;

    [Tooltip("The UI prefab for the food object (must have FoodController attached).")]
    [SerializeField] private GameObject foodPrefab;

    [Tooltip("The aquarium RectTransform where food will spawn and fall.")]
    [SerializeField] private RectTransform aquariumArea;

    [Tooltip("The Feed UI Button.")]
    [SerializeField] private Button feedButton;

    private FoodController currentActiveFood;

    private void Start()
    {
        if (fishMovement == null)
        {
            fishMovement = FindFirstObjectByType<FishMovement>();
        }

        if (aquariumArea == null && fishMovement != null && fishMovement.transform.parent != null)
        {
            aquariumArea = fishMovement.transform.parent as RectTransform;
        }

        if (feedButton != null)
        {
            feedButton.onClick.AddListener(OnFeedButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (feedButton != null)
        {
            feedButton.onClick.RemoveListener(OnFeedButtonClicked);
        }
    }

    /// <summary>
    /// Triggered when the Feed button is clicked.
    /// Can be wired up via script or Unity Inspector OnClick event.
    /// </summary>
    public void OnFeedButtonClicked()
    {
        // For local prototype: prevent spawning multiple foods if one is already falling/active.
        // In future backend integration, this method will call backend API to validate cooldown
        // before executing SpawnAndFeed().
        if (currentActiveFood != null && currentActiveFood.IsActive)
        {
            Debug.Log("[FeedController] Food is already active in the aquarium. Wait until the fish eats it.");
            return;
        }

        SpawnAndFeed();
    }

    /// <summary>
    /// Spawns the food item and instructs the fish to target it.
    /// </summary>
    private void SpawnAndFeed()
    {
        if (foodPrefab == null || aquariumArea == null || fishMovement == null)
        {
            Debug.LogWarning("[FeedController] Missing reference(s). Check Inspector for FoodPrefab, AquariumArea, or FishMovement.");
            return;
        }

        GameObject foodObj = Instantiate(foodPrefab, aquariumArea);
        foodObj.transform.SetAsLastSibling();

        FoodController food = foodObj.GetComponent<FoodController>();
        if (food == null)
        {
            food = foodObj.AddComponent<FoodController>();
        }

        food.Spawn(aquariumArea);
        currentActiveFood = food;

        // Notify fish movement to override its target toward the food
        fishMovement.SetFoodTarget(food);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFoodDrop();
        }

        Debug.Log("[FeedController] Food spawned and fish notified.");
    }
}
