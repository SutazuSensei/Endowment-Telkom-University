using UnityEngine;
using UnityEngine.UI;

public class HueSliderGradient : MonoBehaviour
{
    public Slider slider;

    void Start()
    {
        CreateGradient();
    }

    void CreateGradient()
    {
        Texture2D texture = new Texture2D(256, 1);

        texture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < 256; x++)
        {
            float hue = (float)x / 255f;

            Color color = Color.HSVToRGB(
                hue,
                1f,
                1f
            );

            texture.SetPixel(x, 0, color);
        }

        texture.Apply();

        Image background =
            slider.GetComponentInChildren<Image>();

        if (background != null)
        {
            background.sprite =
                Sprite.Create(
                    texture,
                    new Rect(
                        0,
                        0,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(0.5f, 0.5f)
                );
            }
    }
}