using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Moves the mirror's camera to the reflection of whoever is looking at the mirror on this machine,
/// and frames its projection on the mirror's edges.
/// </summary>
/// <remarks>
/// The viewer is <see cref="Camera.main"/>: the Cinemachine brain puts it on the local player's eyes
/// (or on the player being spectated), so every client - late joiners included - sees its own
/// reflection without anyone being registered. The other players show up because they are rendered.
/// The camera is placed right before it renders, after the brain has moved the view in LateUpdate,
/// so the reflection never trails a frame behind the player.
/// </remarks>
[RequireComponent(typeof(Camera))]
public class MirrorMovement : MonoBehaviour
{
    [Tooltip("The mirror. Its local Z is the mirror's normal; the reflected side is the one at negative Z.")]
    [SerializeField] private Transform mirror;

    [Tooltip("The surface showing the render texture. Its bounds frame the camera's view.")]
    [SerializeField] private Renderer mirrorSurface;

    [Tooltip("Keeps the near plane from collapsing when the viewer presses against the glass.")]
    [SerializeField, Min(0.001f)] private float minNearClip = 0.01f;

    [Header("Performance")]
    [Tooltip("Beyond this many metres from the mirror the camera stays off.")]
    [SerializeField, Min(1f)] private float maxViewDistance = 15f;

    [Tooltip("Most refreshes per second while the mirror is seen. 0 = every frame; otherwise never below 60.")]
    [SerializeField, Min(0)] private int maxRefreshRate;

    [Tooltip("What hides the mirror from the viewer, such as walls and closed doors. Players, props and items are left out so they don't switch the reflection off.")]
    [SerializeField] private LayerMask occluders = ~(
        (1 << 1) | (1 << 2) | (1 << 5) | (1 << 6) | (1 << 8) |      // TransparentFX, Ignore Raycast, UI, Body, Interactable
        (1 << 9) | (1 << 11) | (1 << 12) | (1 << 14) | (1 << 19));  // Monster, Player, DeadPlayer, Recordable, Props

    private const int MinRefreshRate = 60;

    /// <summary>Pulls the sight-line ends off the glass toward the viewer, so the mirror's own collider and its wall don't count.</summary>
    private const float SampleLift = 0.05f;

    /// <summary>The view was decided before the brain moved it this frame: a margin keeps the mirror from popping in late at the screen edge.</summary>
    private const float FrustumMargin = 0.5f;

    private readonly Plane[] _frustum = new Plane[6];

    private Camera _camera;
    private float _nextRenderTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (mirror == null) mirror = transform.parent;
        if (mirrorSurface == null && mirror != null) mirrorSurface = mirror.GetComponentInChildren<Renderer>();
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (_camera != null) _camera.ResetProjectionMatrix();
    }

    private void OnValidate()
    {
        if (maxRefreshRate > 0 && maxRefreshRate < MinRefreshRate) maxRefreshRate = MinRefreshRate;
    }

    /// <summary>
    /// The camera only runs while this machine's viewer can see the mirror; otherwise the texture
    /// keeps its last image and the scene isn't drawn a second time.
    /// </summary>
    private void LateUpdate()
    {
        bool render = IsSeen() && IsRefreshDue();
        if (_camera.enabled != render) _camera.enabled = render;
    }

    private bool IsRefreshDue()
    {
        if (maxRefreshRate <= 0) return true;

        float now = Time.unscaledTime;

        // Half a frame of slack: at exactly the cap, frame-time jitter would otherwise skip every other frame.
        if (now + Time.unscaledDeltaTime * 0.5f < _nextRenderTime) return false;

        _nextRenderTime = Mathf.Max(_nextRenderTime + 1f / Mathf.Max(maxRefreshRate, MinRefreshRate), now);
        return true;
    }

    private bool IsSeen()
    {
        if (mirror == null || mirrorSurface == null || !mirrorSurface.enabled) return false;

        Camera viewer = Camera.main;
        if (viewer == null || viewer == _camera) return false;

        Vector3 eye = viewer.transform.position;

        // From behind the glass there is nothing to reflect.
        if (mirror.InverseTransformPoint(eye).z >= 0f) return false;

        Bounds bounds = mirrorSurface.bounds;
        if ((bounds.ClosestPoint(eye) - eye).sqrMagnitude > maxViewDistance * maxViewDistance) return false;

        bounds.Expand(FrustumMargin);
        GeometryUtility.CalculateFrustumPlanes(viewer, _frustum);
        if (!GeometryUtility.TestPlanesAABB(_frustum, bounds)) return false;

        return HasLineOfSight(eye);
    }

    /// <summary>Seen when the eye reaches the centre or any corner of the glass without a wall in between.</summary>
    private bool HasLineOfSight(Vector3 eye)
    {
        Bounds local = mirrorSurface.localBounds;
        Transform surface = mirrorSurface.transform;
        Vector3 centre = surface.TransformPoint(local.center);

        if (IsClear(eye, centre)) return true;

        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = surface.TransformPoint(new Vector3(
                (i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y,
                local.center.z));

            // A little inside the edge, so a frame around the glass doesn't hide it.
            if (IsClear(eye, Vector3.Lerp(centre, corner, 0.8f))) return true;
        }

        return false;
    }

    private bool IsClear(Vector3 eye, Vector3 point)
    {
        Vector3 toEye = eye - point;
        float distance = toEye.magnitude;
        if (distance <= SampleLift) return true;

        return !Physics.Linecast(eye, point + toEye * (SampleLift / distance), occluders, QueryTriggerInteraction.Ignore);
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != _camera) return;

        Camera viewer = Camera.main;
        if (viewer == null || viewer == _camera || mirror == null || mirrorSurface == null) return;

        Vector3 localViewer = mirror.InverseTransformPoint(viewer.transform.position);

        // From behind the glass there is nothing to reflect: keep the last image.
        if (localViewer.z >= 0f) return;

        // Mirror the eye across the glass and face the viewer's side, square to the mirror.
        transform.SetPositionAndRotation(
            mirror.TransformPoint(new Vector3(localViewer.x, localViewer.y, -localViewer.z)),
            Quaternion.LookRotation(-mirror.forward, mirror.up));

        FrameMirror();
    }

    /// <summary>
    /// Off-axis projection whose near plane is the glass and whose edges are the mirror's edges.
    /// The texture then lines up with the surface from any angle, and the wall behind the mirror
    /// (between this camera and the glass) is clipped away.
    /// </summary>
    private void FrameMirror()
    {
        Bounds bounds = mirrorSurface.localBounds;
        Transform surface = mirrorSurface.transform;

        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        float depth = 0f;

        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = new(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                bounds.center.z);

            Vector3 local = transform.InverseTransformPoint(surface.TransformPoint(corner));
            left = Mathf.Min(left, local.x);
            right = Mathf.Max(right, local.x);
            bottom = Mathf.Min(bottom, local.y);
            top = Mathf.Max(top, local.y);
            depth += local.z * 0.25f;
        }

        float near = Mathf.Max(depth, minNearClip);
        // Pressed against the glass the corners sit closer than the near plane: scale them out to it.
        float scale = near / Mathf.Max(depth, 0.0001f);
        float far = Mathf.Max(_camera.farClipPlane, near + 0.01f);

        _camera.projectionMatrix = Matrix4x4.Frustum(left * scale, right * scale, bottom * scale, top * scale, near, far);
    }
}
