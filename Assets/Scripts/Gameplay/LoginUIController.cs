using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Endowment.Network;

/// <summary>
/// Controls the Login scene UI flow.
/// Supports logging in via Full Name OR NIK (externalId) with the backend game-api (:3000),
/// as well as instant Guest login (offline/sandbox play).
/// </summary>
public class LoginUIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_InputField inputFieldIdentifier;
    [SerializeField] private Button buttonLogin;
    [SerializeField] private Button buttonGuest;
    [SerializeField] private TextMeshProUGUI textWarning;

    [Header("Scene Navigation")]
    [SerializeField] private string targetSceneName = "MainMenu";

    [Header("Strings")]
    [SerializeField] private string emptyInputWarning = "Please enter your full name or NIK.";
    [SerializeField] private string notFoundWarning = "Name or NIK not found in Endowment database.";
    [SerializeField] private string networkErrorWarning = "Server unreachable. You can continue as Guest.";
    [SerializeField] private string loggingInText = "Logging in...";

    private TextMeshProUGUI loginButtonText;
    private string originalLoginButtonText = "Login";

    private void Awake()
    {
        AutoWireElements();
    }

    private void Start()
    {
        if (textWarning != null)
        {
            textWarning.gameObject.SetActive(false);
        }

        if (buttonLogin != null)
        {
            loginButtonText = buttonLogin.GetComponentInChildren<TextMeshProUGUI>();
            if (loginButtonText != null)
            {
                originalLoginButtonText = loginButtonText.text;
            }
            buttonLogin.onClick.AddListener(OnLoginClicked);
        }

        if (buttonGuest != null)
        {
            buttonGuest.onClick.AddListener(OnGuestClicked);
        }

        if (inputFieldIdentifier != null)
        {
            // Pre-fill previously remembered login name if available
            string rememberedName = PlayerPrefs.GetString("endowment_last_login_input", "");
            if (!string.IsNullOrEmpty(rememberedName))
            {
                inputFieldIdentifier.text = rememberedName;
            }

            inputFieldIdentifier.onSubmit.AddListener(val => OnLoginClicked());
            inputFieldIdentifier.onValueChanged.AddListener(val =>
            {
                if (textWarning != null && textWarning.gameObject.activeSelf)
                {
                    textWarning.gameObject.SetActive(false);
                }
            });
        }
    }

    private void AutoWireElements()
    {
        if (inputFieldIdentifier == null)
            inputFieldIdentifier = GetComponentInChildren<TMP_InputField>(true);

        if (buttonLogin == null)
        {
            Transform btnObj = transform.Find("Panel_Login/Button_Login");
            if (btnObj == null) btnObj = transform.Find("Button_Login");
            if (btnObj != null) buttonLogin = btnObj.GetComponent<Button>();
        }

        if (buttonGuest == null)
        {
            Transform btnObj = transform.Find("Panel_Login/Button_LoginGuest");
            if (btnObj == null) btnObj = transform.Find("Button_LoginGuest");
            if (btnObj != null) buttonGuest = btnObj.GetComponent<Button>();
        }

        if (textWarning == null)
        {
            GameObject warnObj = GameObject.Find("Text_Warning");
            if (warnObj != null) textWarning = warnObj.GetComponent<TextMeshProUGUI>();
        }
    }

    public void OnLoginClicked()
    {
        string identifier = inputFieldIdentifier != null ? inputFieldIdentifier.text.Trim() : "";

        if (string.IsNullOrEmpty(identifier))
        {
            ShowWarning(emptyInputWarning);
            return;
        }

        SetLoadingState(true);

        BackendApiClient.Instance.Login(identifier, (data, error) =>
        {
            SetLoadingState(false);

            if (data != null && data.donor != null)
            {
                // Save last input for convenience
                PlayerPrefs.SetString("endowment_last_login_input", identifier);
                PlayerPrefs.Save();

                // Sync repository with returned aquarium fish data
                if (data.aquarium != null)
                {
                    PlayerDataRepository.ApplyLoginAquariumData(data.aquarium);
                }

                Debug.Log($"[LoginUI] Logged in successfully as {data.donor.displayName} ({data.donor.externalId})");
                LoadNextScene();
            }
            else
            {
                if (error != null && error.code == "DONOR_NOT_FOUND")
                {
                    ShowWarning(notFoundWarning);
                }
                else if (error != null && error.code == "NETWORK_ERROR")
                {
                    ShowWarning(networkErrorWarning);
                }
                else
                {
                    string msg = error != null && !string.IsNullOrEmpty(error.message) ? error.message : "Login failed.";
                    ShowWarning(msg);
                }
            }
        });
    }

    public void OnGuestClicked()
    {
        Debug.Log("[LoginUI] Entering as Guest");
        BackendApiClient.Instance.ClearAuthSession();
        LoadNextScene();
    }

    private void SetLoadingState(bool isLoading)
    {
        if (buttonLogin != null) buttonLogin.interactable = !isLoading;
        if (buttonGuest != null) buttonGuest.interactable = !isLoading;
        if (inputFieldIdentifier != null) inputFieldIdentifier.interactable = !isLoading;

        if (loginButtonText != null)
        {
            loginButtonText.text = isLoading ? loggingInText : originalLoginButtonText;
        }
    }

    private void ShowWarning(string message)
    {
        if (textWarning != null)
        {
            textWarning.text = message;
            textWarning.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[LoginUI] Warning: {message}");
        }
    }

    private void LoadNextScene()
    {
        SceneManager.LoadScene(targetSceneName);
    }
}

