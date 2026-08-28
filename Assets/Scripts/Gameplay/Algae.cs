using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Algae : MonoBehaviour, IPointerDownHandler
{
    [Header("Reward Settings")]
    public int minReward = 100;
    public int maxReward = 200;

    private int rewardAmount;

    private AlgaeSpawner spawner;
    private bool isCleaned = false;

    public void Setup(AlgaeSpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    private void Start()
    {
        rewardAmount = Random.Range(minReward, maxReward + 1);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            CleanUp();
        }
    }

    private void CleanUp()
    {
        if (isCleaned) return;
        isCleaned = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClearAlgae();
        }

        GiveReward();

        if (spawner != null)
        {
            spawner.OnAlgaeCleaned(this.gameObject);
        }

        // destroy the ALGAEEEE
        Destroy(gameObject);
    }

    private void GiveReward()
    {
        if (PointsHandler.Instance != null)
        {
            PointsHandler.Instance.AddPoints(rewardAmount);
        }

        Debug.Log($"something something reward +{rewardAmount} koin!");
    }
}
