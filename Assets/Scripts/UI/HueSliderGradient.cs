using UnityEngine;
using UnityEngine.UI;

namespace Endowment.UI
{
    /// <summary>
    /// Generates a 256x1 rainbow gradient sprite and assigns it to the Slider's background image.
    /// </summary>
    public class HueSliderGradient : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Image targetImage;

        private static Texture2D cachedGradientTexture;

        private void Awake()
        {
            if (slider == null) slider = GetComponent<Slider>();
            if (targetImage == null && slider != null)
            {
                Transform bg = slider.transform.Find("Background");
                if (bg != null) targetImage = bg.GetComponent<Image>();
                if (targetImage == null) targetImage = slider.GetComponentInChildren<Image>();
            }

            ApplyGradient();
        }

        public void ApplyGradient()
        {
            if (targetImage == null) return;

            if (cachedGradientTexture == null)
            {
                cachedGradientTexture = new Texture2D(256, 1, TextureFormat.RGB24, false);
                cachedGradientTexture.wrapMode = TextureWrapMode.Clamp;
                cachedGradientTexture.filterMode = FilterMode.Bilinear;

                Color32[] cols = new Color32[256];
                for (int x = 0; x < 256; x++)
                {
                    float hue = (float)x / 255f;
                    cols[x] = Color.HSVToRGB(hue, 1f, 1f);
                }
                cachedGradientTexture.SetPixels32(cols);
                cachedGradientTexture.Apply(false, true);
            }

            Sprite gradientSprite = Sprite.Create(
                cachedGradientTexture,
                new Rect(0, 0, cachedGradientTexture.width, cachedGradientTexture.height),
                new Vector2(0.5f, 0.5f)
            );

            targetImage.sprite = gradientSprite;
            targetImage.type = Image.Type.Simple;
        }
    }
}

