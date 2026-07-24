using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AlgaeSpawner : MonoBehaviour
{
    [Header("Prefab & Parent Settings")]
    public GameObject algaePrefab;
    public RectTransform spawnArea;

    [Header("Spawn Rate Settings")]
    public float InitialSpawnInterval = 4f;
    public float minSpawnInterval = 1f;
    public float difficultyRamp = 0.1f;

    [Header("Max Constraint")]
    public int maxAlgaeOnScreen = 15;

    private float currentSpawnInterval;
    private List<GameObject> activeAlgaeList = new List<GameObject>();


    void Start()
    {
        currentSpawnInterval = InitialSpawnInterval;
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(currentSpawnInterval);

            // check algae count
            if (activeAlgaeList.Count < minSpawnInterval)
            {
                SpawnAlgae();
            }
        }
    }

    void SpawnAlgae()
    {
        if (algaePrefab == null || spawnArea == null) return;

        // prefab alage dibawah parent
        GameObject newAlgae = Instantiate(algaePrefab, spawnArea);
        newAlgae.transform.SetAsLastSibling();

        // hitung posisi acak dibatas RectTransform spawnArea
        Rect rect = spawnArea.rect;
        float randomX = Random.Range(rect.xMin + 50f, rect.xMax - 50f);
        float randomY = Random.Range(rect.yMin + 50f, rect.yMax - 50f);

        RectTransform algaeRect = newAlgae.GetComponent<RectTransform>();
        if (algaeRect != null )
        {
            algaeRect.anchoredPosition = new Vector2(randomX, randomY);
        }

        // referensi spawner ke script Algae
        Algae algaeScript = newAlgae.GetComponent<Algae>();
        if (algaeScript != null)
        {
            algaeScript.Setup(this);
        }

        activeAlgaeList.Add(newAlgae);
        Debug.Log("Algae Spawned");
    }

    public void OnAlgaeCleaned(GameObject algaeObj)
    {
        if (activeAlgaeList.Contains(algaeObj))
        {
            activeAlgaeList.Remove(algaeObj);
        }
    }
}
