using UnityEngine;
using UnityEngine.EventSystems;

public class SVAreaController : MonoBehaviour,
    IPointerDownHandler,
    IDragHandler
{
    public ColorPicker colorPicker;

    private RectTransform rectTransform;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        UpdateColor(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateColor(eventData);
    }

    void UpdateColor(PointerEventData eventData)
    {
        Vector2 localPosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out localPosition
        );

        float x = Mathf.InverseLerp(
            rectTransform.rect.xMin,
            rectTransform.rect.xMax,
            localPosition.x
        );

        float y = Mathf.InverseLerp(
            rectTransform.rect.yMin,
            rectTransform.rect.yMax,
            localPosition.y
        );

        colorPicker.SetSV(
            new Vector2(x, y)
        );
    }
}