using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class Minigame : MonoBehaviour, IPointerDownHandler
{
    [Header("Minigame Settings")]
    public int brushRadius = 50;

    private Image uiImage;
    private RectTransform rectTransform;
    private Texture2D algaeTexture;
    private Color32[] currentPixels;

    private int totalPixels;
    private int cleanedPixelCount = 0;
    
    void Start()
    {
        uiImage = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();

        Texture2D sourceTex = uiImage.sprite.texture;

        // texture duplicate
        algaeTexture = new Texture2D(sourceTex.width, sourceTex.height, TextureFormat.RGBA32, false);
        currentPixels = sourceTex.GetPixels32();
        algaeTexture.SetPixels32(currentPixels);
        algaeTexture.Apply();

        for (int i = 0; i < currentPixels.Length; i++)
        {
            if (currentPixels[i].a == 0)
            {
                cleanedPixelCount++;
            }
        }

        // apply new texture to SpriteRenderer
        Rect rect = new Rect(0, 0, algaeTexture.width, algaeTexture.height);
        uiImage.sprite = Sprite.Create(algaeTexture, rect, new Vector2(0.5f, 0.5f));
    }

     public void OnPointerDown(PointerEventData eventData)
    {
        EraseAlgaeAtMouseClick(eventData);
    }

    void EraseAlgaeAtMouseClick(PointerEventData eventData)
    {
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            Rect rect = rectTransform.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            int pixelX = Mathf.FloorToInt(u * algaeTexture.width);
            int pixelY = Mathf.FloorToInt(v * algaeTexture.height);

            bool updated = false;


            // erase algae in the brush radius
            for (int x = -brushRadius; x <= brushRadius; x++)
            {
                for (int y = -brushRadius; y <= brushRadius; y++)
                {
                    if (x * x + y * y <= brushRadius * brushRadius) // check area
                    {
                        int targetX = pixelX + x;
                        int targetY = pixelY + y;

                        // check if the target pixel is within the texture bounds
                        if (targetX >= 0 && targetX < algaeTexture.width && targetY >= 0 && targetY < algaeTexture.height)
                        {
                            int index = targetY * algaeTexture.width + targetX;

                            // change the alpha (transparency) to 0 to erase the algae
                            if (currentPixels[index].a > 0)
                            {
                                currentPixels[index].a = 0;
                                cleanedPixelCount++;
                                updated = true;
                            }
                        }
                    }
                }
            }
            if (updated)
            {
                algaeTexture.SetPixels32(currentPixels);
                algaeTexture.Apply();
            }
        }
    }

    /// <summary>
    /// calculating the percentage of algae removed
    /// </summary>
        
    public float GetCleanPercentage()
    {
        if (totalPixels == 0) return 0f;
        return ((float)cleanedPixelCount / totalPixels) * 100f;
    }
}
