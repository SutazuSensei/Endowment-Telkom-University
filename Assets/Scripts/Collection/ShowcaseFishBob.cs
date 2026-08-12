using UnityEngine;

/// <summary>
/// Simple vertical bobbing animation for the fish showcase in Collection Scene.
/// Does NOT use FishMovement — this is intentionally a lightweight idle-only effect.
/// </summary>
public class ShowcaseFishBob : MonoBehaviour
{
    [Tooltip("How far the fish bobs vertically (pixels).")]
    [SerializeField] private float bobAmplitude = 8f;

    [Tooltip("How fast the fish bobs.")]
    [SerializeField] private float bobSpeed = 1.5f;

    private RectTransform rect;
    private Vector2 startPosition;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void Start()
    {
        startPosition = rect.anchoredPosition;
    }

    private void Update()
    {
        Vector2 pos = startPosition;
        pos.y += Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        rect.anchoredPosition = pos;
    }
}
