using UnityEngine;
using TMPro;

public class PointsHandler : MonoBehaviour
{
    public static PointsHandler Instance { get; private set; }

    [Header("UI")]
    public TextMeshProUGUI pointText;

    [Header("Points Settings")]
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
        UpdateUI();
    }

    // add points
    public void AddPoints(int amount)
    {
        current += amount;
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
            pointText.text = $"{current}";
        }
    }
}
