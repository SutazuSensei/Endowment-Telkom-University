using UnityEngine;

/// <summary>
/// Controls the natural swimming movement of a single fish within a UI-based aquarium.
/// Attach this to the fish RectTransform that is a child of the aquarium RectTransform.
///
/// The fish cycles through states:
///   Entering → Swimming → Decelerating → Idle → Accelerating → Swimming → Exiting
///                                      ↘ Eating ↗
/// with smooth speed transitions controlled by explicit acceleration / deceleration rates.
/// Also enforces a personal repulsion boundary around any currently inspected fish in Big Aquarium
/// so other fish never swim in front of or obstruct the inspected fish.
/// </summary>
public class FishMovement : MonoBehaviour
{
    // ───────────────────────────── State Machine ─────────────────────────────

    private enum FishState
    {
        Entering,       // Swimming into the aquarium from outside the boundary
        Swimming,       // Cruising toward target at full speed
        Decelerating,   // Approaching target — gradually gliding to a stop
        Idle,           // Stationary with gentle vertical bob
        Accelerating,   // Leaving idle — ramping speed up from zero
        Eating,         // Paused to eat a food item with gentle vertical bob
        Exiting         // Swimming out of the aquarium beyond the boundary to be destroyed
    }

    // ───────────────────────────── Inspector Parameters ─────────────────────────────

    [Header("Movement")]
    [Tooltip("Minimum cruising speed in pixels per second.")]
    [SerializeField] private float moveSpeedMin = 80f;

    [Tooltip("Maximum cruising speed in pixels per second.")]
    [SerializeField] private float moveSpeedMax = 180f;

    [Tooltip("How quickly the fish gains speed (px/s²).")]
    [SerializeField] private float acceleration = 120f;

    [Tooltip("How quickly the fish loses speed when gliding to a stop (px/s²).")]
    [SerializeField] private float deceleration = 150f;

    [Tooltip("Distance (px) at which the fish is considered to have arrived at its target.")]
    [SerializeField] private float arrivalThreshold = 15f;

    [Tooltip("Distance (px) from the target at which the fish begins to slow down.")]
    [SerializeField] private float slowDownDistance = 100f;

    [Header("Feeding")]
    [Tooltip("How long the fish pauses to eat when reaching food (seconds).")]
    [SerializeField] private float eatingDuration = 1.5f;

    [Tooltip("Horizontal & vertical offset of the fish's mouth from its RectTransform pivot/center (pixels). X is mirrored automatically when facing left.")]
    [SerializeField] private Vector2 mouthOffset = new Vector2(105f, 0f);

    [Tooltip("Distance (px) from the fish's mouth to the food item at which eating begins.")]
    [SerializeField] private float foodArrivalThreshold = 30f;

    [Header("Swimming Feel")]
    [Tooltip("How far the fish wobbles perpendicular to its path (pixels).")]
    [SerializeField] private float wobbleAmplitude = 20f;

    [Tooltip("How quickly the wobble oscillates. Higher = faster shimmy.")]
    [SerializeField] private float wobbleFrequency = 1.2f;

    [Header("Idle Behavior")]
    [Tooltip("Minimum idle duration in seconds.")]
    [SerializeField] private float idleDurationMin = 0.5f;

    [Tooltip("Maximum idle duration in seconds.")]
    [SerializeField] private float idleDurationMax = 2.5f;

    [Tooltip("Probability of pausing after reaching a waypoint (0 = never, 1 = always).")]
    [SerializeField, Range(0f, 1f)] private float idleChance = 0.35f;

    [Tooltip("Vertical bobbing amplitude while idle (pixels).")]
    [SerializeField] private float idleBobAmplitude = 3f;

    [Tooltip("Vertical bobbing speed while idle.")]
    [SerializeField] private float idleBobSpeed = 1.5f;

    [Header("Turning")]
    [Tooltip("Check this if the original sprite asset is drawn facing left instead of right.")]
    [SerializeField] private bool invertSpriteFacing = false;

    [Tooltip("How fast the fish smoothly flips horizontally (higher = snappier).")]
    [SerializeField] private float flipSpeed = 5f;

    [Tooltip("Minimum horizontal distance to target before the fish flips direction. Prevents flickering on near-vertical swims.")]
    [SerializeField] private float flipDeadZone = 10f;

    [Header("Tilt")]
    [Tooltip("Maximum rotation angle when swimming up or down (degrees). Set to 0 to disable.")]
    [SerializeField] private float maxTiltAngle = 0f;

    [Tooltip("How quickly the tilt angle interpolates (higher = more responsive).")]
    [SerializeField] private float tiltSpeed = 3f;

    [Header("Boundary")]
    [Tooltip("The RectTransform defining the swim area. If empty, uses the parent RectTransform.")]
    [SerializeField] private RectTransform swimArea;

    [Tooltip("Extra padding from aquarium edges so the fish does not touch the border (pixels).")]
    [SerializeField] private float boundaryPadding = 20f;

    [Header("Inspection Boundary (Safe Zone)")]
    [Tooltip("Radius (in UI pixels) around an inspected fish that other fish are not allowed to enter.")]
    [SerializeField] private float inspectionBoundaryRadius = 320f;

    [Tooltip("How strongly other fish steer away when near the inspected fish's boundary.")]
    [SerializeField] private float boundaryRepulsionSpeed = 240f;

    [Header("Fish Size")]
    [Tooltip("The desired fish size in pixels. The aspect ratio of the original sprite is ~1.87:1.")]
    [SerializeField] private Vector2 fishSize = new Vector2(250f, 134f);

    [Tooltip("Apply fishSize on Start. Disable if sizing is handled elsewhere (e.g. animation or layout).")]
    [SerializeField] private bool applyFishSize = true;

    // ───────────────────────────── Internal State ─────────────────────────────

    private RectTransform fishRect;
    private FishState state = FishState.Swimming;

    /// <summary>
    /// The "logical" position on the straight-line path toward the target.
    /// Wobble / bob is applied on top of this so it never drifts.
    /// </summary>
    private Vector2 basePosition;

    private Vector2 targetPosition;
    private float currentSpeed;
    private float targetSpeed;

    private float perlinSeed;

    /// <summary>Target facing direction: 1 = right, -1 = left.</summary>
    private float targetFacing = 1f;

    private float currentTiltAngle;
    private Vector2 lastMoveDirection;

    private float idleTimer;
    private float idleStartTime;

    private FoodController targetFood;
    private float eatingTimer;
    private float currentHeadingX = 1f;

    private bool hasCustomEntry = false;
    private System.Action onExitCallback;
    private CircleCollider2D boundaryCircleCollider;

    /// <summary>
    /// The fish currently being inspected/followed by the camera in Big Aquarium.
    /// All other fish will respect its boundary radius and steer away.
    /// </summary>
    public static FishMovement InspectedFish { get; set; }

    public Vector2 BasePosition => basePosition;
    public float InspectionBoundaryRadius => (boundaryCircleCollider != null && boundaryCircleCollider.radius > 10f)
        ? boundaryCircleCollider.radius
        : inspectionBoundaryRadius;
    public bool IsExiting => state == FishState.Exiting;
    public bool IsEntering => state == FishState.Entering;

    public float TargetFacing => targetFacing;
    public float HeadingX => currentHeadingX;
    public float NormalizedSpeed => (moveSpeedMax > 0.01f) ? Mathf.Clamp01(currentSpeed / moveSpeedMax) : 0f;

    // ───────────────────────────── Unity Lifecycle ─────────────────────────────

    private void Awake()
    {
        EnsureComponents();

        perlinSeed = Random.Range(0f, 1000f);
        float initialFacing = fishRect.localScale.x >= 0f ? 1f : -1f;
        currentHeadingX = initialFacing;
        targetFacing = invertSpriteFacing ? -initialFacing : initialFacing;
    }

    private void EnsureComponents()
    {
        if (fishRect == null)
        {
            fishRect = GetComponent<RectTransform>();
        }

        if (boundaryCircleCollider == null)
        {
            boundaryCircleCollider = GetComponent<CircleCollider2D>();
        }

        if (swimArea == null && transform.parent != null)
        {
            swimArea = transform.parent as RectTransform;
        }
    }

    private void Start()
    {
        EnsureComponents();

        if (applyFishSize)
        {
            fishRect.sizeDelta = fishSize;
        }

        if (!hasCustomEntry && state != FishState.Entering && state != FishState.Exiting)
        {
            basePosition = fishRect.anchoredPosition;
            ClampBasePosition();
            fishRect.anchoredPosition = basePosition;

            PickNewTarget();
            currentSpeed = targetSpeed;
            state = FishState.Swimming;
        }
    }

    private void OnDestroy()
    {
        if (InspectedFish == this)
        {
            InspectedFish = null;
        }
    }

    private void Update()
    {
        if (state != FishState.Entering && state != FishState.Exiting)
        {
            UpdateFoodTargetTracking();
            EnforceInspectionBoundary();
        }

        switch (state)
        {
            case FishState.Entering:
                UpdateEntering();
                break;
            case FishState.Swimming:
                UpdateSwimming();
                break;
            case FishState.Decelerating:
                UpdateDecelerating();
                break;
            case FishState.Idle:
                UpdateIdle();
                break;
            case FishState.Accelerating:
                UpdateAccelerating();
                break;
            case FishState.Eating:
                UpdateEating();
                break;
            case FishState.Exiting:
                UpdateExiting();
                break;
        }

        UpdateFlip();
        UpdateTilt();
    }

    // ───────────────────────────── Enter / Exit (Rotation) ─────────────────────────────

    /// <summary>
    /// Spawns the fish outside the aquarium boundary and makes it swim naturally into the aquarium.
    /// </summary>
    public void EnterFromOutside(Vector2 outsideStartPos, Vector2 insideTargetPos)
    {
        EnsureComponents();
        hasCustomEntry = true;

        basePosition = outsideStartPos;
        fishRect.anchoredPosition = outsideStartPos;

        targetPosition = insideTargetPos;
        targetSpeed = moveSpeedMax * 1.15f;
        currentSpeed = targetSpeed * 0.9f;
        state = FishState.Entering;

        float horizontalDiff = targetPosition.x - basePosition.x;
        float rawFacing = horizontalDiff >= 0f ? 1f : -1f;
        currentHeadingX = rawFacing;
        targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
    }

    /// <summary>
    /// Instructs the fish to swim out past the nearest horizontal aquarium boundary and destroy itself.
    /// </summary>
    public void SwimOutAndDestroy(System.Action onExited = null)
    {
        EnsureComponents();
        onExitCallback = onExited;
        targetFood = null;
        state = FishState.Exiting;

        Rect area = swimArea != null ? swimArea.rect : new Rect(-960f, -540f, 1920f, 1080f);
        float exitMargin = Mathf.Max(320f, fishRect.sizeDelta.x * 1.5f);

        // Swim out toward the closer side (left or right outside collider)
        float exitX = (basePosition.x >= 0f) ? (area.xMax + exitMargin) : (area.xMin - exitMargin);
        float exitY = Mathf.Clamp(basePosition.y + Random.Range(-90f, 90f), area.yMin + 80f, area.yMax - 80f);

        targetPosition = new Vector2(exitX, exitY);
        targetSpeed = moveSpeedMax * 1.35f;
        currentSpeed = Mathf.Max(currentSpeed, moveSpeedMin);

        float horizontalDiff = targetPosition.x - basePosition.x;
        float rawFacing = horizontalDiff >= 0f ? 1f : -1f;
        currentHeadingX = rawFacing;
        targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
    }

    private void UpdateEntering()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * 1.5f * Time.deltaTime);

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        MoveAndWobble(toTarget, distance);

        if (distance <= arrivalThreshold * 2.5f)
        {
            // Successfully entered the aquarium — switch to normal swimming inside bounds
            state = FishState.Swimming;
            ClampBasePosition();
            PickNewTarget();
        }
    }

    private void UpdateExiting()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * 1.8f * Time.deltaTime);

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        MoveAndWobble(toTarget, distance);

        if (distance <= 35f)
        {
            onExitCallback?.Invoke();
            onExitCallback = null;
            Destroy(gameObject);
        }
    }

    // ───────────────────────────── Inspection Boundary Repulsion ─────────────────────────────

    /// <summary>
    /// Ensures non-inspected fish never enter the personal boundary of the currently inspected fish,
    /// preventing other fish from overlapping or covering the inspected fish.
    /// </summary>
    private void EnforceInspectionBoundary()
    {
        if (InspectedFish == null || InspectedFish == this) return;

        Vector2 inspectedPos = InspectedFish.basePosition;
        float safeRadius = Mathf.Max(InspectionBoundaryRadius, InspectedFish.InspectionBoundaryRadius);

        Vector2 diff = basePosition - inspectedPos;
        float dist = diff.magnitude;

        if (dist < safeRadius)
        {
            Vector2 awayDir = (dist > 0.01f)
                ? (diff / dist)
                : new Vector2(currentHeadingX != 0f ? -currentHeadingX : 1f, 0.25f).normalized;

            float penetrationRatio = 1f - Mathf.Clamp01(dist / safeRadius);
            basePosition += awayDir * (boundaryRepulsionSpeed * (0.6f + penetrationRatio) * Time.deltaTime);

            // Redirect target waypoint outside the inspected fish's boundary
            Rect bounds = GetSwimBounds();
            Vector2 escapeTarget = inspectedPos + awayDir * (safeRadius + 240f);
            if (bounds.width > 0f && bounds.height > 0f)
            {
                escapeTarget.x = Mathf.Clamp(escapeTarget.x, bounds.xMin, bounds.xMax);
                escapeTarget.y = Mathf.Clamp(escapeTarget.y, bounds.yMin, bounds.yMax);

                // If clamped to wall right next to the inspected fish, escape vertically/horizontally to the opposite side
                if (Vector2.Distance(escapeTarget, inspectedPos) < safeRadius * 0.95f)
                {
                    escapeTarget.x = (inspectedPos.x >= 0f) ? bounds.xMin + 100f : bounds.xMax - 100f;
                }
            }

            targetPosition = escapeTarget;
            targetSpeed = moveSpeedMax;
            currentSpeed = Mathf.Max(currentSpeed, moveSpeedMin);

            if (state == FishState.Idle || state == FishState.Decelerating)
            {
                state = FishState.Swimming;
            }
        }
    }

    // ───────────────────────────── Food Tracking & Mouth Position ─────────────────────────────

    /// <summary>
    /// Returns the world/anchored position of the fish's mouth, accounting for facing direction.
    /// </summary>
    private Vector2 GetMouthPosition()
    {
        float heading = (GetComponent<Fish3DView>() != null) ? currentHeadingX : targetFacing;
        return basePosition + new Vector2(mouthOffset.x * heading, mouthOffset.y);
    }

    private void UpdateFoodTargetTracking()
    {
        if (targetFood != null)
        {
            if (targetFood.IsActive)
            {
                targetPosition = targetFood.AnchoredPosition;
            }
            else
            {
                targetFood = null;
                if (state == FishState.Swimming || state == FishState.Decelerating)
                {
                    PickNewTarget();
                    state = FishState.Accelerating;
                }
            }
        }
    }

    /// <summary>
    /// Instructs the fish to override its current random movement target and pursue the given food.
    /// </summary>
    public void SetFoodTarget(FoodController food)
    {
        if (food == null || !food.IsActive) return;
        if (state == FishState.Entering || state == FishState.Exiting) return;

        targetFood = food;
        targetSpeed = moveSpeedMax;
        targetPosition = food.AnchoredPosition;

        if (state == FishState.Idle || state == FishState.Eating || state == FishState.Decelerating)
        {
            state = FishState.Accelerating;
        }
    }

    private bool CheckFoodArrival()
    {
        if (targetFood != null && targetFood.IsActive)
        {
            float mouthDistance = (GetMouthPosition() - targetFood.AnchoredPosition).magnitude;
            float centerDistance = (basePosition - targetFood.AnchoredPosition).magnitude;

            if (mouthDistance <= foodArrivalThreshold || centerDistance <= (mouthOffset.x * 0.8f))
            {
                currentSpeed = 0f;
                ApplyPosition(basePosition);
                StartEating();
                return true;
            }
        }
        return false;
    }

    // ───────────────────────────── Swimming ─────────────────────────────

    private void UpdateSwimming()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        if (CheckFoodArrival()) return;

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        if (targetFood == null && distance <= slowDownDistance)
        {
            state = FishState.Decelerating;
            UpdateDecelerating();
            return;
        }

        MoveAndWobble(toTarget, distance);
    }

    // ───────────────────────────── Decelerating ─────────────────────────────

    private void UpdateDecelerating()
    {
        if (CheckFoodArrival()) return;

        if (targetFood != null && targetFood.IsActive)
        {
            state = FishState.Swimming;
            UpdateSwimming();
            return;
        }

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        float range = slowDownDistance - arrivalThreshold;
        float distanceRatio = (range > 0.01f)
            ? Mathf.Clamp01((distance - arrivalThreshold) / range)
            : 0f;
        float desiredSpeed = targetSpeed * distanceRatio;

        float rate = (currentSpeed > desiredSpeed) ? deceleration : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * Time.deltaTime);

        if (currentSpeed < 1f)
        {
            currentSpeed = 0f;
            ApplyPosition(basePosition);
            OnArrived();
            return;
        }

        MoveAndWobble(toTarget, distance);
    }

    // ───────────────────────────── Accelerating ─────────────────────────────

    private void UpdateAccelerating()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        if (CheckFoodArrival()) return;

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        if (targetFood == null && distance <= slowDownDistance)
        {
            state = FishState.Decelerating;
        }
        else if (currentSpeed >= targetSpeed * 0.95f)
        {
            currentSpeed = targetSpeed;
            state = FishState.Swimming;
        }

        MoveAndWobble(toTarget, distance);
    }

    // ───────────────────────────── Idle & Eating ─────────────────────────────

    private void UpdateIdle()
    {
        idleTimer -= Time.deltaTime;

        float elapsed = Time.time - idleStartTime;
        float fadeIn = Mathf.Clamp01(elapsed / 0.3f);
        float fadeOut = Mathf.Clamp01(idleTimer / 0.3f);
        float bobFade = fadeIn * fadeOut;

        float bob = Mathf.Sin(elapsed * idleBobSpeed) * idleBobAmplitude * bobFade;
        Vector2 idlePos = basePosition;
        idlePos.y += bob;
        ApplyPosition(idlePos);

        if (idleTimer <= 0f)
        {
            PickNewTarget();
            state = FishState.Accelerating;
        }
    }

    private void StartEating()
    {
        state = FishState.Eating;
        eatingTimer = eatingDuration;
        idleStartTime = Time.time;

        if (targetFood != null && targetFood.IsActive)
        {
            targetFood.OnEaten();
        }
        targetFood = null;
    }

    private void UpdateEating()
    {
        eatingTimer -= Time.deltaTime;

        float elapsed = Time.time - idleStartTime;
        float fadeIn = Mathf.Clamp01(elapsed / 0.3f);
        float fadeOut = Mathf.Clamp01(eatingTimer / 0.3f);
        float bobFade = fadeIn * fadeOut;

        float bob = Mathf.Sin(elapsed * idleBobSpeed) * idleBobAmplitude * bobFade;
        Vector2 eatingPos = basePosition;
        eatingPos.y += bob;
        ApplyPosition(eatingPos);

        if (eatingTimer <= 0f)
        {
            PickNewTarget();
            state = FishState.Accelerating;
        }
    }

    // ───────────────────────────── Arrival / Target Selection ─────────────────────────────

    private void OnArrived()
    {
        if (Random.value < idleChance)
        {
            StartIdle();
        }
        else
        {
            PickNewTarget();
            state = FishState.Accelerating;
        }
    }

    private void StartIdle()
    {
        state = FishState.Idle;
        idleTimer = Random.Range(idleDurationMin, idleDurationMax);
        idleStartTime = Time.time;
    }

    private void PickNewTarget()
    {
        Rect bounds = GetSwimBounds();

        if (bounds.width <= 0f || bounds.height <= 0f)
        {
            targetPosition = basePosition;
            targetSpeed = moveSpeedMin;
            return;
        }

        // Try up to 8 times to pick a target outside the inspected fish's boundary
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(bounds.xMin, bounds.xMax),
                Random.Range(bounds.yMin, bounds.yMax)
            );

            if (InspectedFish == null || InspectedFish == this ||
                Vector2.Distance(candidate, InspectedFish.basePosition) >= inspectionBoundaryRadius * 1.1f)
            {
                targetPosition = candidate;
                break;
            }

            targetPosition = candidate;
        }

        targetSpeed = Random.Range(moveSpeedMin, moveSpeedMax);
    }

    // ───────────────────────────── Shared Movement + Wobble ─────────────────────────────

    private void MoveAndWobble(Vector2 toTarget, float distance)
    {
        Vector2 direction = (distance > 0.01f) ? toTarget / distance : lastMoveDirection;
        lastMoveDirection = direction;

        basePosition = Vector2.MoveTowards(basePosition, targetPosition, currentSpeed * Time.deltaTime);

        float speedRatio = (targetSpeed > 0.01f) ? Mathf.Clamp01(currentSpeed / targetSpeed) : 0f;
        float perlinValue = Mathf.PerlinNoise(perlinSeed, Time.time * wobbleFrequency) * 2f - 1f;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 wobble = perpendicular * (perlinValue * wobbleAmplitude * speedRatio);

        ApplyPosition(basePosition + wobble);
    }

    // ───────────────────────────── Flipping ─────────────────────────────

    private void UpdateFlip()
    {
        if (state != FishState.Idle && state != FishState.Eating)
        {
            if (targetFood != null && targetFood.IsActive)
            {
                float foodHorizontalDiff = targetFood.AnchoredPosition.x - basePosition.x;
                if (Mathf.Abs(foodHorizontalDiff) > flipDeadZone)
                {
                    float rawFacing = foodHorizontalDiff > 0f ? 1f : -1f;
                    currentHeadingX = rawFacing;
                    targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
                }
            }
            else
            {
                float horizontalDiff = targetPosition.x - basePosition.x;
                if (Mathf.Abs(horizontalDiff) > flipDeadZone)
                {
                    float rawFacing = horizontalDiff > 0f ? 1f : -1f;
                    currentHeadingX = rawFacing;
                    targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
                }
            }
        }

        Vector3 scale = fishRect.localScale;
        if (GetComponent<Fish3DView>() != null) { fishRect.localScale = Vector3.one; return; }
        scale.x = Mathf.Lerp(scale.x, targetFacing, flipSpeed * Time.deltaTime);
        fishRect.localScale = scale;
    }

    // ───────────────────────────── Tilt ─────────────────────────────

    private void UpdateTilt()
    {
        float targetTilt = 0f;

        if (state != FishState.Idle && state != FishState.Eating && lastMoveDirection.sqrMagnitude > 0.001f)
        {
            targetTilt = lastMoveDirection.y * maxTiltAngle;

            if (targetFacing < 0f)
            {
                targetTilt = -targetTilt;
            }
        }

        if (GetComponent<Fish3DView>() != null) { fishRect.localRotation = Quaternion.identity; return; }
        currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTilt, tiltSpeed * Time.deltaTime);
        fishRect.localRotation = Quaternion.Euler(0f, 0f, currentTiltAngle);
    }

    // ───────────────────────────── Boundary ─────────────────────────────

    private Rect GetSwimBounds()
    {
        if (swimArea == null)
        {
            return new Rect(-500f, -250f, 1000f, 500f);
        }

        Rect area = swimArea.rect;

        float halfW = fishRect.sizeDelta.x * 0.5f;
        float halfH = fishRect.sizeDelta.y * 0.5f;
        float padX = halfW + boundaryPadding;
        float padY = halfH + boundaryPadding;

        return new Rect(
            area.xMin + padX,
            area.yMin + padY,
            area.width - padX * 2f,
            area.height - padY * 2f
        );
    }

    private void ClampBasePosition()
    {
        Rect bounds = GetSwimBounds();
        if (bounds.width <= 0f || bounds.height <= 0f) return;

        basePosition.x = Mathf.Clamp(basePosition.x, bounds.xMin, bounds.xMax);
        basePosition.y = Mathf.Clamp(basePosition.y, bounds.yMin, bounds.yMax);
    }

    /// <summary>
    /// Sets the fish RectTransform position.
    /// Clamped within swim bounds during normal swimming, and unclamped during Entering/Exiting so the fish can cross the aquarium boundary.
    /// </summary>
    private void ApplyPosition(Vector2 position)
    {
        if (state != FishState.Entering && state != FishState.Exiting)
        {
            Rect bounds = GetSwimBounds();
            if (bounds.width > 0f && bounds.height > 0f)
            {
                position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
                position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
            }
        }

        fishRect.anchoredPosition = position;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (InspectedFish == this)
        {
            Gizmos.color = new Color(0.15f, 0.95f, 1f, 0.65f);
            Vector3 lossy = transform.lossyScale;
            float worldRadius = inspectionBoundaryRadius * Mathf.Max(0.01f, Mathf.Abs(lossy.x));
            Gizmos.DrawWireSphere(transform.position, worldRadius);
        }
    }
#endif
}
