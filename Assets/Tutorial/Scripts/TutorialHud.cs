using System;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tutorial
{
    /// <summary>
    /// O que o tutorial põe na tela além do chat: a faixa de objetivo no topo (passo, título e o que
    /// fazer agora) e o cartão do fim, com os botões de voltar ao menu e de jogar de novo. Montado
    /// em código, num canvas próprio acima do HUD do jogador.
    /// </summary>
    public class TutorialHud : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color accent = new(1f, 0.83f, 0.29f, 1f);
        [SerializeField] private Color plate = new(0.05f, 0.045f, 0.045f, 0.82f);

        private RectTransform _banner;
        private CanvasGroup _bannerGroup;
        private TMP_Text _step;
        private TMP_Text _objective;
        private Image _progress;

        private RectTransform _card;
        private TMP_Text _cardTitle;
        private TMP_Text _cardBody;
        private Button _primary;
        private Button _secondary;
        private TMP_Text _primaryLabel;
        private TMP_Text _secondaryLabel;

        private float _pulse;

        private void Awake()
        {
            if (font == null) font = TMP_Settings.defaultFontAsset;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            BuildBanner();
            BuildCard();

            _bannerGroup.alpha = 0f;
            _card.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_bannerGroup.alpha < 1f && _objective.text.Length > 0)
            {
                _bannerGroup.alpha = Mathf.MoveTowards(_bannerGroup.alpha, 1f, Time.unscaledDeltaTime * 3f);
            }

            if (_pulse > 0f)
            {
                _pulse = Mathf.Max(0f, _pulse - Time.unscaledDeltaTime * 2.5f);
                float scale = 1f + 0.06f * _pulse;
                _banner.localScale = new Vector3(scale, scale, 1f);
            }
        }

        /// <summary>Troca o objetivo. <paramref name="step"/> começa em 1.</summary>
        public void SetObjective(int step, int total, string title, string objective)
        {
            _step.text = $"PASSO {step}/{total}  ·  {title.ToUpperInvariant()}";
            _objective.text = objective;
            _progress.fillAmount = total > 0 ? Mathf.Clamp01((step - 1f) / total) : 0f;
            _pulse = 1f;
        }

        /// <summary>Muda só a linha do que fazer, dentro do mesmo passo.</summary>
        public void SetObjectiveText(string objective)
        {
            if (_objective.text == objective) return;

            _objective.text = objective;
            _pulse = 0.6f;
        }

        public void HideBanner() => _bannerGroup.alpha = 0f;

        public void ShowEndCard(string title, string body, string primary, Action onPrimary, string secondary, Action onSecondary)
        {
            _progress.fillAmount = 1f;
            _cardTitle.text = title;
            _cardBody.text = body;

            _primaryLabel.text = primary;
            _primary.onClick.RemoveAllListeners();
            _primary.onClick.AddListener(() => onPrimary?.Invoke());

            bool hasSecondary = !string.IsNullOrEmpty(secondary);
            _secondary.gameObject.SetActive(hasSecondary);
            _secondaryLabel.text = secondary ?? string.Empty;
            _secondary.onClick.RemoveAllListeners();
            if (hasSecondary) _secondary.onClick.AddListener(() => onSecondary?.Invoke());

            EnsureEventSystem();

            _card.gameObject.SetActive(true);
            CursorState.Hold(CursorReason.Victory);
        }

        public void HideEndCard()
        {
            _card.gameObject.SetActive(false);
            CursorState.Release(CursorReason.Victory);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject system = new GameObject("EventSystem (tutorial)");
            system.AddComponent<EventSystem>();
            system.AddComponent<InputSystemUIInputModule>();
        }

        // ------------------------------------------------------------------ montagem

        private void BuildBanner()
        {
            // Embaixo, no centro: o topo é do cartão de doação do jogo, e os cantos de baixo já têm
            // vida, stamina e inventário.
            _banner = NewRect("Objetivo", transform);
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 0f);
            _banner.pivot = new Vector2(0.5f, 0f);
            _banner.anchoredPosition = new Vector2(0f, 26f);
            _banner.sizeDelta = new Vector2(860f, 112f);

            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            Image background = _banner.gameObject.AddComponent<Image>();
            background.color = plate;
            background.raycastTarget = false;

            Image stripe = NewImage("Faixa", _banner, accent);
            stripe.rectTransform.anchorMin = new Vector2(0f, 0f);
            stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            stripe.rectTransform.sizeDelta = new Vector2(8f, 0f);

            _step = NewText("Passo", _banner, 22f, accent, TextAlignmentOptions.TopLeft);
            Place(_step.rectTransform, new Vector2(28f, -12f), new Vector2(-40f, 30f));
            _step.characterSpacing = 3f;

            _objective = NewText("Texto", _banner, 30f, Color.white, TextAlignmentOptions.TopLeft);
            Place(_objective.rectTransform, new Vector2(28f, -42f), new Vector2(-40f, 62f));
            _objective.textWrappingMode = TextWrappingModes.Normal;
            _objective.text = string.Empty;

            RectTransform bar = NewRect("Progresso", _banner);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 5f);
            Image track = bar.gameObject.AddComponent<Image>();
            track.color = new Color(1f, 1f, 1f, 0.12f);
            track.raycastTarget = false;

            _progress = NewImage("Preenchimento", bar, accent);
            _progress.rectTransform.anchorMin = Vector2.zero;
            _progress.rectTransform.anchorMax = Vector2.one;
            _progress.rectTransform.sizeDelta = Vector2.zero;
            _progress.type = Image.Type.Filled;
            _progress.fillMethod = Image.FillMethod.Horizontal;
            _progress.sprite = WhiteSprite();
            _progress.fillAmount = 0f;
        }

        private void BuildCard()
        {
            _card = NewRect("Fim", transform);
            _card.anchorMin = Vector2.zero;
            _card.anchorMax = Vector2.one;
            _card.sizeDelta = Vector2.zero;

            Image dim = _card.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.65f);

            RectTransform box = NewRect("Cartao", _card);
            box.sizeDelta = new Vector2(900f, 520f);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = plate;

            Image stripe = NewImage("Faixa", box, accent);
            stripe.rectTransform.anchorMin = new Vector2(0f, 1f);
            stripe.rectTransform.anchorMax = new Vector2(1f, 1f);
            stripe.rectTransform.pivot = new Vector2(0.5f, 1f);
            stripe.rectTransform.sizeDelta = new Vector2(0f, 8f);

            _cardTitle = NewText("Titulo", box, 54f, accent, TextAlignmentOptions.Top);
            Place(_cardTitle.rectTransform, new Vector2(40f, -36f), new Vector2(-80f, 70f));

            _cardBody = NewText("Texto", box, 28f, Color.white, TextAlignmentOptions.Top);
            Place(_cardBody.rectTransform, new Vector2(50f, -120f), new Vector2(-100f, 260f));
            _cardBody.textWrappingMode = TextWrappingModes.Normal;

            _primary = NewButton("Primario", box, new Vector2(-170f, 60f), accent, Color.black, out _primaryLabel);
            _secondary = NewButton("Secundario", box, new Vector2(170f, 60f), new Color(1f, 1f, 1f, 0.15f), Color.white, out _secondaryLabel);
        }

        private Button NewButton(string name, Transform parent, Vector2 position, Color color, Color textColor, out TMP_Text label)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(300f, 70f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;

            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;

            label = NewText("Texto", rect, 30f, textColor, TextAlignmentOptions.Center);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;

            return button;
        }

        private static void Place(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite _white;

        private static Sprite WhiteSprite()
        {
            if (_white != null) return _white;

            Texture2D texture = Texture2D.whiteTexture;
            _white = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return _white;
        }
    }
}
