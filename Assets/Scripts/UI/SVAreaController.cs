using UnityEngine;
using UnityEngine.EventSystems;

namespace Endowment.UI
{
    /// <summary>
    /// Captures click, drag, and release events on the Saturation-Value 2D RawImage area.
    /// Updates color live during dragging and commits the chosen color on release.
    /// </summary>
    public class SVAreaController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Reference to the parent ColorPicker controller.")]
        public ColorPicker colorPicker;

        [Tooltip("Optional reticle/handle indicating current Saturation & Value point.")]
        public RectTransform handle;

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (colorPicker == null)
            {
                colorPicker = GetComponentInParent<ColorPicker>();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            UpdatePosition(eventData, isDragging: true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UpdatePosition(eventData, isDragging: true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            UpdatePosition(eventData, isDragging: false);
            if (colorPicker != null)
            {
                colorPicker.OnPointerUp();
            }
        }

        private void UpdatePosition(PointerEventData eventData, bool isDragging)
        {
            if (rectTransform == null || colorPicker == null) return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
            {
                Rect r = rectTransform.rect;
                float s = Mathf.InverseLerp(r.xMin, r.xMax, localPoint.x);
                float v = Mathf.InverseLerp(r.yMin, r.yMax, localPoint.y);

                colorPicker.SetSV(new Vector2(s, v), isDragging);
                UpdateHandleVisual(s, v);
            }
        }

        public void UpdateHandleVisual(float s, float v)
        {
            if (handle == null || rectTransform == null) return;

            Rect r = rectTransform.rect;
            float x = Mathf.Lerp(r.xMin, r.xMax, Mathf.Clamp01(s));
            float y = Mathf.Lerp(r.yMin, r.yMax, Mathf.Clamp01(v));
            handle.anchoredPosition = new Vector2(x, y);
        }
    }
}

