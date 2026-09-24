using UnityEngine;

namespace Components
{
    /// <summary>
    /// Lets the mirror's camera render only while the local player could actually see the mirror,
    /// and then at a capped rate.
    /// </summary>
    /// <remarks>
    /// The camera writes a render texture, so Unity drew the whole level through it every frame -
    /// from anywhere on the map, mirror in view or not. That was a second full pass over the scene:
    /// it doubled the draw calls in the room the players start in and paid for its own shadow maps
    /// on top. The texture keeps its last image while the camera rests, so a mirror just outside
    /// the view shows nothing stale when it comes back in.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public class MirrorCamera : MonoBehaviour
    {
        [Tooltip("The surface showing the mirror image. Leave empty to use the renderer this camera sits under.")]
        [SerializeField] private Renderer mirrorSurface;

        [Tooltip("Beyond this many metres from the mirror the image is too small to be worth refreshing.")]
        [SerializeField, Min(1f)] private float maxViewDistance = 15f;

        [Tooltip("Most refreshes per second. The reflection reads fine well below the game's frame rate.")]
        [SerializeField, Range(1, 60)] private int refreshRate = 30;

        private readonly Plane[] _frustum = new Plane[6];

        private Camera _camera;
        private Camera _viewer;
        private float _nextRenderTime;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (mirrorSurface == null) mirrorSurface = GetComponentInParent<Renderer>();

            _camera.enabled = false;
        }

        /// <summary>LateUpdate so the viewer has already moved when the decision is made for this frame.</summary>
        private void LateUpdate()
        {
            bool render = Time.unscaledTime >= _nextRenderTime && IsSeen();

            if (render) _nextRenderTime = Time.unscaledTime + 1f / refreshRate;

            if (_camera.enabled != render) _camera.enabled = render;
        }

        private bool IsSeen()
        {
            if (mirrorSurface == null || !mirrorSurface.enabled) return false;

            if (_viewer == null || !_viewer.isActiveAndEnabled) _viewer = Camera.main;
            if (_viewer == null) return false;

            Bounds bounds = mirrorSurface.bounds;
            Vector3 eye = _viewer.transform.position;

            if ((bounds.ClosestPoint(eye) - eye).sqrMagnitude > maxViewDistance * maxViewDistance) return false;

            GeometryUtility.CalculateFrustumPlanes(_viewer, _frustum);
            return GeometryUtility.TestPlanesAABB(_frustum, bounds);
        }
    }
}
