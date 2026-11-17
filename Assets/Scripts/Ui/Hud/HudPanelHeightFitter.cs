using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// Makes a HUD panel as tall as the list inside it asks for, plus whatever the layout puts
    /// around that list (title, frame, padding), between a minimum and a maximum.
    /// </summary>
    /// <remarks>
    /// <para>Only the panel's own height changes, and everything stretched or anchored inside it
    /// follows on its own. What surrounds the list is measured once from the panel as laid out, so
    /// it never has to be typed in and kept in sync.</para>
    ///
    /// <para>The <see cref="HudAnimationPanel"/> that folds the panel away is the exception: its
    /// travel was measured at the authored height, so it is kept in step with the real one — see
    /// <see cref="KeepFoldedShape"/>.</para>
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

        [Tooltip("What folds the panel away. Empty means the HudAnimationPanel on this object, if any.")]
        [SerializeField] private HudAnimationPanel foldingPanel;

        [Tooltip("With nothing in the list, the panel takes the folding panel's folded shape (closed " +
                 "round the title, as if hidden) while still counting as shown. Needs a folding panel.")]
        [SerializeField] private bool foldWhenEmpty;

        private float _aroundContent;
        private float _authoredHeight;

        // The folded shape was put on because the list was empty, not by the folding panel.
        private bool _foldedForEmpty;

        // Per folding row: the bottom (x) and top (y) travel as authored.
        private Vector2[] _authoredTravel;


        private void Awake()
        {
            if (panel == null) panel = (RectTransform)transform;
            if (foldingPanel == null) foldingPanel = GetComponent<HudAnimationPanel>();

            _aroundContent = panel.rect.height - content.rect.height;
            _authoredHeight = panel.rect.height;

            if (foldingPanel == null) return;

            IReadOnlyList<HudAnimationPanel.Move> moves = foldingPanel.Moves;
            _authoredTravel = new Vector2[moves.Count];

            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] != null) _authoredTravel[i] = new Vector2(moves[i].bottom, moves[i].top);
            }
        }

        private void LateUpdate()
        {
            FitHeight();
            FoldWhileEmpty();
        }

        private void FitHeight()
        {
            // Held while folded away: the folding panel only puts its shapes back while it animates,
            // so a height change now would drag the folded shape out of place. It catches up the
            // moment the panel starts coming back.
            if (foldingPanel != null && !foldingPanel.IsVisible) return;

            // The layout group's preferred height is from the last layout pass: a row added this
            // frame resizes the panel on the next one, which nobody sees.
            float height = LayoutUtility.GetPreferredHeight(content) + _aroundContent;

            if (maxHeight > 0f) height = Mathf.Min(height, maxHeight);
            height = Mathf.Max(height, minHeight);

            if (Mathf.Abs(panel.rect.height - height) < 0.01f) return;

            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            KeepFoldedShape(height - _authoredHeight);
        }

        /// <summary>
        /// An empty list would still leave the frame's own border as a strip under the title, so
        /// with nothing in it the panel wears its folded shape instead - set after the folding
        /// panel's animation each frame, so showing or hiding it while empty changes nothing on
        /// screen. The first row brings the shown shape back, unless the player has folded the
        /// panel away meanwhile: then it already holds the folded shape it should.
        /// </summary>
        private void FoldWhileEmpty()
        {
            if (!foldWhenEmpty || foldingPanel == null || content == null) return;

            if (!HasRows())
            {
                ApplyShape(1f);
                _foldedForEmpty = true;
                return;
            }

            if (!_foldedForEmpty) return;

            _foldedForEmpty = false;
            if (foldingPanel.IsVisible) ApplyShape(0f);
        }

        // Counted by activeSelf, not by layout: a row added under a folded-away parent still counts.
        private bool HasRows()
        {
            foreach (Transform row in content)
            {
                if (row.gameObject.activeSelf) return true;
            }

            return false;
        }

        // 0 is the shown shape, 1 the folded one - the same sum the folding panel itself uses.
        private void ApplyShape(float folded)
        {
            foreach (HudAnimationPanel.Move move in foldingPanel.Moves)
            {
                if (move?.target == null) continue;

                move.target.offsetMin = move.shownMin + new Vector2(move.left, move.bottom) * folded;
                move.target.offsetMax = move.shownMax + new Vector2(move.right, move.top) * folded;
            }
        }

        /// <summary>
        /// Folding travel was authored against the panel at its authored height. An edge anchored
        /// away from the panel's pivot moves when the panel resizes, so its travel grows or shrinks
        /// by just as much and the folded shape lands where it was authored, whatever the height: a
        /// tall list still closes all the way, and what docks under the title still docks there.
        /// An edge that stays put while folding needs nothing — it follows the panel either way.
        /// </summary>
        private void KeepFoldedShape(float heightChange)
        {
            if (foldingPanel == null) return;

            IReadOnlyList<HudAnimationPanel.Move> moves = foldingPanel.Moves;
            float pivot = panel.pivot.y;

            for (int i = 0; i < moves.Count && i < _authoredTravel.Length; i++)
            {
                HudAnimationPanel.Move move = moves[i];
                if (move?.target == null || move.target.parent != panel) continue;

                Vector2 authored = _authoredTravel[i];

                if (authored.x != 0f) move.bottom = authored.x + (pivot - move.target.anchorMin.y) * heightChange;
                if (authored.y != 0f) move.top = authored.y + (pivot - move.target.anchorMax.y) * heightChange;
            }
        }
    }
}
