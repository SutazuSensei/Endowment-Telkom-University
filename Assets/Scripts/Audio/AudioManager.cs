using UnityEngine;

/// <summary>
/// Global Audio Manager.
/// Persists across scenes to play BGM, Ambient loops, and one-shot SFX seamlessly.
/// Auto-instantiates itself on game start if placed in a Resources folder.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip bgmClip;
    [SerializeField] private AudioClip ambientClip;
    
    [Header("SFX Clips")]
    [SerializeField] private AudioClip clearAlgaeClip;
    [SerializeField] private AudioClip foodDropClip;
    [SerializeField] private AudioClip purchaseClip;
    [SerializeField] private AudioClip uiSelectClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        // Prevent duplicate instantiations if already manually placed in the scene
        if (Instance != null) return;

        // Auto-load the prefab from Resources folder
        GameObject prefab = Resources.Load<GameObject>("AudioManager");
        if (prefab != null)
        {
            Instantiate(prefab);
            Debug.Log("[AudioManager] Auto-initialized from Resources.");
        }
        else
        {
            Debug.LogWarning("[AudioManager] Prefab not found in Resources folder. Please run the Setup Audio Tool.");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Load saved volumes or default to 1.0
        float bgmVol = PlayerPrefs.GetFloat("BGM_Volume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SFX_Volume", 1f);

        SetBGMVolume(bgmVol);
        SetSFXVolume(sfxVol);

        // Start continuous loops
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.loop = true;
            if (!bgmSource.isPlaying) bgmSource.Play();
        }

        if (ambientSource != null && ambientClip != null)
        {
            ambientSource.clip = ambientClip;
            ambientSource.loop = true;
            if (!ambientSource.isPlaying) ambientSource.Play();
        }
    }

    public void SetBGMVolume(float volume)
    {
        if (bgmSource != null) bgmSource.volume = volume;
        if (ambientSource != null) ambientSource.volume = volume;
        PlayerPrefs.SetFloat("BGM_Volume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null) sfxSource.volume = volume;
        PlayerPrefs.SetFloat("SFX_Volume", volume);
        PlayerPrefs.Save();
    }

    public float GetBGMVolume()
    {
        return PlayerPrefs.GetFloat("BGM_Volume", 1f);
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat("SFX_Volume", 1f);
    }

    /// <summary>
    /// Plays a one-shot sound effect.
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // Explicit helper methods for specific events

    public void PlayClearAlgae()
    {
        PlaySFX(clearAlgaeClip);
    }

    public void PlayFoodDrop()
    {
        PlaySFX(foodDropClip);
    }

    public void PlayPurchaseSuccess()
    {
        PlaySFX(purchaseClip);
    }

    public void PlayUISelect()
    {
        PlaySFX(uiSelectClip);
    }
}
