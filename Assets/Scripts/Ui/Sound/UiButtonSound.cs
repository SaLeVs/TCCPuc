using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ui.Sound
{
    public enum UiSoundKind
    {
        [Tooltip("Decided from the button's text, name and what it calls.")]
        Auto,
        Click,
        Confirm,
        Close,
        Silent
    }

    /// <summary>
    /// Plays the UI sound for the Selectable it sits on. <see cref="UiSoundRouter"/> adds one to every
    /// button, toggle, dropdown and slider at runtime with <see cref="UiSoundKind.Auto"/>; add it by hand
    /// in a prefab or scene to force a kind (or Silent) on a particular button.
    /// </summary>
    /// <remarks>
    /// Buttons sound from their onClick, so only a click that actually happens is heard (never on a
    /// greyed-out button). Toggles and dropdowns sound on the press itself. Sliders tick only while the
    /// player is moving them - never when a script sets the value, as the options screen does on open.
    /// </remarks>
    [DisallowMultipleComponent]
    public class UiButtonSound : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISubmitHandler, IMoveHandler
    {
        [SerializeField] private UiSoundKind kind = UiSoundKind.Auto;

        private Selectable _selectable;
        private Button _button;
        private Slider _slider;
        private bool _pointerHeld;
        private float _lastMoveTime = -10f;
        private int _lastNotch = int.MinValue;
        private float _lastTickTime = -10f;
        private bool _hooked;

        public UiSoundKind Kind
        {
            get => kind;
            set => kind = value;
        }

        private void Awake() => Hook();

        private void Hook()
        {
            if (_hooked) return;
            _hooked = true;

            _selectable = GetComponent<Selectable>();
            _button = _selectable as Button;
            _slider = _selectable as Slider;

            if (_button != null) _button.onClick.AddListener(OnButtonClicked);
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnSliderChanged);
                _lastNotch = Notch(_slider);
            }
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnButtonClicked);
            if (_slider != null) _slider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        private void OnButtonClicked()
        {
            UiSoundRouter.Play(Resolve());
        }

        // Toggles and dropdowns: the press is the event (their value also changes from code, which must stay quiet).
        private bool SoundsOnPress => _selectable != null && _button == null && _slider == null
                                      && !(_selectable is Scrollbar) && !(_selectable is InputField)
                                      && !(_selectable is TMPro.TMP_InputField);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (SoundsOnPress && _selectable.IsInteractable()) UiSoundRouter.Play(Resolve());
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (SoundsOnPress && _selectable.IsInteractable()) UiSoundRouter.Play(Resolve());
        }

        public void OnPointerDown(PointerEventData eventData) => _pointerHeld = true;

        public void OnPointerUp(PointerEventData eventData) => _pointerHeld = false;

        public void OnMove(AxisEventData eventData) => _lastMoveTime = Time.unscaledTime;

        private void OnDisable() => _pointerHeld = false;

        private void OnSliderChanged(float value)
        {
            int notch = Notch(_slider);
            if (notch == _lastNotch) return;
            _lastNotch = notch;

            // Only the player's hand moves the bar audibly.
            bool byPlayer = _pointerHeld || Time.unscaledTime - _lastMoveTime < 0.3f;
            if (!byPlayer || !_slider.IsInteractable() || kind == UiSoundKind.Silent) return;

            UiSoundSettingsSO settings = UiSoundRouter.Settings;
            if (settings == null) return;

            if (Time.unscaledTime - _lastTickTime < settings.sliderMinInterval) return;
            _lastTickTime = Time.unscaledTime;

            float pitch = Mathf.Lerp(settings.sliderPitch.x, settings.sliderPitch.y, _slider.normalizedValue);
            UiSoundRouter.PlayTick(pitch);
        }

        private static int Notch(Slider slider)
        {
            UiSoundSettingsSO settings = UiSoundRouter.Settings;
            int notches = settings != null ? settings.sliderNotches : 20;

            float range = slider.maxValue - slider.minValue;
            if (slider.wholeNumbers && range > 0f && range <= notches) return Mathf.RoundToInt(slider.value);

            return Mathf.FloorToInt(slider.normalizedValue * notches + 0.0001f);
        }

        /// <summary>The kind set on this component, or the one its text, name and target method suggest.</summary>
        public UiSoundKind Resolve()
        {
            if (kind != UiSoundKind.Auto) return kind;

            // Asked before this object was ever active (Awake has not run): read the selectable now.
            Hook();

            return Guess(this);
        }

        #region Guessing the kind

        private static readonly string[] ConfirmWords =
        {
            "ESTOU PRONTO", "PRONTO", "READY", "SALVAR", "SAVE", "CONFIRMAR", "CONFIRM", "INICIAR", "COMECAR",
            "JOGAR", "CRIAR", "HOSPEDAR", "ENTRAR", "ASSINE AQUI", "APLICAR", "ACEITAR"
        };

        private static readonly string[] CloseWords =
        {
            "VOLTAR", "MENU", "MENU PRINCIPAL", "SAIR", "SAIR DO JOGO", "FECHAR", "X", "OK", "CONTINUAR",
            "CANCELAR", "BACK", "CLOSE", "EXIT", "QUIT"
        };

        private static readonly string[] ConfirmCalls =
            { "ready", "save", "confirm", "startgame", "connect", "createlobby", "createlan", "joinlobby", "joinlan", "starthost", "startclient", "launch" };

        private static readonly string[] CloseCalls =
            { "exit", "close", "quit", "leave", "return", "back", "cancel" };

        private static readonly Regex Tags = new("<[^>]+>");

        private static UiSoundKind Guess(UiButtonSound sound)
        {
            // Toggles, dropdowns and their items are just clicks.
            if (sound._button == null) return UiSoundKind.Click;

            string label = Normalize(ReadLabel(sound));

            if (!string.IsNullOrEmpty(label))
            {
                if (Matches(label, ConfirmWords)) return UiSoundKind.Confirm;
                if (Matches(label, CloseWords)) return UiSoundKind.Close;
            }

            // What the button calls: the first listed is the one that matters most.
            Button.ButtonClickedEvent click = sound._button.onClick;
            for (int i = 0; i < click.GetPersistentEventCount(); i++)
            {
                string method = click.GetPersistentMethodName(i).ToLowerInvariant();
                if (method == "setactive" || method == "onbuttonclick") continue;

                if (Contains(method, CloseCalls)) return UiSoundKind.Close;
                if (Contains(method, ConfirmCalls)) return UiSoundKind.Confirm;
            }

            string name = sound.gameObject.name.ToLowerInvariant();
            if (Contains(name, CloseCalls)) return UiSoundKind.Close;
            if (Contains(name, ConfirmCalls)) return UiSoundKind.Confirm;

            return UiSoundKind.Click;
        }

        private static string ReadLabel(UiButtonSound sound)
        {
            TMPro.TMP_Text tmp = sound.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null) return tmp.text;

            Text legacy = sound.GetComponentInChildren<Text>(true);
            return legacy != null ? legacy.text : null;
        }

        private static bool Matches(string label, string[] words)
        {
            foreach (string word in words)
            {
                if (label == word) return true;
            }

            return false;
        }

        private static bool Contains(string text, string[] words)
        {
            foreach (string word in words)
            {
                if (text.Contains(word)) return true;
            }

            return false;
        }

        /// <summary>Upper case, no rich-text tags, accents, spacing tricks ("C R É D I T O S") or stray symbols.</summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = Tags.Replace(text, string.Empty).Normalize(NormalizationForm.FormD);

            StringBuilder builder = new(text.Length);
            foreach (char c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c) || c == ' ') builder.Append(char.ToUpperInvariant(c));
            }

            string clean = Regex.Replace(builder.ToString(), " +", " ").Trim();

            // Letter-spaced titles: "C R E D I T O S" reads as one word.
            if (Regex.IsMatch(clean, "^(\\S )+\\S$")) clean = clean.Replace(" ", string.Empty);

            return clean;
        }

        #endregion
    }
}
