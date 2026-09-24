using System.Collections.Concurrent;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Components
{
    public enum ToastKind
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Notices that pop up in the middle of the screen and drift upward, in any scene and in any
    /// build. The console does not exist in a build, so this is where something going wrong shows.
    /// </summary>
    /// <remarks>
    /// <para>Installs itself before the first scene loads and survives scene changes; nothing has
    /// to be placed in a scene. Its look and switches live on the <c>Resources/ScreenToast</c>
    /// prefab, and it falls back to TMP's default font and a flat plate if that prefab is missing.</para>
    ///
    /// <para>Every <c>Debug.LogError</c> and exception is shown on its own, from any thread, so
    /// failures deep inside networking or an <c>async void</c> surface without anyone having to
    /// remember to report them. Call <see cref="Show"/> for anything that should reach the player
    /// directly. The same message arriving again while its toast is still up bumps a counter
    /// instead of stacking copies, so an error thrown every frame stays one line.</para>
    /// </remarks>
    public class ScreenToast : MonoBehaviour
    {
        private const string PrefabResource = "ScreenToast";
        private const int MaxMessageLength = 220;

        [Header("Look")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite plateSprite;
        [SerializeField] private Color plateColor = new(0.05f, 0.045f, 0.045f, 0.92f);
        [SerializeField] private Color textColor = new(0.93f, 0.9f, 0.88f, 1f);
        [SerializeField] private Color infoColor = new(0.878f, 0.847f, 0.827f, 1f);
        [SerializeField] private Color warningColor = new(0.96f, 0.72f, 0.25f, 1f);
        [SerializeField] private Color errorColor = new(0.93f, 0.26f, 0.24f, 1f);

        [Header("Behaviour")]
        [Tooltip("Show every Debug.LogError and exception as a toast.")]
        [SerializeField] private bool showLoggedErrors = true;

        [Tooltip("Show every Debug.LogWarning as a toast. Off by default: several warnings are " +
                 "content reminders that fire at the start of every match.")]
        [SerializeField] private bool showLoggedWarnings;

        [SerializeField, Min(1f)] private float lifetime = 5f;
        [SerializeField, Min(1f)] private float errorLifetime = 7f;

        [Tooltip("How far a toast drifts up over its lifetime, in reference pixels (1920x1080).")]
        [SerializeField] private float riseDistance = 90f;

        [Tooltip("Where toasts are born, in reference pixels from the centre of the screen.")]
        [SerializeField] private float spawnOffsetY = -40f;

        [SerializeField, Min(1)] private int maxVisible = 4;
        [SerializeField] private float maxTextWidth = 760f;

        private struct Pending
        {
            public string Message;
            public string Detail;
            public ToastKind Kind;
        }

        private sealed class Toast
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Accent;
            public TMP_Text Title;
            public TMP_Text Message;
            public TMP_Text Detail;

            public string Key;
            public string Text;
            public ToastKind Kind;
            public int Count;
            public float Age;
            public float Lifetime;
            public float Y;
            public bool Placed;
            public float Height;
            public bool Dying;
        }

        private static readonly ConcurrentQueue<Pending> Incoming = new();

        // Guards against a toast that fails while being built logging an error, which would ask
        // for another toast, which would fail again.
        [System.ThreadStatic] private static bool _building;

        private static ScreenToast _instance;

        private readonly List<Toast> _alive = new();
        private readonly Stack<Toast> _pool = new();

        private RectTransform _area;

        /// <summary>Puts a message on screen. Safe to call from any thread and from any scene.</summary>
        public static void Show(string message, ToastKind kind = ToastKind.Error)
        {
            Enqueue(message, null, kind);
        }

        private static void Enqueue(string message, string detail, ToastKind kind)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            Incoming.Enqueue(new Pending { Message = message, Detail = detail, Kind = kind });
        }

        /// <summary>Domain reload may be off, so statics from the last play session are cleared by hand.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            while (Incoming.TryDequeue(out _)) { }

            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            ScreenToast prefab = Resources.Load<ScreenToast>(PrefabResource);
            ScreenToast toast = prefab != null
                ? Instantiate(prefab)
                : new GameObject(nameof(ScreenToast)).AddComponent<ScreenToast>();

            toast.name = nameof(ScreenToast);
            DontDestroyOnLoad(toast.gameObject);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (font == null) font = TMP_Settings.defaultFontAsset;

            BuildCanvas();
        }

        private void OnEnable() => Application.logMessageReceivedThreaded += OnLogMessage;

        private void OnDisable() => Application.logMessageReceivedThreaded -= OnLogMessage;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (_building) return;

            bool isError = type is LogType.Error or LogType.Exception or LogType.Assert;

            if (isError && showLoggedErrors)
            {
                Enqueue(condition, FirstUsefulFrame(stackTrace), ToastKind.Error);
            }
            else if (type == LogType.Warning && showLoggedWarnings)
            {
                Enqueue(condition, null, ToastKind.Warning);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _building = true;

            try
            {
                while (Incoming.TryDequeue(out Pending pending)) Add(pending);
            }
            finally
            {
                _building = false;
            }

            Animate(dt);
        }

        // ------------------------------------------------------------------ Lifetime

        private void Add(Pending pending)
        {
            string text = pending.Message.Trim();
            if (text.Length > MaxMessageLength) text = text.Substring(0, MaxMessageLength) + "...";

            string key = pending.Kind + "|" + text;

            // Same thing again while it is still up: count it instead of stacking a copy.
            foreach (Toast existing in _alive)
            {
                if (existing.Dying || existing.Key != key) continue;

                // Kept up for as long as it keeps happening. The age is left alone so the toast does
                // not drop back down; only its end moves.
                existing.Count++;
                existing.Lifetime = Mathf.Max(existing.Lifetime, existing.Age + LifetimeFor(existing.Kind) * 0.5f);
                existing.Title.text = $"{TitleFor(existing.Kind)}   x{existing.Count}";
                return;
            }

            // Room for the new one: the oldest leaves early rather than the newest never showing.
            int living = 0;
            foreach (Toast t in _alive) if (!t.Dying) living++;

            for (int i = 0; living >= maxVisible && i < _alive.Count; i++)
            {
                if (_alive[i].Dying) continue;

                Kill(_alive[i]);
                living--;
            }

            Toast toast = _pool.Count > 0 ? _pool.Pop() : CreateToast();

            toast.Key = key;
            toast.Text = text;
            toast.Kind = pending.Kind;
            toast.Count = 1;
            toast.Age = 0f;
            toast.Lifetime = LifetimeFor(pending.Kind);
            toast.Placed = false;
            toast.Dying = false;

            Color accent = ColorFor(pending.Kind);
            toast.Accent.color = accent;
            toast.Title.color = accent;
            toast.Title.text = TitleFor(pending.Kind);
            toast.Message.text = text;
            toast.Detail.text = pending.Detail ?? string.Empty;
            toast.Detail.gameObject.SetActive(!string.IsNullOrEmpty(pending.Detail));

            Layout(toast);

            toast.Root.gameObject.SetActive(true);
            toast.Root.SetAsLastSibling();
            _alive.Add(toast);
        }

        private void Kill(Toast toast)
        {
            toast.Dying = true;

            // Skip straight to the fade.
            toast.Age = Mathf.Max(toast.Age, toast.Lifetime - FadeOutSeconds);
        }

        private const float FadeInSeconds = 0.25f;
        private const float FadeOutSeconds = 0.6f;
        private const float RiseSeconds = 3f;
        private const float Spacing = 12f;

        /// <summary>
        /// Each toast drifts up on its own clock, and is never allowed below the top of the one that
        /// arrived after it - newest at the bottom, older ones pushed up as new ones come in.
        /// </summary>
        private void Animate(float dt)
        {
            float follow = 1f - Mathf.Exp(-18f * dt);
            float floor = float.MinValue;

            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Toast toast = _alive[i];
                toast.Age += dt;

                if (toast.Age >= toast.Lifetime)
                {
                    _alive.RemoveAt(i);
                    toast.Root.gameObject.SetActive(false);
                    _pool.Push(toast);
                    continue;
                }

                float drift = Mathf.Clamp01(toast.Age / RiseSeconds);
                float rise = riseDistance * (1f - (1f - drift) * (1f - drift));
                float target = Mathf.Max(spawnOffsetY + rise, floor);

                toast.Y = toast.Placed ? Mathf.Lerp(toast.Y, target, follow) : target;
                toast.Placed = true;

                floor = toast.Y + toast.Height + Spacing;

                float fadeIn = Mathf.Clamp01(toast.Age / FadeInSeconds);
                float fadeOut = Mathf.Clamp01((toast.Lifetime - toast.Age) / FadeOutSeconds);
                float pop = Mathf.Lerp(0.9f, 1f, 1f - Mathf.Pow(1f - fadeIn, 3f));

                toast.Group.alpha = fadeIn * fadeOut;
                toast.Root.anchoredPosition = new Vector2(0f, toast.Y);
                toast.Root.localScale = new Vector3(pop, pop, 1f);
            }
        }

        private float LifetimeFor(ToastKind kind) => kind == ToastKind.Error ? errorLifetime : lifetime;

        // ------------------------------------------------------------------ Building

        private void BuildCanvas()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // No GraphicRaycaster: toasts must never eat a click meant for the menu under them.

            _area = NewRect("Toasts", transform);
            _area.anchorMin = Vector2.zero;
            _area.anchorMax = Vector2.one;
            _area.offsetMin = _area.offsetMax = Vector2.zero;
        }

        private Toast CreateToast()
        {
            Toast toast = new();

            toast.Root = NewRect("Toast", _area);
            toast.Root.pivot = new Vector2(0.5f, 0f);
            toast.Group = toast.Root.gameObject.AddComponent<CanvasGroup>();
            toast.Group.blocksRaycasts = false;
            toast.Group.interactable = false;

            Image plate = toast.Root.gameObject.AddComponent<Image>();
            plate.sprite = plateSprite;
            plate.type = plateSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            plate.pixelsPerUnitMultiplier = 3f;
            plate.color = plateColor;
            plate.raycastTarget = false;

            toast.Accent = NewImage("Accent", toast.Root);
            RectTransform accent = toast.Accent.rectTransform;
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.pivot = new Vector2(0f, 0.5f);
            accent.sizeDelta = new Vector2(5f, -16f);
            accent.anchoredPosition = new Vector2(12f, 0f);

            toast.Title = NewText("Title", toast.Root, 17f, FontStyles.Bold);
            toast.Title.characterSpacing = 6f;
            toast.Message = NewText("Message", toast.Root, 23f, FontStyles.Normal);
            toast.Message.color = textColor;
            toast.Detail = NewText("Detail", toast.Root, 15f, FontStyles.Italic);
            toast.Detail.color = new Color(textColor.r, textColor.g, textColor.b, 0.55f);

            return toast;
        }

        private const float PadLeft = 30f;
        private const float PadRight = 22f;
        private const float PadY = 12f;
        private const float LineGap = 3f;

        /// <summary>Sizes the plate around the text by hand. Layout groups would re-run every frame the toast moves.</summary>
        private void Layout(Toast toast)
        {
            float wrap = maxTextWidth;

            Vector2 title = toast.Title.GetPreferredValues(toast.Title.text, wrap, 0f);
            Vector2 message = toast.Message.GetPreferredValues(toast.Message.text, wrap, 0f);
            Vector2 detail = toast.Detail.gameObject.activeSelf
                ? toast.Detail.GetPreferredValues(toast.Detail.text, wrap, 0f)
                : Vector2.zero;

            float width = Mathf.Min(wrap, Mathf.Max(title.x, message.x, detail.x));
            width = Mathf.Max(width, 220f);

            float y = -PadY;
            y = Place(toast.Title.rectTransform, y, width, title.y);
            y = Place(toast.Message.rectTransform, y - LineGap, width, message.y);
            if (toast.Detail.gameObject.activeSelf) y = Place(toast.Detail.rectTransform, y - LineGap, width, detail.y);

            toast.Height = -y + PadY;
            toast.Root.sizeDelta = new Vector2(width + PadLeft + PadRight, toast.Height);
        }

        private static float Place(RectTransform rect, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(PadLeft, top);
            rect.sizeDelta = new Vector2(width, height);

            return top - height;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image NewImage(string name, Transform parent)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text NewText(string name, Transform parent, float size, FontStyles style)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private Color ColorFor(ToastKind kind) => kind switch
        {
            ToastKind.Error => errorColor,
            ToastKind.Warning => warningColor,
            _ => infoColor
        };

        private static string TitleFor(ToastKind kind) => kind switch
        {
            ToastKind.Error => "ERROR",
            ToastKind.Warning => "WARNING",
            _ => "NOTICE"
        };

        /// <summary>
        /// "Lan.cs:67" or the method name, from the first frame that is not the logger itself -
        /// enough to find the line from a build with no console.
        /// </summary>
        private static string FirstUsefulFrame(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return null;

            foreach (string raw in stackTrace.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("UnityEngine.Debug") || line.StartsWith("UnityEngine.Logger")) continue;
                if (line.StartsWith("UnityEngine.DebugLogHandler")) continue;

                int at = line.LastIndexOf("(at ");
                if (at >= 0)
                {
                    string location = line.Substring(at + 4).TrimEnd(')');
                    int slash = location.LastIndexOfAny(new[] { '/', '\\' });
                    if (slash >= 0) location = location.Substring(slash + 1);

                    int paren = line.IndexOf(" (");
                    string method = paren > 0 ? line.Substring(0, paren) : line;

                    return $"{method}  -  {location}";
                }

                return line.Length > 120 ? line.Substring(0, 120) + "..." : line;
            }

            return null;
        }
    }
}
