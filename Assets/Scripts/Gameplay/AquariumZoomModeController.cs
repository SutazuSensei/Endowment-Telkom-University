using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Connects the Prototype's CameraZoomTransition with Personal Aquarium / Big Aquarium mode.
/// Supports both:
/// 1. In-scene Zoom Out / Zoom In toggle buttons (Btn_bigAquarium / Btn_smallAquarium).
/// 2. Automatic Zoom Out on Start when entering the BigAquarium scene.
/// </summary>
public class AquariumZoomModeController : MonoBehaviour
{
    [Header("Camera Zoom Reference")]
    [SerializeField] private CameraZoomTransition cameraZoomTransition;

    [Header("Mode Toggle Buttons (Optional)")]
    [SerializeField] private Button btnBigAquarium;
    [SerializeField] private Button btnSmallAquarium;

    [Header("Mode Containers (Optional)")]
    [Tooltip("Objects to show only in Big Aquarium mode (e.g. extra donor fish container).")]
    [SerializeField] private GameObject[] bigAquariumObjects;

    [Tooltip("Objects to show only in Personal Aquarium mode (e.g. personal HUD panels).")]
    [SerializeField] private GameObject[] personalAquariumObjects;

    [Header("Startup Behavior")]
    [SerializeField] private bool zoomOutOnStart = false;

    private void Start()
    {
        if (cameraZoomTransition == null)
        {
            cameraZoomTransition = FindFirstObjectByType<CameraZoomTransition>();
        }

        if (btnBigAquarium != null)
        {
            btnBigAquarium.onClick.AddListener(EnterBigAquariumMode);
        }

        if (btnSmallAquarium != null)
        {
            btnSmallAquarium.onClick.AddListener(EnterPersonalAquariumMode);
        }

        if (zoomOutOnStart)
        {
            EnterBigAquariumMode();
        }
        else
        {
            if (btnBigAquarium != null) btnBigAquarium.gameObject.SetActive(true);
            if (btnSmallAquarium != null) btnSmallAquarium.gameObject.SetActive(false);
        }
    }

    public void EnterBigAquariumMode()
    {
        if (cameraZoomTransition != null)
        {
            cameraZoomTransition.TransitionOut();
        }

        if (btnBigAquarium != null) btnBigAquarium.gameObject.SetActive(false);
        if (btnSmallAquarium != null) btnSmallAquarium.gameObject.SetActive(true);

        if (bigAquariumObjects != null)
        {
            foreach (var obj in bigAquariumObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (personalAquariumObjects != null)
        {
            foreach (var obj in personalAquariumObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    public void EnterPersonalAquariumMode()
    {
        if (cameraZoomTransition != null)
        {
            cameraZoomTransition.TransitionBack();
        }

        if (btnBigAquarium != null) btnBigAquarium.gameObject.SetActive(true);
        if (btnSmallAquarium != null) btnSmallAquarium.gameObject.SetActive(false);

        if (bigAquariumObjects != null)
        {
            foreach (var obj in bigAquariumObjects)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        if (personalAquariumObjects != null)
        {
            foreach (var obj in personalAquariumObjects)
            {
                if (obj != null) obj.SetActive(true);
            }
        }
    }
}

