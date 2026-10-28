using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// Makes a HUD panel as tall as the list inside it asks for, plus whatever the layout puts
    /// around that list (title, frame, padding), between a minimum and a maximum.
    /// </summary>
    /// <remarks>
    /// Only the panel's own height changes. Everything stretched or anchored inside it follows on
    /// its own, and so do the edges a <see cref="HudAnimationPanel"/> moves, since those are
    /// offsets from the very anchors that move with it. What surrounds the list is measured once
    /// from the panel as laid out, so it never has to be typed in and kept in sync.
    /// </remarks>
    public class HudPanelHeightFitter : MonoBehaviour
    {
        [Tooltip("The rect that grows and shrinks. Empty means this object.")]
        [SerializeField] private RectTransform panel;

        [Tooltip("The list, stretched inside the panel. Its layout group decides how tall it wants to be.")]
        [SerializeField] private RectTransform content;

        [Tooltip("Shortest the panel gets, empty list included. 0 lets it close round just the frame.")]
        [SerializeField, Min(0f)] private float minHeight;

        [Tooltip("Tallest the panel gets. Past it the list is cut off by its mask. 0 means no limit.")]
        [SerializeField, Min(0f)] private float maxHeight;

        private float _aroundContent;


        private void Awake()
        {
            if (panel == null) panel = (RectTransform)transform;

            _aroundContent = panel.rect.height - content.rect.height;
        }

        private void LateUpdate()
        {
            // The layout group's preferred height is from the last layout pass: a row added this
            // frame resizes the panel on the next one, which nobody sees.
            float height = LayoutUtility.GetPreferredHeight(content) + _aroundContent;

            if (maxHeight > 0f) height = Mathf.Min(height, maxHeight);
            height = Mathf.Max(height, minHeight);

            if (Mathf.Abs(panel.rect.height - height) < 0.01f) return;

            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
