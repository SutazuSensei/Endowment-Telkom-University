using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the visual lifecycle and falling physics of a food item inside the aquarium UI.
/// Responsible only for spawn positioning, downward falling motion, bottom collision, and destruction when eaten.
/// </summary>
public class FoodController : MonoBehaviour
{
    [Header("Falling Settings")]
    [Tooltip("How fast the food falls downwards (pixels per second).")]
    [SerializeField] private float fallSpeed = 50f;

    [Tooltip("Padding from the top edge of the aquarium where food spawns (pixels).")]
    [SerializeField] private float spawnTopPadding = 40f;

    [Tooltip("Padding from the bottom edge of the aquarium where food stops falling (pixels).")]
    [SerializeField] private float bottomPadding = 40f;

    [Tooltip("Horizontal padding from aquarium walls so food doesn't spawn too close to edges (pixels).")]
    [SerializeField] private float horizontalPadding = 50f;

    private RectTransform foodRect;
    private float targetBottomY;
    private bool hasLanded = false;

    public Vector2 AnchoredPosition => foodRect != null ? foodRect.anchoredPosition : Vector2.zero;
    public bool IsActive => gameObject != null && gameObject.activeInHierarchy;

    private void Awake()
    {
        foodRect = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Spawns the food at the top of the given aquarium area with a random X coordinate.
    /// </summary>
    public void Spawn(RectTransform swimArea)
    {
        if (foodRect == null)
        {
            foodRect = GetComponent<RectTransform>();
        }

        Rect area = swimArea.rect;
        float randomX = Random.Range(area.xMin + horizontalPadding, area.xMax - horizontalPadding);
        float startY = area.yMax - spawnTopPadding;
        targetBottomY = area.yMin + bottomPadding;

        foodRect.anchoredPosition = new Vector2(randomX, startY);
        hasLanded = false;
    }

    private void Update()
    {
        if (hasLanded || foodRect == null) return;

        Vector2 pos = foodRect.anchoredPosition;
        pos.y -= fallSpeed * Time.deltaTime;

        if (pos.y <= targetBottomY)
        {
            pos.y = targetBottomY;
            hasLanded = true;
        }

        foodRect.anchoredPosition = pos;
    }

    /// <summary>
    /// Called by FishMovement when the fish reaches the food and begins eating.
    /// </summary>
    public void OnEaten()
    {
        Destroy(gameObject);
    }
}
