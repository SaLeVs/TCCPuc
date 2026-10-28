using Player;
using UnityEngine;

namespace Ui
{
    /// <summary>
    /// Keeps a player's name floating just over their head the way shooters do: square to the
    /// screen and the same size on screen near or far — or shrinking a little with distance, when
    /// the near and far heights differ. It is gone wherever it would only get in the way: over its
    /// own player, over whoever is being spectated, and over a body that is dead or hidden.
    /// </summary>
    /// <remarks>
    /// <para>Placed by hand every frame rather than parented to the head: the head bone carries the
    /// model's 100x scale and its rotation, and the tag wants neither. While its player is knocked
    /// down it stands on the ragdoll's head, since the body it was over is hidden then.</para>
    ///
    /// <para>Its size is the canvas's own rect height, scaled so that rect covers the chosen
    /// fraction of the screen. What is inside is laid out by the layout group under it.</para>
    /// </remarks>
    [RequireComponent(typeof(Canvas))]
    public class PlayerNameTag : MonoBehaviour
    {
        [SerializeField] private PlayerState playerState;
        [SerializeField] private PlayerKnockdown playerKnockdown;

        [Tooltip("The head bone. The tag stands on it, so it follows crouching and leaning.")]
        [SerializeField] private Transform head;

        [Tooltip("The body's renderer. While it is off the body is not there to be named: dead, " +
                 "escaped or hidden.")]
        [SerializeField] private Renderer bodyRenderer;

        [Header("Placement")]
        [Tooltip("Metres from the head bone up to the bottom of the tag. Enough to clear the hat.")]
        [SerializeField, Min(0f)] private float heightAboveHead = 0.5f;

        [Header("Size on screen")]
        [Tooltip("Height of the tag as a fraction of the screen's height, up to Near Distance.")]
        [SerializeField, Range(0.005f, 0.2f)] private float nearScreenHeight = 0.035f;

        [Tooltip("Height of the tag as a fraction of the screen's height, from Far Distance on. The " +
                 "same as Near keeps it the same size at any distance.")]
        [SerializeField, Range(0.005f, 0.2f)] private float farScreenHeight = 0.026f;

        [Tooltip("Metres. Closer than this the tag keeps its near height.")]
        [SerializeField, Min(0f)] private float nearDistance = 2f;

        [Tooltip("Metres. Further than this the tag keeps its far height; in between it shrinks smoothly.")]
        [SerializeField, Min(0f)] private float farDistance = 20f;

        [Tooltip("Metres. Hidden past this. 0 shows it at any distance.")]
        [SerializeField, Min(0f)] private float maxVisibleDistance;

        private Canvas _canvas;
        private RectTransform _rect;
        private Camera _camera;


        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
            _rect = (RectTransform)transform;
        }

        private void LateUpdate()
        {
            // After the animation, so it stands on the head where it is drawn this frame.
            bool visible = TryPlace();

            if (_canvas.enabled != visible) _canvas.enabled = visible;
        }

        private bool TryPlace()
        {
            if (!playerState.IsSpawned || playerState.IsOwner || playerState.IsDead) return false;
            if (!TryGetCamera(out Camera view)) return false;

            // Spectating this player: the view comes out of their head, right under the tag.
            if (FirstPersonHiddenRenderers.IsRiddenBy(view, playerState.transform)) return false;

            Transform anchor = AnchorHead();
            if (anchor == null) return false;

            Vector3 position = anchor.position + Vector3.up * heightAboveHead;
            Transform viewTransform = view.transform;
            Vector3 toTag = position - viewTransform.position;
            float distance = toTag.magnitude;

            if (maxVisibleDistance > 0f && distance > maxVisibleDistance) return false;

            // Depth along the view rather than straight-line distance: it is what perspective
            // divides by, so this is what keeps the size on screen exact off-centre too.
            float depth = Vector3.Dot(toTag, viewTransform.forward);
            if (depth <= view.nearClipPlane) return false;

            float screenHeight = Mathf.Lerp(nearScreenHeight, farScreenHeight,
                Mathf.InverseLerp(nearDistance, farDistance, distance));
            float worldHeight = screenHeight * 2f * depth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float scale = worldHeight / Mathf.Max(_rect.rect.height, 0.01f);

            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;

            transform.SetPositionAndRotation(position, viewTransform.rotation);
            transform.localScale = new Vector3(scale / parentScale.x, scale / parentScale.y, scale / parentScale.z);
            return true;
        }

        private Transform AnchorHead()
        {
            // Knocked down, the body is hidden and the ragdoll is what lies there.
            if (playerKnockdown != null && playerKnockdown.IsKnockedDown && playerKnockdown.Ragdoll != null)
            {
                return playerKnockdown.Ragdoll.HeadBone;
            }

            if (bodyRenderer != null && !bodyRenderer.enabled) return null;

            return head;
        }

        private bool TryGetCamera(out Camera view)
        {
            // Looked up again if it goes away: the view can move to another camera after death.
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }

            view = _camera;
            return view != null;
        }
    }
}
