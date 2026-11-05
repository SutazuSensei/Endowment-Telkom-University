using UnityEngine;
using System.Collections;

public class CameraZoomTransition : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] private Camera targetCamera;

    [Header("Zoom Out Target")]
    [SerializeField] private float targetSize = 129f;
    [SerializeField] private float targetY = 120f;

    [Header("Transition")]
    [SerializeField] private float duration = 1.5f;
    [SerializeField] private AnimationCurve curve =
        AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Posisi dan ukuran awal
    private float startSize;
    private float startY;

    private Coroutine transitionCoroutine;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = GetComponent<Camera>();

        // Simpan posisi awal
        startSize = targetCamera.orthographicSize;
        startY = targetCamera.transform.position.y;
    }

    // BUTTON 1
    // Zoom keluar + kamera naik
    public void TransitionOut()
    {
        StartTransition(
            targetSize,
            targetY
        );
    }

    // BUTTON 2
    // Kembali ke kondisi awal
    public void TransitionBack()
    {
        StartTransition(
            startSize,
            startY
        );
    }

    private void StartTransition(float destinationSize, float destinationY)
    {
        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            Transition(destinationSize, destinationY)
        );
    }

    private IEnumerator Transition(float destinationSize, float destinationY)
    {
        float elapsed = 0f;

        // Ambil kondisi kamera SAAT transisi dimulai
        float currentSize = targetCamera.orthographicSize;
        float currentY = targetCamera.transform.position.y;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Smooth transition
            float smoothT = curve.Evaluate(t);

            // Orthographic Size
            targetCamera.orthographicSize = Mathf.Lerp(
                currentSize,
                destinationSize,
                smoothT
            );

            // Posisi Y
            Vector3 position = targetCamera.transform.position;

            position.y = Mathf.Lerp(
                currentY,
                destinationY,
                smoothT
            );

            targetCamera.transform.position = position;

            yield return null;
        }

        // Pastikan tepat di posisi tujuan
        targetCamera.orthographicSize = destinationSize;

        Vector3 finalPosition = targetCamera.transform.position;
        finalPosition.y = destinationY;

        targetCamera.transform.position = finalPosition;

        transitionCoroutine = null;
    }
}