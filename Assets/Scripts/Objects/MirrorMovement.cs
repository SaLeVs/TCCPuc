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

    private Camera _camera;

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
