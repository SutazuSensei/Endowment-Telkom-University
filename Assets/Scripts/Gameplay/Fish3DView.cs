using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders and animates the 3D Fish model (Blue Powder Tang) + 3D Accessories (Top Hat, Glasses)
/// synchronized to this UI RectTransform's screen position.
/// Bridges the LoFi 2D RectTransform gameplay systems (FishMovement, ShowcaseFishBob, Food, OwnerName)
/// with the Prototype's 3D SkinnedMeshRenderer, Animator, fishmask shader, and CameraZoomTransition.
/// </summary>
public class Fish3DView : MonoBehaviour
{
    [Header("3D Prefabs")]
    [SerializeField] private GameObject fish3DPrefab;
    [SerializeField] private GameObject hat3DPrefab;
    [SerializeField] private GameObject glasses3DPrefab;

    [Header("Scale & Depth Settings")]
    [SerializeField] private float baseWorldDepthFromCamera = 103.3f; // z = -19.7 when camera is at z = -123
    [SerializeField] private float scaleMultiplier = 1.0f;
    [SerializeField] private float referenceCameraOrthoSize = 81.42f;

    [Header("3D Swimming Motion")]
    [SerializeField] private float turnSmoothTime = 0.26f;
    [SerializeField] private float maxPitchAngle = 20f;
    [SerializeField] private float pitchSmoothSpeed = 6f;
    [SerializeField] private float swimYawSwayAmplitude = 3.5f;
    [SerializeField] private float swimYawSwayFrequency = 3.2f;
    [SerializeField] private float minAnimatorSpeed = 0.65f;
    [SerializeField] private float maxAnimatorSpeed = 1.50f;

    private GameObject fish3DInstance;
    private Transform modelTransform;
    private Animator fishAnimator;
    private SkinnedMeshRenderer fishRenderer;
    private Transform hatSocket;
    private Transform glassesSocket;

    private GameObject hatInstance;
    private GameObject glassesInstance;

    private Camera cam3D;
    private Camera cam2D;
    private RectTransform rectTransform;
    private FishMovement fishMovement;

    private MaterialPropertyBlock fishPropBlock;
    private MaterialPropertyBlock accPropBlock;
    private static readonly int FishColorID = Shader.PropertyToID("_FishColor");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private string currentTier = "Common";
    private string currentColorVariant = "blue";
    private string currentAccessoryId = "";

    private float currentYaw = 90f;
    private float yawVelocity = 0f;
    private float currentPitch = 0f;
    private float currentRoll = 0f;
    private float randomPhase;
    private float depthJitter;
    private Vector2 prevAnchoredPos;
    private bool initializedPos = false;

    public GameObject Fish3DInstance => fish3DInstance;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        fishMovement = GetComponent<FishMovement>();
        fishPropBlock = new MaterialPropertyBlock();
        accPropBlock = new MaterialPropertyBlock();

        randomPhase = Random.Range(0f, 100f);
        depthJitter = Random.Range(-3.5f, 3.5f);

        HideLegacy2DSprites();
        Ensure3DInstance();
    }

    private void Start()
    {
        HideLegacy2DSprites();
        EnsureCameras();
        ApplyVisualState();
        UpdateWorldTransform(true);
    }

    private void OnEnable()
    {
        if (fish3DInstance != null)
        {
            fish3DInstance.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (fish3DInstance != null)
        {
            fish3DInstance.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (fish3DInstance != null)
        {
            Destroy(fish3DInstance);
        }
    }

    private void LateUpdate()
    {
        HideLegacy2DSprites();
        UpdateWorldTransform(false);
    }

    public void SetScaleMultiplier(float multiplier)
    {
        scaleMultiplier = multiplier;
    }

    public void ApplyCustomization(string tier, string colorVariant, string accessoryId)
    {
        if (!string.IsNullOrEmpty(tier)) currentTier = tier;
        if (!string.IsNullOrEmpty(colorVariant)) currentColorVariant = colorVariant;
        currentAccessoryId = accessoryId ?? "";

        Ensure3DInstance();
        ApplyVisualState();
    }

    private void HideLegacy2DSprites()
    {
        Image selfImg = GetComponent<Image>();
        if (selfImg != null && selfImg.enabled)
        {
            selfImg.enabled = false;
        }

        // Hide 2D accessory overlay images while keeping text (like ownerText) visible
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name.IndexOf("Accessory", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Image childImg = child.GetComponent<Image>();
                if (childImg != null && childImg.enabled)
                {
                    childImg.enabled = false;
                }
            }
        }
    }

    private void EnsureCameras()
    {
        if (cam3D == null)
        {
            GameObject c3dObj = GameObject.Find("3d camera");
            if (c3dObj != null) cam3D = c3dObj.GetComponent<Camera>();
            if (cam3D == null) cam3D = Camera.main;
        }

        if (cam2D == null)
        {
            GameObject c2dObj = GameObject.Find("2d camera");
            if (c2dObj != null) cam2D = c2dObj.GetComponent<Camera>();
        }
    }

    private void Ensure3DInstance()
    {
        if (fish3DInstance != null) return;

        if (fish3DPrefab == null) return;

        fish3DInstance = Instantiate(fish3DPrefab);
        fish3DInstance.name = $"Fish3D_{gameObject.name}";

        modelTransform = fish3DInstance.transform.Find("Model");
        if (modelTransform == null) modelTransform = fish3DInstance.transform;

        fishAnimator = fish3DInstance.GetComponentInChildren<Animator>();
        fishRenderer = fish3DInstance.GetComponentInChildren<SkinnedMeshRenderer>();

        if (fishAnimator != null)
        {
            fishAnimator.enabled = true;
            // Randomize animation start time so multiple fish never flap in robotic sync
            fishAnimator.Play(0, 0, Random.value);
        }

        // Locate sockets on Head bone
        Transform[] allTransforms = fish3DInstance.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.name == "HatSocket") hatSocket = t;
            else if (t.name == "GlassesSocket") glassesSocket = t;
        }

        if (hatSocket != null && hat3DPrefab != null)
        {
            hatInstance = Instantiate(hat3DPrefab, hatSocket, false);
            hatInstance.name = "Acc3D_TopHat";
            hatInstance.transform.localPosition = Vector3.zero;
            hatInstance.transform.localRotation = Quaternion.identity;
            hatInstance.transform.localScale = Vector3.one;
            hatInstance.SetActive(false);
        }

        if (glassesSocket != null && glasses3DPrefab != null)
        {
            glassesInstance = Instantiate(glasses3DPrefab, glassesSocket, false);
            glassesInstance.name = "Acc3D_Glasses";
            glassesInstance.transform.localPosition = Vector3.zero;
            glassesInstance.transform.localRotation = Quaternion.identity;
            glassesInstance.transform.localScale = Vector3.one;
            glassesInstance.SetActive(false);
        }
    }

    private void ApplyVisualState()
    {
        if (fish3DInstance == null) return;

        // 1. Apply Shader Color (_FishColor) via MaterialPropertyBlock
        if (fishRenderer != null)
        {
            Color targetColor = ResolveFishColor(currentColorVariant);
            fishRenderer.GetPropertyBlock(fishPropBlock);
            fishPropBlock.SetColor(FishColorID, targetColor);
            fishRenderer.SetPropertyBlock(fishPropBlock);
        }

        // 2. Apply 3D Accessory (Top Hat or Glasses) attached to Head bone socket
        bool showHat = false;
        bool showGlasses = false;

        if (!string.IsNullOrEmpty(currentAccessoryId))
        {
            string idLower = currentAccessoryId.ToLowerInvariant();
            if (idLower.Contains("hat"))
            {
                showHat = true;
                ApplyHatVariantStyle(idLower);
            }
            else if (idLower.Contains("glass") || idLower.Contains("starfish"))
            {
                showGlasses = true;
                ApplyGlassesVariantStyle(idLower);
            }
        }

        if (hatInstance != null) hatInstance.SetActive(showHat);
        if (glassesInstance != null) glassesInstance.SetActive(showGlasses);
    }

    private void ApplyHatVariantStyle(string idLower)
    {
        if (hatInstance == null) return;

        Color crownColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        Color ribbonColor = new Color(0.85f, 0.18f, 0.22f, 1f);

        if (idLower.Contains("cowboy"))
        {
            crownColor = new Color(0.48f, 0.28f, 0.12f, 1f);
            ribbonColor = new Color(0.92f, 0.72f, 0.20f, 1f);
        }
        else if (idLower.Contains("sailor"))
        {
            crownColor = new Color(0.92f, 0.95f, 0.98f, 1f);
            ribbonColor = new Color(0.15f, 0.38f, 0.85f, 1f);
        }
        else if (idLower.Contains("unicorn"))
        {
            crownColor = new Color(0.88f, 0.45f, 0.85f, 1f);
            ribbonColor = new Color(0.98f, 0.85f, 0.25f, 1f);
        }

        SetChildRendererColor(hatInstance.transform, "Brim", crownColor);
        SetChildRendererColor(hatInstance.transform, "Crown", crownColor);
        SetChildRendererColor(hatInstance.transform, "Ribbon", ribbonColor);
    }

    private void ApplyGlassesVariantStyle(string idLower)
    {
        if (glassesInstance == null) return;

        Color frameColor = new Color(0.95f, 0.78f, 0.20f, 1f); // Gold (Fancy)
        if (idLower.Contains("redsand") || idLower.Contains("evil"))
        {
            frameColor = new Color(0.90f, 0.15f, 0.18f, 1f); // Evil Red
        }
        else if (idLower.Contains("starfish"))
        {
            frameColor = new Color(0.98f, 0.50f, 0.18f, 1f); // Starfish Coral Orange
        }

        foreach (MeshRenderer mr in glassesInstance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (mr.name.StartsWith("Lens")) continue;
            mr.GetPropertyBlock(accPropBlock);
            accPropBlock.SetColor(BaseColorID, frameColor);
            mr.SetPropertyBlock(accPropBlock);
        }
    }

    private void SetChildRendererColor(Transform root, string childName, Color color)
    {
        Transform t = root.Find(childName);
        if (t == null) return;
        MeshRenderer mr = t.GetComponent<MeshRenderer>();
        if (mr == null) return;
        mr.GetPropertyBlock(accPropBlock);
        accPropBlock.SetColor(BaseColorID, color);
        mr.SetPropertyBlock(accPropBlock);
    }

    public static Color ResolveFishColor(string variant)
    {
        if (string.IsNullOrEmpty(variant)) return new Color(0.134f, 0.194f, 0.292f, 1f);

        switch (variant.Trim().ToLowerInvariant())
        {
            case "red":
                return new Color(0.92f, 0.20f, 0.20f, 1f);
            case "blue":
                return new Color(0.15f, 0.45f, 0.95f, 1f);
            case "yellow":
                return new Color(0.95f, 0.80f, 0.12f, 1f);
            case "green":
                return new Color(0.18f, 0.82f, 0.35f, 1f);
            default:
                if (ColorUtility.TryParseHtmlString(variant, out Color parsed))
                    return parsed;
                return new Color(0.15f, 0.45f, 0.95f, 1f);
        }
    }

    private float GetTierScale(string tier)
    {
        if (string.IsNullOrEmpty(tier)) return 0.35f;

        switch (tier.Trim().ToLowerInvariant())
        {
            case "common":
                return 0.28f;
            case "rare":
                return 0.34f;
            case "epic":
                return 0.41f;
            case "legendary":
                return 0.49f;
            default:
                return 0.35f;
        }
    }

    private void UpdateWorldTransform(bool instant)
    {
        if (fish3DInstance == null) return;
        EnsureCameras();
        if (cam3D == null) return;

        // 1. Convert UI RectTransform position to 3D Camera World Position
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCam, transform.position);
        float depth = baseWorldDepthFromCamera + depthJitter;
        Vector3 worldPos = cam3D.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        fish3DInstance.transform.position = worldPos;

        // 2. Compute Tier Scale * Zoom Scale (shrinks smoothly when 2d camera zooms out in Big Aquarium!)
        float tierScale = GetTierScale(currentTier);
        float zoomRatio = 1f;
        if (cam2D != null && cam2D.orthographic && cam2D.orthographicSize > 1f)
        {
            zoomRatio = referenceCameraOrthoSize / cam2D.orthographicSize;
        }
        float finalScale = tierScale * scaleMultiplier * zoomRatio;
        modelTransform.localScale = new Vector3(finalScale, finalScale, finalScale);

        // 3. Natural 3D Rotation (Yaw + Pitch + Banking) & Animator Speed
        Vector2 currentAnchoredPos = rectTransform != null ? rectTransform.anchoredPosition : (Vector2)transform.position;
        if (!initializedPos)
        {
            prevAnchoredPos = currentAnchoredPos;
            initializedPos = true;
        }

        Vector2 frameDelta = (Time.deltaTime > 0.0001f)
            ? (currentAnchoredPos - prevAnchoredPos) / Time.deltaTime
            : Vector2.zero;
        prevAnchoredPos = currentAnchoredPos;

        float facing = 1f;
        float speedRatio = 0.2f;
        float verticalVelNorm = 0f;

        if (fishMovement != null)
        {
            facing = fishMovement.HeadingX;
            speedRatio = fishMovement.NormalizedSpeed;
            verticalVelNorm = Mathf.Clamp(frameDelta.y / 140f, -1f, 1f);
        }
        else
        {
            // Showcase mode (Collection scene): face right and gently sway
            facing = 1f;
            speedRatio = 0.35f;
            verticalVelNorm = Mathf.Clamp(frameDelta.y / 40f, -0.5f, 0.5f);
        }

        float targetYaw = (facing >= 0f) ? 90f : 270f;
        if (instant)
        {
            currentYaw = targetYaw;
            yawVelocity = 0f;
            currentPitch = 0f;
            currentRoll = 0f;
        }
        else
        {
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, turnSmoothTime, 540f, Time.deltaTime);
        }

        float sway = Mathf.Sin(Time.time * swimYawSwayFrequency + randomPhase) *
                     swimYawSwayAmplitude * Mathf.Lerp(0.4f, 1.0f, speedRatio);

        float targetPitch = -verticalVelNorm * maxPitchAngle * Mathf.Clamp01(speedRatio + 0.25f);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, pitchSmoothSpeed * Time.deltaTime);

        float targetRoll = Mathf.Clamp(-yawVelocity * 0.045f, -14f, 14f);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 8f * Time.deltaTime);

        fish3DInstance.transform.rotation = Quaternion.Euler(currentPitch, currentYaw + sway, currentRoll);

        // 4. Sync Animator tail speed to swimming speed
        if (fishAnimator != null)
        {
            fishAnimator.speed = Mathf.Lerp(minAnimatorSpeed, maxAnimatorSpeed, speedRatio);
        }
    }
}

