using UnityEngine;

namespace Endowment.Gameplay
{
    /// <summary>
    /// Animates separated fish parts (Ekor, Sirip1, Sirip2) by rotating them on their pivot axes.
    /// Designed specifically for modular fish models like NemoFihTierSperate.
    /// Oscillates cleanly around each part's initial resting rotation without drifting.
    /// </summary>
    [ExecuteAlways]
    public class ModularFishAnimator : MonoBehaviour
    {
        public enum RotationAxis { X, Y, Z }

        [Header("Fish Part References")]
        [Tooltip("Parent transform holding the tail mesh (with pivot at base of tail).")]
        [SerializeField] private Transform ekor;

        [Tooltip("Parent transform holding left pectoral fin (with pivot at fin base).")]
        [SerializeField] private Transform sirip1;

        [Tooltip("Parent transform holding right pectoral fin (with pivot at fin base).")]
        [SerializeField] private Transform sirip2;

        [Header("Rotation Axes")]
        [Tooltip("Axis of rotation for Ekor. Default is Z.")]
        [SerializeField] private RotationAxis ekorAxis = RotationAxis.Z;

        [Tooltip("Axis of rotation for Sirip 1. Default is Z.")]
        [SerializeField] private RotationAxis sirip1Axis = RotationAxis.Z;

        [Tooltip("Axis of rotation for Sirip 2. Default is Z.")]
        [SerializeField] private RotationAxis sirip2Axis = RotationAxis.Z;

        [Header("Ekor (Tail) Motion")]
        [Tooltip("Wagging speed/frequency of the tail.")]
        [SerializeField] private float ekorSpeed = 8.0f;

        [Tooltip("Maximum oscillation angle in degrees (+/- range).")]
        [Range(0f, 60f)]
        [SerializeField] private float ekorAngleRange = 25.0f;

        [Header("Sirip (Fins) Motion")]
        [Tooltip("Flapping speed/frequency of pectoral fins.")]
        [SerializeField] private float siripSpeed = 7.5f;

        [Tooltip("Maximum flapping angle in degrees (+/- range).")]
        [Range(0f, 60f)]
        [SerializeField] private float siripAngleRange = 20.0f;

        [Tooltip("Phase offset between tail and fin flapping for organic motion.")]
        [SerializeField] private float finPhaseOffset = 0.5f;

        [Tooltip("Inverts Sirip2 flapping direction so both fins paddle synchronously.")]
        [SerializeField] private bool invertSirip2 = true;

        [Header("Speed & Controls")]
        [Tooltip("Speed multiplier dynamically scaled by swimming movement.")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float speedMultiplier = 1.0f;

        [Tooltip("Enables live preview in Scene View during Edit Mode without pressing Play.")]
        [SerializeField] private bool previewInEditMode = true;

        // Base resting local Euler angles
        private Vector3 ekorBaseEuler;
        private Vector3 sirip1BaseEuler;
        private Vector3 sirip2BaseEuler;

        private bool hasRecordedBase = false;

        private void Reset()
        {
            AutoFindParts();
        }

        private void Awake()
        {
            AutoFindParts();
            RecordBaseRotations();
        }

        private void OnEnable()
        {
            RecordBaseRotations();
        }

        private void OnDisable()
        {
            RestoreBaseRotations();
        }

        public void AutoFindParts()
        {
            if (ekor == null) ekor = FindDeepChild(transform, "Ekor");
            if (sirip1 == null) sirip1 = FindDeepChild(transform, "Sirip1");
            if (sirip2 == null) sirip2 = FindDeepChild(transform, "Sirip2");
        }

        private Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(t.name, childName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }
            return null;
        }

        public void RecordBaseRotations()
        {
            if (ekor != null) ekorBaseEuler = ekor.localEulerAngles;
            if (sirip1 != null) sirip1BaseEuler = sirip1.localEulerAngles;
            if (sirip2 != null) sirip2BaseEuler = sirip2.localEulerAngles;
            hasRecordedBase = true;
        }

        public void RestoreBaseRotations()
        {
            if (!hasRecordedBase) return;
            if (ekor != null) ekor.localEulerAngles = ekorBaseEuler;
            if (sirip1 != null) sirip1.localEulerAngles = sirip1BaseEuler;
            if (sirip2 != null) sirip2.localEulerAngles = sirip2BaseEuler;
        }

        private void Update()
        {
            if (!Application.isPlaying && !previewInEditMode) return;

            float time = (Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup);
            AnimateParts(time);
        }

        public void AnimateParts(float time)
        {
            float t = time * speedMultiplier;

            // 1. Ekor Oscillation
            if (ekor != null)
            {
                float ekorDelta = Mathf.Sin(t * ekorSpeed) * ekorAngleRange;
                ekor.localEulerAngles = ApplyOffset(ekorBaseEuler, ekorAxis, ekorDelta);
            }

            // 2. Sirip1 (Left Fin) Flapping
            if (sirip1 != null)
            {
                float sirip1Delta = Mathf.Sin(t * siripSpeed + finPhaseOffset) * siripAngleRange;
                sirip1.localEulerAngles = ApplyOffset(sirip1BaseEuler, sirip1Axis, sirip1Delta);
            }

            // 3. Sirip2 (Right Fin) Flapping
            if (sirip2 != null)
            {
                float sign = invertSirip2 ? -1.0f : 1.0f;
                float sirip2Delta = Mathf.Sin(t * siripSpeed + finPhaseOffset) * (siripAngleRange * sign);
                sirip2.localEulerAngles = ApplyOffset(sirip2BaseEuler, sirip2Axis, sirip2Delta);
            }
        }

        private Vector3 ApplyOffset(Vector3 baseEuler, RotationAxis axis, float offsetDeg)
        {
            Vector3 euler = baseEuler;
            switch (axis)
            {
                case RotationAxis.X: euler.x += offsetDeg; break;
                case RotationAxis.Y: euler.y += offsetDeg; break;
                case RotationAxis.Z: euler.z += offsetDeg; break;
            }
            return euler;
        }

        public void SetSpeedMultiplier(float speed)
        {
            speedMultiplier = Mathf.Clamp(speed, 0.4f, 2.5f);
        }
    }
}

