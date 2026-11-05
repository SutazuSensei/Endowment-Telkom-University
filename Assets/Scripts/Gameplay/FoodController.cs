using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the visual lifecycle, wriggling animation, and falling physics of the primary food item (Cacing Ijo).
/// Responsible for:
/// - Looping frame-by-frame animation of the wriggling worm.
/// - Spawn positioning with gentle horizontal drift while falling.
/// - Downward sinking motion and seabed collision.
/// - Destruction when consumed by the fish.
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

    [Header("Worm Animation Settings")]
    [Tooltip("Animated sprite frames of the wriggling green worm (Cacing Ijo).")]
    [SerializeField] private Sprite[] animationFrames;

    [Tooltip("Animation speed in frames per second.")]
    [SerializeField] private float frameRate = 24f;

    [Tooltip("Target UI Image displaying the worm.")]
    [SerializeField] private Image targetImage;

    [Tooltip("Subtle horizontal sway amplitude while sinking.")]
    [SerializeField] private float swayAmplitude = 18f;

    [Tooltip("Subtle horizontal sway frequency while sinking.")]
    [SerializeField] private float swayFrequency = 2.5f;

    private RectTransform foodRect;
    private float targetBottomY;
    private bool hasLanded = false;
    private float baseX;
    private float spawnTime;
    private float animTimer;
    private int currentFrameIndex = 0;

    public Vector2 AnchoredPosition => foodRect != null ? foodRect.anchoredPosition : Vector2.zero;
    public bool IsActive => gameObject != null && gameObject.activeInHierarchy;

    private void Awake()
    {
        foodRect = GetComponent<RectTransform>();
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (targetImage != null)
        {
            targetImage.preserveAspect = true;
            targetImage.color = Color.white;
            if (animationFrames != null && animationFrames.Length > 0)
            {
                targetImage.sprite = animationFrames[0];
            }
        }
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
        baseX = Random.Range(area.xMin + horizontalPadding, area.xMax - horizontalPadding);
        float startY = area.yMax - spawnTopPadding;
        targetBottomY = area.yMin + bottomPadding;

        foodRect.anchoredPosition = new Vector2(baseX, startY);
        hasLanded = false;
        spawnTime = Time.time;
        currentFrameIndex = Random.Range(0, Mathf.Max(1, animationFrames != null ? animationFrames.Length : 1));
    }

    private void Update()
    {
        UpdateAnimation();

        if (hasLanded || foodRect == null) return;

        Vector2 pos = foodRect.anchoredPosition;
        pos.y -= fallSpeed * Time.deltaTime;

        // Gentle horizontal sway while sinking down
        float elapsed = Time.time - spawnTime;
        pos.x = baseX + Mathf.Sin(elapsed * swayFrequency) * swayAmplitude;

        if (pos.y <= targetBottomY)
        {
            pos.y = targetBottomY;
            hasLanded = true;
        }

        foodRect.anchoredPosition = pos;
    }

    private void UpdateAnimation()
    {
        if (animationFrames == null || animationFrames.Length == 0 || targetImage == null) return;

        animTimer += Time.deltaTime;
        float frameInterval = 1f / Mathf.Max(1f, frameRate);

        if (animTimer >= frameInterval)
        {
            animTimer -= frameInterval;
            currentFrameIndex = (currentFrameIndex + 1) % animationFrames.Length;
            targetImage.sprite = animationFrames[currentFrameIndex];
        }
    }

    /// <summary>
    /// Assigns animation frames at runtime or editor time.
    /// </summary>
    public void SetAnimationFrames(Sprite[] frames, float fps = 24f)
    {
        animationFrames = frames;
        frameRate = fps;
        if (targetImage != null && animationFrames != null && animationFrames.Length > 0)
        {
            targetImage.sprite = animationFrames[0];
        }
    }

    /// <summary>
    /// Called by FishMovement when the fish reaches the food and begins eating.
    /// </summary>
    public void OnEaten()
    {
        Destroy(gameObject);
    }
}
