using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Ui
{
    /// <summary>
    /// One HUD element that folds away and comes back. What moves is a list: every row names a
    /// RectTransform and how far each of its edges travels, so a panel that collapses its title,
    /// its body and its counter is three rows of data instead of three fields in a subclass.
    /// Presentation only - it knows nothing about input. Something else decides when to toggle it.
    /// </summary>
    public class HudAnimationPanel : MonoBehaviour
    {
        [Serializable]
        public class Move
        {
            [Tooltip("Rect this row moves. Any rect at all - it does not have to be a child.")]
            public RectTransform target;

            [Tooltip("Travel of the left edge when hiding, in screen axes (+ goes right). Facing " +
                     "edges with the SAME value slide the rect; DIFFERENT values resize it.")]
            public float left;

            [Tooltip("Travel of the right edge (+ goes right).")]
            public float right;

            [Tooltip("Travel of the bottom edge (+ goes up).")]
            public float bottom;

            [Tooltip("Travel of the top edge (+ goes up).")]
            public float top;

            [Tooltip("Seconds this row takes. Rows run on their own clocks, so they need not match.")]
            [Min(0f)] public float duration = 0.25f;

            [Tooltip("Easing for this row.")]
            public AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

            [Tooltip("Seconds to wait before this row starts, for staggering rows on purpose.")]
            [Min(0f)] public float delay;

            // Written by the capture tool in the context menu; not used at runtime.
            [HideInInspector] public Vector2 authoredShownMin;
            [HideInInspector] public Vector2 authoredShownMax;

            [NonSerialized] public Vector2 shownMin;
            [NonSerialized] public Vector2 shownMax;

            public float TotalDuration => delay + duration;
        }

        [Serializable]
        public class VisibilityEvent : UnityEvent<bool>
        {
        }

        [Tooltip("Everything that moves when this panel is toggled.")]
        [SerializeField] private List<Move> moves = new();

        [Header("Objects")]
        [Tooltip("Switched off once the panel is fully hidden - a message list that would only " +
                 "cost a canvas batch while nobody can read it, for instance.")]
        [SerializeField] private GameObject[] hideWhenHidden;

        [Tooltip("Switched on once the panel is fully hidden - a stub or a handle, for instance.")]
        [SerializeField] private GameObject[] showWhenHidden;

        [Header("Start")]
        [SerializeField] private bool visibleOnStart = true;

        [Header("Events")]
        [Tooltip("Fires with the new state the moment the panel is told to change, not when the " +
                 "animation lands. Drop things like DonationUiController.SetCompact in here.")]
        [SerializeField] private VisibilityEvent onVisibilityChanged;

        [SerializeField, HideInInspector] private bool hasAuthoredShown;

        public bool IsVisible { get; private set; } = true;

        private Coroutine _routine;

        private void Awake()
        {
            // The shown shape is whatever the panel has in the scene, so nobody has to keep a
            // second copy of the layout in sync with the one they can actually see.
            foreach (Move move in moves)
            {
                if (move?.target == null) continue;

                move.shownMin = move.target.offsetMin;
                move.shownMax = move.target.offsetMax;
            }

            SetVisible(visibleOnStart, true);
        }

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        public void Toggle() => SetVisible(!IsVisible);

        /// <summary>
        /// Drives the panel towards <paramref name="visible"/>. <paramref name="instant"/> skips the
        /// animation, which is what Awake uses so the very first frame is already right.
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

            onVisibilityChanged?.Invoke(visible);

            // Coming back, the extras return before the animation so they are visible on the way
            // in. Going away, they only leave once it has fully closed.
            if (visible) ApplyObjectStates(false);

            if (instant)
            {
                Settle(visible);
                return;
            }

            _routine = StartCoroutine(Animate(visible));
        }

        private IEnumerator Animate(bool visible)
        {
            float longest = 0f;

            foreach (Move move in moves)
            {
                if (move != null && move.TotalDuration > longest) longest = move.TotalDuration;
            }

            float timer = 0f;

            while (timer < longest)
            {
                // Unscaled: the HUD should keep animating even if something freezes timeScale.
                timer += Time.unscaledDeltaTime;

                foreach (Move move in moves)
                {
                    ApplyMove(move, HiddenAmountAt(move, timer, visible));
                }

                yield return null;
            }

            Settle(visible);
            _routine = null;
        }

        private void Settle(bool visible)
        {
            foreach (Move move in moves)
            {
                ApplyMove(move, visible ? 0f : 1f);
            }

            if (!visible) ApplyObjectStates(true);
        }

        /// <summary>0 is fully shown, 1 is fully hidden, measured on this row's own clock.</summary>
        private static float HiddenAmountAt(Move move, float timer, bool visible)
        {
            if (move == null) return 0f;

            float t = move.duration <= 0f ? 1f : Mathf.Clamp01((timer - move.delay) / move.duration);
            float eased = move.curve != null ? move.curve.Evaluate(t) : t;

            return visible ? 1f - eased : eased;
        }

        private static void ApplyMove(Move move, float hiddenAmount)
        {
            if (move?.target == null) return;

            move.target.offsetMin = move.shownMin + new Vector2(move.left, move.bottom) * hiddenAmount;
            move.target.offsetMax = move.shownMax + new Vector2(move.right, move.top) * hiddenAmount;
        }

        private void ApplyObjectStates(bool fullyHidden)
        {
            SetActive(hideWhenHidden, !fullyHidden);
            SetActive(showWhenHidden, fullyHidden);
        }

        private static void SetActive(GameObject[] targets, bool active)
        {
            if (targets == null) return;

            foreach (GameObject target in targets)
            {
                // Guarded because this runs on every toggle and SetActive is not free.
                if (target == null || target.activeSelf == active) continue;

                target.SetActive(active);
            }
        }

        [ContextMenu("1. Mark current shapes as SHOWN")]
        private void MarkShownShapes()
        {
            foreach (Move move in moves)
            {
                if (move?.target == null) continue;

                move.authoredShownMin = move.target.offsetMin;
                move.authoredShownMax = move.target.offsetMax;
            }

            hasAuthoredShown = true;

            Debug.Log($"{name}: shown shape marked for {moves.Count} row(s). Arrange the panel the way it should look hidden, then run step 2.", this);
            MarkDirty();
        }

        [ContextMenu("2. Capture current shapes as HIDDEN")]
        private void CaptureHiddenShapes()
        {
            if (!hasAuthoredShown)
            {
                Debug.LogWarning($"{name}: run step 1 first, otherwise there is nothing to measure the travel against.", this);
                return;
            }

            foreach (Move move in moves)
            {
                if (move?.target == null) continue;

                move.left = move.target.offsetMin.x - move.authoredShownMin.x;
                move.bottom = move.target.offsetMin.y - move.authoredShownMin.y;
                move.right = move.target.offsetMax.x - move.authoredShownMax.x;
                move.top = move.target.offsetMax.y - move.authoredShownMax.y;

                // Put it back, so nobody saves the prefab stuck in its hidden shape.
                move.target.offsetMin = move.authoredShownMin;
                move.target.offsetMax = move.authoredShownMax;
            }

            Debug.Log($"{name}: hidden shape captured for {moves.Count} row(s), panel restored to its shown shape.", this);
            MarkDirty();
        }

        [ContextMenu("Preview hidden shapes")]
        private void PreviewHidden()
        {
            foreach (Move move in moves)
            {
                if (move?.target == null) continue;

                move.target.offsetMin += new Vector2(move.left, move.bottom);
                move.target.offsetMax += new Vector2(move.right, move.top);
            }
        }

        [ContextMenu("Preview shown shapes")]
        private void PreviewShown()
        {
            foreach (Move move in moves)
            {
                if (move?.target == null) continue;

                move.target.offsetMin -= new Vector2(move.left, move.bottom);
                move.target.offsetMax -= new Vector2(move.right, move.top);
            }
        }

        private void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
