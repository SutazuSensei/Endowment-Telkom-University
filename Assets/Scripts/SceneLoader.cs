using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public string sceneName;

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Quit Game triggered!");
        
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #elif UNITY_WEBGL
        // On WebGL, Application.Quit() halts the main loop. 
        // It's the standard way to 'exit' a Unity WebGL game, which freezes the canvas.
        Application.Quit();
        #else
        Application.Quit();
        #endif
    }
}
