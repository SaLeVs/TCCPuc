using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace Ui.EndScreen
{
    /// <summary>
    /// Makes the end-scene camera behave like a security camera on a motorised mount: it sweeps
    /// slowly from side to side on its own, follows the mouse when the viewer takes over, zooms
    /// with the scroll wheel and cuts between the scene's cameras (arrows / A-D / number keys),
    /// with a burst of static and a glitch on the image at every cut.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SecurityCamera : MonoBehaviour
    {
        [Serializable]
        public struct View
        {
            public string label;
            [Tooltip("Empty: where the camera is placed in the scene.")]
            public Transform point;
            [Tooltip("0: the camera's own field of view.")]
            public float fieldOfView;
            [Tooltip("Half the side-to-side sweep, in degrees.")]
            public float sweep;
        }

        [SerializeField] private View[] views = { new View { label = "CAM 01", sweep = 8f } };

        [Header("Mount")]
        [Tooltip("Degrees per second of the motor, sweeping on its own.")]
        [SerializeField] private float sweepSpeed = 3.5f;
        [SerializeField] private Vector2 sweepPause = new Vector2(1.5f, 3.5f);
        [Tooltip("How far the mouse can turn it, left-right and up-down, as a fraction of the field of view.")]
        [SerializeField] private Vector2 mouseReach = new Vector2(0.3f, 0.18f);
        [Tooltip("Seconds to reach where the mouse points: the motor lags behind.")]
        [SerializeField] private float mouseLag = 0.45f;
        [Tooltip("Seconds without moving the mouse before it goes back to sweeping.")]
        [SerializeField] private float handBackAfter = 4f;
        [SerializeField] private float shake = 0.12f;

        [Header("Zoom")]
        [SerializeField] private float maxZoom = 2.4f;
        [SerializeField] private float zoomStep = 0.15f;
        [SerializeField] private float zoomLag = 0.25f;

        [Header("Image")]
        [SerializeField] private SecurityCameraOverlay overlay;
        [Tooltip("Optional: chromatic aberration and lens distortion in it get a kick at every cut.")]
        [SerializeField] private Volume glitchVolume;
        [SerializeField] private Vector2 randomGlitchEvery = new Vector2(6f, 14f);
        [Tooltip("Added to the volume's post exposure in this scene: a dark set needs more.")]
        [SerializeField] private float exposureBoost;

        private Camera _camera;
        private readonly List<(Vector3 position, Quaternion rotation, float fov)> _poses = new();
        private int _current;

        private Vector2 _offset;
        private Vector2 _offsetVelocity;
        private float _sweepTarget;
        private float _pauseUntil;
        private float _lastMouseTime = -999f;
        private Vector2 _lastMousePosition;

        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private float _zoomVelocity;

        private float _glitch;
        private float _nextRandomGlitch;
        private ChromaticAberration _aberration;
        private LensDistortion _distortion;
        private float _baseAberration;
        private float _baseDistortion;

        public int ViewCount => views.Length;
        public int CurrentView => _current;
        public string CurrentLabel => views.Length > 0 ? views[_current].label : "";

        public event Action<int> OnViewChanged;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            Vector3 ownPosition = transform.position;
            Quaternion ownRotation = transform.rotation;
            float ownFov = _camera.fieldOfView;

            foreach (View view in views)
            {
                Vector3 position = view.point != null ? view.point.position : ownPosition;
                Quaternion rotation = view.point != null ? view.point.rotation : ownRotation;
                float fov = view.fieldOfView > 0f ? view.fieldOfView : ownFov;
                _poses.Add((position, rotation, fov));
            }

            if (glitchVolume != null && glitchVolume.profile != null)
            {
                // .profile is a copy for this run: the asset is left alone.
                if (glitchVolume.profile.TryGet(out _aberration)) _baseAberration = _aberration.intensity.value;
                if (glitchVolume.profile.TryGet(out _distortion)) _baseDistortion = _distortion.intensity.value;
                if (exposureBoost != 0f && glitchVolume.profile.TryGet(out ColorAdjustments color))
                {
                    color.postExposure.Override(color.postExposure.value + exposureBoost);
                }
            }

            _sweepTarget = views.Length > 0 ? views[0].sweep : 0f;
            _nextRandomGlitch = Time.time + Random.Range(randomGlitchEvery.x, randomGlitchEvery.y);
        }

        private void Start()
        {
            if (overlay != null) overlay.Bind(this);

            // Wherever the mouse already is does not count as the viewer taking over.
            if (Mouse.current != null) _lastMousePosition = Mouse.current.position.ReadValue();

            Apply(0f);
            OnViewChanged?.Invoke(_current);
        }

        public void Next() => SwitchTo((_current + 1) % views.Length);

        public void Previous() => SwitchTo((_current - 1 + views.Length) % views.Length);

        public void SwitchTo(int index)
        {
            if (views.Length == 0 || index == _current) return;

            _current = Mathf.Clamp(index, 0, views.Length - 1);
            _offset = Vector2.zero;
            _offsetVelocity = Vector2.zero;
            _zoom = _zoomTarget = 1f;
            _sweepTarget = views[_current].sweep * (Random.value < 0.5f ? -1f : 1f);
            _pauseUntil = Time.time + 1f;

            Glitch(1f);
            if (overlay != null) overlay.Static(0.35f);
            OnViewChanged?.Invoke(_current);
        }

        public void Glitch(float amount) => _glitch = Mathf.Max(_glitch, amount);

        private void Update()
        {
            if (views.Length == 0) return;

            ReadInput();
            Steer();

            _zoom = Mathf.SmoothDamp(_zoom, _zoomTarget, ref _zoomVelocity, zoomLag);

            if (Time.time >= _nextRandomGlitch)
            {
                Glitch(Random.Range(0.25f, 0.55f));
                if (overlay != null) overlay.Static(0.08f);
                _nextRandomGlitch = Time.time + Random.Range(randomGlitchEvery.x, randomGlitchEvery.y);
            }

            _glitch = Mathf.MoveTowards(_glitch, 0f, Time.deltaTime * 2.5f);
            ApplyGlitch();
        }

        private void LateUpdate() => Apply(Time.time);

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) Next();
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) Previous();

                for (int i = 0; i < Mathf.Min(views.Length, 9); i++)
                {
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame) SwitchTo(i);
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            if ((position - _lastMousePosition).sqrMagnitude > 4f)
            {
                _lastMouseTime = Time.time;
                _lastMousePosition = position;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _zoomTarget = Mathf.Clamp(_zoomTarget + Mathf.Sign(scroll) * zoomStep * _zoomTarget, 1f, maxZoom);
            }
        }

        private void Steer()
        {
            Vector2 target;
            float lag = mouseLag;

            bool viewerHasIt = Time.time - _lastMouseTime < handBackAfter && Application.isFocused;
            if (viewerHasIt)
            {
                Vector2 mouse = _lastMousePosition;
                Vector2 normalized = new Vector2(mouse.x / Mathf.Max(1, Screen.width), mouse.y / Mathf.Max(1, Screen.height)) * 2f - Vector2.one;
                normalized = Vector2.ClampMagnitude(normalized, 1.2f);
                // Zoomed in, the same mouse move turns it less, like a real operator would.
                float fov = _poses[_current].fov / _zoom;
                target = new Vector2(normalized.x * mouseReach.x, -normalized.y * mouseReach.y) * fov;
                _pauseUntil = Time.time + 1.5f;
            }
            else
            {
                float sweep = views[_current].sweep;
                if (Time.time >= _pauseUntil && Mathf.Abs(_offset.x - _sweepTarget) < 0.2f)
                {
                    _sweepTarget = _sweepTarget > 0f ? -sweep : sweep;
                    _pauseUntil = Time.time + Random.Range(sweepPause.x, sweepPause.y);
                }

                float x = Time.time < _pauseUntil ? _offset.x : Mathf.MoveTowards(_offset.x, _sweepTarget, sweepSpeed * Time.deltaTime);
                target = new Vector2(x, Mathf.MoveTowards(_offset.y, 0f, sweepSpeed * 0.5f * Time.deltaTime));
                lag = 0.15f;
            }

            _offset = Vector2.SmoothDamp(_offset, target, ref _offsetVelocity, lag);
        }

        private void Apply(float time)
        {
            if (_poses.Count == 0) return;

            var pose = _poses[_current];
            float jitterX = (Mathf.PerlinNoise(time * 0.7f, 0.3f) - 0.5f) * 2f * shake;
            float jitterY = (Mathf.PerlinNoise(0.8f, time * 0.6f) - 0.5f) * 2f * shake;

            // Pan around the world up axis, tilt around the camera's own right: a real mount.
            Quaternion pan = Quaternion.AngleAxis(_offset.x + jitterX, Vector3.up);
            Quaternion rotation = pan * pose.rotation * Quaternion.AngleAxis(_offset.y + jitterY, Vector3.right);

            transform.SetPositionAndRotation(pose.position, rotation);
            _camera.fieldOfView = pose.fov / _zoom;
        }

        private void ApplyGlitch()
        {
            float kick = _glitch * _glitch;
            if (_aberration != null) _aberration.intensity.Override(Mathf.Clamp01(_baseAberration + kick * 0.8f));
            if (_distortion != null) _distortion.intensity.Override(Mathf.Clamp(_baseDistortion - kick * 0.35f, -1f, 1f));
        }

        public float Zoom => _zoom;
    }
}
