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
    ///
    /// <para>Visual com as peças do HUD do jogo: a faixa é o painel de missões (fundo cinza com borda
    /// de papel rasgado) com a etiqueta de papel do título "MISSÕES"; o cartão do fim segue a tela de
    /// vitória (fita de filme, fundo de papel amassado do Opções e o botão do Win/Lose).</para>
    /// </summary>
    public class TutorialHud : MonoBehaviour
    {
        [Header("Fontes do jogo")]
        [Tooltip("Texto corrido (Acme, a do painel de missões).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Título do cartão do fim (FreckleFace, a do INTERVALO).")]
        [SerializeField] private TMP_FontAsset titleFont;
        [Tooltip("Botões (Lalezar, a do botão SAIR da vitória).")]
        [SerializeField] private TMP_FontAsset buttonFont;

        [Header("Sprites do HUD do jogo")]
        [Tooltip("UI_BackgroundMissions: fundo do painel de missões (fatiado).")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private float panelPixelsPerUnit = 23.15f;
        [Tooltip("UI_BackgroundTitleMissions: a etiqueta de papel do título MISSÕES.")]
        [SerializeField] private Sprite labelSprite;
        [Tooltip("UI_LineMissions: a linha de papel rasgado (barra de progresso).")]
        [SerializeField] private Sprite lineSprite;
        [Tooltip("UI_BackgroundSettings: o papel amassado do Opções (cartão do fim).")]
        [SerializeField] private Sprite cardSprite;
        [Tooltip("Film_Straight: a fita de filme da tela de vitória.")]
        [SerializeField] private Sprite filmSprite;
        [Tooltip("Button_ON: o botão da tela de vitória/derrota.")]
        [SerializeField] private Sprite buttonSprite;
        [Tooltip("UI_Line_Decoration: o fio decorativo do Opções.")]
        [SerializeField] private Sprite decorationSprite;

        [Header("Cores do HUD do jogo")]
        [SerializeField] private Color paper = new(0.878f, 0.839f, 0.812f, 1f);
        [SerializeField] private Color ink = new(0.027f, 0.024f, 0.024f, 1f);
        [SerializeField] private Color buttonInk = new(0.196f, 0.196f, 0.196f, 1f);

        // Sem o sprite (cena antiga), o fundo vira uma placa escura lisa.
        private static readonly Color FallbackPlate = new(0.05f, 0.045f, 0.045f, 0.82f);

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
            if (titleFont == null) titleFont = font;
            if (buttonFont == null) buttonFont = font;

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

        // A etiqueta do passo sai um pouco para cima da faixa, como a do painel de missões.
        private const float TopMargin = 40f;

        /// <summary>Desce a faixa (em pixels de referência) para não ficar atrás de algo no topo; 0 volta ao lugar.</summary>
        public void SetBannerDrop(float drop) => _banner.anchoredPosition = new Vector2(0f, -TopMargin - Mathf.Max(0f, drop));

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
            // Em cima, no centro, entre o painel de missões (esquerda) e o REC (direita). Na lição
            // de doação o diretor desce a faixa para baixo do cartão de doação do jogo.
            _banner = NewRect("Objetivo", transform);
            _banner.anchorMin = _banner.anchorMax = new Vector2(0.5f, 1f);
            _banner.pivot = new Vector2(0.5f, 1f);
            _banner.anchoredPosition = new Vector2(0f, -TopMargin);
            _banner.sizeDelta = new Vector2(820f, 118f);

            _bannerGroup = _banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerGroup.interactable = false;

            // Fundo: o mesmo do painel de missões (cinza com borda de papel rasgado).
            Image background = _banner.gameObject.AddComponent<Image>();
            background.raycastTarget = false;

            if (panelSprite != null)
            {
                background.sprite = panelSprite;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = panelPixelsPerUnit;
                background.color = Color.white;
            }
            else
            {
                background.color = FallbackPlate;
            }

            // Etiqueta de papel com o passo, saindo por cima da faixa (a do título MISSÕES).
            RectTransform label = NewRect("Etiqueta", _banner);
            label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
            label.pivot = new Vector2(0f, 0.5f);
            label.anchoredPosition = new Vector2(-12f, 2f);
            label.sizeDelta = new Vector2(470f, 52f);

            Image labelImage = label.gameObject.AddComponent<Image>();
            labelImage.raycastTarget = false;
            labelImage.sprite = labelSprite;
            labelImage.color = labelSprite != null ? Color.white : paper;

            _step = NewText("Passo", label, 26f, ink, TextAlignmentOptions.Center, font);
            Stretch(_step.rectTransform, new Vector2(18f, 4f), new Vector2(-18f, -4f));
            _step.characterSpacing = 2f;
            _step.enableAutoSizing = true;
            _step.fontSizeMin = 16f;
            _step.fontSizeMax = 26f;

            _objective = NewText("Texto", _banner, 30f, paper, TextAlignmentOptions.Left, font);
            Stretch(_objective.rectTransform, new Vector2(30f, 26f), new Vector2(-30f, -32f));
            _objective.textWrappingMode = TextWrappingModes.Normal;

            // Objetivo comprido encolhe para caber na faixa em vez de vazar por baixo dela.
            _objective.enableAutoSizing = true;
            _objective.fontSizeMin = 20f;
            _objective.fontSizeMax = 30f;
            _objective.text = string.Empty;

            // Progresso: a linha de papel rasgado do painel de missões, apagada por baixo e acesa até o passo.
            RectTransform bar = NewRect("Progresso", _banner);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 12f);
            bar.sizeDelta = new Vector2(-60f, 8f);

            Image track = bar.gameObject.AddComponent<Image>();
            track.sprite = lineSprite;
            track.color = new Color(paper.r, paper.g, paper.b, 0.22f);
            track.raycastTarget = false;

            _progress = NewImage("Preenchimento", bar, paper);
            _progress.rectTransform.anchorMin = Vector2.zero;
            _progress.rectTransform.anchorMax = Vector2.one;
            _progress.rectTransform.sizeDelta = Vector2.zero;
            _progress.sprite = lineSprite != null ? lineSprite : WhiteSprite();
            _progress.type = Image.Type.Filled;
            _progress.fillMethod = Image.FillMethod.Horizontal;
            _progress.fillAmount = 0f;
        }

        private void BuildCard()
        {
            _card = NewRect("Fim", transform);
            _card.anchorMin = Vector2.zero;
            _card.anchorMax = Vector2.one;
            _card.sizeDelta = Vector2.zero;

            Image dim = _card.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.8f);

            // Fita de filme em cima e embaixo, como na tela de vitória.
            if (filmSprite != null)
            {
                Film("Filme (cima)", new Vector2(0.5f, 1f), 0f);
                Film("Filme (baixo)", new Vector2(0.5f, 0f), 180f);
            }

            RectTransform box = NewRect("Cartao", _card);
            box.sizeDelta = new Vector2(1000f, 560f);
            Image boxImage = box.gameObject.AddComponent<Image>();
            boxImage.sprite = cardSprite;
            boxImage.color = cardSprite != null ? Color.white : FallbackPlate;

            _cardTitle = NewText("Titulo", box, 76f, paper, TextAlignmentOptions.Center, titleFont);
            Place(_cardTitle.rectTransform, new Vector2(40f, -34f), new Vector2(-80f, 100f));
            _cardTitle.enableAutoSizing = true;
            _cardTitle.fontSizeMin = 40f;
            _cardTitle.fontSizeMax = 76f;

            Decoration("Fio (cima)", box, -146f);

            _cardBody = NewText("Texto", box, 30f, paper, TextAlignmentOptions.Top, font);
            Place(_cardBody.rectTransform, new Vector2(70f, -172f), new Vector2(-140f, 200f));
            _cardBody.textWrappingMode = TextWrappingModes.Normal;

            Decoration("Fio (baixo)", box, -384f);

            _primary = NewButton("Primario", box, new Vector2(-185f, 48f), Color.white, out _primaryLabel);
            _secondary = NewButton("Secundario", box, new Vector2(185f, 48f), new Color(0.78f, 0.78f, 0.78f, 1f), out _secondaryLabel);
        }

        private void Film(string name, Vector2 anchor, float rotation)
        {
            RectTransform strip = NewRect(name, _card);
            strip.anchorMin = new Vector2(0f, anchor.y);
            strip.anchorMax = new Vector2(1f, anchor.y);
            strip.sizeDelta = new Vector2(40f, 170f);
            strip.localRotation = Quaternion.Euler(0f, 0f, rotation);
            strip.anchoredPosition = new Vector2(0f, anchor.y > 0.5f ? -85f : 85f);

            Image image = strip.gameObject.AddComponent<Image>();
            image.sprite = filmSprite;
            image.raycastTarget = false;
        }

        private void Decoration(string name, Transform parent, float y)
        {
            if (decorationSprite == null) return;

            RectTransform line = NewRect(name, parent);
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = new Vector2(0f, y);
            line.sizeDelta = new Vector2(760f, 14f);

            Image image = line.gameObject.AddComponent<Image>();
            image.sprite = decorationSprite;
            image.raycastTarget = false;
        }

        private Button NewButton(string name, Transform parent, Vector2 position, Color tint, out TMP_Text label)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(330f, 80f);

            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = buttonSprite;
            image.color = buttonSprite != null ? tint : paper * tint;

            // Mesmas cores do botão SAIR da tela de vitória.
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.colors = colors;

            label = NewText("Texto", rect, 34f, buttonInk, TextAlignmentOptions.Center, buttonFont);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;

            return button;
        }

        /// <summary>Preenche o pai com as margens dadas (canto de baixo-esquerda e de cima-direita).</summary>
        private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = minOffset;
            rect.offsetMax = maxOffset;
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

        private static TMP_Text NewText(string name, Transform parent, float size, Color color, TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (fontAsset != null) text.font = fontAsset;
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
