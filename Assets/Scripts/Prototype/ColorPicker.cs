using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ColorPicker : MonoBehaviour
{
    [Header("UI")]
    public RawImage svArea;
    public Slider hueSlider;
    public Image colorPreview;

    [Header("Fish")]
    public Renderer fishRenderer;

    private Texture2D svTexture;

    private float hue = 0f;
    private float saturation = 1f;
    private float value = 1f;

    private MaterialPropertyBlock propertyBlock;

    private static readonly int FishColorID = Shader.PropertyToID("_FishColor");

    void Start()
    {
        propertyBlock = new MaterialPropertyBlock();

        CreateSVTexture();

        hueSlider.onValueChanged.AddListener(OnHueChanged);

        UpdateColor();
    }

    void CreateSVTexture()
    {
        int width = 256;
        int height = 256;

        svTexture = new Texture2D(width, height);

        svTexture.wrapMode = TextureWrapMode.Clamp;
        svTexture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float s = (float)x / (width - 1);
                float v = (float)y / (height - 1);

                Color color = Color.HSVToRGB(hue, s, v);

                svTexture.SetPixel(x, y, color);
            }
        }

        svTexture.Apply();

        svArea.texture = svTexture;
    }

    void OnHueChanged(float newHue)
    {
        hue = newHue;

        CreateSVTexture();

        UpdateColor();
    }

    void UpdateColor()
    {
        Color color = Color.HSVToRGB(
            hue,
            saturation,
            value
        );

        colorPreview.color = color;

        ApplyColorToFish(color);
    }

    void ApplyColorToFish(Color color)
    {
        if (fishRenderer == null)
            return;

        fishRenderer.GetPropertyBlock(propertyBlock);

        propertyBlock.SetColor(
            FishColorID,
            color
        );

        fishRenderer.SetPropertyBlock(propertyBlock);
    }

    public void SetSV(Vector2 position)
    {
        saturation = Mathf.Clamp01(position.x);
        value = Mathf.Clamp01(position.y);

        UpdateColor();
    }
}