using UnityEngine;
using TMPro;

public class PointsHandler : MonoBehaviour
{
    public static PointsHandler Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI pointText;

    [Header("Points Settings")]
    public string prefix = "Score: ";

    /// <summary>
    /// Cached points value. Always kept in sync with PlayerDataRepository.
    /// </summary>
    public int current = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;   
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Load points from persistent repository
        current = PlayerDataRepository.GetPoints();
        UpdateUI();
    }

    // add points
    public void AddPoints(int amount)
    {
        // Persist to repository (saves to PlayerPrefs)
        PlayerDataRepository.AddPoints(amount);

        // Sync cached value
        current = PlayerDataRepository.GetPoints();
        UpdateUI();
    }

    public int GetCurrent()
    {
        return current;
    }

    private void UpdateUI()
    {
        if (pointText != null)
        {
            pointText.text = $"{prefix}{current}";
        }
    }
}
