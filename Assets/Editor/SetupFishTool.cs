using UnityEngine;
using UnityEditor;

public class SetupFishTool : EditorWindow
{
    [MenuItem("Tools/Setup All Fish Config")]
    public static void SetupAllFish()
    {
        // ==========================================
        // 1. Load All Sprites
        // ==========================================
        string pathCommon = "Assets/Sprites/Fish/Common/";
        Sprite c1 = AssetDatabase.LoadAssetAtPath<Sprite>(pathCommon + "Clownfish - Common (Sketch).png");
        Sprite c2 = AssetDatabase.LoadAssetAtPath<Sprite>(pathCommon + "Clownfish2.png");
        Sprite c3 = AssetDatabase.LoadAssetAtPath<Sprite>(pathCommon + "Clownfish3.png");
        Sprite c4 = AssetDatabase.LoadAssetAtPath<Sprite>(pathCommon + "Clownfish4.png");

        string pathRare = "Assets/Sprites/Fish/Rare/";
        Sprite r1 = AssetDatabase.LoadAssetAtPath<Sprite>(pathRare + "Tuna1.png");
        Sprite r2 = AssetDatabase.LoadAssetAtPath<Sprite>(pathRare + "Tuna2.png");
        Sprite r3 = AssetDatabase.LoadAssetAtPath<Sprite>(pathRare + "Tuna3.png");
        Sprite r4 = AssetDatabase.LoadAssetAtPath<Sprite>(pathRare + "Tuna4.png");

        string pathEpic = "Assets/Sprites/Fish/Epic/";
        Sprite e1 = AssetDatabase.LoadAssetAtPath<Sprite>(pathEpic + "Stringray - Epic.png");
        Sprite e2 = AssetDatabase.LoadAssetAtPath<Sprite>(pathEpic + "Stringray2.png");
        Sprite e3 = AssetDatabase.LoadAssetAtPath<Sprite>(pathEpic + "Stringray3.png");
        Sprite e4 = AssetDatabase.LoadAssetAtPath<Sprite>(pathEpic + "Stringray4.png");

        string pathLegendary = "Assets/Sprites/Fish/Legendary/";
        Sprite l1 = AssetDatabase.LoadAssetAtPath<Sprite>(pathLegendary + "whale1.png");
        Sprite l2 = AssetDatabase.LoadAssetAtPath<Sprite>(pathLegendary + "whale2.png");
        Sprite l3 = AssetDatabase.LoadAssetAtPath<Sprite>(pathLegendary + "whale3.png");
        Sprite l4 = AssetDatabase.LoadAssetAtPath<Sprite>(pathLegendary + "Whale4.png");

        if (c1 == null || r1 == null || e1 == null || l1 == null)
        {
            Debug.LogError("[SetupFishTool] Gagal memuat beberapa sprite ikan. Pastikan path file benar.");
            return;
        }

        // ==========================================
        // 2. Define Sizes
        // ==========================================
        Vector2 sizeCommon = new Vector2(200f, 100f);
        Vector2 sizeRare = new Vector2(250f, 125f);
        Vector2 sizeEpic = new Vector2(300f, 150f);
        Vector2 sizeLegendary = new Vector2(400f, 200f);

        string[] colors = { "red", "blue", "green", "yellow" };
        
        Sprite[] commonSprites = { c1, c2, c3, c4 };
        Sprite[] rareSprites = { r1, r2, r3, r4 };
        Sprite[] epicSprites = { e1, e2, e3, e4 };
        Sprite[] legendarySprites = { l1, l2, l3, l4 };

        bool appliedAny = false;

        // ==========================================
        // 3. Setup FishColorApplier (Aquarium)
        // ==========================================
        #if UNITY_2023_1_OR_NEWER
        FishColorApplier applier = FindFirstObjectByType<FishColorApplier>(FindObjectsInactive.Include);
        #else
        FishColorApplier applier = FindObjectOfType<FishColorApplier>(true);
        #endif

        if (applier != null)
        {
            SerializedObject so = new SerializedObject(applier);
            SerializedProperty prop = so.FindProperty("fishSprites");
            
            prop.ClearArray();
            prop.arraySize = 16;
            
            int idx = 0;
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Common", colors[i], commonSprites[i], sizeCommon); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Rare", colors[i], rareSprites[i], sizeRare); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Epic", colors[i], epicSprites[i], sizeEpic); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Legendary", colors[i], legendarySprites[i], sizeLegendary); }
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(applier);
            Debug.Log("[SetupFishTool] Berhasil mengatur 16 varian ikan di FishColorApplier (Aquarium scene)!");
            appliedAny = true;
        }

        // ==========================================
        // 4. Setup CollectionUIController (Collection)
        // ==========================================
        #if UNITY_2023_1_OR_NEWER
        CollectionUIController collUI = FindFirstObjectByType<CollectionUIController>(FindObjectsInactive.Include);
        #else
        CollectionUIController collUI = FindObjectOfType<CollectionUIController>(true);
        #endif

        if (collUI != null)
        {
            SerializedObject so = new SerializedObject(collUI);
            SerializedProperty prop = so.FindProperty("fishSprites");
            
            prop.ClearArray();
            prop.arraySize = 16;
            
            int idx = 0;
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Common", colors[i], commonSprites[i], sizeCommon); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Rare", colors[i], rareSprites[i], sizeRare); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Epic", colors[i], epicSprites[i], sizeEpic); }
            for (int i = 0; i < 4; i++) { AssignFishSprite(prop.GetArrayElementAtIndex(idx++), "Legendary", colors[i], legendarySprites[i], sizeLegendary); }
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(collUI);
            Debug.Log("[SetupFishTool] Berhasil mengatur 16 varian ikan di CollectionUIController (Collection scene)!");
            appliedAny = true;
        }

        // ==========================================
        // 5. Setup AquariumFish (Big Aquarium / Prefabs)
        // ==========================================
        int updatedBigFishes = 0;

        // Cari di semua prefab
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            AquariumFish[] aqFishesInPrefab = prefab.GetComponentsInChildren<AquariumFish>(true);
            foreach (AquariumFish f in aqFishesInPrefab)
            {
                SerializedObject so = new SerializedObject(f);
                SerializedProperty prop = so.FindProperty("rarityConfigs");
                
                prop.ClearArray();
                prop.arraySize = 16;
                
                int idx = 0;
                for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Common", colors[i], commonSprites[i], sizeCommon, Color.white); }
                for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Rare", colors[i], rareSprites[i], sizeRare, Color.cyan); }
                for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Epic", colors[i], epicSprites[i], sizeEpic, Color.magenta); }
                for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Legendary", colors[i], legendarySprites[i], sizeLegendary, Color.yellow); }
                
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(f);
                PrefabUtility.SavePrefabAsset(prefab);
                updatedBigFishes++;
            }
        }

        // Cari juga di scene yang aktif
        #if UNITY_2023_1_OR_NEWER
        AquariumFish[] aqFishesInScene = FindObjectsByType<AquariumFish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
        AquariumFish[] aqFishesInScene = FindObjectsOfType<AquariumFish>(true);
        #endif

        foreach (AquariumFish f in aqFishesInScene)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(f.gameObject)) continue; // Abaikan jika ini murni asset prefab

            SerializedObject so = new SerializedObject(f);
            SerializedProperty prop = so.FindProperty("rarityConfigs");
            
            prop.ClearArray();
            prop.arraySize = 16;
            
            int idx = 0;
            for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Common", colors[i], commonSprites[i], sizeCommon, Color.white); }
            for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Rare", colors[i], rareSprites[i], sizeRare, Color.cyan); }
            for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Epic", colors[i], epicSprites[i], sizeEpic, Color.magenta); }
            for (int i = 0; i < 4; i++) { AssignRarityConfig(prop.GetArrayElementAtIndex(idx++), "Legendary", colors[i], legendarySprites[i], sizeLegendary, Color.yellow); }
            
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(f);
            updatedBigFishes++;
        }

        if (updatedBigFishes > 0)
        {
            Debug.Log($"[SetupFishTool] Berhasil mengatur rarityConfigs pada {updatedBigFishes} AquariumFish (Big Aquarium).");
            appliedAny = true;
        }

        if (!appliedAny)
        {
            Debug.LogWarning("[SetupFishTool] Tidak menemukan Script apa-apa untuk di-setup. Buka scene Aquarium, Collection, atau Big Aquarium lalu jalankan lagi.");
        }
    }

    private static void AssignFishSprite(SerializedProperty el, string tier, string colorVariant, Sprite sprite, Vector2 size)
    {
        el.FindPropertyRelative("tier").stringValue = tier;
        el.FindPropertyRelative("colorVariant").stringValue = colorVariant;
        el.FindPropertyRelative("fishSprite").objectReferenceValue = sprite;
        el.FindPropertyRelative("customSize").vector2Value = size;
    }

    private static void AssignRarityConfig(SerializedProperty el, string rarityName, string colorVariant, Sprite sprite, Vector2 size, Color textColor)
    {
        el.FindPropertyRelative("rarityName").stringValue = rarityName;
        el.FindPropertyRelative("colorVariant").stringValue = colorVariant;
        el.FindPropertyRelative("fishSprite").objectReferenceValue = sprite;
        el.FindPropertyRelative("customSize").vector2Value = size;
        el.FindPropertyRelative("textColor").colorValue = textColor;
    }
}
