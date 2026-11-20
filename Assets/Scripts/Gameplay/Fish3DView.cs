using UnityEngine;
using Endowment.Gameplay;

/// <summary>
/// Renders and animates the 3D Fish model + 3D Accessories (Top Hat, Glasses)
/// synchronized to this UI RectTransform's screen position.
/// 
/// Pulls its 3D Fish Prefab, 3D Accessory Prefabs, Tier Scales, and Color Palette
/// from the Single Source of Truth (Resources/FishVisualConfig.asset) so that
/// Aquarium, Collection, and BigAquarium always render the exact same 3D fish model.
/// </summary>
public class Fish3DView : MonoBehaviour
{
    [Header("Optional Local Overrides (Default: Loaded from FishVisualConfig)")]
    [SerializeField] private GameObject fish3DPrefab;
    [SerializeField] private GameObject hat3DPrefab;
    [SerializeField] private GameObject glasses3DPrefab;

    [Header("Scale & Depth Settings")]
    [SerializeField] private float baseWorldDepthFromCamera = 103.3f;
    [SerializeField] private float scaleMultiplier = 1.0f;
    [SerializeField] private float referenceCameraOrthoSize = 81.42f;

    [Header("3D Swimming Motion")]
    [SerializeField] private float turnSmoothTime = 0.26f;
    [SerializeField] private float maxPitchAngle = 8f;
    [SerializeField] private float pitchSmoothSpeed = 8.5f;
    [SerializeField] private float swimYawSwayAmplitude = 3.5f;
    [SerializeField] private float swimYawSwayFrequency = 3.2f;
    [SerializeField] private float minAnimatorSpeed = 0.65f;
    [SerializeField] private float maxAnimatorSpeed = 1.50f;

    private GameObject fish3DInstance;
    private GameObject currentSpawnedModelPrefab;
    private Transform modelTransform;
    private Animator fishAnimator;
    private ProceduralFishAnimator proceduralAnimator;
    private ModularFishAnimator modularAnimator;
    private DynamicRiggedFishAnimator dynamicRiggedAnimator;
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
    private FishTierVFX tierVFX;
    private static readonly int FishColorID = Shader.PropertyToID("_FishColor");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");
    private static readonly int MetallicID = Shader.PropertyToID("_Metallic");
    private static readonly int RimColorID = Shader.PropertyToID("_RimColor");
    private static readonly int RimPowerID = Shader.PropertyToID("_RimPower");
    private static readonly int RimIntensityID = Shader.PropertyToID("_RimIntensity");
    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowStrengthID = Shader.PropertyToID("_GlowStrength");
    private static readonly int MaskGlowWeightID = Shader.PropertyToID("_MaskGlowWeight");
    private static readonly int PulseSpeedID = Shader.PropertyToID("_PulseSpeed");
    private static readonly int IridescenceStrengthID = Shader.PropertyToID("_IridescenceStrength");
    private static readonly int ShineSpeedID = Shader.PropertyToID("_ShineSpeed");
    private static readonly int ShineStrengthID = Shader.PropertyToID("_ShineStrength");

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

        Ensure3DInstance();
    }

    private void Start()
    {
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
        UpdateWorldTransform(false);
    }

    public void SetScaleMultiplier(float multiplier)
    {
        scaleMultiplier = multiplier;
    }

    public void SetFishModelPrefab(GameObject newPrefab)
    {
        if (newPrefab == null || fish3DPrefab == newPrefab) return;

        fish3DPrefab = newPrefab;
        if (fish3DInstance != null)
        {
            Destroy(fish3DInstance);
            fish3DInstance = null;
        }
        Ensure3DInstance();
        ApplyVisualState();
    }

    public void ApplyCustomization(string tier, string colorVariant, string accessoryId)
    {
        if (!string.IsNullOrEmpty(tier)) currentTier = tier;
        if (!string.IsNullOrEmpty(colorVariant)) currentColorVariant = colorVariant;
        currentAccessoryId = accessoryId ?? "";

        ResolveSingleSourcePrefabs(out GameObject targetFishPrefab, out _, out _);
        if (targetFishPrefab != null && targetFishPrefab != currentSpawnedModelPrefab && fish3DInstance != null)
        {
            Destroy(fish3DInstance);
            fish3DInstance = null;
        }

        Ensure3DInstance();
        ApplyVisualState();
    }

    /// <summary>
    /// Triggers a celebratory bubble/sparkle burst on the fish (e.g. when fed or inspected).
    /// </summary>
    public void PlaySpecialBurst(bool isFeed = false)
    {
        if (tierVFX != null)
        {
            tierVFX.PlayBurst(isFeed);
        }
    }

    /// <summary>
    /// Updates the fish material colors immediately in real-time (e.g. while dragging ColorPicker)
    /// without re-instantiating the model or causing GC allocations.
    /// </summary>
    public void SetLiveColor(Color color)
    {
        currentColorVariant = "#" + ColorUtility.ToHtmlStringRGB(color);
        if (fish3DInstance == null) return;

        ApplyColorAndTierShader(color);
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

    private void ResolveSingleSourcePrefabs(out GameObject resolvedFishPrefab, out GameObject resolvedHatPrefab, out GameObject resolvedGlassesPrefab)
    {
        FishVisualConfig config = FishVisualConfig.Instance;
        resolvedFishPrefab = (config != null) ? config.GetFishPrefabForTier(currentTier) : fish3DPrefab;
        if (resolvedFishPrefab == null) resolvedFishPrefab = (config != null && config.activeFish3DPrefab != null) ? config.activeFish3DPrefab : fish3DPrefab;
        resolvedHatPrefab = (config != null && config.hat3DPrefab != null) ? config.hat3DPrefab : hat3DPrefab;
        resolvedGlassesPrefab = (config != null && config.glasses3DPrefab != null) ? config.glasses3DPrefab : glasses3DPrefab;
    }

    private void Ensure3DInstance()
    {
        if (fish3DInstance != null) return;

        ResolveSingleSourcePrefabs(out GameObject targetFishPrefab, out GameObject targetHatPrefab, out GameObject targetGlassesPrefab);
        if (targetFishPrefab == null) return;

        currentSpawnedModelPrefab = targetFishPrefab;
        fish3DInstance = Instantiate(targetFishPrefab);
        fish3DInstance.name = $"Fish3D_{gameObject.name}";

        modelTransform = fish3DInstance.transform.Find("Model");
        if (modelTransform == null) modelTransform = fish3DInstance.transform;

        fishAnimator = fish3DInstance.GetComponentInChildren<Animator>();
        proceduralAnimator = fish3DInstance.GetComponentInChildren<ProceduralFishAnimator>();
        modularAnimator = fish3DInstance.GetComponentInChildren<ModularFishAnimator>();
        dynamicRiggedAnimator = fish3DInstance.GetComponentInChildren<DynamicRiggedFishAnimator>();

        if (dynamicRiggedAnimator != null)
        {
            dynamicRiggedAnimator.Bind(fishMovement, this);
            if (fishAnimator != null)
            {
                fishAnimator.enabled = false;
            }
        }
        else if (fishAnimator != null)
        {
            fishAnimator.enabled = true;
            fishAnimator.Play(0, 0, Random.value);
        }

        hatSocket = null;
        glassesSocket = null;
        Transform[] allTransforms = fish3DInstance.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.name == "HatSocket") hatSocket = t;
            else if (t.name == "GlassesSocket") glassesSocket = t;
        }

        if (hatSocket == null && proceduralAnimator != null) hatSocket = proceduralAnimator.HatSocket;
        if (glassesSocket == null && proceduralAnimator != null) glassesSocket = proceduralAnimator.GlassesSocket;

        if (hatSocket != null && targetHatPrefab != null)
        {
            hatInstance = Instantiate(targetHatPrefab, hatSocket, false);
            hatInstance.name = "Acc3D_TopHat";
            hatInstance.transform.localPosition = Vector3.zero;
            hatInstance.transform.localRotation = Quaternion.identity;
            hatInstance.transform.localScale = Vector3.one;
            hatInstance.SetActive(false);
        }

        if (glassesSocket != null && targetGlassesPrefab != null)
        {
            glassesInstance = Instantiate(targetGlassesPrefab, glassesSocket, false);
            glassesInstance.name = "Acc3D_Glasses";
            glassesInstance.transform.localPosition = Vector3.zero;
            glassesInstance.transform.localRotation = Quaternion.identity;
            glassesInstance.transform.localScale = Vector3.one;
            glassesInstance.SetActive(false);
        }

        tierVFX = fish3DInstance.GetComponent<FishTierVFX>();
        if (tierVFX == null)
        {
            tierVFX = fish3DInstance.AddComponent<FishTierVFX>();
        }
        EnsureCameras();
        tierVFX.EnsureInitialized(cam3D);
    }

    private void ApplyVisualState()
    {
        if (fish3DInstance == null) return;

        // 1. Apply Shader Color (_FishColor / _BaseColor) & Tier Effects via MaterialPropertyBlock
        Color targetColor = ResolveFishColor(currentColorVariant);
        ApplyColorAndTierShader(targetColor);

        if (tierVFX != null)
        {
            tierVFX.ApplyTier(currentTier, targetColor, GetCurrentVFXScale());
        }

        // 2. Apply 3D Accessory (Top Hat or Glasses) attached to socket
        bool showHat = false;
        bool showGlasses = false;

        if (!string.IsNullOrEmpty(currentAccessoryId))
        {
            string idLower = currentAccessoryId.ToLowerInvariant();
            if (idLower != "none")
            {
                if (idLower.Contains("hat") || idLower.Contains("cap"))
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
        }

        if (hatInstance != null) hatInstance.SetActive(showHat);
        if (glassesInstance != null) glassesInstance.SetActive(showGlasses);
    }

    private void ApplyColorAndTierShader(Color targetColor)
    {
        if (fish3DInstance == null) return;
        if (fishPropBlock == null) fishPropBlock = new MaterialPropertyBlock();

        GetTierShaderSettings(
            currentTier, targetColor,
            out float smoothness, out float metallic,
            out Color rimColor, out float rimPower, out float rimIntensity,
            out Color glowColor, out float glowStrength, out float maskGlowWeight, out float pulseSpeed,
            out float iridescence, out float shineSpeed, out float shineStrength);

        Renderer[] allRenderers = fish3DInstance.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in allRenderers)
        {
            if (hatSocket != null && r.transform.IsChildOf(hatSocket)) continue;
            if (glassesSocket != null && r.transform.IsChildOf(glassesSocket)) continue;
            if (tierVFX != null && r.transform.IsChildOf(tierVFX.transform))
            {
                if (r.name.StartsWith("VFX_")) continue;
            }

            r.GetPropertyBlock(fishPropBlock);
            fishPropBlock.SetColor(FishColorID, targetColor);
            fishPropBlock.SetColor(BaseColorID, targetColor);
            fishPropBlock.SetFloat(SmoothnessID, smoothness);
            fishPropBlock.SetFloat(MetallicID, metallic);
            fishPropBlock.SetColor(RimColorID, rimColor);
            fishPropBlock.SetFloat(RimPowerID, rimPower);
            fishPropBlock.SetFloat(RimIntensityID, rimIntensity);
            fishPropBlock.SetColor(GlowColorID, glowColor);
            fishPropBlock.SetFloat(GlowStrengthID, glowStrength);
            fishPropBlock.SetFloat(MaskGlowWeightID, maskGlowWeight);
            fishPropBlock.SetFloat(PulseSpeedID, pulseSpeed);
            fishPropBlock.SetFloat(IridescenceStrengthID, iridescence);
            fishPropBlock.SetFloat(ShineSpeedID, shineSpeed);
            fishPropBlock.SetFloat(ShineStrengthID, shineStrength);
            r.SetPropertyBlock(fishPropBlock);
        }
    }

    private static void GetTierShaderSettings(
        string tier, Color baseColor,
        out float smoothness, out float metallic,
        out Color rimColor, out float rimPower, out float rimIntensity,
        out Color glowColor, out float glowStrength, out float maskGlowWeight, out float pulseSpeed,
        out float iridescence, out float shineSpeed, out float shineStrength)
    {
        string lower = string.IsNullOrEmpty(tier) ? "common" : tier.Trim().ToLowerInvariant();
        if (lower == "rare" || lower == "tier2" || lower == "tier 2")
        {
            smoothness = 0.65f;
            metallic = 0.08f;
            rimColor = Color.Lerp(baseColor, new Color(0.55f, 0.92f, 1.0f, 1f), 0.72f);
            rimPower = 3.6f;
            rimIntensity = 0.55f;
            glowColor = new Color(0.4f, 0.9f, 1f, 1f);
            glowStrength = 0.0f;
            maskGlowWeight = 0.6f;
            pulseSpeed = 1.8f;
            iridescence = 0.18f;
            shineSpeed = 0.35f;
            shineStrength = 0.45f;
        }
        else if (lower == "epic" || lower == "tier3" || lower == "tier 3")
        {
            smoothness = 0.76f;
            metallic = 0.16f;
            rimColor = Color.Lerp(baseColor, new Color(0.72f, 0.48f, 1.0f, 1f), 0.58f);
            rimPower = 3.0f;
            rimIntensity = 1.45f;
            glowColor = Color.Lerp(baseColor, new Color(0.55f, 0.90f, 1.0f, 1f), 0.45f);
            glowStrength = 0.38f;
            maskGlowWeight = 0.55f;
            pulseSpeed = 2.2f;
            iridescence = 0.42f;
            shineSpeed = 0.55f;
            shineStrength = 0.75f;
        }
        else if (lower == "legendary" || lower == "tier4" || lower == "tier 4")
        {
            smoothness = 0.88f;
            metallic = 0.30f;
            rimColor = Color.Lerp(baseColor, new Color(1.0f, 0.85f, 0.25f, 1f), 0.75f);
            rimPower = 2.6f;
            rimIntensity = 2.30f;
            glowColor = Color.Lerp(baseColor, new Color(1.0f, 0.82f, 0.30f, 1f), 0.55f);
            glowStrength = 0.55f;
            maskGlowWeight = 0.60f;
            pulseSpeed = 2.8f;
            iridescence = 0.72f;
            shineSpeed = 0.75f;
            shineStrength = 1.20f;
        }
        else
        {
            smoothness = 0.45f;
            metallic = 0.0f;
            rimColor = Color.white;
            rimPower = 4.0f;
            rimIntensity = 0.0f;
            glowColor = Color.black;
            glowStrength = 0.0f;
            maskGlowWeight = 0.6f;
            pulseSpeed = 1.5f;
            iridescence = 0.0f;
            shineSpeed = 0.35f;
            shineStrength = 0.0f;
        }
    }

    private float GetCurrentVFXScale()
    {
        float tierScale = GetTierScale(currentTier);
        float zoomRatio = 1f;
        if (cam2D != null && cam2D.orthographic && cam2D.orthographicSize > 1f)
        {
            zoomRatio = referenceCameraOrthoSize / cam2D.orthographicSize;
        }
        return GetVFXScaleFromFinal(tierScale * scaleMultiplier * zoomRatio);
    }

    private float GetVFXScaleFromFinal(float finalScale)
    {
        string lower = string.IsNullOrEmpty(currentTier) ? "common" : currentTier.Trim().ToLowerInvariant();
        float boost = (lower == "legendary" || lower == "tier4" || lower == "tier 4") ? 3.8f : 3.0f;
        return finalScale * boost;
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
        else if (idLower.Contains("telu"))
        {
            crownColor = new Color(0.82f, 0.12f, 0.16f, 1f);
            ribbonColor = new Color(0.96f, 0.96f, 0.98f, 1f);
        }
        else if (idLower.Contains("graduation"))
        {
            crownColor = new Color(0.10f, 0.10f, 0.13f, 1f);
            ribbonColor = new Color(0.95f, 0.78f, 0.18f, 1f);
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

        Color frameColor = new Color(0.95f, 0.78f, 0.20f, 1f);
        if (idLower.Contains("redsand") || idLower.Contains("evil"))
        {
            frameColor = new Color(0.90f, 0.15f, 0.18f, 1f);
        }
        else if (idLower.Contains("starfish"))
        {
            frameColor = new Color(0.98f, 0.50f, 0.18f, 1f);
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
        if (FishVisualConfig.Instance != null)
        {
            return FishVisualConfig.Instance.ResolveColor(variant);
        }

        if (string.IsNullOrEmpty(variant)) return new Color(0.15f, 0.45f, 0.95f, 1f);

        switch (variant.Trim().ToLowerInvariant())
        {
            case "red":
            case "coral_pink":
                return new Color(0.92f, 0.20f, 0.20f, 1f);
            case "blue":
            case "default":
            case "ocean_blue":
                return new Color(0.15f, 0.45f, 0.95f, 1f);
            case "yellow":
            case "golden":
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
        if (FishVisualConfig.Instance != null)
        {
            return FishVisualConfig.Instance.GetScaleForTier(tier);
        }

        if (string.IsNullOrEmpty(tier)) return 0.60f;

        switch (tier.Trim().ToLowerInvariant())
        {
            case "common":
            case "tier1":
            case "tier 1":
                return 0.60f;
            case "rare":
            case "tier2":
            case "tier 2":
                return 0.90f;
            case "epic":
            case "tier3":
            case "tier 3":
                return 0.92f;
            case "legendary":
            case "tier4":
            case "tier 4":
                return 0.68f;
            default:
                return 0.60f;
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

        // 2. Compute Tier Scale * Zoom Scale
        float tierScale = GetTierScale(currentTier);
        float zoomRatio = 1f;
        if (cam2D != null && cam2D.orthographic && cam2D.orthographicSize > 1f)
        {
            zoomRatio = referenceCameraOrthoSize / cam2D.orthographicSize;
        }
        float finalScale = tierScale * scaleMultiplier * zoomRatio;
        modelTransform.localScale = new Vector3(finalScale, finalScale, finalScale);

        if (tierVFX != null)
        {
            float spd = (fishMovement != null) ? fishMovement.NormalizedSpeed : 0.35f;
            tierVFX.UpdateWorldProportions(GetVFXScaleFromFinal(finalScale), spd, fishMovement == null);
        }

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
            // Clean, deliberate vertical slope (free from Perlin noise wobble)
            verticalVelNorm = Mathf.Clamp(fishMovement.VerticalSlope, -1f, 1f);
        }
        else
        {
            facing = 1f;
            speedRatio = 0.35f;
            verticalVelNorm = Mathf.Clamp(frameDelta.y / 60f, -0.5f, 0.5f);
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

        // Smooth pitch that returns cleanly to 0 when idle/gliding
        float targetPitch = -verticalVelNorm * maxPitchAngle * Mathf.Clamp01(speedRatio);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, pitchSmoothSpeed * Time.deltaTime);

        float targetRoll = Mathf.Clamp(-yawVelocity * 0.045f, -14f, 14f);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, 8f * Time.deltaTime);

        fish3DInstance.transform.rotation = Quaternion.Euler(currentPitch, currentYaw + sway, currentRoll);

        // 4. Sync Animator / Dynamic Rigged Animator / Procedural / Modular fin speed to swimming speed
        float calculatedSpeed = Mathf.Lerp(minAnimatorSpeed, maxAnimatorSpeed, speedRatio);
        if (dynamicRiggedAnimator != null && dynamicRiggedAnimator.enabled)
        {
            dynamicRiggedAnimator.UpdateMotion(speedRatio, yawVelocity, currentPitch, (speedRatio < 0.05f));
        }
        else if (fishAnimator != null && fishAnimator.enabled)
        {
            fishAnimator.speed = calculatedSpeed;
        }
        if (proceduralAnimator != null)
        {
            proceduralAnimator.SetSpeedMultiplier(calculatedSpeed);
        }
        if (modularAnimator != null)
        {
            modularAnimator.SetSpeedMultiplier(calculatedSpeed);
        }
    }
}
