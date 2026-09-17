using UnityEngine;

namespace Ui
{
    /// <summary>
    /// The chat doesn't leave the screen like the other panels: it slides right until only its
    /// title is left standing beside the audience bar, and that stub keeps showing the viewer
    /// count. So on top of the slide it shrinks the title and re-fits the count inside it.
    /// </summary>
    public class ChatHudPanel : HudPanel
    {
        [Header("Title collapse")]
        [Tooltip("The bar holding the chat icon and the viewer count.")]
        [SerializeField] private RectTransform titleRect;

        [Tooltip("Width the title shrinks to while collapsed. This is what decides how wide the " +
                 "stub left beside the audience bar is.")]
        [SerializeField, Min(0f)] private float collapsedTitleWidth = 130f;

        [Header("Viewer count")]
        [Tooltip("Label with the number of viewers. It stays readable while collapsed, so it gets " +
                 "its own collapsed width and position instead of being clipped by the title.")]
        [SerializeField] private RectTransform viewersRect;

        [SerializeField, Min(0f)] private float collapsedViewersWidth = 70f;
        [SerializeField] private Vector2 collapsedViewersPosition = new Vector2(20f, 0f);

        [Header("Hidden extras")]
        [Tooltip("Message list. Switched off once the chat is fully collapsed so it stops drawing " +
                 "and stops costing a canvas batch while nobody can read it.")]
        [SerializeField] private GameObject chatView;

        [Tooltip("Optional tab shown only while the chat is fully collapsed.")]
        [SerializeField] private GameObject handleIcon;

        private float _shownTitleWidth;
        private float _shownViewersWidth;
        private Vector2 _shownViewersPosition;

        protected override void CacheShownState()
        {
            if (titleRect != null) _shownTitleWidth = titleRect.sizeDelta.x;

            if (viewersRect != null)
            {
                _shownViewersWidth = viewersRect.sizeDelta.x;
                _shownViewersPosition = viewersRect.anchoredPosition;
            }
        }

        protected override void ApplyProgress(float hiddenAmount)
        {
            base.ApplyProgress(hiddenAmount);

            if (titleRect != null)
            {
                Vector2 size = titleRect.sizeDelta;
                size.x = Mathf.Lerp(_shownTitleWidth, collapsedTitleWidth, hiddenAmount);
                titleRect.sizeDelta = size;
            }

            if (viewersRect != null)
            {
                Vector2 size = viewersRect.sizeDelta;
                size.x = Mathf.Lerp(_shownViewersWidth, collapsedViewersWidth, hiddenAmount);
                viewersRect.sizeDelta = size;
                viewersRect.anchoredPosition = Vector2.Lerp(_shownViewersPosition, collapsedViewersPosition, hiddenAmount);
            }

            // Guarded because ApplyProgress runs every frame of the slide and SetActive is not free.
            SetActiveIfNeeded(chatView, hiddenAmount < 1f);
            SetActiveIfNeeded(handleIcon, hiddenAmount >= 1f);
        }

        private static void SetActiveIfNeeded(GameObject target, bool active)
        {
            if (target == null || target.activeSelf == active) return;

            target.SetActive(active);
        }
    }
}
