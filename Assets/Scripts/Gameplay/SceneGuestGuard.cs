using UnityEngine;
using UnityEngine.SceneManagement;
using Endowment.Network;

namespace Endowment.Gameplay
{
    /// <summary>
    /// Attached to restricted scenes (Aquarium, Collection, Store).
    /// If entered by a Guest session, immediately redirects back to MainMenu.
    /// </summary>
    public class SceneGuestGuard : MonoBehaviour
    {
        [SerializeField] private string fallbackSceneName = "MainMenu";

        private void Awake()
        {
            CheckAccess();
        }

        private void OnEnable()
        {
            CheckAccess();
        }

        private void CheckAccess()
        {
            bool isGuest = BackendApiClient.Instance == null || BackendApiClient.Instance.IsGuest;
            if (isGuest)
            {
                Debug.LogWarning($"[SceneGuestGuard] Guest session detected in restricted scene '{gameObject.scene.name}'. Redirecting to '{fallbackSceneName}'.");
                SceneManager.LoadScene(fallbackSceneName);
            }
        }
    }
}

