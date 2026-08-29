using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor Tool: Membaca data AccessoryConfig (placements) dari CollectionUIController
/// yang sedang aktif di scene, lalu menyalinnya ke FishColorApplier dan AquariumFish prefabs.
/// 
/// Cara Pakai:
/// 1. Buka scene Collection (pastikan CollectionUIController ada).
/// 2. Klik Tools > Sync Accessory Placements from Collection.
/// 3. Tool akan menyalin placement data ke FishColorApplier (jika ada di scene) 
///    dan ke semua prefab yang memiliki AquariumFish.
/// </summary>
public class SyncAccessoryPlacementTool : EditorWindow
{
    [MenuItem("Tools/Sync Accessory Placements from Collection")]
    public static void SyncFromCollection()
    {
        // ====================================================
        // 1. Cari CollectionUIController di scene aktif
        // ====================================================
        #if UNITY_2023_1_OR_NEWER
        CollectionUIController collUI = FindFirstObjectByType<CollectionUIController>(FindObjectsInactive.Include);
        #else
        CollectionUIController collUI = FindObjectOfType<CollectionUIController>(true);
        #endif

        if (collUI == null)
        {
            Debug.LogError("[SyncAccessoryPlacement] CollectionUIController TIDAK DITEMUKAN di scene aktif! Pastikan Anda membuka scene Collection terlebih dahulu.");
            return;
        }

        // ====================================================
        // 2. Baca data dari CollectionUIController
        // ====================================================
        SerializedObject collSO = new SerializedObject(collUI);
        SerializedProperty collAccessories = collSO.FindProperty("accessoryItems");

        if (collAccessories == null || collAccessories.arraySize == 0)
        {
            Debug.LogWarning("[SyncAccessoryPlacement] Tidak ada accessoryItems di CollectionUIController. Jalankan Setup All Fish Config dulu.");
            return;
        }

        // Simpan data ke dalam list sementara
        List<AccessoryData> extractedData = new List<AccessoryData>();

        for (int i = 0; i < collAccessories.arraySize; i++)
        {
            SerializedProperty item = collAccessories.GetArrayElementAtIndex(i);
            string accId = item.FindPropertyRelative("accessoryId").stringValue;
            Object spriteObj = item.FindPropertyRelative("accessorySprite").objectReferenceValue;
            SerializedProperty srcPlacements = item.FindPropertyRelative("placements");

            List<PlacementData> placementList = new List<PlacementData>();
            if (srcPlacements != null)
            {
                for (int j = 0; j < srcPlacements.arraySize; j++)
                {
                    SerializedProperty p = srcPlacements.GetArrayElementAtIndex(j);
                    placementList.Add(new PlacementData
                    {
                        tier = p.FindPropertyRelative("tier").stringValue,
                        offset = p.FindPropertyRelative("offset").vector2Value,
                        scale = p.FindPropertyRelative("scale").vector2Value,
                        rotation = p.FindPropertyRelative("rotation").floatValue
                    });
                }
            }

            extractedData.Add(new AccessoryData
            {
                accessoryId = accId,
                sprite = spriteObj as Sprite,
                placements = placementList
            });
        }

        Debug.Log($"[SyncAccessoryPlacement] Berhasil membaca {extractedData.Count} aksesoris dari Collection.");

        // ====================================================
        // 2B. Baca customSize dari CollectionUIController (fishSprites)
        // ====================================================
        SerializedProperty collFishSprites = collSO.FindProperty("fishSprites");
        Dictionary<string, Vector2> extractedSizes = new Dictionary<string, Vector2>();

        if (collFishSprites != null)
        {
            for (int i = 0; i < collFishSprites.arraySize; i++)
            {
                SerializedProperty el = collFishSprites.GetArrayElementAtIndex(i);
                string tier = el.FindPropertyRelative("tier").stringValue;
                Vector2 customSize = el.FindPropertyRelative("customSize").vector2Value;

                // Hanya simpan satu ukuran per tier (karena ukuran ikan diasumsikan sama untuk semua warna di tier yg sama)
                if (!extractedSizes.ContainsKey(tier) && customSize != Vector2.zero)
                {
                    extractedSizes[tier] = customSize;
                }
            }
            Debug.Log($"[SyncAccessoryPlacement] Berhasil membaca ukuran (customSize) dari {extractedSizes.Count} tier.");
        }

        int totalSynced = 0;

        // ====================================================
        // 3. Apply ke semua AquariumFish di prefabs (Big Aquarium)
        // ====================================================
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            AquariumFish[] fishes = prefab.GetComponentsInChildren<AquariumFish>(true);
            foreach (AquariumFish fish in fishes)
            {
                SerializedObject so = new SerializedObject(fish);
                ApplyToTarget(so, "accessoryConfigs", extractedData, false);
                SyncSizesToTarget(so, "rarityConfigs", extractedSizes);
                EditorUtility.SetDirty(fish);
                PrefabUtility.SavePrefabAsset(prefab);
                totalSynced++;
            }
        }

        // ====================================================
        // 4. Apply ke FishColorApplier di scene Aquarium
        // ====================================================
        string originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        bool userAllowedSave = UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        if (userAllowedSave)
        {
            // Cari path scene Aquarium
            string[] sceneGuids = AssetDatabase.FindAssets("Aquarium t:Scene");
            string aquariumScenePath = "";
            foreach (string sg in sceneGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(sg);
                if (p.EndsWith("Aquarium.unity"))
                {
                    aquariumScenePath = p;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(aquariumScenePath))
            {
                UnityEngine.SceneManagement.Scene aquariumScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(aquariumScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                
                #if UNITY_2023_1_OR_NEWER
                FishColorApplier applier = FindFirstObjectByType<FishColorApplier>(FindObjectsInactive.Include);
                #else
                FishColorApplier applier = FindObjectOfType<FishColorApplier>(true);
                #endif

                if (applier != null)
                {
                    SerializedObject applierSO = new SerializedObject(applier);
                    ApplyToTarget(applierSO, "accessories", extractedData, false);
                    SyncSizesToTarget(applierSO, "fishSprites", extractedSizes);
                    EditorUtility.SetDirty(applier);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(aquariumScene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(aquariumScene);
                    Debug.Log("[SyncAccessoryPlacement] Berhasil sync aksesoris dan ukuran ikan ke FishColorApplier (Aquarium scene).");
                    totalSynced++;
                }

                // Restore original scene (Collection)
                if (!string.IsNullOrEmpty(originalScenePath) && originalScenePath != aquariumScenePath)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }
            }
        }

        Debug.Log($"[SyncAccessoryPlacement] Selesai! Total {totalSynced} komponen berhasil di-sync.");
    }

    private static void ApplyToTarget(SerializedObject so, string propertyName, List<AccessoryData> data, bool hasDisplayName)
    {
        SerializedProperty targetProp = so.FindProperty(propertyName);
        if (targetProp == null)
        {
            Debug.LogWarning($"[SyncAccessoryPlacement] Property '{propertyName}' tidak ditemukan.");
            return;
        }

        targetProp.ClearArray();
        targetProp.arraySize = data.Count;

        for (int i = 0; i < data.Count; i++)
        {
            SerializedProperty el = targetProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("accessoryId").stringValue = data[i].accessoryId;
            el.FindPropertyRelative("accessorySprite").objectReferenceValue = data[i].sprite;

            // displayName hanya ada di CollectionUIController.AccessoryItem
            SerializedProperty displayNameProp = el.FindPropertyRelative("displayName");
            if (displayNameProp != null)
            {
                displayNameProp.stringValue = data[i].accessoryId;
            }

            // Copy placements
            SerializedProperty targetPlacements = el.FindPropertyRelative("placements");
            if (targetPlacements != null && data[i].placements != null)
            {
                targetPlacements.ClearArray();
                targetPlacements.arraySize = data[i].placements.Count;

                for (int j = 0; j < data[i].placements.Count; j++)
                {
                    SerializedProperty p = targetPlacements.GetArrayElementAtIndex(j);
                    p.FindPropertyRelative("tier").stringValue = data[i].placements[j].tier;
                    p.FindPropertyRelative("offset").vector2Value = data[i].placements[j].offset;
                    p.FindPropertyRelative("scale").vector2Value = data[i].placements[j].scale;
                    p.FindPropertyRelative("rotation").floatValue = data[i].placements[j].rotation;
                }
            }
        }
        so.ApplyModifiedProperties();
    }

    private static void SyncSizesToTarget(SerializedObject so, string propertyName, Dictionary<string, Vector2> tierSizes)
    {
        SerializedProperty targetProp = so.FindProperty(propertyName);
        if (targetProp == null) return;

        for (int i = 0; i < targetProp.arraySize; i++)
        {
            SerializedProperty el = targetProp.GetArrayElementAtIndex(i);
            
            // Check property name: 'tier' in TierColorSprite, 'rarityName' in FishRarityConfig
            SerializedProperty tierProp = el.FindPropertyRelative("tier");
            if (tierProp == null) tierProp = el.FindPropertyRelative("rarityName");
            
            if (tierProp != null && tierSizes.TryGetValue(tierProp.stringValue, out Vector2 customSize))
            {
                SerializedProperty sizeProp = el.FindPropertyRelative("customSize");
                if (sizeProp != null)
                {
                    sizeProp.vector2Value = customSize;
                }
            }
        }
        so.ApplyModifiedProperties();
    }

    // Helper data classes
    private class AccessoryData
    {
        public string accessoryId;
        public Sprite sprite;
        public List<PlacementData> placements;
    }

    private class PlacementData
    {
        public string tier;
        public Vector2 offset;
        public Vector2 scale;
        public float rotation;
    }
}
