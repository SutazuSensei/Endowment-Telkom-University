using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the visibility of the Settings Panel and Volume Panel in the Main Menu scene.
/// Connects BtnSettings / BtnVolume to open their respective panels, and their Back buttons to close them.
/// </summary>
public class SettingsController : MonoBehaviour
{
    [Header("Settings Panel UI")]
    [Tooltip("The Settings Panel GameObject that will be shown/hidden.")]
    [SerializeField] private GameObject settingsPanel;

    [Tooltip("The Settings Button (BtnSettings) that opens the Settings Panel.")]
    [SerializeField] private Button btnSettings;

    [Tooltip("The Back Button inside the Settings Panel that closes it.")]
    [SerializeField] private Button btnBack;

    [Header("Volume Panel UI")]
    [Tooltip("The Volume Panel GameObject that will be shown/hidden.")]
    [SerializeField] private GameObject volumePanel;

    [Tooltip("The Volume Button (BtnVolume) that opens the Volume Panel.")]
    [SerializeField] private Button btnVolume;

    [Tooltip("The Back Button inside the Volume Panel that closes it.")]
    [SerializeField] private Button btnVolumeBack;

    [Header("Behavior")]
    [Tooltip("If true, automatically hides both Settings and Volume panels when the scene starts.")]
    [SerializeField] private bool closeOnStart = true;

    private void Start()
    {
        if (closeOnStart)
        {
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (volumePanel != null) volumePanel.SetActive(false);
        }

        if (btnSettings != null)
        {
            btnSettings.onClick.AddListener(OpenSettings);
        }

        if (btnBack != null)
        {
            btnBack.onClick.AddListener(CloseSettings);
        }

        if (btnVolume != null)
        {
            btnVolume.onClick.AddListener(OpenVolume);
        }

        if (btnVolumeBack != null)
        {
            btnVolumeBack.onClick.AddListener(CloseVolume);
        }
    }

    private void OnDestroy()
    {
        if (btnSettings != null)
        {
            btnSettings.onClick.RemoveListener(OpenSettings);
        }

        if (btnBack != null)
        {
            btnBack.onClick.RemoveListener(CloseSettings);
        }

        if (btnVolume != null)
        {
            btnVolume.onClick.RemoveListener(OpenVolume);
        }

        if (btnVolumeBack != null)
        {
            btnVolumeBack.onClick.RemoveListener(CloseVolume);
        }
    }

    // ───────────────────────────── Settings Panel ─────────────────────────────

    /// <summary>
    /// Opens the Settings Panel and hides the Volume Panel if open.
    /// Can also be called via Unity Inspector OnClick event.
    /// </summary>
    public void OpenSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }

        if (volumePanel != null)
        {
            volumePanel.SetActive(false);
        }
    }

    /// <summary>
    /// Closes the Settings Panel.
    /// Can also be called via Unity Inspector OnClick event.
    /// </summary>
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Toggles the Settings Panel visibility.
    /// </summary>
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            bool willOpen = !settingsPanel.activeSelf;
            settingsPanel.SetActive(willOpen);
            if (willOpen && volumePanel != null)
            {
                volumePanel.SetActive(false);
            }
        }
    }

    // ───────────────────────────── Volume Panel ─────────────────────────────

    /// <summary>
    /// Opens the Volume Panel and hides the Settings Panel if open.
    /// Can also be called via Unity Inspector OnClick event.
    /// </summary>
    public void OpenVolume()
    {
        if (volumePanel != null)
        {
            volumePanel.SetActive(true);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Closes the Volume Panel.
    /// Can also be called via Unity Inspector OnClick event.
    /// </summary>
    public void CloseVolume()
    {
        if (volumePanel != null)
        {
            volumePanel.SetActive(false);
        }
    }

    /// <summary>
    /// Toggles the Volume Panel visibility.
    /// </summary>
    public void ToggleVolume()
    {
        if (volumePanel != null)
        {
            bool willOpen = !volumePanel.activeSelf;
            volumePanel.SetActive(willOpen);
            if (willOpen && settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }
    }
}
