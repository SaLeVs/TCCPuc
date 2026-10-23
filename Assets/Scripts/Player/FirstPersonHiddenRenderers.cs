using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

namespace Player
{
    /// <summary>
    /// Keeps the head out of the view of whoever is looking through it, without taking it out of
    /// the world: for that camera the renderers only cast shadows, every other camera draws them.
    /// </summary>
    /// <remarks>
    /// <para>The view rides a body through its Cinemachine camera — on CameraRoot while alive,
    /// parented to the ragdoll's head while down or dead — so "is this camera inside me" is just
    /// whether the brain's live camera sits under this body. That needs no ownership: remote
    /// players' cameras are never live on this machine, and the ragdoll learns it is the local one
    /// the moment the death camera is parented to it.</para>
    ///
    /// <para>It is decided per camera, in beginCameraRendering, and not by layer or bone scale: the
    /// mirror and any other camera still see the head, its shadow stays on the floor, and the
    /// layer swaps PlayerDead does on the whole hierarchy have nothing to undo.</para>
    /// </remarks>
    public class FirstPersonHiddenRenderers : MonoBehaviour
    {
        [Tooltip("What the riding camera must not see: the head and anything fixed to it.")]
        [SerializeField] private Renderer[] renderers;

        [Tooltip("The body the camera rides. Empty means this object.")]
        [SerializeField] private Transform bodyRoot;

        private ShadowCastingMode[] _shownModes;

        private void Awake()
        {
            if (bodyRoot == null) bodyRoot = transform;

            _shownModes = new ShadowCastingMode[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) _shownModes[i] = renderers[i].shadowCastingMode;
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

            // Leave the renderers as the prefab had them, not as the last camera wanted them.
            Apply(false);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera viewCamera)
        {
            Apply(IsRiddenBy(viewCamera));
        }

        private bool IsRiddenBy(Camera viewCamera)
        {
            if (!viewCamera.TryGetComponent(out CinemachineBrain brain)) return false;

            // Mid-blend this is the camera being blended to, which is the one the view is heading for.
            Component live = brain.ActiveVirtualCamera as Component;
            return live != null && live.transform.IsChildOf(bodyRoot);
        }

        private void Apply(bool hidden)
        {
            if (_shownModes == null) return;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer target = renderers[i];
                if (target == null) continue;

                ShadowCastingMode mode = hidden ? ShadowCastingMode.ShadowsOnly : _shownModes[i];
                if (target.shadowCastingMode != mode) target.shadowCastingMode = mode;
            }
        }
    }
}
