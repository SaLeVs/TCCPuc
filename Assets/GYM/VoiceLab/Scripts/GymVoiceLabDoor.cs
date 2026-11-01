using System;
using UnityEngine;

namespace LevelDesign.Gym.VoiceLab
{
    /// <summary>
    /// A door of the map replica, opened and shut from the lab's keys. The game's Door is networked
    /// and was stripped with the rest of the gameplay; this keeps only its swing — the same leaves,
    /// angles and mirroring, read from the Door when the replica was built — because an open or shut
    /// door is exactly what changes how a voice crosses between rooms.
    /// </summary>
    public class GymVoiceLabDoor : MonoBehaviour
    {
        [Serializable]
        public class Leaf
        {
            public Transform pivot;
            public float closedYaw;
            public float mirror = 1f;
        }

        [SerializeField] private Leaf[] leaves = Array.Empty<Leaf>();
        [SerializeField] private float closedAngle;
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private Vector3 hingeAxis = Vector3.up;
        [SerializeField] private Collider closedBlocker;
        [SerializeField, Min(1f)] private float degreesPerSecond = 300f;

        private float _angle;
        private float _target;
        private bool _initialised;

        public bool IsOpen => !Mathf.Approximately(_target, closedAngle);

        public void Configure(Leaf[] doorLeaves, float shutAngle, float fullyOpenAngle, Vector3 axis, Collider blocker)
        {
            leaves = doorLeaves ?? Array.Empty<Leaf>();
            closedAngle = shutAngle;
            openAngle = fullyOpenAngle;
            hingeAxis = axis;
            closedBlocker = blocker;
        }

        private void Awake()
        {
            _angle = closedAngle;
            _target = closedAngle;
            _initialised = true;
            enabled = false;
        }

        public void SetOpen(bool open)
        {
            if (!_initialised) Awake();

            _target = open ? openAngle : closedAngle;
            if (closedBlocker != null) closedBlocker.enabled = !open;

            enabled = true;
        }

        public void Toggle() => SetOpen(!IsOpen);

        private void Update()
        {
            _angle = Mathf.MoveTowards(_angle, _target, degreesPerSecond * Time.deltaTime);

            bool vertical = hingeAxis == Vector3.up;

            foreach (Leaf leaf in leaves)
            {
                if (leaf == null || leaf.pivot == null) continue;

                float turn = leaf.closedYaw + leaf.mirror * (_angle - closedAngle);
                leaf.pivot.localRotation = vertical ? Quaternion.Euler(0f, turn, 0f) : Quaternion.AngleAxis(turn, hingeAxis);
            }

            if (Mathf.Approximately(_angle, _target)) enabled = false;
        }
    }
}
