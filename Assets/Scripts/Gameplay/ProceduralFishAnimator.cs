using UnityEngine;

namespace Endowment.Gameplay
{
    /// <summary>
    /// Animates static fish meshes (like NemoFihTier.fbx) without requiring skeletal bones (Option A).
    /// Uses dynamic vertex wave deformation targeting ONLY the tail and fins:
    /// - Head & front body: 100% rigid and undeformed (drawn RED in Gizmo visualizer).
    /// - Tail / caudal fin: Traveling lateral wave in local Z, intensifying quadratically toward the tail tip (GREEN/CYAN).
    /// - Dorsal & ventral fins: High-frequency flutter in local Z.
    /// - Pectoral fins: Forward/backward paddle in local X and lateral flap in local Z.
    /// 
    /// Fully optimized for WebGL (MarkDynamic buffer, zero heap allocations per frame).
    /// Includes real-time Scene View Gizmo visualizer to tune moving vs rigid vertices.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class ProceduralFishAnimator : MonoBehaviour
    {
        [Header("Tail Animation (Caudal Fin)")]
        [Tooltip("Base wagging frequency in radians per second.")]
        [SerializeField] private float tailFrequency = 8.5f;

        [Tooltip("Maximum lateral sway distance in local Z units at the tail tip. Increase for wider, more dramatic wagging.")]
        [SerializeField] private float tailAmplitude = 0.0038f;

        [Tooltip("Traveling wave spatial frequency along the spine (creates natural S-curve).")]
        [SerializeField] private float tailWaveNumber = 320.0f;

        [Header("Dorsal & Ventral Fins (Top & Bottom)")]
        [Tooltip("Flutter speed for dorsal and anal fins.")]
        [SerializeField] private float finFrequency = 10.0f;

        [Tooltip("Flutter amplitude in local Z units.")]
        [SerializeField] private float finAmplitude = 0.0012f;

        [Header("Pectoral Fins (Side Flippers)")]
        [Tooltip("Paddling frequency for left and right pectoral fins.")]
        [SerializeField] private float pectoralFrequency = 8.0f;

        [Tooltip("Forward/backward paddle amplitude in local X units.")]
        [SerializeField] private float pectoralPaddleAmplitude = 0.0008f;

        [Tooltip("In/out flap amplitude in local Z units.")]
        [SerializeField] private float pectoralFlapAmplitude = 0.0006f;

        [Header("Anatomy Slicing & Thresholds (Manual Tuning)")]
        [Tooltip("Local X coordinate where the tail starts bending. Vertices in front of this are 100% rigid (head/torso).")]
        [Range(-0.0050f, 0.0020f)]
        [SerializeField] private float tailStartThreshold = -0.0002f;

        [Tooltip("Local Y coordinate where dorsal (top) fin flutter begins.")]
        [Range(0.0005f, 0.0030f)]
        [SerializeField] private float dorsalStartThreshold = 0.0014f;

        [Tooltip("Local Y coordinate where ventral/pelvic (bottom) fin flutter begins.")]
        [Range(-0.0030f, -0.0005f)]
        [SerializeField] private float ventralStartThreshold = -0.0014f;

        [Tooltip("Local Z width distance where pectoral side fins begin.")]
        [Range(0.0002f, 0.0010f)]
        [SerializeField] private float pectoralZThreshold = 0.00060f;

        [Header("Gizmo Visualizer (Scene View)")]
        [Tooltip("Draw vertex weight spheres in Scene View: Red = Rigid Head/Torso, Green/Cyan = Moving Tail/Fins.")]
        [SerializeField] private bool showGizmoVisualizer = true;

        [Tooltip("Size of vertex gizmo spheres in Scene View.")]
        [Range(0.00005f, 0.00060f)]
        [SerializeField] private float gizmoSphereSize = 0.00022f;

        [Header("Speed Multiplier")]
        [Tooltip("Scales the animation speed. Driven dynamically by FishMovement.NormalizedSpeed.")]
        [SerializeField] private float speedMultiplier = 1.0f;

        [Header("Accessory Sockets (Auto-Generated)")]
        [SerializeField] private Transform hatSocket;
        [SerializeField] private Transform glassesSocket;

        private MeshFilter meshFilter;
        private Mesh dynamicMesh;
        private Vector3[] baseVertices;
        private Vector3[] animatedVertices;

        // Precomputed vertex weights (0 = rigid, 1 = fully deformed)
        private float[] tailWeights;
        private float[] dorsalWeights;
        private float[] ventralWeights;
        private float[] pectoralWeights;
        private float[] pectoralSideSign; // +1 for right fin, -1 for left fin

        private float randomPhase;
        private bool isInitialized = false;

        public Transform HatSocket => hatSocket;
        public Transform GlassesSocket => glassesSocket;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;

            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning("[ProceduralFishAnimator] Missing MeshFilter or sharedMesh.");
                return;
            }

            // Create an independent runtime instance marked dynamic for WebGL fast buffer updates
            dynamicMesh = Instantiate(meshFilter.sharedMesh);
            dynamicMesh.name = $"{meshFilter.sharedMesh.name}_ProceduralInstance";
            dynamicMesh.MarkDynamic();
            meshFilter.mesh = dynamicMesh;

            baseVertices = dynamicMesh.vertices;
            int count = baseVertices.Length;
            animatedVertices = new Vector3[count];

            randomPhase = Random.Range(0f, 100f);

            ComputeWeights(dynamicMesh.bounds, baseVertices);
            EnsureSockets(dynamicMesh.bounds);
            isInitialized = true;
        }

        /// <summary>
        /// Computes anatomical deformation weights for each vertex.
        /// Public so it can be re-run in Edit Mode / OnValidate when tuning sliders.
        /// </summary>
        public void ComputeWeights(Bounds bounds, Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return;

            int count = vertices.Length;
            if (tailWeights == null || tailWeights.Length != count)
            {
                tailWeights = new float[count];
                dorsalWeights = new float[count];
                ventralWeights = new float[count];
                pectoralWeights = new float[count];
                pectoralSideSign = new float[count];
            }

            float minX = bounds.min.x;     // Tail fin tip ~ -0.0067
            float minY = bounds.min.y;     // Ventral fin tip ~ -0.0035
            float maxY = bounds.max.y;     // Dorsal fin tip ~ +0.0035
            float maxZ = bounds.extents.z; // Pectoral fin outer edge ~ 0.0011

            for (int i = 0; i < count; i++)
            {
                Vector3 v = vertices[i];

                // 1. Caudal Fin / Tail Weight
                if (v.x < tailStartThreshold)
                {
                    float t = Mathf.Clamp01((tailStartThreshold - v.x) / (tailStartThreshold - minX));
                    // Quadratic easing: smooth transition near peduncle, flexible whip at tip
                    tailWeights[i] = t * t;
                }
                else
                {
                    tailWeights[i] = 0f;
                }

                // 2. Dorsal Fin Weight (Top Ridge)
                if (v.y > dorsalStartThreshold && v.x < 0.0008f)
                {
                    float t = Mathf.Clamp01((v.y - dorsalStartThreshold) / (maxY - dorsalStartThreshold));
                    dorsalWeights[i] = t;
                }
                else
                {
                    dorsalWeights[i] = 0f;
                }

                // 3. Ventral / Pelvic Fin Weight (Bottom Ridge)
                if (v.y < ventralStartThreshold && v.x < 0.0012f)
                {
                    float t = Mathf.Clamp01((ventralStartThreshold - v.y) / (ventralStartThreshold - minY));
                    ventralWeights[i] = t;
                }
                else
                {
                    ventralWeights[i] = 0f;
                }

                // 4. Pectoral Fins (Left & Right Side Flippers)
                if (Mathf.Abs(v.z) >= pectoralZThreshold && v.x >= -0.0015f && v.x <= 0.0028f)
                {
                    float t = Mathf.Clamp01((Mathf.Abs(v.z) - pectoralZThreshold) / (maxZ - pectoralZThreshold));
                    pectoralWeights[i] = t;
                    pectoralSideSign[i] = Mathf.Sign(v.z);
                }
                else
                {
                    pectoralWeights[i] = 0f;
                    pectoralSideSign[i] = 0f;
                }
            }
        }

        private void OnValidate()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            Mesh m = (dynamicMesh != null) ? dynamicMesh : (meshFilter != null ? meshFilter.sharedMesh : null);
            if (m != null)
            {
                ComputeWeights(m.bounds, m.vertices);
            }
        }

        private void EnsureSockets(Bounds bounds)
        {
            // Auto-create HatSocket and GlassesSocket on the head if not assigned
            if (hatSocket == null)
            {
                Transform found = transform.Find("HatSocket");
                if (found == null)
                {
                    GameObject go = new GameObject("HatSocket");
                    go.transform.SetParent(transform, false);
                    // Place above the forehead / crown
                    go.transform.localPosition = new Vector3(bounds.max.x * 0.45f, bounds.max.y * 0.85f, 0f);
                    go.transform.localRotation = Quaternion.identity;
                    hatSocket = go.transform;
                }
                else
                {
                    hatSocket = found;
                }
            }

            if (glassesSocket == null)
            {
                Transform found = transform.Find("GlassesSocket");
                if (found == null)
                {
                    GameObject go = new GameObject("GlassesSocket");
                    go.transform.SetParent(transform, false);
                    // Place on the bridge of the snout / eye region
                    go.transform.localPosition = new Vector3(bounds.max.x * 0.68f, bounds.max.y * 0.20f, 0f);
                    go.transform.localRotation = Quaternion.identity;
                    glassesSocket = go.transform;
                }
                else
                {
                    glassesSocket = found;
                }
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized || dynamicMesh == null) return;

            float time = (Time.time + randomPhase) * speedMultiplier;

            // Tail travelling wave
            float tailOmega = time * tailFrequency;
            // Fins flutter
            float finOmega = time * finFrequency;
            // Pectoral paddle
            float pecOmega = time * pectoralFrequency;

            int count = baseVertices.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = baseVertices[i];
                float offsetX = 0f;
                float offsetZ = 0f;

                // 1. Tail Sway (Local Z)
                float tWeight = tailWeights[i];
                if (tWeight > 0.0001f)
                {
                    float phase = tailOmega + v.x * tailWaveNumber;
                    offsetZ += Mathf.Sin(phase) * tailAmplitude * tWeight;
                }

                // 2. Dorsal Fin Flutter (Local Z)
                float dWeight = dorsalWeights[i];
                if (dWeight > 0.0001f)
                {
                    float phase = finOmega + v.x * (tailWaveNumber * 1.3f) + 0.6f;
                    offsetZ += Mathf.Sin(phase) * finAmplitude * dWeight;
                }

                // 3. Ventral Fin Flutter (Local Z)
                float vWeight = ventralWeights[i];
                if (vWeight > 0.0001f)
                {
                    float phase = finOmega + v.x * (tailWaveNumber * 1.3f) + 1.2f;
                    offsetZ += Mathf.Sin(phase) * finAmplitude * vWeight;
                }

                // 4. Pectoral Fin Paddle (Local X forward/back & Local Z flap)
                float pWeight = pectoralWeights[i];
                if (pWeight > 0.0001f)
                {
                    float side = pectoralSideSign[i];
                    offsetX += Mathf.Sin(pecOmega) * pectoralPaddleAmplitude * pWeight;
                    offsetZ += Mathf.Cos(pecOmega) * pectoralFlapAmplitude * side * pWeight;
                }

                animatedVertices[i].x = v.x + offsetX;
                animatedVertices[i].y = v.y;
                animatedVertices[i].z = v.z + offsetZ;
            }

            // Apply deformed vertices to mesh buffer with fast GPU push
            dynamicMesh.vertices = animatedVertices;
            dynamicMesh.RecalculateNormals();
            dynamicMesh.RecalculateBounds();
        }

        public void SetSpeedMultiplier(float speed)
        {
            speedMultiplier = Mathf.Clamp(speed, 0.4f, 2.5f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showGizmoVisualizer) return;

            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            Mesh m = (dynamicMesh != null) ? dynamicMesh : (meshFilter != null ? meshFilter.sharedMesh : null);
            if (m == null) return;

            Vector3[] verts = m.vertices;
            if (verts == null || verts.Length == 0) return;

            if (tailWeights == null || tailWeights.Length != verts.Length)
            {
                ComputeWeights(m.bounds, verts);
            }

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            Bounds b = m.bounds;

            // 1. Draw Slicing Plane for Tail Threshold (Cyan wireframe boundary)
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            Vector3 planeCenter = localToWorld.MultiplyPoint3x4(new Vector3(tailStartThreshold, b.center.y, b.center.z));
            Vector3 pTopLeft = localToWorld.MultiplyPoint3x4(new Vector3(tailStartThreshold, b.max.y * 1.25f, b.max.z * 1.4f));
            Vector3 pTopRight = localToWorld.MultiplyPoint3x4(new Vector3(tailStartThreshold, b.max.y * 1.25f, b.min.z * 1.4f));
            Vector3 pBottomRight = localToWorld.MultiplyPoint3x4(new Vector3(tailStartThreshold, b.min.y * 1.25f, b.min.z * 1.4f));
            Vector3 pBottomLeft = localToWorld.MultiplyPoint3x4(new Vector3(tailStartThreshold, b.min.y * 1.25f, b.max.z * 1.4f));

            Gizmos.DrawLine(pTopLeft, pTopRight);
            Gizmos.DrawLine(pTopRight, pBottomRight);
            Gizmos.DrawLine(pBottomRight, pBottomLeft);
            Gizmos.DrawLine(pBottomLeft, pTopLeft);

            // 2. Draw Colored Spheres on every single vertex
            // RED = 100% Rigid Head & Torso
            // YELLOW/GREEN/CYAN = Moving Tail & Fins
            float radius = gizmoSphereSize * Mathf.Max(0.001f, transform.lossyScale.x);

            for (int i = 0; i < verts.Length; i++)
            {
                float t = (tailWeights != null && i < tailWeights.Length) ? tailWeights[i] : 0f;
                float d = (dorsalWeights != null && i < dorsalWeights.Length) ? dorsalWeights[i] : 0f;
                float v = (ventralWeights != null && i < ventralWeights.Length) ? ventralWeights[i] : 0f;
                float p = (pectoralWeights != null && i < pectoralWeights.Length) ? pectoralWeights[i] : 0f;

                float maxW = Mathf.Max(t, d, v, p);

                Color dotColor;
                if (maxW < 0.0001f)
                {
                    // Solid RED: Rigid (stationary)
                    dotColor = new Color(1.0f, 0.15f, 0.15f, 0.95f);
                }
                else
                {
                    // Gradient: Yellow (start of tail) -> Neon Green (mid tail) -> Electric Cyan (whip tip)
                    if (maxW < 0.5f)
                    {
                        dotColor = Color.Lerp(new Color(1f, 0.88f, 0.15f, 0.95f), new Color(0.2f, 1f, 0.35f, 1f), maxW * 2f);
                    }
                    else
                    {
                        dotColor = Color.Lerp(new Color(0.2f, 1f, 0.35f, 1f), new Color(0.1f, 0.9f, 1f, 1f), (maxW - 0.5f) * 2f);
                    }
                }

                Gizmos.color = dotColor;
                Vector3 worldVert = localToWorld.MultiplyPoint3x4(verts[i]);
                Gizmos.DrawSphere(worldVert, radius);
            }
        }
#endif

        private void OnDestroy()
        {
            if (dynamicMesh != null)
            {
                Destroy(dynamicMesh);
            }
        }
    }
}
