using System;
using UnityEngine;
using UnityEngine.UI;
using Endowment.Network;

/// <summary>
/// Controls the Main Menu view and adjusts button accessibility based on whether
/// the active session is a Guest or a registered Endowment Donor.
/// - Guest: "My Aquarium" (BT_myAqua) and "Collection" (BtnCollection) are locked/disabled.
///          Only "Big Aquarium" (BT_bigAqua) is accessible.
/// - Donor: All menu buttons are fully unlocked.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Menu Buttons")]
    [SerializeField] private Button buttonMyAquarium;
    [SerializeField] private Button buttonCollection;
    [SerializeField] private Button buttonBigAquarium;
    [SerializeField] private Button buttonExit;

    [Header("Visual Feedback")]
    [Tooltip("CanvasGroup alpha applied to locked buttons")]
    [SerializeField] private float lockedAlpha = 0.4f;

    private void Awake()
    {
        AutoWireButtons();
    }

    private void Start()
    {
        ApplyAccessPermissions();
    }

    private void OnEnable()
    {
        ApplyAccessPermissions();
    }

    private void AutoWireButtons()
    {
        if (buttonMyAquarium == null)
        {
            GameObject go = GameObject.Find("Canvas/BtnMenu/BT_myAqua");
            if (go != null) buttonMyAquarium = go.GetComponent<Button>();
        }

        if (buttonCollection == null)
        {
            GameObject go = GameObject.Find("Canvas/BtnMenu/BtnCollection");
            if (go != null) buttonCollection = go.GetComponent<Button>();
        }

        if (buttonBigAquarium == null)
        {
            GameObject go = GameObject.Find("Canvas/BtnMenu/BT_bigAqua (1)");
            if (go != null) buttonBigAquarium = go.GetComponent<Button>();
        }

        if (buttonExit == null)
        {
            GameObject go = GameObject.Find("Canvas/BtnMenu/BtnExit");
            if (go != null) buttonExit = go.GetComponent<Button>();
        }
    }

    public void ApplyAccessPermissions()
    {
        bool isGuest = BackendApiClient.Instance == null || BackendApiClient.Instance.IsGuest;

        if (buttonMyAquarium != null)
        {
            buttonMyAquarium.interactable = !isGuest;
            SetButtonVisualState(buttonMyAquarium, !isGuest);
        }

        if (buttonCollection != null)
        {
            buttonCollection.interactable = !isGuest;
            SetButtonVisualState(buttonCollection, !isGuest);
        }

        if (buttonBigAquarium != null)
        {
            buttonBigAquarium.interactable = true;
        }

        Debug.Log($"[MainMenuController] Access permissions applied. IsGuest: {isGuest} (MyAqua: {!isGuest}, Collection: {!isGuest}, BigAqua: True)");
    }

    private void SetButtonVisualState(Button button, bool isUnlocked)
    {
        if (button == null) return;

        CanvasGroup cg = button.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = button.gameObject.AddComponent<CanvasGroup>();
        }

        cg.alpha = isUnlocked ? 1f : lockedAlpha;
        cg.blocksRaycasts = isUnlocked;
        cg.interactable = isUnlocked;
    }
}

