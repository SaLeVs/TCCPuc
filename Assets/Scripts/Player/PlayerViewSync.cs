using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Lets a spectator see exactly what this player sees: where they aim, crouched or standing,
    /// and through their ragdoll's eyes while they are knocked down.
    /// </summary>
    /// <remarks>
    /// <para>A spectator looks through this player's own Cinemachine camera, on the spectator's
    /// machine. Its aim never arrived: PanTilt aims from the mouse axes, and those only move on
    /// the owner, so the view stayed pointed wherever it spawned while the body turned under it.
    /// The owner now sends the two axes and every other peer eases its copy of the camera onto
    /// them.</para>
    ///
    /// <para>The position is worked out from that aim too. CameraRoot sits 30 cm in front of the
    /// face, on a body the owner keeps turned exactly to the aim; the copy of the body turns on its
    /// own NetworkTransform and trails behind after a fast turn, which swung the copy of the
    /// camera around the head and into the shoulders. So the copy follows a point placed like the
    /// owner's: CameraRoot's synced offset (crouch, run), turned by the aim's yaw.</para>
    ///
    /// <para>Knocked down, the owner's view rides their ragdoll's head. Every peer spawns its own
    /// ragdoll from the same impulse, so the copy rides the local one the same way: that is the
    /// head drawn on this machine, and FirstPersonHiddenRenderers hides it from this view because
    /// the camera follows it.</para>
    /// </remarks>
    public class PlayerViewSync : NetworkBehaviour
    {
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private CinemachinePanTilt panTilt;
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private PlayerKnockdown playerKnockdown;
        [SerializeField] private PlayerCameraOffset playerCameraOffset;

        [Tooltip("How fast a spectator's view catches up with the aim that arrives over the network.")]
        [SerializeField, Min(1f)] private float lookSmoothing = 18f;

        [Tooltip("Smallest change of aim, in degrees, worth sending.")]
        [SerializeField, Min(0f)] private float sendThreshold = 0.05f;

        // Owner writes its PanTilt axes in the World frame: x = pan (yaw), y = tilt (pitch).
        private readonly NetworkVariable<Vector2> _look = new(Vector2.zero,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Non-owners only: what this copy of the camera follows while the player is standing.
        private Transform _viewPoint;

        private bool _onRagdoll;
        private PlayerRagdoll _riddenRagdoll;
        private Transform _ragdollEyes;
        private CinemachinePanTilt.ReferenceFrames _savedReferenceFrame;
        private bool _wasLive;


        public override void OnNetworkSpawn()
        {
            if (IsOwner) return;

            _viewPoint = new GameObject("SpectatorView").transform;
            _viewPoint.SetParent(transform, worldPositionStays: false);
            cinemachineCamera.Follow = _viewPoint;
        }

        public override void OnNetworkDespawn()
        {
            LeaveRagdoll();

            if (_viewPoint == null) return;

            cinemachineCamera.Follow = cameraRoot;
            Destroy(_viewPoint.gameObject);
            _viewPoint = null;
        }

        // Before the CinemachineBrain (execution order 100), so the camera is placed this frame.
        private void LateUpdate()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                SendLook();
                return;
            }

            FollowRagdoll();

            if (!_onRagdoll) ApplyLook();
        }

        private void SendLook()
        {
            // Down or dead the axes are zeroed and the view rides the ragdoll, which every peer
            // has its own copy of; the last aim stays, ready for when they get up.
            if (panTilt.ReferenceFrame != CinemachinePanTilt.ReferenceFrames.World) return;

            Vector2 look = new Vector2(panTilt.PanAxis.Value, panTilt.TiltAxis.Value);
            Vector2 sent = _look.Value;

            if (Mathf.Abs(Mathf.DeltaAngle(sent.x, look.x)) < sendThreshold && Mathf.Abs(sent.y - look.y) < sendThreshold) return;

            _look.Value = look;
        }

        /// <summary>
        /// Eases this copy of the camera onto the owner's aim. It snaps when the view has just been
        /// handed to it, so switching who you watch does not swing in from where it last pointed.
        /// </summary>
        private void ApplyLook()
        {
            bool live = cinemachineCamera.IsLive;
            float t = live && _wasLive ? 1f - Mathf.Exp(-lookSmoothing * Time.deltaTime) : 1f;
            _wasLive = live;

            Vector2 target = _look.Value;
            float pan = panTilt.PanAxis.Value + Mathf.DeltaAngle(panTilt.PanAxis.Value, target.x) * t;

            panTilt.PanAxis.Value = Mathf.Repeat(pan + 180f, 360f) - 180f;
            panTilt.TiltAxis.Value = Mathf.Lerp(panTilt.TiltAxis.Value, target.y, t);

            if (_viewPoint != null)
            {
                Quaternion aimYaw = Quaternion.Euler(0f, panTilt.PanAxis.Value, 0f);
                _viewPoint.position = transform.position + aimYaw * cameraRoot.localPosition;
            }
        }

        /// <summary>
        /// Knocked down, CameraRoot's synced pose is the owner's spot on THEIR ragdoll's head,
        /// in bone space, which means nothing next to this peer's body. The copy rides this peer's
        /// ragdoll instead, looking down its gaze and kept out of the floor and walls like the
        /// owner's camera.
        /// </summary>
        private void FollowRagdoll()
        {
            PlayerRagdoll ragdoll = playerKnockdown != null ? playerKnockdown.Ragdoll : null;

            if (_onRagdoll && (ragdoll == null || ragdoll != _riddenRagdoll)) LeaveRagdoll();
            if (!_onRagdoll && ragdoll != null) RideRagdoll(ragdoll);

            if (!_onRagdoll || _ragdollEyes == null || !cinemachineCamera.IsLive) return;

            Vector3 head = _riddenRagdoll.HeadBone.position;
            _ragdollEyes.position = playerCameraOffset != null
                ? playerCameraOffset.ClearOfGeometry(head, _riddenRagdoll.HipsBone)
                : head;
        }

        private void RideRagdoll(PlayerRagdoll ragdoll)
        {
            if (ragdoll.HeadBone == null) return;

            // On the head, so it turns with it; the gaze is a fixed rotation in bone space.
            _ragdollEyes = new GameObject("SpectatorEyes").transform;
            _ragdollEyes.SetParent(ragdoll.HeadBone, worldPositionStays: false);
            _ragdollEyes.localRotation = ragdoll.HeadGaze;

            // TrackingTarget with zeroed axes: the view looks exactly down the eyes' forward.
            _savedReferenceFrame = panTilt.ReferenceFrame;
            panTilt.ReferenceFrame = CinemachinePanTilt.ReferenceFrames.TrackingTarget;
            panTilt.PanAxis.Value = 0f;
            panTilt.TiltAxis.Value = 0f;
            cinemachineCamera.Follow = _ragdollEyes;

            _riddenRagdoll = ragdoll;
            _onRagdoll = true;
        }

        private void LeaveRagdoll()
        {
            if (!_onRagdoll) return;

            _onRagdoll = false;
            _riddenRagdoll = null;

            cinemachineCamera.Follow = _viewPoint != null ? _viewPoint : cameraRoot;
            panTilt.ReferenceFrame = _savedReferenceFrame;

            // The axes were zeroed: snap back onto the owner's aim instead of easing from there.
            _wasLive = false;

            // Usually already going with the ragdoll; Destroy waits for the end of the frame.
            if (_ragdollEyes != null) Destroy(_ragdollEyes.gameObject);
            _ragdollEyes = null;
        }
    }
}
