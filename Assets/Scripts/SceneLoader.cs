using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Endowment.Network;

public class SceneLoader : MonoBehaviour
{
    public string sceneName;

    public void LoadScene(string targetScene)
    {
        bool isGuest = BackendApiClient.Instance == null || BackendApiClient.Instance.IsGuest;

        // Security Guard: Guest session is strictly restricted to BigAquarium, MainMenu, and Login
        if (isGuest && (string.Equals(targetScene, "Aquarium", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(targetScene, "Collection", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(targetScene, "Store", StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogWarning($"[SceneLoader] Guest mode restricted from '{targetScene}'. Redirecting to MainMenu.");
            SceneManager.LoadScene("MainMenu");
            return;
        }

        SceneManager.LoadScene(targetScene);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game triggered!");

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #elif UNITY_WEBGL
        Application.Quit();
        #else
        Application.Quit();
        #endif
    }
}
