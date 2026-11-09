using UnityEngine;

/// <summary>
/// Animasi dinamis prosedural untuk rig tulang ikan 3D (Nemo & Tuna).
/// Menggerakkan rantai tulang (spine/tail, sirip, dan kepala) secara natural
/// menggunakan traveling wave (gelombang berjalan) yang tersinkronisasi 
/// dengan kecepatan renang dan arah belok dari FishMovement.
/// 
/// Komponen ini terpisah dari FishMovement dan Animator:
/// - Saat AKTIF: Mengontrol tulang rig secara dinamis dan otomatis menonaktifkan Animator.
/// - Saat NONAKTIF: Mengembalikan pose tulang ke posisi semula (rest pose) dan mengaktifkan kembali Animator.
/// Hal ini memudahkan pengujian komparasi langsung di Inspector via toggle checkbox.
/// </summary>
public class DynamicRiggedFishAnimator : MonoBehaviour
{
    [Header("Mode & Kompatibilitas")]
    [Tooltip("Jika dicentang, otomatis menonaktifkan Animator saat script ini aktif, dan mengaktifkannya kembali saat script ini dinonaktifkan.")]
    [SerializeField] private bool manageAnimator = true;

    [Tooltip("Pengali intensitas keseluruhan gerakan (1.0 = normal/natural, < 1.0 = lebih tenang, > 1.0 = lebih bertenaga).")]
    [Range(0.2f, 2.0f)]
    [SerializeField] private float masterIntensity = 1.0f;

    [Header("Frekuensi & Kecepatan Kibasan Ekor")]
    [Tooltip("Frekuensi kibasan ekor saat ikan meluncur pelan atau idle (putaran per detik).")]
    [SerializeField] private float idleFrequency = 1.1f;

    [Tooltip("Frekuensi kibasan ekor saat ikan berenang di kecepatan maksimum (putaran per detik).")]
    [SerializeField] private float maxSwimFrequency = 3.4f;

    [Header("Amplitudo Kibasan Badan & Ekor (Derajat)")]
    [Tooltip("Sudut kibasan ujung sirip ekor (DEF-Body.004) saat berenang penuh. Dibuat moderat agar natural.")]
    [SerializeField] private float tailFinMaxAngle = 13.5f;

    [Tooltip("Sudut kibasan pangkal ekor (DEF-Body.001) saat berenang penuh.")]
    [SerializeField] private float tailBaseMaxAngle = 9.0f;

    [Tooltip("Sudut kibasan badan belakang (DEF-Body.003).")]
    [SerializeField] private float midBodyRearMaxAngle = 5.0f;

    [Tooltip("Sudut kibasan badan tengah (DEF-Body.002).")]
    [SerializeField] private float midBodyFrontMaxAngle = 2.5f;

    [Tooltip("Sudut kibasan pangkal badan/torso (DEF-Body). Tetap minim agar kepala tidak goyang berlebihan.")]
    [SerializeField] private float baseTorsoMaxAngle = 1.0f;

    [Tooltip("Sudut kibasan lembut saat ikan diam/idle (gerakan bernapas di tempat).")]
    [SerializeField] private float idleBreatheAngle = 2.8f;

    [Header("Propagasi Gelombang (S-Curve Delay)")]
    [Tooltip("Keterlambatan fase antar ruas tulang belakang. Menghasilkan lekukan gelombang S yang luwes dan alami.")]
    [SerializeField] private float spineWaveDelay = 0.52f;

    [Header("Lekukan Badan Saat Belok (Spine Curvature)")]
    [Tooltip("Seberapa kuat tulang belakang melengkung mengikuti arah belokan ikan (degrees).")]
    [SerializeField] private float turnCurvatureStrength = 7.5f;

    [Tooltip("Kecepatan transisi lekukan saat berbelok (kehalusan belokan).")]
    [SerializeField] private float turnSmoothSpeed = 4.5f;

    [Header("Sirip Dada (Pectoral Fins)")]
    [Tooltip("Amplitudo kepakan sirip dada kiri & kanan (DEF-Fin.L / DEF-Fin.R).")]
    [SerializeField] private float finFlapAmplitude = 14.0f;

    [Tooltip("Pengali frekuensi kepakan sirip relatif terhadap kibasan ekor.")]
    [SerializeField] private float finFrequencyMultiplier = 1.25f;

    [Tooltip("Sudut sirip membuka sedikit saat meluncur lambat/idle sebagai penyeimbang air.")]
    [SerializeField] private float finIdleSpreadAngle = 4.0f;

    [Header("Stabilisasi Kepala (Head Counter-Yaw)")]
    [Tooltip("Gerakan penyeimbang halus pada kepala berlawanan arah dengan kibasan ekor agar pandangan tetap fokus ke depan.")]
    [SerializeField] private float headStabilizeAngle = 1.8f;

    [Header("Referensi Tulang (Otomatis Dicari Jika Kosong)")]
    [SerializeField] private Transform boneBody;       // DEF-Body
    [SerializeField] private Transform boneBody2;      // DEF-Body.002
    [SerializeField] private Transform boneBody3;      // DEF-Body.003
    [SerializeField] private Transform boneBody1;      // DEF-Body.001
    [SerializeField] private Transform boneTailTip;    // DEF-Body.004
    [SerializeField] private Transform boneHead;       // DEF-Head
    [SerializeField] private Transform boneFinL;       // DEF-Fin.L
    [SerializeField] private Transform boneFinL2;      // DEF-Fin.L.002
    [SerializeField] private Transform boneFinR;       // DEF-Fin.R
    [SerializeField] private Transform boneFinR2;      // DEF-Fin.R.002

    [Header("Komponen Animator Terkait")]
    [Tooltip("Komponen Animator yang dinonaktifkan saat script ini aktif. Otomatis dicari jika kosong.")]
    [SerializeField] private Animator targetAnimator;

    // Rest pose cache (Rotasi bawaan FBX)
    private Quaternion restRotBody;
    private Quaternion restRotBody2;
    private Quaternion restRotBody3;
    private Quaternion restRotBody1;
    private Quaternion restRotTailTip;
    private Quaternion restRotHead;
    private Quaternion restRotFinL;
    private Quaternion restRotFinL2;
    private Quaternion restRotFinR;
    private Quaternion restRotFinR2;
    private bool hasCachedRestPose = false;

    // Komponen eksternal
    private FishMovement boundMovement;
    private MonoBehaviour boundView;

    // State dinamis runtime
    private float currentSpeedRatio = 0.35f;
    private float targetSpeedRatio = 0.35f;
    private float currentTurnYawVelocity = 0f;
    private float smoothedTurnAngle = 0f;
    private float turnAngleVelocity = 0f;
    private float currentPitch = 0f;
    private bool isIdleState = false;

    private float waveCycleTime = 0f;
    private float finCycleTime = 0f;
    private float randomPhase = 0f;

    private void Awake()
    {
        randomPhase = Random.Range(0f, 100f);
        waveCycleTime = randomPhase;
        finCycleTime = randomPhase;

        EnsureBonesFound();
        CacheRestPose();
        EnsureAnimatorReference();
    }

    private void Start()
    {
        if (manageAnimator)
        {
            EnsureAnimatorReference();
            if (targetAnimator != null)
            {
                targetAnimator.enabled = false;
            }
        }
    }

    private void EnsureAnimatorReference()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
            if (targetAnimator == null) targetAnimator = GetComponentInChildren<Animator>(true);
            if (targetAnimator == null) targetAnimator = GetComponentInParent<Animator>();
        }
    }

    private void OnEnable()
    {
        EnsureBonesFound();
        CacheRestPose();

        // Matikan Animator jika manageAnimator aktif
        if (manageAnimator)
        {
            EnsureAnimatorReference();
            if (targetAnimator != null)
            {
                targetAnimator.enabled = false;
            }
        }
    }

    private void OnDisable()
    {
        // Kembalikan semua tulang ke pose istirahat (rest pose) agar tidak beku miring
        ResetBonesToRestPose();

        // Aktifkan kembali Animator jika manageAnimator aktif
        if (manageAnimator && targetAnimator != null)
        {
            targetAnimator.enabled = true;
        }
    }

    /// <summary>
    /// Menghubungkan script ini dengan FishMovement dan Fish3DView secara dinamis.
    /// </summary>
    public void Bind(FishMovement movement, MonoBehaviour view)
    {
        boundMovement = movement;
        boundView = view;
    }

    /// <summary>
    /// Menerima data pergerakan langsung dari Fish3DView setiap frame.
    /// </summary>
    public void UpdateMotion(float normalizedSpeed, float turnYawVelocity, float pitchAngle, bool isIdle)
    {
        targetSpeedRatio = Mathf.Clamp01(normalizedSpeed);
        currentTurnYawVelocity = turnYawVelocity;
        currentPitch = pitchAngle;
        isIdleState = isIdle;
    }

    private void LateUpdate()
    {
        if (manageAnimator)
        {
            EnsureAnimatorReference();
            if (targetAnimator != null && targetAnimator.enabled)
            {
                targetAnimator.enabled = false;
            }
        }

        if (!hasCachedRestPose)
        {
            CacheRestPose();
            if (!hasCachedRestPose) return;
        }

        // 1. Ambil data pergerakan jika belum di-update secara eksternal oleh Fish3DView
        if (boundMovement != null)
        {
            targetSpeedRatio = boundMovement.NormalizedSpeed;
            isIdleState = targetSpeedRatio < 0.05f;
        }

        // Haluskan transisi kecepatan renang
        currentSpeedRatio = Mathf.MoveTowards(currentSpeedRatio, targetSpeedRatio, Time.deltaTime * 3.0f);

        // 2. Hitung frekuensi gelombang renang (lambat saat idle, cepat saat renang kencang)
        float activeFrequency = Mathf.Lerp(idleFrequency, maxSwimFrequency, currentSpeedRatio);
        waveCycleTime += Time.deltaTime * activeFrequency * Mathf.PI * 2.0f;
        finCycleTime += Time.deltaTime * (activeFrequency * finFrequencyMultiplier) * Mathf.PI * 2.0f;

        // 3. Hitung lekukan badan saat berbelok (Spine Curvature)
        // turnYawVelocity positif = belok ke satu sisi, negatif = belok ke sisi lain
        float targetTurnAngle = Mathf.Clamp(currentTurnYawVelocity * 0.035f, -turnCurvatureStrength, turnCurvatureStrength);
        smoothedTurnAngle = Mathf.SmoothDamp(smoothedTurnAngle, targetTurnAngle, ref turnAngleVelocity, 1.0f / turnSmoothSpeed, 50f, Time.deltaTime);

        // 4. Hitung amplitudo adaptif (intensitas gerakan)
        float speedFactor = Mathf.Lerp(0.35f, 1.0f, currentSpeedRatio);
        float idleBlend = isIdleState ? (1.0f - currentSpeedRatio) : 0f;

        float intensity = masterIntensity;

        // 5. Animasi Ruas Tulang Belakang (Spine & Caudal Fin)
        // Gelombang berjalan bergerak dari depan (delay besar) ke belakang (delay kecil)
        // Rotasi lateral pada sumbu lokal Z
        if (boneBody != null)
        {
            float phase = waveCycleTime - (4.0f * spineWaveDelay);
            float sway = Mathf.Sin(phase) * (baseTorsoMaxAngle * speedFactor + idleBreatheAngle * 0.2f * idleBlend);
            float turn = smoothedTurnAngle * 0.15f;
            boneBody.localRotation = restRotBody * Quaternion.Euler(0f, 0f, (sway + turn) * intensity);
        }

        if (boneBody2 != null)
        {
            float phase = waveCycleTime - (3.0f * spineWaveDelay);
            float sway = Mathf.Sin(phase) * (midBodyFrontMaxAngle * speedFactor + idleBreatheAngle * 0.35f * idleBlend);
            float turn = smoothedTurnAngle * 0.35f;
            boneBody2.localRotation = restRotBody2 * Quaternion.Euler(0f, 0f, (sway + turn) * intensity);
        }

        if (boneBody3 != null)
        {
            float phase = waveCycleTime - (2.0f * spineWaveDelay);
            float sway = Mathf.Sin(phase) * (midBodyRearMaxAngle * speedFactor + idleBreatheAngle * 0.55f * idleBlend);
            float turn = smoothedTurnAngle * 0.65f;
            boneBody3.localRotation = restRotBody3 * Quaternion.Euler(0f, 0f, (sway + turn) * intensity);
        }

        if (boneBody1 != null)
        {
            float phase = waveCycleTime - (1.0f * spineWaveDelay);
            float sway = Mathf.Sin(phase) * (tailBaseMaxAngle * speedFactor + idleBreatheAngle * 0.80f * idleBlend);
            float turn = smoothedTurnAngle * 0.85f;
            boneBody1.localRotation = restRotBody1 * Quaternion.Euler(0f, 0f, (sway + turn) * intensity);
        }

        if (boneTailTip != null)
        {
            float phase = waveCycleTime;
            float sway = Mathf.Sin(phase) * (tailFinMaxAngle * speedFactor + idleBreatheAngle * 1.0f * idleBlend);
            float turn = smoothedTurnAngle * 1.0f;
            boneTailTip.localRotation = restRotTailTip * Quaternion.Euler(0f, 0f, (sway + turn) * intensity);
        }

        // 6. Stabilisasi Kepala (Head Counter-Balance)
        if (boneHead != null)
        {
            // Bergerak sedikit berlawanan dengan pangkal ekor agar kepala tampak stabil
            float headSway = -Mathf.Sin(waveCycleTime - (3.5f * spineWaveDelay)) * (headStabilizeAngle * speedFactor);
            float headTurn = -smoothedTurnAngle * 0.15f;
            boneHead.localRotation = restRotHead * Quaternion.Euler(0f, 0f, (headSway + headTurn) * intensity);
        }

        // 7. Sirip Dada (Pectoral Fins)
        // Mendayung maju-mundur dan mengepak lateral
        float finFlap = Mathf.Sin(finCycleTime) * (finFlapAmplitude * speedFactor);
        float finPaddle = Mathf.Cos(finCycleTime) * (finFlapAmplitude * 0.35f * speedFactor);
        float idleSpread = finIdleSpreadAngle * (1.0f - currentSpeedRatio);

        if (boneFinL != null)
        {
            boneFinL.localRotation = restRotFinL * Quaternion.Euler(finPaddle * intensity, 0f, (finFlap + idleSpread) * intensity);
        }
        if (boneFinL2 != null)
        {
            boneFinL2.localRotation = restRotFinL2 * Quaternion.Euler(finPaddle * 0.5f * intensity, 0f, (finFlap * 0.6f) * intensity);
        }

        if (boneFinR != null)
        {
            // Sirip kanan dengan sedikit offset fase untuk kesan organik
            float finFlapR = Mathf.Sin(finCycleTime + 0.3f) * (finFlapAmplitude * speedFactor);
            float finPaddleR = Mathf.Cos(finCycleTime + 0.3f) * (finFlapAmplitude * 0.35f * speedFactor);
            boneFinR.localRotation = restRotFinR * Quaternion.Euler(finPaddleR * intensity, 0f, (-finFlapR - idleSpread) * intensity);
        }
        if (boneFinR2 != null)
        {
            float finFlapR = Mathf.Sin(finCycleTime + 0.3f) * (finFlapAmplitude * speedFactor);
            float finPaddleR = Mathf.Cos(finCycleTime + 0.3f) * (finFlapAmplitude * 0.35f * speedFactor);
            boneFinR2.localRotation = restRotFinR2 * Quaternion.Euler(finPaddleR * 0.5f * intensity, 0f, (-finFlapR * 0.6f) * intensity);
        }
    }

    /// <summary>
    /// Mencari referensi tulang rig secara otomatis berdasarkan nama standar FBX.
    /// </summary>
    public void EnsureBonesFound()
    {
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allChildren)
        {
            switch (t.name)
            {
                case "DEF-Body":
                    if (boneBody == null) boneBody = t;
                    break;
                case "DEF-Body.002":
                    if (boneBody2 == null) boneBody2 = t;
                    break;
                case "DEF-Body.003":
                    if (boneBody3 == null) boneBody3 = t;
                    break;
                case "DEF-Body.001":
                    if (boneBody1 == null) boneBody1 = t;
                    break;
                case "DEF-Body.004":
                    if (boneTailTip == null) boneTailTip = t;
                    break;
                case "DEF-Head":
                    if (boneHead == null) boneHead = t;
                    break;
                case "DEF-Fin.L":
                    if (boneFinL == null) boneFinL = t;
                    break;
                case "DEF-Fin.L.002":
                    if (boneFinL2 == null) boneFinL2 = t;
                    break;
                case "DEF-Fin.R":
                    if (boneFinR == null) boneFinR = t;
                    break;
                case "DEF-Fin.R.002":
                    if (boneFinR2 == null) boneFinR2 = t;
                    break;
            }
        }
    }

    /// <summary>
    /// Menyimpan rotasi bawaan rig saat pertama kali diinisialisasi.
    /// </summary>
    public void CacheRestPose()
    {
        if (hasCachedRestPose) return;

        EnsureBonesFound();

        if (boneBody != null) restRotBody = boneBody.localRotation;
        if (boneBody2 != null) restRotBody2 = boneBody2.localRotation;
        if (boneBody3 != null) restRotBody3 = boneBody3.localRotation;
        if (boneBody1 != null) restRotBody1 = boneBody1.localRotation;
        if (boneTailTip != null) restRotTailTip = boneTailTip.localRotation;
        if (boneHead != null) restRotHead = boneHead.localRotation;
        if (boneFinL != null) restRotFinL = boneFinL.localRotation;
        if (boneFinL2 != null) restRotFinL2 = boneFinL2.localRotation;
        if (boneFinR != null) restRotFinR = boneFinR.localRotation;
        if (boneFinR2 != null) restRotFinR2 = boneFinR2.localRotation;

        if (boneTailTip != null || boneBody != null)
        {
            hasCachedRestPose = true;
        }
    }

    /// <summary>
    /// Mengembalikan semua tulang ke pose istirahat awal.
    /// </summary>
    public void ResetBonesToRestPose()
    {
        if (!hasCachedRestPose) return;

        if (boneBody != null) boneBody.localRotation = restRotBody;
        if (boneBody2 != null) boneBody2.localRotation = restRotBody2;
        if (boneBody3 != null) boneBody3.localRotation = restRotBody3;
        if (boneBody1 != null) boneBody1.localRotation = restRotBody1;
        if (boneTailTip != null) boneTailTip.localRotation = restRotTailTip;
        if (boneHead != null) boneHead.localRotation = restRotHead;
        if (boneFinL != null) boneFinL.localRotation = restRotFinL;
        if (boneFinL2 != null) boneFinL2.localRotation = restRotFinL2;
        if (boneFinR != null) boneFinR.localRotation = restRotFinR;
        if (boneFinR2 != null) boneFinR2.localRotation = restRotFinR2;
    }

    private void Reset()
    {
        EnsureBonesFound();
        CacheRestPose();
    }
}
