using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Endowment.UI
{
    /// <summary>
    /// Full Custom ColorPicker UI controller for the 3D Fish.
    /// Manages:
    /// 1. 2D Saturation-Value box with dynamic gradient texture (128x128, zero-allocation).
    /// 2. Hue slider with rainbow gradient.
    /// 3. Real-time live color updates on Fish3DView (60 FPS, GC-free).
    /// 4. Debounced/on-release saving to PlayerDataRepository and backend API.
    /// 5. Hex code text and Color Preview UI display.
    /// </summary>
    public class ColorPicker : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private RawImage svArea;
        [SerializeField] private SVAreaController svController;
        [SerializeField] private Slider hueSlider;
        [SerializeField] private Image colorPreview;
        [SerializeField] private TextMeshProUGUI textHex;

        [Header("Quick Preset Buttons (Optional)")]
        [SerializeField] private Button[] presetButtons;
        [SerializeField] private Color[] presetColors;

        [Header("3D Showcase Target (Auto-found if null)")]
        [SerializeField] private Fish3DView targetFish3D;

        [Header("Settings")]
        [SerializeField] private bool autoSaveOnRelease = true;
        [SerializeField] private float debounceSaveTime = 0.4f;

        private Texture2D svTexture;
        private Color32[] pixelBuffer;
        private const int TEX_SIZE = 128;

        private float currentHue = 0.58f; // default blueish
        private float currentSat = 0.84f;
        private float currentVal = 0.95f;

        private Color currentColor;
        private string currentHex = "#2673F2";

        private float saveCooldownTimer = -1f;
        private bool pendingSave = false;

        public Color CurrentColor => currentColor;
        public string CurrentHex => currentHex;

        public event Action<Color, string> OnColorChanged;
        public event Action<string> OnColorSaved;

        private void Awake()
        {
            InitializeTexture();

            if (svController == null && svArea != null)
            {
                svController = svArea.GetComponent<SVAreaController>();
            }

            if (hueSlider != null)
            {
                hueSlider.onValueChanged.AddListener(OnHueSliderChanged);

                // Add EventTrigger for Slider pointer up to commit save immediately
                EventTrigger trigger = hueSlider.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = hueSlider.gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                entry.callback.AddListener((data) => { OnPointerUp(); });
                trigger.triggers.Add(entry);
            }
        }

        private void Start()
        {
            if (targetFish3D == null)
            {
                targetFish3D = FindFirstObjectByType<Fish3DView>();
            }

            if (presetButtons != null && presetColors != null)
            {
                for (int i = 0; i < presetButtons.Length && i < presetColors.Length; i++)
                {
                    if (presetButtons[i] == null) continue;
                    Color c = presetColors[i];
                    presetButtons[i].onClick.AddListener(() => SetColor(c, true));
                }
            }

            // Sync with current player fish data
            FishData data = PlayerDataRepository.GetPlayerFish();
            if (data != null && !string.IsNullOrEmpty(data.colorVariant))
            {
                SetColorFromHex(data.colorVariant, notifySave: false);
            }
            else
            {
                UpdateColor(triggerSave: false, isLive: false);
            }
        }

        private void Update()
        {
            if (pendingSave && saveCooldownTimer > 0f)
            {
                saveCooldownTimer -= Time.deltaTime;
                if (saveCooldownTimer <= 0f)
                {
                    CommitSave();
                }
            }
        }

        private void OnDestroy()
        {
            if (svTexture != null)
            {
                Destroy(svTexture);
                svTexture = null;
            }
        }

        private void InitializeTexture()
        {
            if (svTexture != null) return;

            svTexture = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
            svTexture.wrapMode = TextureWrapMode.Clamp;
            svTexture.filterMode = FilterMode.Bilinear;

            pixelBuffer = new Color32[TEX_SIZE * TEX_SIZE];

            if (svArea != null)
            {
                svArea.texture = svTexture;
            }
        }

        private void RegenerateSVTexture()
        {
            if (svTexture == null || pixelBuffer == null) InitializeTexture();

            for (int y = 0; y < TEX_SIZE; y++)
            {
                float v = (float)y / (TEX_SIZE - 1);
                int rowOffset = y * TEX_SIZE;
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    float s = (float)x / (TEX_SIZE - 1);
                    pixelBuffer[rowOffset + x] = Color.HSVToRGB(currentHue, s, v);
                }
            }

            svTexture.SetPixels32(pixelBuffer);
            svTexture.Apply(false);
        }

        public void SetSV(Vector2 position, bool isDragging)
        {
            currentSat = Mathf.Clamp01(position.x);
            currentVal = Mathf.Clamp01(position.y);

            UpdateColor(triggerSave: !isDragging, isLive: true);

            if (isDragging)
            {
                // Debounce save in case user doesn't trigger OnPointerUp
                saveCooldownTimer = debounceSaveTime;
                pendingSave = true;
            }
        }

        public void OnHueSliderChanged(float newHue)
        {
            currentHue = Mathf.Clamp01(newHue);
            RegenerateSVTexture();
            UpdateColor(triggerSave: false, isLive: true);

            saveCooldownTimer = debounceSaveTime;
            pendingSave = true;
        }

        public void OnPointerUp()
        {
            if (autoSaveOnRelease)
            {
                CommitSave();
            }
        }

        private void UpdateColor(bool triggerSave, bool isLive)
        {
            currentColor = Color.HSVToRGB(currentHue, currentSat, currentVal);
            currentHex = "#" + ColorUtility.ToHtmlStringRGB(currentColor);

            if (colorPreview != null)
            {
                colorPreview.color = currentColor;
            }

            if (textHex != null)
            {
                textHex.text = currentHex;
            }

            if (isLive)
            {
                if (targetFish3D == null) targetFish3D = FindFirstObjectByType<Fish3DView>();
                if (targetFish3D != null)
                {
                    targetFish3D.SetLiveColor(currentColor);
                }
            }

            OnColorChanged?.Invoke(currentColor, currentHex);

            if (triggerSave)
            {
                CommitSave();
            }
        }

        public void SetColor(Color color, bool notifySave = false)
        {
            Color.RGBToHSV(color, out currentHue, out currentSat, out currentVal);

            if (hueSlider != null)
            {
                hueSlider.SetValueWithoutNotify(currentHue);
            }

            RegenerateSVTexture();

            if (svController != null)
            {
                svController.UpdateHandleVisual(currentSat, currentVal);
            }

            UpdateColor(triggerSave: notifySave, isLive: true);
        }

        public void SetColorFromHex(string hexOrName, bool notifySave = false)
        {
            Color resolved = Fish3DView.ResolveFishColor(hexOrName);
            SetColor(resolved, notifySave);
        }

        public void CommitSave()
        {
            pendingSave = false;
            saveCooldownTimer = -1f;

            PlayerDataRepository.SaveFishColor(currentHex);
            OnColorSaved?.Invoke(currentHex);

            var collectionUI = FindFirstObjectByType<CollectionUIController>();
            if (collectionUI != null)
            {
                collectionUI.RefreshShowcase();
            }

            Debug.Log($"[ColorPicker] Saved fish color: {currentHex}");
        }
    }
}
