using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Algae : MonoBehaviour, IPointerDownHandler
{
    [Header("Reward Settings")]
    public int rewardAmount = 10;

    private AlgaeSpawner spawner;

    public void Setup(AlgaeSpawner spawnerRef)
    {
        spawner = spawnerRef;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CleanUp();
    }

    private void CleanUp()
    {
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
