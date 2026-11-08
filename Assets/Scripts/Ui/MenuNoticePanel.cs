using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Notice of the main menu: a background, a text and one button. Tells the player why something
    /// did not work (wrong code, invalid IP, match not found...) and stays up until OK is pressed.
    /// While something is loading it says what is going on instead, with the button hidden or
    /// turned into a cancel.
    /// </summary>
    /// <remarks>
    /// Free to restyle: only <see cref="messageText"/> and <see cref="button"/> are required. The
    /// button is hooked up here in code, so its OnClick list in the inspector can stay empty.
    /// </remarks>
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuNoticePanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button button;

        [Tooltip("Optional: text of the button, switched between the two labels below.")]
        [SerializeField] private TMP_Text buttonLabel;

        [SerializeField] private string okLabel = "OK";
        [SerializeField] private string cancelLabel = "CANCELAR";

        /// <summary>Raised when the notice goes away.</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeInHierarchy;

        /// <summary>True while it shows a loading message rather than a result.</summary>
        public bool IsWaiting { get; private set; }

        private Action _onCancel;
        private bool _wired;

        /// <summary>Shows a message with the OK button.</summary>
        public void Show(string message)
        {
            Open(message, false, null);
        }

        /// <summary>
        /// Shows what is loading. Without <paramref name="onCancel"/> there is no button at all;
        /// with it, the button cancels.
        /// </summary>
        public void ShowWaiting(string message, Action onCancel = null)
        {
            Open(message, true, onCancel);
        }

        public void Hide()
        {
            IsWaiting = false;
            _onCancel = null;

            if (!gameObject.activeSelf) return;

            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void Open(string message, bool waiting, Action onCancel)
        {
            Wire();

            IsWaiting = waiting;
            _onCancel = onCancel;

            if (messageText != null) messageText.text = message;
            if (button != null) button.gameObject.SetActive(!waiting || onCancel != null);
            if (buttonLabel != null) buttonLabel.text = waiting ? cancelLabel : okLabel;

            // Drawn over everything else of its panel.
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            // The menu locks its buttons through a CanvasGroup on the canvas root while a notice is
            // up. The notice itself has to stay clickable, so it does not follow parent groups.
            GetComponent<CanvasGroup>().ignoreParentGroups = true;

            if (button != null) button.onClick.AddListener(OnButtonClicked);
        }

        private void OnButtonClicked()
        {
            if (!IsWaiting)
            {
                Hide();
                return;
            }

            // One cancel per wait: a second click must not run it twice.
            Action cancel = _onCancel;
            _onCancel = null;
            cancel?.Invoke();
        }
    }
}
