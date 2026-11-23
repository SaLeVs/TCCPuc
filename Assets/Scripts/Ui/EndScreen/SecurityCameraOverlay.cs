using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Ui.EndScreen
{
    /// <summary>
    /// The security-camera look drawn over the image: scanlines, a light video noise, a tracking
    /// band that rolls down now and then, a blinking REC, the camera name with date and time, the
    /// zoom, and the buttons to change camera. Textures are made in code; the canvas only needs
    /// the text objects and buttons wired here.
    /// </summary>
    public class SecurityCameraOverlay : MonoBehaviour
    {
        [Header("Image")]
        [SerializeField] private RawImage scanlines;
        [SerializeField] private RawImage noise;
        [SerializeField] private RawImage trackingBand;
        [SerializeField, Range(0f, 1f)] private float scanlineAlpha = 0.16f;
        [SerializeField, Range(0f, 1f)] private float noiseAlpha = 0.06f;
        [SerializeField] private Vector2 trackingEvery = new Vector2(5f, 11f);

        [Header("Text")]
        [SerializeField] private Graphic recDot;
        [SerializeField] private TMP_Text cameraLabel;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private TMP_Text zoomLabel;
        [SerializeField] private TMP_Text viewCounter;

        [Header("Buttons")]
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        private SecurityCamera _camera;
        private float _static;
        private float _trackingStart = -1f;
        private float _nextTracking;
        private DateTime _startClock;
        private float _startTime;

        private void Awake()
        {
            if (scanlines != null) scanlines.texture = MakeScanlines();
            if (noise != null) noise.texture = MakeNoise();
            if (trackingBand != null)
            {
                trackingBand.texture = MakeBand();
                trackingBand.color = new Color(1f, 1f, 1f, 0f);
            }

            _startClock = DateTime.Now;
            _startTime = Time.unscaledTime;
            _nextTracking = Time.time + Random.Range(1.5f, 3f);
        }

        public void Bind(SecurityCamera securityCamera)
        {
            _camera = securityCamera;
            _camera.OnViewChanged += OnViewChanged;

            if (previousButton != null) previousButton.onClick.AddListener(_camera.Previous);
            if (nextButton != null) nextButton.onClick.AddListener(_camera.Next);

            bool several = _camera.ViewCount > 1;
            if (previousButton != null) previousButton.gameObject.SetActive(several);
            if (nextButton != null) nextButton.gameObject.SetActive(several);
            if (viewCounter != null) viewCounter.gameObject.SetActive(several);

            OnViewChanged(_camera.CurrentView);
        }

        private void OnDestroy()
        {
            if (_camera != null) _camera.OnViewChanged -= OnViewChanged;
        }

        /// <summary>A burst of static over the whole image, fading out in about this long.</summary>
        public void Static(float strength) => _static = Mathf.Max(_static, strength * 2.5f);

        private void OnViewChanged(int index)
        {
            if (cameraLabel != null) cameraLabel.text = _camera.CurrentLabel;
            if (viewCounter != null) viewCounter.text = $"{index + 1}/{_camera.ViewCount}";
        }

        private void Update()
        {
            float time = Time.unscaledTime;

            if (scanlines != null)
            {
                // About one line every 3 px, crawling slowly upwards.
                float lines = Screen.height / 3f;
                scanlines.uvRect = new Rect(0f, -time * 0.6f / lines * 10f, 1f, lines);
                scanlines.color = new Color(1f, 1f, 1f, scanlineAlpha);
            }

            _static = Mathf.MoveTowards(_static, 0f, Time.unscaledDeltaTime * 2.5f);

            if (noise != null)
            {
                float scale = Screen.height / 256f / 2f;
                noise.uvRect = new Rect(Random.value, Random.value, Screen.width / 256f / 2f, scale);
                noise.color = new Color(1f, 1f, 1f, Mathf.Clamp01(noiseAlpha + _static));
            }

            UpdateTracking(time);

            if (recDot != null) recDot.enabled = time % 1.2f < 0.75f;

            if (clock != null)
            {
                DateTime now = _startClock.AddSeconds(time - _startTime);
                clock.text = now.ToString("dd/MM/yyyy  HH:mm:ss");
            }

            if (zoomLabel != null && _camera != null)
            {
                float zoom = _camera.Zoom;
                zoomLabel.text = zoom > 1.02f ? $"ZOOM x{zoom:0.0}" : "";
            }
        }

        private void UpdateTracking(float time)
        {
            if (trackingBand == null) return;

            if (_trackingStart < 0f && Time.time >= _nextTracking)
            {
                _trackingStart = time;
                _nextTracking = Time.time + Random.Range(trackingEvery.x, trackingEvery.y);
                if (_camera != null) _camera.Glitch(0.3f);
            }

            if (_trackingStart < 0f) return;

            const float duration = 1.6f;
            float t = (time - _trackingStart) / duration;
            if (t >= 1f)
            {
                _trackingStart = -1f;
                trackingBand.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            RectTransform rect = trackingBand.rectTransform;
            RectTransform parent = (RectTransform)rect.parent;
            float height = parent.rect.height;
            rect.anchoredPosition = new Vector2(Random.Range(-4f, 4f), Mathf.Lerp(height * 0.55f, -height * 0.55f, t));
            trackingBand.color = new Color(1f, 1f, 1f, 0.22f * Mathf.Sin(t * Mathf.PI));
            trackingBand.uvRect = new Rect(Random.value, 0f, 3f, 1f);
        }

        private static Texture2D MakeScanlines()
        {
            Texture2D texture = new Texture2D(1, 4, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "Scanlines"
            };
            texture.SetPixels(new[]
            {
                new Color(0f, 0f, 0f, 1f),
                new Color(0f, 0f, 0f, 0.35f),
                new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0.35f)
            });
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeNoise()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
                name = "VideoNoise"
            };

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                float v = Random.value;
                pixels[i] = new Color(v, v, v, Random.value);
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeBand()
        {
            const int width = 128;
            const int height = 32;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "TrackingBand"
            };

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float edge = Mathf.Sin(y / (float)(height - 1) * Mathf.PI);
                for (int x = 0; x < width; x++)
                {
                    float grain = 0.6f + 0.4f * Random.value;
                    pixels[y * width + x] = new Color(grain, grain, grain, edge * grain);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
