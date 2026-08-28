using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Automatically plays the global UI select sound when the attached Button is clicked.
/// This script is designed to be injected to all UI Buttons by an Editor Tool.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerClickHandler
{
    private Button btn;

    private void Awake()
    {
        btn = GetComponent<Button>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Only play if button is interactable and left-clicked
        if (btn != null && btn.interactable && eventData.button == PointerEventData.InputButton.Left)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayUISelect();
            }
        }
    }
}
