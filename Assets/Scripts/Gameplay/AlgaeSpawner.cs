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
    public float minSpawnInterval = 10f;
    public float maxSpawnInterval = 15f;

    [Header("Max Constraint")]
    public int maxAlgaeOnScreen = 15;

    private float currentSpawnInterval;
    private List<GameObject> activeAlgaeList = new List<GameObject>();


    void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    IEnumerator SpawnRoutine()
    {
        while (true)
        {
            float randomInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(randomInterval);

            CleanUpNullElements();

            // check algae count
            if (activeAlgaeList.Count < maxAlgaeOnScreen)
            {
                SpawnAlgae();
            }
        }
    }

    void SpawnAlgae()
    {
        if (algaePrefab == null || spawnArea == null) return;

        // prefab algae dibawah parent spawnArea
        GameObject newAlgae = Instantiate(algaePrefab, spawnArea);
        newAlgae.transform.SetAsLastSibling();

        // hitung posisi acak di sumbu X, tapi sumbu Y dikunci di dasar Aquarium
        Rect rect = spawnArea.rect;
        float randomX = Random.Range(rect.xMin + 50f, rect.xMax - 50f);
        float fixedY = rect.yMin + 50f; // Dasar aquarium ditambah padding agar tidak terpotong

        RectTransform algaeRect = newAlgae.GetComponent<RectTransform>();
        if (algaeRect != null )
        {
            algaeRect.anchoredPosition = new Vector2(randomX, fixedY);
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

    private void CleanUpNullElements()
    {
        activeAlgaeList.RemoveAll(item => item == null);
    }
}
