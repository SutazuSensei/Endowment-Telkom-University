using UnityEngine;

namespace Endowment.Gameplay
{
    /// <summary>
    /// Attaches and manages WebGL-friendly Particle VFX, Trail wake, Soft Halo Aura,
    /// and celebratory feed/inspect bursts for each 3D Fish based on its Donor Tier.
    /// Everything stays on the parent fish's layer (Layer 7 "fish") so 3d camera renders it cleanly.
    /// </summary>
    public class FishTierVFX : MonoBehaviour
    {
        private static Texture2D cachedSparkleTex;
        private static Texture2D cachedBubbleTex;
        private static Texture2D cachedGlowTex;
        private static Material cachedSparkleMat;
        private static Material cachedBubbleMat;
        private static Material cachedTrailMat;
        private static Material cachedAuraMat;

        private ParticleSystem ambientParticles;
        private ParticleSystem burstParticles;
        private TrailRenderer tailTrail;
        private Transform auraTransform;
        private MeshRenderer auraRenderer;
        private MaterialPropertyBlock auraPropBlock;
        private Camera cam3D;

        private string activeTier = "Common";
        private Color activeAuraColor = Color.clear;
        private float activeAuraBaseAlpha = 0f;
        private float currentWorldScale = 1f;
        private int framesSinceSpawn = 0;
        private bool initialized = false;

        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorID = Shader.PropertyToID("_Color");

        public void EnsureInitialized(Camera targetCam3D)
        {
            if (targetCam3D != null) cam3D = targetCam3D;
            if (initialized) return;

            EnsureSharedResources();
            int fishLayer = gameObject.layer;

            // 1. Ambient Tier Particles (sparkles / micro-bubbles)
            GameObject ambObj = new GameObject("VFX_TierAmbient");
            ambObj.layer = fishLayer;
            ambObj.transform.SetParent(transform, false);
            ambObj.transform.localPosition = Vector3.zero;
            ambientParticles = ambObj.AddComponent<ParticleSystem>();
            ConfigureBaseParticleSystem(ambientParticles, loop: true);

            // 2. One-Shot Burst Particles (Feed / Inspect reaction)
            GameObject burstObj = new GameObject("VFX_TierBurst");
            burstObj.layer = fishLayer;
            burstObj.transform.SetParent(transform, false);
            burstObj.transform.localPosition = Vector3.zero;
            burstParticles = burstObj.AddComponent<ParticleSystem>();
            ConfigureBaseParticleSystem(burstParticles, loop: false);

            // 3. Tail Ribbon Trail (Legendary Marlin)
            GameObject trailObj = new GameObject("VFX_LegendaryTrail");
            trailObj.layer = fishLayer;
            trailObj.transform.SetParent(transform, false);
            trailObj.transform.localPosition = new Vector3(0f, 0f, -12f);
            tailTrail = trailObj.AddComponent<TrailRenderer>();
            ConfigureTrailRenderer(tailTrail);
            tailTrail.enabled = false;

            // 4. Soft Underwater Halo Aura Quad (Epic & Legendary)
            GameObject auraObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            auraObj.name = "VFX_TierAura";
            auraObj.layer = fishLayer;
            Destroy(auraObj.GetComponent<Collider>());
            auraObj.transform.SetParent(transform, false);
            auraObj.transform.localPosition = Vector3.zero;
            auraTransform = auraObj.transform;
            auraRenderer = auraObj.GetComponent<MeshRenderer>();
            auraRenderer.sharedMaterial = cachedAuraMat;
            auraRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraRenderer.receiveShadows = false;
            auraPropBlock = new MaterialPropertyBlock();
            auraObj.SetActive(false);

            initialized = true;
        }

        public void ApplyTier(string tier, Color fishColor, float approxModelScale)
        {
            EnsureInitialized(cam3D);
            activeTier = NormalizeTier(tier);
            currentWorldScale = Mathf.Max(0.15f, approxModelScale);

            int fishLayer = gameObject.layer;
            if (ambientParticles != null) ambientParticles.gameObject.layer = fishLayer;
            if (burstParticles != null) burstParticles.gameObject.layer = fishLayer;
            if (tailTrail != null) tailTrail.gameObject.layer = fishLayer;
            if (auraTransform != null) auraTransform.gameObject.layer = fishLayer;

            ConfigureAmbientForTier(activeTier, fishColor);
            ConfigureTrailForTier(activeTier);
            ConfigureAuraForTier(activeTier, fishColor);
        }

        public void UpdateWorldProportions(float approxModelScale, float normalizedSpeed, bool showcaseMode)
        {
            if (!initialized) return;
            currentWorldScale = Mathf.Max(0.15f, approxModelScale);

            // Keep tail trail anchored proportionally behind the fish and disable continuous trail in static Showcase
            if (tailTrail != null)
            {
                framesSinceSpawn++;
                bool allowTrail = (activeTier == "legendary") && !showcaseMode;
                if (framesSinceSpawn <= 4)
                {
                    tailTrail.enabled = false;
                    tailTrail.emitting = false;
                    tailTrail.Clear();
                }
                else
                {
                    if (!tailTrail.enabled && allowTrail)
                    {
                        tailTrail.Clear();
                        tailTrail.enabled = true;
                    }
                    else if (tailTrail.enabled && !allowTrail)
                    {
                        tailTrail.Clear();
                        tailTrail.enabled = false;
                    }
                    tailTrail.emitting = allowTrail && normalizedSpeed > 0.12f;
                }

                tailTrail.transform.localPosition = new Vector3(0f, 0f, -25f * currentWorldScale);
                float baseW = Mathf.Clamp(currentWorldScale * 1.8f, 0.45f, 1.8f);
                tailTrail.startWidth = baseW;
                tailTrail.endWidth = baseW * 0.05f;
            }

            // Face 3D camera with subtle breathing glow behind the fish
            if (auraTransform != null && auraTransform.gameObject.activeSelf)
            {
                if (cam3D == null)
                {
                    GameObject c3d = GameObject.Find("3d camera");
                    if (c3d != null) cam3D = c3d.GetComponent<Camera>();
                }

                Vector3 camForward = cam3D != null ? cam3D.transform.forward : Vector3.forward;
                Vector3 camUp = cam3D != null ? cam3D.transform.up : Vector3.up;

                // Push slightly away from camera so aura always sits cleanly BEHIND the 3D fish mesh
                auraTransform.position = transform.position + camForward * 6.5f;
                auraTransform.rotation = Quaternion.LookRotation(camForward, camUp);

                float pulse = 0.90f + 0.12f * Mathf.Sin(Time.time * (activeTier == "legendary" ? 2.6f : 1.9f));
                float radius = GetAuraRadiusForTier(activeTier) * currentWorldScale * pulse;
                auraTransform.localScale = new Vector3(radius * 1.45f, radius, 1f);

                if (auraRenderer != null && auraPropBlock != null)
                {
                    Color col = activeAuraColor;
                    col.a = activeAuraBaseAlpha * (0.82f + 0.18f * Mathf.Sin(Time.time * 2.2f));
                    auraRenderer.GetPropertyBlock(auraPropBlock);
                    auraPropBlock.SetColor(BaseColorID, col);
                    auraPropBlock.SetColor(ColorID, col);
                    auraRenderer.SetPropertyBlock(auraPropBlock);
                }
            }
        }

        public void PlayBurst(bool isFeed)
        {
            if (!initialized || burstParticles == null) return;

            var main = burstParticles.main;
            var renderer = burstParticles.GetComponent<ParticleSystemRenderer>();
            int count = isFeed ? 12 : 16;

            if (activeTier == "legendary")
            {
                if (renderer != null) renderer.sharedMaterial = cachedSparkleMat;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.0f, 0.92f, 0.45f, 1f), new Color(1.0f, 0.72f, 0.15f, 1f));
                main.startSize = new ParticleSystem.MinMaxCurve(3.2f * currentWorldScale, 6.2f * currentWorldScale);
                count = isFeed ? 20 : 26;
            }
            else if (activeTier == "epic")
            {
                if (renderer != null) renderer.sharedMaterial = cachedSparkleMat;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.50f, 0.92f, 1.0f, 1f), new Color(0.82f, 0.50f, 1.0f, 1f));
                main.startSize = new ParticleSystem.MinMaxCurve(2.6f * currentWorldScale, 4.8f * currentWorldScale);
                count = isFeed ? 16 : 20;
            }
            else if (activeTier == "rare")
            {
                if (renderer != null) renderer.sharedMaterial = cachedBubbleMat;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.70f, 0.96f, 1.0f, 0.9f), new Color(0.35f, 0.80f, 1.0f, 0.9f));
                main.startSize = new ParticleSystem.MinMaxCurve(2.2f * currentWorldScale, 4.2f * currentWorldScale);
                count = 14;
            }
            else
            {
                if (renderer != null) renderer.sharedMaterial = cachedBubbleMat;
                main.startColor = new Color(0.85f, 0.97f, 1.0f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.8f * currentWorldScale, 3.4f * currentWorldScale);
                count = 10;
            }

            main.startSpeed = new ParticleSystem.MinMaxCurve(8f * currentWorldScale, 20f * currentWorldScale);
            burstParticles.Emit(count);
        }

        private void OnEnable()
        {
            framesSinceSpawn = 0;
            if (tailTrail != null)
            {
                tailTrail.emitting = false;
                tailTrail.enabled = false;
                tailTrail.Clear();
            }
        }

        private void OnDisable()
        {
            if (tailTrail != null)
            {
                tailTrail.emitting = false;
                tailTrail.Clear();
            }
        }

        private void ConfigureAmbientForTier(string tier, Color fishColor)
        {
            if (ambientParticles == null) return;

            var emission = ambientParticles.emission;
            var main = ambientParticles.main;
            var shape = ambientParticles.shape;
            var sizeOverLife = ambientParticles.sizeOverLifetime;
            var renderer = ambientParticles.GetComponent<ParticleSystemRenderer>();

            if (tier == "common")
            {
                emission.rateOverTime = 0f;
                ambientParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.25f),
                new Keyframe(0.25f, 1.0f),
                new Keyframe(1f, 0.0f)
            ));

            if (tier == "rare")
            {
                if (renderer != null) renderer.sharedMaterial = cachedBubbleMat;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * currentWorldScale, 6.0f * currentWorldScale);
                main.startSize = new ParticleSystem.MinMaxCurve(1.8f * currentWorldScale, 3.4f * currentWorldScale);
                main.gravityModifier = -0.18f;
                main.maxParticles = 10;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.65f, 0.95f, 1.0f, 0.65f),
                    new Color(0.88f, 0.98f, 1.0f, 0.45f));
                shape.radius = 8f * currentWorldScale;
                ambientParticles.transform.localPosition = new Vector3(0f, 0f, -10f * currentWorldScale);
                emission.rateOverTime = 3.2f;
            }
            else if (tier == "epic")
            {
                if (renderer != null) renderer.sharedMaterial = cachedSparkleMat;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f * currentWorldScale, 5.5f * currentWorldScale);
                main.startSize = new ParticleSystem.MinMaxCurve(2.2f * currentWorldScale, 4.4f * currentWorldScale);
                main.gravityModifier = -0.05f;
                main.maxParticles = 16;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.45f, 0.92f, 1.0f, 0.95f),
                    new Color(0.80f, 0.48f, 1.0f, 0.95f));
                shape.radius = 16f * currentWorldScale;
                ambientParticles.transform.localPosition = Vector3.zero;
                emission.rateOverTime = 6.5f;
            }
            else // legendary
            {
                if (renderer != null) renderer.sharedMaterial = cachedSparkleMat;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.0f * currentWorldScale, 7.5f * currentWorldScale);
                main.startSize = new ParticleSystem.MinMaxCurve(2.8f * currentWorldScale, 5.8f * currentWorldScale);
                main.gravityModifier = -0.04f;
                main.maxParticles = 24;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(1.0f, 0.94f, 0.52f, 1.0f),
                    new Color(1.0f, 0.72f, 0.16f, 0.95f));
                shape.radius = 22f * currentWorldScale;
                ambientParticles.transform.localPosition = Vector3.zero;
                emission.rateOverTime = 12.0f;
            }

            if (!ambientParticles.isPlaying)
            {
                ambientParticles.Play();
            }
        }

        private void ConfigureTrailForTier(string tier)
        {
            if (tailTrail == null) return;

            bool isLegendary = (tier == "legendary");
            framesSinceSpawn = 0;
            tailTrail.Clear();
            tailTrail.emitting = false;
            tailTrail.enabled = false;
            if (!isLegendary) return;

            tailTrail.time = 0.28f;
            tailTrail.minVertexDistance = 0.35f;

            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.0f, 0.90f, 0.45f), 0f),
                    new GradientColorKey(new Color(1.0f, 0.72f, 0.18f), 0.6f),
                    new GradientColorKey(new Color(0.55f, 0.88f, 1.0f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.32f, 0f),
                    new GradientAlphaKey(0.14f, 0.55f),
                    new GradientAlphaKey(0.0f, 1f)
                }
            );
            tailTrail.colorGradient = g;
        }

        private void ConfigureAuraForTier(string tier, Color fishColor)
        {
            if (auraTransform == null) return;

            if (tier == "epic")
            {
                activeAuraColor = Color.Lerp(new Color(0.52f, 0.82f, 1.0f, 1f), new Color(0.75f, 0.45f, 1.0f, 1f), 0.5f);
                activeAuraBaseAlpha = 0.16f;
                auraTransform.gameObject.SetActive(true);
            }
            else if (tier == "legendary")
            {
                activeAuraColor = new Color(1.0f, 0.82f, 0.25f, 1f);
                activeAuraBaseAlpha = 0.24f;
                auraTransform.gameObject.SetActive(true);
            }
            else
            {
                activeAuraBaseAlpha = 0f;
                auraTransform.gameObject.SetActive(false);
            }
        }

        private static float GetAuraRadiusForTier(string tier)
        {
            if (tier == "legendary") return 66f;
            if (tier == "epic") return 46f;
            return 28f;
        }

        private static void ConfigureBaseParticleSystem(ParticleSystem ps, bool loop)
        {
            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            if (!loop)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
                var em = ps.emission;
                em.rateOverTime = 0f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 6f;
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.2f, 1.0f),
                    new Keyframe(1f, 0f)
                ));
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = cachedSparkleMat;
            }
        }

        private static void ConfigureTrailRenderer(TrailRenderer tr)
        {
            tr.time = 0.45f;
            tr.minVertexDistance = 0.25f;
            tr.numCornerVertices = 2;
            tr.numCapVertices = 2;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.sharedMaterial = cachedTrailMat;
        }

        private static string NormalizeTier(string tier)
        {
            if (string.IsNullOrEmpty(tier)) return "common";
            string lower = tier.Trim().ToLowerInvariant();
            if (lower == "tier2" || lower == "tier 2" || lower == "rare") return "rare";
            if (lower == "tier3" || lower == "tier 3" || lower == "epic") return "epic";
            if (lower == "tier4" || lower == "tier 4" || lower == "legendary") return "legendary";
            return "common";
        }

        private static void EnsureSharedResources()
        {
            if (cachedSparkleTex == null) cachedSparkleTex = CreateSparkleTexture(64);
            if (cachedBubbleTex == null) cachedBubbleTex = CreateBubbleTexture(64);
            if (cachedGlowTex == null) cachedGlowTex = CreateSoftGlowTexture(64);

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = particleShader;

            if (cachedSparkleMat == null && particleShader != null)
            {
                cachedSparkleMat = CreateAdditiveParticleMaterial(particleShader, cachedSparkleTex);
            }
            if (cachedBubbleMat == null && particleShader != null)
            {
                cachedBubbleMat = CreateAlphaParticleMaterial(particleShader, cachedBubbleTex);
            }
            if (cachedTrailMat == null && particleShader != null)
            {
                cachedTrailMat = CreateAdditiveParticleMaterial(particleShader, cachedGlowTex);
            }
            if (cachedAuraMat == null && unlitShader != null)
            {
                cachedAuraMat = CreateAdditiveUnlitMaterial(unlitShader, cachedGlowTex);
            }
        }

        private static Material CreateAdditiveParticleMaterial(Shader shader, Texture2D tex)
        {
            Material m = new Material(shader);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f); // Additive
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        private static Material CreateAlphaParticleMaterial(Shader shader, Texture2D tex)
        {
            Material m = new Material(shader);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f); // Alpha
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        private static Material CreateAdditiveUnlitMaterial(Shader shader, Texture2D tex)
        {
            Material m = new Material(shader);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 2990;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return m;
        }

        private static Texture2D CreateSparkleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float core = Mathf.Pow(Mathf.Clamp01(1f - r), 2.8f);
                    float crossH = Mathf.Exp(-Mathf.Abs(ny) * 10f) * Mathf.Clamp01(1f - Mathf.Abs(nx));
                    float crossV = Mathf.Exp(-Mathf.Abs(nx) * 10f) * Mathf.Clamp01(1f - Mathf.Abs(ny));
                    float a = Mathf.Clamp01(core + (crossH + crossV) * 0.85f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D CreateBubbleTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float mask = Mathf.Clamp01((0.92f - r) * 8f);
                    float rim = Mathf.Pow(Mathf.Clamp01(r / 0.88f), 3.2f) * 0.85f;
                    float specDist = Mathf.Sqrt((nx + 0.32f) * (nx + 0.32f) + (ny - 0.32f) * (ny - 0.32f));
                    float highlight = Mathf.Clamp01(1f - specDist * 3.8f);
                    float a = mask * Mathf.Clamp01(0.18f + rim + highlight * 0.85f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D CreateSoftGlowTexture(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - half) / half;
                    float ny = (y - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float falloff = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, falloff);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
