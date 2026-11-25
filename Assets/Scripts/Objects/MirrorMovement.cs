using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One camera that reflects a whole wall of mirrors: it sits at the reflection of whoever is looking on
/// this machine and renders only the part of the glass that is on their screen.
/// </summary>
/// <remarks>
/// <para>The viewer is <see cref="Camera.main"/>: the Cinemachine brain puts it on the local player's eyes
/// (or on the player being spectated), so every client - late joiners included - sees its own
/// reflection. The other players show up because they are rendered.</para>
/// <para>Every material slot of <see cref="surfaces"/> that uses the "TCC/Mirror Reflection" shader is glass.
/// The shader looks the image up by world position, so a whole row of frames costs one render, and
/// the projection can be cropped to what is on screen without the glass noticing.</para>
/// <para>The camera only runs while the glass is in front of the viewer, within reach, on screen and not
/// behind a wall; otherwise the texture keeps its last image.</para>
/// </remarks>
[RequireComponent(typeof(Camera))]
public class MirrorMovement : MonoBehaviour
{
    [Tooltip("The mirror's plane. Its local Z points into the wall; the viewer stands on the negative side.")]
    [SerializeField] private Transform mirror;

    [Tooltip("Renderers holding the glass. Only their slots with the TCC/Mirror Reflection shader are used. All of them must lie on the mirror's plane.")]
    [SerializeField] private Renderer[] surfaces;

    [Header("Image")]
    [Tooltip("Size of the reflection texture as a fraction of the screen.")]
    [SerializeField, Range(0.25f, 1f)] private float resolutionScale = 0.75f;

    [Tooltip("Keeps the near plane from collapsing when the viewer presses against the glass.")]
    [SerializeField, Min(0.001f)] private float minNearClip = 0.01f;

    [Header("Performance")]
    [Tooltip("Beyond this many metres from the glass the camera stays off.")]
    [SerializeField, Min(1f)] private float maxViewDistance = 15f;

    [Tooltip("Most refreshes per second while the mirror is seen. 0 = every frame; otherwise never below 60.")]
    [SerializeField, Min(0)] private int maxRefreshRate;

    [Tooltip("What hides the mirror from the viewer, such as walls and closed doors. Players, props and items are left out so they don't switch the reflection off.")]
    [SerializeField] private LayerMask occluders = ~(
        (1 << 1) | (1 << 2) | (1 << 5) | (1 << 6) | (1 << 8) |      // TransparentFX, Ignore Raycast, UI, Body, Interactable
        (1 << 9) | (1 << 11) | (1 << 12) | (1 << 14) | (1 << 19));  // Monster, Player, DeadPlayer, Recordable, Props

    private const int MinRefreshRate = 60;

    /// <summary>
    /// A hit this close to the glass's plane is what the mirror hangs on - its own collider, the wall,
    /// the wall collider that boxes in the dressing table - not something hiding it.
    /// </summary>
    private const float MountDepth = 0.4f;

    /// <summary>Visibility is decided before the brain moves the view this frame: a margin keeps the mirror from switching on late at the screen edge.</summary>
    private const float VisibilityMargin = 0.5f;

    /// <summary>Extra glass around the cropped area, so bilinear filtering never reads past the image.</summary>
    private const float CropMargin = 0.05f;

    private static readonly int MirrorTexId = Shader.PropertyToID("_MirrorTex");
    private static readonly int MirrorVpId = Shader.PropertyToID("_MirrorVP");

    private struct GlassSlot
    {
        public Renderer Renderer;
        public int MaterialIndex;
    }

    private readonly List<GlassSlot> _slots = new();
    private readonly List<Vector3> _sightPoints = new();
    private readonly Plane[] _frustum = new Plane[6];
    private readonly List<Vector3> _polygon = new();
    private readonly List<Vector3> _clipped = new();
    private readonly Vector3[] _corners = new Vector3[4];

    private Camera _camera;
    private MaterialPropertyBlock _block;
    private RenderTexture _texture;

    /// <summary>The glass on the mirror's plane, in the mirror's local X/Y.</summary>
    private Rect _glass;
    private float _planeZ;
    private float _nextRenderTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        _block = new MaterialPropertyBlock();

        if (mirror == null) mirror = transform.parent;

        // Behind the glass, so its culling from there would hide the very room it reflects.
        _camera.useOcclusionCulling = false;
        // Before the player's camera, so the glass shows this frame's image.
        _camera.depth = -50f;
        _camera.enabled = false;
        // The world only: name tags, interaction markers and other world-space UI face the
        // player's camera and have no place in a reflection.
        _camera.cullingMask &= ~LayerMask.GetMask("UI", "HideForMirror");

        CollectGlass();
        if (_slots.Count == 0)
        {
            Debug.LogWarning($"{name}: no surface uses the TCC/Mirror Reflection shader; the mirror stays off.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        if (_camera != null)
        {
            _camera.enabled = false;
            _camera.ResetProjectionMatrix();
        }
    }

    private void OnDestroy()
    {
        if (_texture == null) return;

        if (_camera != null) _camera.targetTexture = null;
        _texture.Release();
        Destroy(_texture);
    }

    private void OnValidate()
    {
        if (maxRefreshRate > 0 && maxRefreshRate < MinRefreshRate) maxRefreshRate = MinRefreshRate;
    }

    private void CollectGlass()
    {
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        float sumZ = 0f;
        int cornerCount = 0;

        if (surfaces == null || mirror == null) return;

        foreach (Renderer surface in surfaces)
        {
            if (surface == null) continue;

            Mesh mesh = surface switch
            {
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                _ => surface.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null
            };

            Material[] materials = surface.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null || !materials[i].HasProperty(MirrorTexId)) continue;

                _slots.Add(new GlassSlot { Renderer = surface, MaterialIndex = i });

                Bounds bounds = mesh != null && i < mesh.subMeshCount ? mesh.GetSubMesh(i).bounds : surface.localBounds;
                Vector3 centre = mirror.InverseTransformPoint(surface.transform.TransformPoint(bounds.center));
                _sightPoints.Add(centre);

                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 local = mirror.InverseTransformPoint(surface.transform.TransformPoint(corner));

                    minX = Mathf.Min(minX, local.x);
                    maxX = Mathf.Max(maxX, local.x);
                    minY = Mathf.Min(minY, local.y);
                    maxY = Mathf.Max(maxY, local.y);
                    sumZ += local.z;
                    cornerCount++;
                }
            }
        }

        if (cornerCount == 0) return;

        _glass = Rect.MinMaxRect(minX, minY, maxX, maxY);
        _planeZ = sumZ / cornerCount;

        for (int i = 0; i < _sightPoints.Count; i++)
        {
            Vector3 point = _sightPoints[i];
            point.z = _planeZ;
            _sightPoints[i] = point;
        }

        // The corners of the whole wall too, a little inside so a frame doesn't hide them.
        Vector2 middle = _glass.center;
        for (int i = 0; i < 4; i++)
        {
            Vector2 corner = new((i & 1) == 0 ? _glass.xMin : _glass.xMax, (i & 2) == 0 ? _glass.yMin : _glass.yMax);
            Vector2 inset = Vector2.Lerp(middle, corner, 0.9f);
            _sightPoints.Add(new Vector3(inset.x, inset.y, _planeZ));
        }
    }

    private void LateUpdate()
    {
        bool render = IsSeen() && IsRefreshDue();
        if (render) EnsureTexture();
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

    /// <summary>One texture the size of a fraction of the screen, remade only when the screen changes size.</summary>
    private void EnsureTexture()
    {
        int width = Mathf.Max(64, Mathf.RoundToInt(Screen.width * resolutionScale));
        int height = Mathf.Max(64, Mathf.RoundToInt(Screen.height * resolutionScale));

        if (_texture != null && _texture.width == width && _texture.height == height) return;

        if (_texture != null)
        {
            _camera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
        }

        // HDR, like the player's camera, so the glass gets tone-mapped once, by the player's post-processing.
        _texture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
        {
            name = $"{name} Reflection",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1
        };
        _texture.Create();
        _camera.targetTexture = _texture;
    }

    private bool IsSeen()
    {
        Camera viewer = Camera.main;
        if (viewer == null || viewer == _camera || _slots.Count == 0) return false;

        Vector3 eye = viewer.transform.position;
        Vector3 localEye = mirror.InverseTransformPoint(eye);

        // From behind the glass there is nothing to reflect.
        if (localEye.z >= _planeZ) return false;

        Vector3 closest = mirror.TransformPoint(new Vector3(
            Mathf.Clamp(localEye.x, _glass.xMin, _glass.xMax),
            Mathf.Clamp(localEye.y, _glass.yMin, _glass.yMax),
            _planeZ));
        if ((closest - eye).sqrMagnitude > maxViewDistance * maxViewDistance) return false;

        if (!TryGetVisibleGlass(viewer, VisibilityMargin, out _)) return false;

        return HasLineOfSight(eye);
    }

    /// <summary>The part of the glass inside the viewer's frustum, as a rectangle on the mirror's plane.</summary>
    private bool TryGetVisibleGlass(Camera viewer, float margin, out Rect visible)
    {
        visible = default;

        _polygon.Clear();
        _polygon.Add(mirror.TransformPoint(new Vector3(_glass.xMin, _glass.yMin, _planeZ)));
        _polygon.Add(mirror.TransformPoint(new Vector3(_glass.xMax, _glass.yMin, _planeZ)));
        _polygon.Add(mirror.TransformPoint(new Vector3(_glass.xMax, _glass.yMax, _planeZ)));
        _polygon.Add(mirror.TransformPoint(new Vector3(_glass.xMin, _glass.yMax, _planeZ)));

        GeometryUtility.CalculateFrustumPlanes(viewer, _frustum);
        for (int i = 0; i < _frustum.Length && _polygon.Count > 0; i++)
        {
            Plane plane = _frustum[i];
            plane.distance += margin;
            ClipPolygon(plane);
        }

        if (_polygon.Count == 0) return false;

        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 point in _polygon)
        {
            Vector3 local = mirror.InverseTransformPoint(point);
            minX = Mathf.Min(minX, local.x);
            maxX = Mathf.Max(maxX, local.x);
            minY = Mathf.Min(minY, local.y);
            maxY = Mathf.Max(maxY, local.y);
        }

        visible = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return visible.width > 0.0001f && visible.height > 0.0001f;
    }

    /// <summary>Sutherland-Hodgman against one plane, keeping its positive side.</summary>
    private void ClipPolygon(Plane plane)
    {
        _clipped.Clear();

        for (int i = 0; i < _polygon.Count; i++)
        {
            Vector3 current = _polygon[i];
            Vector3 next = _polygon[(i + 1) % _polygon.Count];
            float currentDistance = plane.GetDistanceToPoint(current);
            float nextDistance = plane.GetDistanceToPoint(next);

            if (currentDistance >= 0f) _clipped.Add(current);
            if ((currentDistance >= 0f) != (nextDistance >= 0f))
                _clipped.Add(Vector3.Lerp(current, next, currentDistance / (currentDistance - nextDistance)));
        }

        _polygon.Clear();
        _polygon.AddRange(_clipped);
    }

    /// <summary>Seen when the eye reaches the middle of any frame, or a corner of the wall of glass, without a wall in between.</summary>
    private bool HasLineOfSight(Vector3 eye)
    {
        foreach (Vector3 local in _sightPoints)
        {
            if (IsClear(eye, mirror.TransformPoint(local))) return true;
        }

        return false;
    }

    private bool IsClear(Vector3 eye, Vector3 point)
    {
        if (!Physics.Linecast(eye, point, out RaycastHit hit, occluders, QueryTriggerInteraction.Ignore)) return true;

        // The first hit is the nearest to the eye: if even that one is on the mirror's wall, nothing hides the glass.
        return Mathf.Abs(mirror.InverseTransformPoint(hit.point).z - _planeZ) <= MountDepth;
    }

    /// <summary>
    /// Placed right before rendering, after the brain moved the view in LateUpdate, so the reflection
    /// never trails a frame behind the player.
    /// </summary>
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera rendering)
    {
        if (rendering != _camera) return;

        Camera viewer = Camera.main;
        if (viewer == null || viewer == _camera) return;

        Vector3 localEye = mirror.InverseTransformPoint(viewer.transform.position);
        if (localEye.z >= _planeZ) return;

        // Mirror the eye across the glass and face the viewer's side, square to the mirror.
        transform.SetPositionAndRotation(
            mirror.TransformPoint(new Vector3(localEye.x, localEye.y, 2f * _planeZ - localEye.z)),
            Quaternion.LookRotation(-mirror.forward, mirror.up));

        // Only the glass that is on screen: the texture's pixels all land where the player looks.
        Rect crop = TryGetVisibleGlass(viewer, 0f, out Rect visible)
            ? Rect.MinMaxRect(
                Mathf.Max(_glass.xMin, visible.xMin - CropMargin), Mathf.Max(_glass.yMin, visible.yMin - CropMargin),
                Mathf.Min(_glass.xMax, visible.xMax + CropMargin), Mathf.Min(_glass.yMax, visible.yMax + CropMargin))
            : _glass;

        FrameGlass(crop);

        Matrix4x4 viewProjection = _camera.projectionMatrix * _camera.worldToCameraMatrix;
        foreach (GlassSlot slot in _slots)
        {
            if (slot.Renderer == null) continue;

            slot.Renderer.GetPropertyBlock(_block, slot.MaterialIndex);
            _block.SetTexture(MirrorTexId, _texture);
            _block.SetMatrix(MirrorVpId, viewProjection);
            slot.Renderer.SetPropertyBlock(_block, slot.MaterialIndex);
        }
    }

    /// <summary>
    /// Off-axis projection whose near plane is the glass and whose edges are <paramref name="area"/>.
    /// The wall behind the mirror, between this camera and the glass, is clipped away.
    /// </summary>
    private void FrameGlass(Rect area)
    {
        _corners[0] = new Vector3(area.xMin, area.yMin, _planeZ);
        _corners[1] = new Vector3(area.xMax, area.yMin, _planeZ);
        _corners[2] = new Vector3(area.xMin, area.yMax, _planeZ);
        _corners[3] = new Vector3(area.xMax, area.yMax, _planeZ);

        float left = float.MaxValue, right = float.MinValue, bottom = float.MaxValue, top = float.MinValue;
        float depth = 0f;

        foreach (Vector3 corner in _corners)
        {
            Vector3 local = transform.InverseTransformPoint(mirror.TransformPoint(corner));
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
