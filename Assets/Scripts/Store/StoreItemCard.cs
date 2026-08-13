using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Component attached to each Store item card prefab.
/// StoreUIController populates these references when instantiating cards at runtime.
/// </summary>
public class StoreItemCard : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Image displaying the accessory icon/sprite.")]
    public Image iconImage;

    [Tooltip("Text displaying the accessory name.")]
    public TextMeshProUGUI nameText;

    [Tooltip("Text displaying the price.")]
    public TextMeshProUGUI priceText;

    [Tooltip("Button for purchasing the accessory.")]
    public Button buyButton;

    [Tooltip("Text on the buy button ('Beli' or 'Owned').")]
    public TextMeshProUGUI buyButtonText;

    /// <summary>The accessory ID this card represents. Set by StoreUIController.</summary>
    [HideInInspector] public string accessoryId;
}
