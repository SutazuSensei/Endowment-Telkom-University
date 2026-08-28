using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.IO;

public class SetupAudioTool : EditorWindow
{
    [MenuItem("Tools/Setup Global Audio System")]
    public static void SetupAudio()
    {
        CreateAudioManagerPrefab();
        InjectUIButtonSoundsToScenes();
        InjectUIButtonSoundsToPrefabs();
        
        Debug.Log("[SetupAudioTool] Selesai! Audio Manager telah dibuat dan UIButtonSound telah dipasang ke semua tombol.");
    }

    private static void CreateAudioManagerPrefab()
    {
        string resourcesPath = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(resourcesPath))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Load Clips
        AudioClip bgm = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/BGM/magiksolo-aquarium-fish-132518.mp3");
        AudioClip ambient = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/AquariumAmbient.mp3");
        AudioClip algae = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/ClearALgae.mp3");
        AudioClip food = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/FoodDrop.mp3");
        AudioClip purchase = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Purchase.mp3");
        AudioClip uiSelect = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/UISelectButton.wav");

        if (bgm == null || ambient == null || algae == null || food == null || purchase == null || uiSelect == null)
        {
            Debug.LogError("[SetupAudioTool] Gagal memuat satu atau lebih file Audio. Pastikan nama dan path sesuai dengan folder Assets/Audio/...");
            return;
        }

        // Create temporary GameObject
        GameObject audioObj = new GameObject("AudioManager");
        AudioManager manager = audioObj.AddComponent<AudioManager>();

        // Add 3 AudioSources
        AudioSource bgmSource = audioObj.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        
        AudioSource ambientSource = audioObj.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;

        AudioSource sfxSource = audioObj.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        // Use SerializedObject to set private fields on AudioManager
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("bgmSource").objectReferenceValue = bgmSource;
        so.FindProperty("ambientSource").objectReferenceValue = ambientSource;
        so.FindProperty("sfxSource").objectReferenceValue = sfxSource;

        so.FindProperty("bgmClip").objectReferenceValue = bgm;
        so.FindProperty("ambientClip").objectReferenceValue = ambient;
        so.FindProperty("clearAlgaeClip").objectReferenceValue = algae;
        so.FindProperty("foodDropClip").objectReferenceValue = food;
        so.FindProperty("purchaseClip").objectReferenceValue = purchase;
        so.FindProperty("uiSelectClip").objectReferenceValue = uiSelect;
        
        so.ApplyModifiedProperties();

        // Save as Prefab
        string prefabPath = "Assets/Resources/AudioManager.prefab";
        PrefabUtility.SaveAsPrefabAsset(audioObj, prefabPath);

        // Destroy temporary object
        DestroyImmediate(audioObj);
        
        Debug.Log("[SetupAudioTool] AudioManager Prefab berhasil dibuat di Assets/Resources/AudioManager.prefab");
    }

    private static void InjectUIButtonSoundsToScenes()
    {
        // Temukan semua Button di scene yang aktif
        #if UNITY_2023_1_OR_NEWER
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        #else
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        #endif

        int count = 0;
        foreach (Button btn in buttons)
        {
            // Skip prefabs that are not in the scene (for older Unity versions)
            if (btn.gameObject.scene.name == null) continue;

            if (btn.GetComponent<UIButtonSound>() == null)
            {
                btn.gameObject.AddComponent<UIButtonSound>();
                EditorUtility.SetDirty(btn.gameObject);
                count++;
            }
        }

        Debug.Log($"[SetupAudioTool] Berhasil menyuntikkan UIButtonSound ke {count} tombol di Scene yang aktif.");
    }

    private static void InjectUIButtonSoundsToPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int count = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool modified = false;
            Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                if (btn.GetComponent<UIButtonSound>() == null)
                {
                    btn.gameObject.AddComponent<UIButtonSound>();
                    modified = true;
                    count++;
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(prefab);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }

        Debug.Log($"[SetupAudioTool] Berhasil menyuntikkan UIButtonSound ke {count} tombol di dalam Prefabs.");
    }
}
