using System;
using System.Collections;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// Drives one HUD panel between the shape it has in the scene and a hidden shape, by animating
    /// its edges rather than its position. Moving both edges by the same amount slides the panel;
    /// moving one edge collapses it, so a panel can fold into its own title instead of flying off.
    /// Presentation only: it knows nothing about input, about the other panels, or about what the
    /// panel is for. Something else decides when to call <see cref="SetVisible"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class HudPanel : MonoBehaviour
    {
        [Header("Hidden state")]
        [Tooltip("How the left and bottom edges move when hidden, in the same units the Rect " +
                 "Transform inspector calls Left and Bottom. Raising Y folds the panel upward, " +
                 "raising X folds it in from the left.")]
        [SerializeField] private Vector2 hiddenOffsetMin = Vector2.zero;

        [Tooltip("How the right and top edges move when hidden. Positive X pushes the right edge " +
                 "right, positive Y pushes the top edge up. Give this the same value as the min " +
                 "offset and the panel slides whole instead of collapsing.")]
        [SerializeField] private Vector2 hiddenOffsetMax = Vector2.zero;

        [Tooltip("Alpha while hidden. Needs a CanvasGroup on this object to do anything; 1 keeps " +
                 "the panel fully opaque on the way out.")]
        [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 1f;

        [Header("Animation")]
        [Tooltip("Seconds the transition takes. 0 snaps.")]
        [SerializeField, Min(0f)] private float duration = 0.25f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Start")]
        [SerializeField] private bool visibleOnStart = true;

        /// <summary>Fires the moment the intent changes, not when the transition finishes.</summary>
        public event Action<bool> VisibilityChanged;

        public bool IsVisible { get; private set; } = true;

        protected RectTransform Rect { get; private set; }

        private CanvasGroup _canvasGroup;
        private Vector2 _shownOffsetMin;
        private Vector2 _shownOffsetMax;
        private Coroutine _routine;

        protected virtual void Awake()
        {
            Rect = (RectTransform)transform;
            _canvasGroup = GetComponent<CanvasGroup>();

            _shownOffsetMin = Rect.offsetMin;
            _shownOffsetMax = Rect.offsetMax;

            CacheShownState();
            SetVisible(visibleOnStart, true);
        }

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        public void Toggle() => SetVisible(!IsVisible);

        /// <summary>
        /// Drives the panel towards <paramref name="visible"/>. <paramref name="instant"/> skips the
        /// transition, which is what Awake uses so the very first frame is already right.
        /// </summary>
        public void SetVisible(bool visible, bool instant = false)
        {
            if (IsVisible == visible && !instant) return;

            IsVisible = visible;

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            VisibilityChanged?.Invoke(visible);

            if (instant || duration <= 0f)
            {
                ApplyProgress(visible ? 0f : 1f);
                return;
            }

            _routine = StartCoroutine(Animate(visible));
        }

        /// <summary>
        /// Read whatever the subclass needs to interpolate away from. Runs once, before the first
        /// <see cref="ApplyProgress"/>, so the values captured are the ones authored in the scene.
        /// </summary>
        protected virtual void CacheShownState()
        {
        }

        /// <summary>
        /// 0 is fully shown, 1 is fully hidden. Overrides drive whatever else changes with the
        /// panel and must call base.
        /// </summary>
        protected virtual void ApplyProgress(float hiddenAmount)
        {
            Rect.offsetMin = _shownOffsetMin + hiddenOffsetMin * hiddenAmount;
            Rect.offsetMax = _shownOffsetMax + hiddenOffsetMax * hiddenAmount;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(1f, hiddenAlpha, hiddenAmount);
            }
        }

        private IEnumerator Animate(bool visible)
        {
            float from = visible ? 1f : 0f;
            float to = visible ? 0f : 1f;
            float timer = 0f;

            while (timer < duration)
            {
                // Unscaled: the HUD should keep animating even if something freezes timeScale.
                timer += Time.unscaledDeltaTime;
                ApplyProgress(Mathf.Lerp(from, to, curve.Evaluate(Mathf.Clamp01(timer / duration))));
                yield return null;
            }

            ApplyProgress(to);
            _routine = null;
        }

        [ContextMenu("Preview hidden shape")]
        private void PreviewHidden()
        {
            RectTransform rect = (RectTransform)transform;
            rect.offsetMin += hiddenOffsetMin;
            rect.offsetMax += hiddenOffsetMax;
        }

        [ContextMenu("Preview shown shape")]
        private void PreviewShown()
        {
            RectTransform rect = (RectTransform)transform;
            rect.offsetMin -= hiddenOffsetMin;
            rect.offsetMax -= hiddenOffsetMax;
        }
    }
}
