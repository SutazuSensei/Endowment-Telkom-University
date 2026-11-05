using UnityEngine;

public class RandomFishMovement : MonoBehaviour
{
    [Header("Relative Movement")]
    [SerializeField] float speedMultiplier = 1f;

    // Waktu dasar untuk menempuh
    // satu lebar kamera dari kiri ke kanan
    [SerializeField] float baseCrossTime = 10f;

    [Header("Direction Change")]
    [SerializeField] float minDirectionTime = 2f;
    [SerializeField] float maxDirectionTime = 5f;
    [SerializeField] float turnSpeed = 1.5f;

    [Header("Random Movement")]
    [SerializeField] float verticalMovement = 1f;
    [SerializeField] float verticalFrequency = 1f;

    [Header("Pause")]
    [SerializeField] float minPause = 0.5f;
    [SerializeField] float maxPause = 2f;

    [Range(0f, 1f)]
    [SerializeField] float pauseChance = 0.25f;

    [Header("Camera Boundary")]
    [SerializeField] Camera targetCamera;
    [SerializeField] float horizontalMargin = 0.5f;
    [SerializeField] float verticalMargin = 0.5f;

    [Header("Depth")]
    [SerializeField] float depthMovement = 0.5f;

    [Header("Fish Rotation")]
    [SerializeField] float sideRotation = 90f;
    [SerializeField] float randomRotation = 5f;
    [SerializeField] float rotationSpeed = 3f;

    [Header("Fish Animation")]
    [SerializeField] Animator animator;

    private Vector3 direction;
    private Vector3 targetDirection;

    private float currentSpeed;
    private float directionTimer;
    private float pauseTimer;

    private float verticalOffset;
    private float rotationOffset;

    private float startZ;
    private float targetZ;

    private float targetY;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator != null)
            animator.enabled = true;

        startZ = transform.position.z;
        targetZ = startZ;

        verticalOffset = Random.Range(0f, 100f);
        rotationOffset = Random.Range(0f, 100f);

        // Arah awal random
        direction =
            Random.insideUnitSphere;

        direction.z = 0f;
        direction.Normalize();

        targetDirection = direction;

        // Hitung kecepatan berdasarkan kamera
        currentSpeed = CalculateSpeed();

        directionTimer =
            Random.Range(
                minDirectionTime,
                maxDirectionTime
            );

        SetTargetRotation();
    }

    void Update()
    {
        if (targetCamera == null)
            return;

        // =========================================
        // PAUSE
        // =========================================

        if (pauseTimer > 0f)
        {
            pauseTimer -= Time.deltaTime;

            UpdateRotation();

            return;
        }

        // =========================================
        // DIRECTION TIMER
        // =========================================

        directionTimer -= Time.deltaTime;

        if (directionTimer <= 0f)
        {
            ChangeDirection();

            if (Random.value < pauseChance)
            {
                pauseTimer =
                    Random.Range(
                        minPause,
                        maxPause
                    );
            }
        }

        // =========================================
        // MOVEMENT
        // =========================================

        UpdateDirection();
        MoveFish();
        KeepInsideCamera();
        UpdateRotation();
    }

    // =========================================================
    // CALCULATE RELATIVE SPEED
    // =========================================================

    float CalculateSpeed()
    {
        float cameraWidth =
            GetCameraWidth();

        // Kecepatan agar ikan menempuh
        // satu lebar kamera dalam baseCrossTime
        float speed =
            cameraWidth /
            baseCrossTime;

        return speed *
               speedMultiplier;
    }

    // =========================================================
    // CAMERA WIDTH
    // =========================================================

    float GetCameraWidth()
    {
        if (targetCamera == null)
            return 10f;

        float height =
            targetCamera.orthographicSize * 2f;

        float width =
            height *
            targetCamera.aspect;

        return width;
    }

    // =========================================================
    // CHANGE DIRECTION
    // =========================================================

    void ChangeDirection()
    {
        Vector2 randomDirection =
            Random.insideUnitCircle.normalized;

        if (randomDirection.sqrMagnitude < 0.1f)
        {
            randomDirection =
                Random.insideUnitCircle.normalized;
        }

        targetDirection =
            new Vector3(
                randomDirection.x,
                randomDirection.y,
                0f
            ).normalized;

        // Kecepatan selalu dihitung
        // berdasarkan ukuran kamera
        currentSpeed =
            CalculateSpeed();

        directionTimer =
            Random.Range(
                minDirectionTime,
                maxDirectionTime
            );

        targetZ =
            startZ +
            Random.Range(
                -depthMovement,
                depthMovement
            );

        SetTargetRotation();
    }

    // =========================================================
    // UPDATE DIRECTION
    // =========================================================

    void UpdateDirection()
    {
        direction =
            Vector3.Slerp(
                direction,
                targetDirection,
                turnSpeed *
                Time.deltaTime
            ).normalized;

        float wave =
            Mathf.Sin(
                Time.time *
                verticalFrequency +
                verticalOffset
            );

        direction.y +=
            wave *
            verticalMovement *
            0.05f;

        direction.Normalize();

        float newZ =
            Mathf.Lerp(
                transform.position.z,
                targetZ,
                Time.deltaTime
            );

        Vector3 position =
            transform.position;

        position.z = newZ;

        transform.position =
            position;
    }

    // =========================================================
    // MOVE
    // =========================================================

    void MoveFish()
    {
        transform.position +=
            direction *
            currentSpeed *
            Time.deltaTime;
    }

    // =========================================================
    // CAMERA BOUNDARY
    // =========================================================

    void KeepInsideCamera()
    {
        float depth =
            Mathf.Abs(
                transform.position.z -
                targetCamera.transform.position.z
            );

        Vector3 bottomLeft =
            targetCamera.ViewportToWorldPoint(
                new Vector3(
                    0f,
                    0f,
                    depth
                )
            );

        Vector3 topRight =
            targetCamera.ViewportToWorldPoint(
                new Vector3(
                    1f,
                    1f,
                    depth
                )
            );

        float minX =
            bottomLeft.x +
            horizontalMargin;

        float maxX =
            topRight.x -
            horizontalMargin;

        float minY =
            bottomLeft.y +
            verticalMargin;

        float maxY =
            topRight.y -
            verticalMargin;

        Vector3 position =
            transform.position;

        // LEFT
        if (position.x < minX)
        {
            position.x = minX;

            targetDirection.x =
                Mathf.Abs(
                    targetDirection.x
                );

            direction.x =
                Mathf.Abs(
                    direction.x
                );

            SetTargetRotation();
        }

        // RIGHT
        if (position.x > maxX)
        {
            position.x = maxX;

            targetDirection.x =
                -Mathf.Abs(
                    targetDirection.x
                );

            direction.x =
                -Mathf.Abs(
                    direction.x
                );

            SetTargetRotation();
        }

        // BOTTOM
        if (position.y < minY)
        {
            position.y = minY;

            targetDirection.y =
                Mathf.Abs(
                    targetDirection.y
                );

            direction.y =
                Mathf.Abs(
                    direction.y
                );
        }

        // TOP
        if (position.y > maxY)
        {
            position.y = maxY;

            targetDirection.y =
                -Mathf.Abs(
                    targetDirection.y
                );

            direction.y =
                -Mathf.Abs(
                    direction.y
                );
        }

        transform.position =
            position;
    }

    // =========================================================
    // TARGET ROTATION
    // =========================================================

    void SetTargetRotation()
    {
        if (targetDirection.x >= 0f)
        {
            targetY =
                sideRotation;
        }
        else
        {
            targetY =
                sideRotation + 180f;
        }
    }

    // =========================================================
    // ROTATION
    // =========================================================

    void UpdateRotation()
    {
        float randomY =
            Mathf.Sin(
                Time.time * 0.7f +
                rotationOffset
            ) *
            randomRotation;

        float finalY =
            targetY +
            randomY;

        Quaternion targetRotation =
            Quaternion.Euler(
                0f,
                finalY,
                0f
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed *
                Time.deltaTime
            );
    }
}