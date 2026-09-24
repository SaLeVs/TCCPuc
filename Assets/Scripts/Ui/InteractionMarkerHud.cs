using System.Collections.Generic;
using Components;
using Inputs;
using Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ui
{
    /// <summary>
    /// Draws camcorder-style brackets around every <see cref="InteractionMarker"/> near the local
    /// player: faint at a distance, locking on with a kick when the crosshair lands on one, with the
    /// key prompt underneath. Objects the stream cares about wear an eye that turns red while the
    /// camera is actually recording them.
    /// </summary>
    /// <remarks>
    /// <para>Widgets are built in code and pooled, so there is no prefab to keep in step with the
    /// script. Everything here is cosmetic and local: it reads the interactor and the vision
    /// sensors, it never tells them anything.</para>
    ///
    /// <para>Placement runs in <see cref="Canvas.preWillRenderCanvases"/> rather than LateUpdate.
    /// Cinemachine moves the camera in its own LateUpdate, and whichever of the two ran second
    /// decided whether the brackets trailed the object by a frame - a visible wobble whenever the
    /// player turned. By the time canvases are about to render, the camera has settled.</para>
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public class InteractionMarkerHud : MonoBehaviour
    {
        private const string BlockedLabel = "Unavailable";

        [Header("References")]
        [SerializeField] private PlayerInteractor interactor;
        [SerializeField] private InputReader inputReader;

        [Tooltip("Sensors whose view counts as \"being recorded\". Leave empty to use every " +
                 "VisionSensor on the interactor's player.")]
        [SerializeField] private VisionSensor[] recordingSensors;

        [Header("Art")]
        [SerializeField] private Sprite cornerSprite;
        [SerializeField] private Sprite eyeSprite;
        [SerializeField] private Sprite recordingDotSprite;
        [SerializeField] private Sprite keySprite;
        [SerializeField] private Sprite labelSprite;
        [SerializeField] private TMP_FontAsset font;

        [Header("Colours")]
        [SerializeField] private Color idleColor = new(0.878f, 0.847f, 0.827f, 1f);
        [SerializeField] private Color focusColor = new(1f, 0.98f, 0.93f, 1f);
        [SerializeField] private Color blockedColor = new(0.87f, 0.34f, 0.3f, 1f);
        [SerializeField] private Color recordingColor = new(0.94f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color keyTextColor = new(0.08f, 0.07f, 0.07f, 1f);

        [Header("Reach")]
        [Tooltip("Metres at which brackets appear around things the player can use.")]
        [SerializeField, Min(0f)] private float revealDistance = 8f;

        [Tooltip("Metres at which the eye appears on things the audience wants to see. Wider " +
                 "than the use range, because these are worth walking over to.")]
        [SerializeField, Min(0f)] private float recordableRevealDistance = 14f;

        [Tooltip("Opacity of a marker at the edge of its reach. It fades up as the player closes in.")]
        [SerializeField, Range(0f, 1f)] private float farAlpha = 0.3f;

        [Tooltip("Most markers on screen at once, nearest first. The one under the crosshair is " +
                 "always kept.")]
        [SerializeField, Min(1)] private int maxMarkers = 10;

        [Tooltip("What hides a marker. Triggers never do.")]
        [SerializeField] private LayerMask occlusionMask = ~0;

        [SerializeField, Min(0.02f)] private float occlusionCheckInterval = 0.12f;

        [Header("Shape")]
        [SerializeField] private float minFrameSize = 44f;
        [SerializeField] private float maxFrameSize = 640f;
        [SerializeField] private float framePadding = 12f;
        [SerializeField] private float cornerSize = 30f;

        [Tooltip("Extra room the frame opens up when the crosshair locks on, so the brackets never sit on the object.")]
        [SerializeField] private float focusPadding = 10f;

        [Tooltip("How far the corners overshoot outward when the crosshair locks on.")]
        [SerializeField] private float lockOnKick = 22f;

        [SerializeField] private float eyeHeight = 34f;

        private sealed class Widget
        {
            public InteractionMarker Marker;

            public RectTransform Root;
            public CanvasGroup Group;
            public readonly Image[] Corners = new Image[4];
            public Image Eye;
            public Image RecordingDot;
            public RectTransform Prompt;
            public CanvasGroup PromptGroup;
            public GameObject Key;
            public TMP_Text KeyText;
            public TMP_Text Label;

            public Vector2 Center;
            public Vector2 Size;
            public bool Placed;

            public float Alpha;
            public float Appear;
            public float Focus;
            public float Blocked;
            public float Recording;
            public bool WasFocused;
            public float LockTime;

            public bool Wanted;
            public bool Visible;
            public float OcclusionTimer;
            public float Phase;
        }

        private struct Candidate
        {
            public InteractionMarker Marker;
            public Bounds Bounds;
            public float Distance;
            public float Reach;
            public bool Focused;
        }

        private static readonly Vector2 CornerPivot = new(0f, 1f);

        private readonly Dictionary<InteractionMarker, Widget> _live = new();
        private readonly Stack<Widget> _pool = new();
        private readonly List<Candidate> _candidates = new();
        private readonly List<InteractionMarker> _released = new();
        private readonly HashSet<GameObject> _inFrame = new();
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Vector3[] _boundsCorners = new Vector3[8];

        private RectTransform _rect;
        private Canvas _canvas;
        private Camera _worldCamera;
        private Transform _playerRoot;
        private string _keyDisplay;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>().rootCanvas;

            if (interactor != null) _playerRoot = interactor.transform.root;

            if ((recordingSensors == null || recordingSensors.Length == 0) && _playerRoot != null)
                recordingSensors = _playerRoot.GetComponentsInChildren<VisionSensor>(true);

            // Subscribed for the whole lifetime, not per enable: the HUD is switched off while the
            // player is locked at a board, and missing an exit then would leave a prop stuck red.
            if (recordingSensors == null) return;

            foreach (VisionSensor sensor in recordingSensors)
            {
                if (sensor == null) continue;

                sensor.OnTargetEnter += MarkInFrame;
                sensor.OnTargetEnterStatic += MarkInFrame;
                sensor.OnTargetExit += MarkOutOfFrame;
                sensor.OnTargetExitStatic += MarkOutOfFrame;
            }
        }

        private void OnDestroy()
        {
            if (recordingSensors == null) return;

            foreach (VisionSensor sensor in recordingSensors)
            {
                if (sensor == null) continue;

                sensor.OnTargetEnter -= MarkInFrame;
                sensor.OnTargetEnterStatic -= MarkInFrame;
                sensor.OnTargetExit -= MarkOutOfFrame;
                sensor.OnTargetExitStatic -= MarkOutOfFrame;
            }
        }

        private void OnEnable() => Canvas.preWillRenderCanvases += Refresh;

        /// <summary>Back to a clean slate, so returning to the HUD fades markers in afresh.</summary>
        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= Refresh;

            _released.Clear();
            _released.AddRange(_live.Keys);

            foreach (InteractionMarker marker in _released) Release(marker);
        }

        private void MarkInFrame(GameObject target)
        {
            if (target != null) _inFrame.Add(target);
        }

        private void MarkOutOfFrame(GameObject target)
        {
            if (target != null) _inFrame.Remove(target);
        }

        private void Refresh()
        {
            float dt = Time.unscaledDeltaTime;

            if (interactor == null || !interactor.IsSpawned || !interactor.IsOwner || !ResolveCamera())
            {
                FadeEverythingOut(dt);
                return;
            }

            CollectCandidates();

            foreach (Widget widget in _live.Values) widget.Wanted = false;

            foreach (Candidate candidate in _candidates)
            {
                if (!_live.TryGetValue(candidate.Marker, out Widget widget))
                    widget = Acquire(candidate.Marker);

                widget.Wanted = true;
                UpdateWidget(widget, candidate, dt);
            }

            FadeUnwanted(dt);
        }

        private bool ResolveCamera()
        {
            if (_worldCamera == null || !_worldCamera.isActiveAndEnabled) _worldCamera = Camera.main;

            return _worldCamera != null;
        }

        private void CollectCandidates()
        {
            _candidates.Clear();

            Vector3 eye = _worldCamera.transform.position;

            foreach (InteractionMarker marker in InteractionMarker.Active)
            {
                bool usable = marker.IsInteractable;
                bool recordable = marker.IsRecordable;

                if (!usable && !recordable) continue;
                if (marker.transform.IsChildOf(_playerRoot)) continue;
                if (!marker.TryGetBounds(out Bounds bounds)) continue;

                float reach = marker.RevealDistance > 0f
                    ? marker.RevealDistance
                    : Mathf.Max(usable ? revealDistance : 0f, recordable ? recordableRevealDistance : 0f);

                float distance = Vector3.Distance(eye, bounds.ClosestPoint(eye));
                bool focused = IsFocused(marker);

                if (!focused)
                {
                    if (distance > reach) continue;

                    Vector3 viewport = _worldCamera.WorldToViewportPoint(bounds.center);
                    if (viewport.z <= 0f) continue;
                    if (viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f) continue;
                }

                _candidates.Add(new Candidate
                {
                    Marker = marker,
                    Bounds = bounds,
                    Distance = distance,
                    Reach = reach,
                    Focused = focused
                });
            }

            _candidates.Sort((a, b) => a.Focused != b.Focused
                ? (a.Focused ? -1 : 1)
                : a.Distance.CompareTo(b.Distance));

            if (_candidates.Count > maxMarkers)
                _candidates.RemoveRange(maxMarkers, _candidates.Count - maxMarkers);
        }

        /// <summary>
        /// By ownership rather than identity: a door answers the raycast through whichever leaf was
        /// hit, while its marker sits on the frame around both.
        /// </summary>
        private bool IsFocused(InteractionMarker marker)
        {
            return interactor.HoveredInteractable is Component hovered && marker.Owns(hovered);
        }

        private void UpdateWidget(Widget widget, Candidate candidate, float dt)
        {
            InteractionMarker marker = candidate.Marker;
            bool focused = candidate.Focused;

            // Whatever the crosshair rests on is visible by definition - the interactor's own
            // raycast just reached it - so it skips the occlusion test.
            widget.OcclusionTimer -= dt;
            if (focused)
            {
                widget.Visible = true;
            }
            else if (widget.OcclusionTimer <= 0f)
            {
                widget.OcclusionTimer = occlusionCheckInterval * Random.Range(0.8f, 1.2f);
                widget.Visible = HasLineOfSight(marker, candidate.Bounds);
            }

            bool onScreen = ProjectBounds(candidate.Bounds, out Vector2 center, out Vector2 size);

            float nearAlpha = Mathf.Lerp(1f, farAlpha, Mathf.InverseLerp(candidate.Reach * 0.35f, candidate.Reach, candidate.Distance));
            float targetAlpha = widget.Visible && onScreen ? (focused ? 1f : nearAlpha) : 0f;

            widget.Alpha = Damp(widget.Alpha, targetAlpha, 10f, dt);
            widget.Appear = Damp(widget.Appear, targetAlpha > 0f ? 1f : 0f, 7f, dt);
            widget.Focus = Damp(widget.Focus, focused ? 1f : 0f, 14f, dt);
            widget.Blocked = Damp(widget.Blocked, focused && interactor.IsHoveredBlocked ? 1f : 0f, 12f, dt);
            widget.Recording = Damp(widget.Recording, marker.IsRecordable && IsRecording(marker) ? 1f : 0f, 6f, dt);

            if (focused && !widget.WasFocused) OnLockOn(widget);
            widget.WasFocused = focused;
            widget.LockTime += dt;

            if (!onScreen) return;

            if (!widget.Placed)
            {
                widget.Center = center;
                widget.Size = size;
                widget.Placed = true;
            }
            else
            {
                // Quick enough to feel glued on, slow enough to iron out an animated mesh's
                // bounds breathing from frame to frame.
                float follow = 1f - Mathf.Exp(-28f * dt);
                widget.Center = Vector2.Lerp(widget.Center, center, follow);
                widget.Size = Vector2.Lerp(widget.Size, size, follow);
            }

            Draw(widget);
        }

        private void OnLockOn(Widget widget)
        {
            widget.LockTime = 0f;
            widget.Root.SetAsLastSibling();

            _keyDisplay = ReadKeyDisplay();

            widget.Key.SetActive(!string.IsNullOrEmpty(_keyDisplay));
            widget.KeyText.text = _keyDisplay;
        }

        private void Draw(Widget widget)
        {
            float time = Time.unscaledTime;
            float focus = widget.Focus;

            widget.Root.anchoredPosition = widget.Center;
            widget.Group.alpha = widget.Alpha;

            // Idle corners breathe a little so the scene never looks frozen behind them; a locked
            // one holds still after a damped kick outward, like a lens snapping to focus.
            float breathe = (1f - focus) * 2f * Mathf.Sin(time * 2.1f + widget.Phase);
            float kick = focus * lockOnKick * Mathf.Exp(-9f * widget.LockTime) * Mathf.Cos(17f * widget.LockTime);
            float arrive = (1f - widget.Appear) * 28f;

            float halfW = widget.Size.x * 0.5f + focus * focusPadding + breathe + kick + arrive;
            float halfH = widget.Size.y * 0.5f + focus * focusPadding + breathe + kick + arrive;

            float idleCorner = Mathf.Clamp(Mathf.Min(widget.Size.x, widget.Size.y) * 0.3f, 16f, cornerSize * 0.75f);
            float corner = Mathf.Lerp(idleCorner, cornerSize, focus);

            Color frameColor = Color.Lerp(idleColor, focusColor, focus);
            frameColor = Color.Lerp(frameColor, blockedColor, widget.Blocked);

            PlaceCorner(widget.Corners[0], -halfW, halfH, corner, frameColor);
            PlaceCorner(widget.Corners[1], halfW, halfH, corner, frameColor);
            PlaceCorner(widget.Corners[2], halfW, -halfH, corner, frameColor);
            PlaceCorner(widget.Corners[3], -halfW, -halfH, corner, frameColor);

            DrawEye(widget, halfH, time);
            DrawPrompt(widget, halfH);
        }

        private void DrawEye(Widget widget, float halfH, float time)
        {
            bool recordable = widget.Marker.IsRecordable;
            if (widget.Eye.gameObject.activeSelf != recordable) widget.Eye.gameObject.SetActive(recordable);
            if (!recordable) return;

            float recording = widget.Recording;
            float pulse = 1f + 0.12f * recording * Mathf.Sin(time * 7f);

            RectTransform eye = widget.Eye.rectTransform;
            eye.anchoredPosition = new Vector2(0f, halfH + 8f + Mathf.Sin(time * 1.6f + widget.Phase) * 1.5f);
            eye.localScale = new Vector3(pulse, pulse, 1f);
            widget.Eye.color = Color.Lerp(idleColor, recordingColor, recording);

            // Blinks with the REC light in the corner of the screen, so the two read as the same thing.
            float blink = Mathf.Sin(time * 5f) > -0.2f ? 1f : 0.15f;
            widget.RecordingDot.color = new Color(1f, 1f, 1f, recording * blink);
        }

        private void DrawPrompt(Widget widget, float halfH)
        {
            bool usable = widget.Marker.IsInteractable;
            float show = usable ? widget.Focus : 0f;

            if (widget.Prompt.gameObject.activeSelf != show > 0.01f)
                widget.Prompt.gameObject.SetActive(show > 0.01f);

            if (show <= 0.01f) return;

            bool blocked = widget.Blocked > 0.5f;
            string label = blocked ? BlockedLabel : widget.Marker.ActionLabel.ToUpperInvariant();

            if (widget.Label.text != label) widget.Label.text = label;
            widget.Label.gameObject.SetActive(!string.IsNullOrEmpty(label));
            widget.Key.SetActive(!blocked && !string.IsNullOrEmpty(_keyDisplay));

            widget.Label.color = Color.Lerp(focusColor, blockedColor, widget.Blocked);
            widget.PromptGroup.alpha = show;
            widget.Prompt.anchoredPosition = new Vector2(0f, -halfH - 12f - (1f - show) * 10f);
        }

        private static void PlaceCorner(Image image, float x, float y, float size, Color color)
        {
            RectTransform corner = image.rectTransform;
            corner.anchoredPosition = new Vector2(x, y);
            corner.sizeDelta = new Vector2(size, size);
            image.color = color;
        }

        private void FadeUnwanted(float dt)
        {
            _released.Clear();

            foreach (Widget widget in _live.Values)
            {
                if (widget.Wanted) continue;

                widget.Alpha = Damp(widget.Alpha, 0f, 12f, dt);
                widget.Focus = Damp(widget.Focus, 0f, 14f, dt);
                widget.Group.alpha = widget.Alpha;

                if (widget.Alpha < 0.01f || widget.Marker == null || !widget.Marker.isActiveAndEnabled)
                    _released.Add(widget.Marker);
            }

            foreach (InteractionMarker marker in _released) Release(marker);
        }

        private void FadeEverythingOut(float dt)
        {
            foreach (Widget widget in _live.Values) widget.Wanted = false;

            FadeUnwanted(dt);
        }

        private bool IsRecording(InteractionMarker marker)
        {
            if (_inFrame.Count == 0) return false;

            foreach (GameObject target in _inFrame)
            {
                if (marker.Represents(target)) return true;
            }

            return false;
        }

        /// <summary>
        /// Two samples, centre and near the top, so a prop half behind a crate still shows. The
        /// player's own body and the object itself never count as cover.
        /// </summary>
        private bool HasLineOfSight(InteractionMarker marker, Bounds bounds)
        {
            Vector3 origin = _worldCamera.transform.position;

            return IsClear(origin, bounds.center, marker)
                   || IsClear(origin, bounds.center + Vector3.up * bounds.extents.y * 0.8f, marker);
        }

        private bool IsClear(Vector3 origin, Vector3 target, InteractionMarker marker)
        {
            Vector3 toTarget = target - origin;
            float length = toTarget.magnitude;
            if (length < 0.01f) return true;

            int count = Physics.RaycastNonAlloc(origin, toTarget / length, _hits, length, occlusionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i].collider;

                if (marker.Owns(hit)) continue;
                if (_playerRoot != null && hit.transform.IsChildOf(_playerRoot)) continue;

                return false;
            }

            return true;
        }

        /// <summary>
        /// The object's box, flattened onto the HUD. Anything with a corner behind the camera
        /// collapses to its centre at the minimum size, which only happens for things right on
        /// top of the lens anyway.
        /// </summary>
        private bool ProjectBounds(Bounds bounds, out Vector2 center, out Vector2 size)
        {
            Vector3 min = bounds.min, max = bounds.max;
            _boundsCorners[0] = new Vector3(min.x, min.y, min.z);
            _boundsCorners[1] = new Vector3(max.x, min.y, min.z);
            _boundsCorners[2] = new Vector3(min.x, max.y, min.z);
            _boundsCorners[3] = new Vector3(max.x, max.y, min.z);
            _boundsCorners[4] = new Vector3(min.x, min.y, max.z);
            _boundsCorners[5] = new Vector3(max.x, min.y, max.z);
            _boundsCorners[6] = new Vector3(min.x, max.y, max.z);
            _boundsCorners[7] = new Vector3(max.x, max.y, max.z);

            Vector2 screenMin = new(float.MaxValue, float.MaxValue);
            Vector2 screenMax = new(float.MinValue, float.MinValue);
            bool behind = false;

            foreach (Vector3 corner in _boundsCorners)
            {
                Vector3 screen = _worldCamera.WorldToScreenPoint(corner);
                if (screen.z <= 0.05f)
                {
                    behind = true;
                    break;
                }

                screenMin = Vector2.Min(screenMin, screen);
                screenMax = Vector2.Max(screenMax, screen);
            }

            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            if (behind)
            {
                Vector3 screenCenter = _worldCamera.WorldToScreenPoint(bounds.center);
                size = Vector2.one * (minFrameSize + framePadding * 2f);

                if (screenCenter.z <= 0f)
                {
                    center = default;
                    return false;
                }

                return RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenCenter, uiCamera, out center);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenMin, uiCamera, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenMax, uiCamera, out Vector2 localMax);

            center = (localMin + localMax) * 0.5f;

            Vector2 raw = localMax - localMin;
            size = new Vector2(
                Mathf.Clamp(raw.x, minFrameSize, maxFrameSize) + framePadding * 2f,
                Mathf.Clamp(raw.y, minFrameSize, maxFrameSize) + framePadding * 2f);

            return true;
        }

        private string ReadKeyDisplay()
        {
            if (inputReader == null) return string.Empty;

            // A keyboard and a gamepad binding come back as "E | Button South"; the first one is
            // the one this HUD is laid out for.
            string display = inputReader.GetInteractBindingDisplay();
            int split = display.IndexOf('|');

            return (split >= 0 ? display.Substring(0, split) : display).Trim();
        }

        private static float Damp(float current, float target, float rate, float dt)
        {
            return Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * dt));
        }

        #region Pool

        private Widget Acquire(InteractionMarker marker)
        {
            Widget widget = _pool.Count > 0 ? _pool.Pop() : CreateWidget();

            widget.Marker = marker;
            widget.Placed = false;
            widget.Alpha = 0f;
            widget.Appear = 0f;
            widget.Focus = 0f;
            widget.Blocked = 0f;
            widget.Recording = 0f;
            widget.WasFocused = false;
            widget.LockTime = 10f;
            widget.Visible = false;
            widget.OcclusionTimer = 0f;
            widget.Phase = Random.Range(0f, Mathf.PI * 2f);

            widget.Group.alpha = 0f;
            widget.Prompt.gameObject.SetActive(false);
            widget.Root.gameObject.SetActive(true);

            _live.Add(marker, widget);
            return widget;
        }

        private void Release(InteractionMarker marker)
        {
            if (!_live.Remove(marker, out Widget widget)) return;

            widget.Marker = null;
            widget.Root.gameObject.SetActive(false);
            _pool.Push(widget);
        }

        private Widget CreateWidget()
        {
            Widget widget = new();

            widget.Root = NewRect("Marker", _rect);
            widget.Group = widget.Root.gameObject.AddComponent<CanvasGroup>();
            widget.Group.blocksRaycasts = false;
            widget.Group.interactable = false;

            // One sprite, turned four ways. Pivot on the outer corner so rotating it about that
            // point swings the arms inward for each quadrant.
            for (int i = 0; i < 4; i++)
            {
                Image corner = NewImage($"Corner{i}", widget.Root, cornerSprite);
                corner.rectTransform.pivot = CornerPivot;
                corner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f * i);
                AddShadow(corner.gameObject);
                widget.Corners[i] = corner;
            }

            widget.Eye = NewImage("Eye", widget.Root, eyeSprite);
            widget.Eye.preserveAspect = true;
            widget.Eye.rectTransform.pivot = new Vector2(0.5f, 0f);
            widget.Eye.rectTransform.sizeDelta = new Vector2(eyeHeight * AspectOf(eyeSprite), eyeHeight);
            AddShadow(widget.Eye.gameObject);

            widget.RecordingDot = NewImage("RecordingDot", widget.Eye.rectTransform, recordingDotSprite);
            RectTransform dot = widget.RecordingDot.rectTransform;
            dot.anchorMin = dot.anchorMax = new Vector2(1f, 1f);
            dot.sizeDelta = new Vector2(11f, 11f);
            dot.anchoredPosition = new Vector2(2f, -2f);

            BuildPrompt(widget);

            return widget;
        }

        private void BuildPrompt(Widget widget)
        {
            widget.Prompt = NewRect("Prompt", widget.Root);
            widget.Prompt.pivot = new Vector2(0.5f, 1f);
            widget.PromptGroup = widget.Prompt.gameObject.AddComponent<CanvasGroup>();

            Image plate = widget.Prompt.gameObject.AddComponent<Image>();
            plate.sprite = labelSprite;
            plate.type = Image.Type.Sliced;
            plate.pixelsPerUnitMultiplier = 3f;
            plate.raycastTarget = false;

            HorizontalLayoutGroup row = widget.Prompt.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(8, 14, 6, 6);
            row.spacing = 10f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;

            ContentSizeFitter fit = widget.Prompt.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image key = NewImage("Key", widget.Prompt, keySprite);
            key.type = Image.Type.Sliced;
            key.pixelsPerUnitMultiplier = 3f;
            widget.Key = key.gameObject;

            HorizontalLayoutGroup keyRow = key.gameObject.AddComponent<HorizontalLayoutGroup>();
            keyRow.padding = new RectOffset(9, 9, 1, 5);
            keyRow.childAlignment = TextAnchor.MiddleCenter;
            keyRow.childControlWidth = keyRow.childControlHeight = true;
            keyRow.childForceExpandWidth = keyRow.childForceExpandHeight = false;

            LayoutElement keySize = key.gameObject.AddComponent<LayoutElement>();
            keySize.minWidth = 34f;
            keySize.minHeight = 34f;

            widget.KeyText = NewText("KeyText", key.rectTransform, 22f, keyTextColor);
            widget.Label = NewText("Label", widget.Prompt, 24f, focusColor);
            widget.Label.characterSpacing = 4f;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject go = new(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;

            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            return rect;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Keeps the thin grungy strokes readable against a bright sky or a lit wall.</summary>
        private static void AddShadow(GameObject target)
        {
            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        private static float AspectOf(Sprite sprite)
        {
            if (sprite == null || sprite.rect.height <= 0f) return 1f;

            return sprite.rect.width / sprite.rect.height;
        }

        #endregion
    }
}
