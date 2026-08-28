using UnityEngine;

/// <summary>
/// Controls the natural swimming movement of a single fish within a UI-based aquarium.
/// Attach this to the fish RectTransform that is a child of the aquarium RectTransform.
///
/// The fish cycles through five states:
///   Swimming → Decelerating → Idle → Accelerating → Swimming
///                           ↘ Eating ↗
/// with smooth speed transitions controlled by explicit acceleration / deceleration rates.
/// Perlin-noise wobble, horizontal flipping, optional tilt, and idle bobbing are layered on top.
/// </summary>
public class FishMovement : MonoBehaviour
{
    // ───────────────────────────── State Machine ─────────────────────────────

    private enum FishState
    {
        Swimming,       // Cruising toward target at full speed
        Decelerating,   // Approaching target — gradually gliding to a stop
        Idle,           // Stationary with gentle vertical bob
        Accelerating,   // Leaving idle — ramping speed up from zero
        Eating          // Paused to eat a food item with gentle vertical bob
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

    // ───────────────────────────── Unity Lifecycle ─────────────────────────────

    private void Awake()
    {
        fishRect = GetComponent<RectTransform>();

        if (swimArea == null)
        {
            swimArea = transform.parent as RectTransform;
        }

        perlinSeed = Random.Range(0f, 1000f);
        float initialFacing = fishRect.localScale.x >= 0f ? 1f : -1f;
        targetFacing = invertSpriteFacing ? -initialFacing : initialFacing;
    }

    private void Start()
    {
        if (applyFishSize)
        {
            fishRect.sizeDelta = fishSize;
        }

        basePosition = fishRect.anchoredPosition;
        ClampBasePosition();
        fishRect.anchoredPosition = basePosition;

        PickNewTarget();
        currentSpeed = targetSpeed;
        state = FishState.Swimming;
    }

    private void Update()
    {
        UpdateFoodTargetTracking();

        switch (state)
        {
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
        }

        UpdateFlip();
        UpdateTilt();
    }

    // ───────────────────────────── Food Tracking & Mouth Position ─────────────────────────────

    /// <summary>
    /// Returns the world/anchored position of the fish's mouth, accounting for facing direction.
    /// </summary>
    private Vector2 GetMouthPosition()
    {
        return basePosition + new Vector2(mouthOffset.x * targetFacing, mouthOffset.y);
    }

    private void UpdateFoodTargetTracking()
    {
        if (targetFood != null)
        {
            if (targetFood.IsActive)
            {
                // Always target the true food coordinates so the fish never moves in the wrong direction
                targetPosition = targetFood.AnchoredPosition;
            }
            else
            {
                // Food disappeared or was destroyed externally -> resume random target
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

        targetFood = food;
        targetSpeed = moveSpeedMax;
        targetPosition = food.AnchoredPosition;

        // Always switch to Accelerating/Swimming so the fish actively pursues the food without slowing down
        if (state == FishState.Idle || state == FishState.Eating || state == FishState.Decelerating)
        {
            state = FishState.Accelerating;
        }
    }

    /// <summary>
    /// Checks whether the mouth of the fish has reached the food item.
    /// </summary>
    private bool CheckFoodArrival()
    {
        if (targetFood != null && targetFood.IsActive)
        {
            float mouthDistance = (GetMouthPosition() - targetFood.AnchoredPosition).magnitude;
            float centerDistance = (basePosition - targetFood.AnchoredPosition).magnitude;

            // Arrived if the mouth touches the food OR if the food dropped onto the fish's body/tail
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

    /// <summary>
    /// Cruising state. The fish maintains or ramps toward its target speed and checks
    /// whether it has entered the slow-down zone around its waypoint.
    /// </summary>
    private void UpdateSwimming()
    {
        // Ensure speed is at cruising level (covers transition from Accelerating)
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Immediate food arrival check from mouth or body position
        if (CheckFoodArrival()) return;

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        // If chasing food, do NOT enter Decelerating state — keep swimming at full speed until food is caught!
        if (targetFood == null && distance <= slowDownDistance)
        {
            state = FishState.Decelerating;
            UpdateDecelerating();
            return;
        }

        MoveAndWobble(toTarget, distance);
    }

    // ───────────────────────────── Decelerating ─────────────────────────────

    /// <summary>
    /// The fish glides to a stop as it approaches the waypoint. Speed is scaled
    /// proportionally to the remaining distance within the slow-down zone, and the
    /// rate of change is capped by the deceleration / acceleration parameters so the
    /// transition is always smooth.
    /// </summary>
    private void UpdateDecelerating()
    {
        // Check food arrival
        if (CheckFoodArrival()) return;

        // If targetFood was set while decelerating, switch immediately to Swimming so we don't slow down
        if (targetFood != null && targetFood.IsActive)
        {
            state = FishState.Swimming;
            UpdateSwimming();
            return;
        }

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        // Desired speed scales linearly: targetSpeed at slowDownDistance → 0 at arrivalThreshold
        float range = slowDownDistance - arrivalThreshold;
        float distanceRatio = (range > 0.01f)
            ? Mathf.Clamp01((distance - arrivalThreshold) / range)
            : 0f;
        float desiredSpeed = targetSpeed * distanceRatio;

        // Use the appropriate rate depending on whether we need to speed up or slow down.
        // (speed up can happen if the fish entered the zone while still accelerating)
        float rate = (currentSpeed > desiredSpeed) ? deceleration : acceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, rate * Time.deltaTime);

        // Normal waypoint arrival: speed has effectively reached zero — the fish has glided to a stop
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

    /// <summary>
    /// The fish ramps speed up from zero after leaving idle (or after a no-idle stop).
    /// Once cruising speed is reached the state transitions to Swimming.
    /// </summary>
    private void UpdateAccelerating()
    {
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        // Immediate food arrival check from mouth or body position
        if (CheckFoodArrival()) return;

        Vector2 toTarget = targetPosition - basePosition;
        float distance = toTarget.magnitude;

        // If chasing food, do NOT enter Decelerating state — keep accelerating until cruising speed or caught!
        if (targetFood == null && distance <= slowDownDistance)
        {
            state = FishState.Decelerating;
        }
        // Otherwise, once near cruising speed, enter normal swimming
        else if (currentSpeed >= targetSpeed * 0.95f)
        {
            currentSpeed = targetSpeed;
            state = FishState.Swimming;
        }

        MoveAndWobble(toTarget, distance);
    }

    // ───────────────────────────── Idle & Eating ─────────────────────────────

    /// <summary>
    /// The fish is stationary and bobs gently. The bob fades in over 0.3 s at the
    /// start and fades out over the last 0.3 s for seamless transitions.
    /// </summary>
    private void UpdateIdle()
    {
        idleTimer -= Time.deltaTime;

        // Bob using elapsed time since idle started so the oscillation always begins at 0.
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

    /// <summary>
    /// The fish pauses to eat after reaching the food.
    /// Uses seamless bobbing and smoothly transitions back to Accelerating toward a new target.
    /// </summary>
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
            // No idle — pick a new target and start accelerating immediately
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
            Debug.LogWarning("[FishMovement] Swim area is too small for the fish. Check aquarium size, fish size, or boundary padding.");
            return;
        }

        targetPosition = new Vector2(
            Random.Range(bounds.xMin, bounds.xMax),
            Random.Range(bounds.yMin, bounds.yMax)
        );

        targetSpeed = Random.Range(moveSpeedMin, moveSpeedMax);
    }

    // ───────────────────────────── Shared Movement + Wobble ─────────────────────────────

    /// <summary>
    /// Advances the fish toward the target and applies Perlin-noise wobble.
    /// Wobble amplitude is proportional to speed ratio so it fades naturally
    /// during acceleration and deceleration — no separate fade logic needed.
    /// Shared by Swimming, Decelerating, and Accelerating states.
    /// </summary>
    private void MoveAndWobble(Vector2 toTarget, float distance)
    {
        Vector2 direction = (distance > 0.01f) ? toTarget / distance : lastMoveDirection;
        lastMoveDirection = direction;

        // Advance logical position toward target
        basePosition = Vector2.MoveTowards(basePosition, targetPosition, currentSpeed * Time.deltaTime);

        // Wobble perpendicular to movement direction, scaled by speed ratio
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
                // When pursuing food, always face directly toward the food item
                float foodHorizontalDiff = targetFood.AnchoredPosition.x - basePosition.x;
                if (Mathf.Abs(foodHorizontalDiff) > flipDeadZone)
                {
                    float rawFacing = foodHorizontalDiff > 0f ? 1f : -1f;
                    targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
                }
            }
            else
            {
                // Normal waypoint flipping
                float horizontalDiff = targetPosition.x - basePosition.x;
                if (Mathf.Abs(horizontalDiff) > flipDeadZone)
                {
                    float rawFacing = horizontalDiff > 0f ? 1f : -1f;
                    targetFacing = invertSpriteFacing ? -rawFacing : rawFacing;
                }
            }
        }

        // Smoothly interpolate scale.x toward the target facing
        Vector3 scale = fishRect.localScale;
        scale.x = Mathf.Lerp(scale.x, targetFacing, flipSpeed * Time.deltaTime);
        fishRect.localScale = scale;
    }

    // ───────────────────────────── Tilt ─────────────────────────────

    private void UpdateTilt()
    {
        float targetTilt = 0f;

        if (state != FishState.Idle && state != FishState.Eating && lastMoveDirection.sqrMagnitude > 0.001f)
        {
            // Tilt proportionally to the vertical component of movement
            targetTilt = lastMoveDirection.y * maxTiltAngle;

            // When the fish faces left (scale.x flipped), the Z-rotation visual is
            // mirrored, so invert the tilt to keep the nose pointing in the swim direction.
            if (targetFacing < 0f)
            {
                targetTilt = -targetTilt;
            }
        }

        currentTiltAngle = Mathf.Lerp(currentTiltAngle, targetTilt, tiltSpeed * Time.deltaTime);
        fishRect.localRotation = Quaternion.Euler(0f, 0f, currentTiltAngle);
    }

    // ───────────────────────────── Boundary ─────────────────────────────

    /// <summary>
    /// Returns the safe swim area in local coordinates of the swim area RectTransform,
    /// inset by the fish's half-size and the extra padding.
    /// </summary>
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

    /// <summary>
    /// Clamps <see cref="basePosition"/> within the swim bounds.
    /// Called once at Start to ensure the fish begins inside the aquarium.
    /// </summary>
    private void ClampBasePosition()
    {
        Rect bounds = GetSwimBounds();
        if (bounds.width <= 0f || bounds.height <= 0f) return;

        basePosition.x = Mathf.Clamp(basePosition.x, bounds.xMin, bounds.xMax);
        basePosition.y = Mathf.Clamp(basePosition.y, bounds.yMin, bounds.yMax);
    }

    /// <summary>
    /// Sets the fish RectTransform position, clamped within the swim bounds as a safety net.
    /// </summary>
    private void ApplyPosition(Vector2 position)
    {
        Rect bounds = GetSwimBounds();
        if (bounds.width > 0f && bounds.height > 0f)
        {
            position.x = Mathf.Clamp(position.x, bounds.xMin, bounds.xMax);
            position.y = Mathf.Clamp(position.y, bounds.yMin, bounds.yMax);
        }

        fishRect.anchoredPosition = position;
    }
}
