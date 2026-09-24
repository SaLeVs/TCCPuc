using System;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    /// <summary>
    /// Deals with a shut door on the monster's path: walks to a spot centred in front of the
    /// doorway, squares up to it, swipes, and waits for the leaf to swing clear.
    ///
    /// <para>While it holds a door it owns the agent. The state machine keeps running during the
    /// walk-up and squaring-up, and if the active state changes in that time the door is dropped —
    /// the new state may not be going through it at all, and if it is, the path check picks the
    /// door straight back up. From the swipe on it is committed: <see cref="MonsterBrain"/> stops
    /// ticking the state machine until the door has swung, then gives the agent back through
    /// <see cref="HSM.StateMachine.ResumeLeaf"/>, so the active state restores its own speed,
    /// rotation, destination and animation instead of this class guessing them.</para>
    /// </summary>
    public class MonsterDoorForcer : NetworkBehaviour
    {
        public static Action<Vector3> OnDoorHitSound;

        public event Action OnDoorHitAnimation;

        /// <summary>The door is dealt with and the active state should take the agent back.</summary>
        public event Action OnForcingFinished;

        [Tooltip("How far ahead along its path the monster looks for a shut door.")]
        [SerializeField] private float detectDistance = 2f;

        [Tooltip("Height of the probe off the floor, so it meets the leaf and not the threshold.")]
        [SerializeField] private float probeHeight = 1f;

        [Tooltip("How far in front of the leaf it stands to swipe, centred on the doorway.")]
        [SerializeField, Min(0.3f)] private float standOffDistance = 0.9f;

        [Tooltip("How close to that spot counts as in position.")]
        [SerializeField, Min(0.05f)] private float standTolerance = 0.2f;

        [Tooltip("Seconds it may spend walking to the spot before it swipes from wherever it got to.")]
        [SerializeField, Min(0.1f)] private float maxApproachSeconds = 2f;

        [Tooltip("Degrees off square that still counts as facing the door.")]
        [SerializeField, Range(1f, 45f)] private float faceTolerance = 8f;

        [Tooltip("Seconds it may spend turning to face the door before it swipes anyway.")]
        [SerializeField, Min(0.1f)] private float maxAlignSeconds = 0.6f;

        [Tooltip("How long the swipe takes before the door gives.")]
        [SerializeField] private float attackSeconds = 0.9f;

        [Tooltip("How fast it turns while walking up to the door and squaring up to it.")]
        [SerializeField] private float faceDoorSpeed = 8f;

        [Tooltip("Pause before it will go for the same door again, so slamming it in its face cannot make it stutter. Other doors are looked for straight away.")]
        [SerializeField] private float retryCooldown = 0.6f;

        [Tooltip("Layers the door leaves live on. Door_* prefabs use Doors; SM_Double_Door_V2 uses Interactable.")]
        [SerializeField] private LayerMask doorLayers;

        public bool IsForcingDoor => _phase != Phase.None;

        /// <summary>True from the swipe on: nothing may interrupt it until the door has swung open.</summary>
        public bool IsCommitted => _phase == Phase.Swiping || _phase == Phase.WaitingForSwing;

        // Reading the agent's path allocates, so the lookahead runs a few times a second rather
        // than every frame. At chase speed that is still well inside the detect distance.
        private const float ScanInterval = 0.1f;

        // Safety net: a leaf that never reports it has stopped must not hold the monster forever.
        private const float MaxWaitForSwingSeconds = 3f;

        private enum Phase
        {
            None,
            Approaching,
            Aligning,
            Swiping,
            WaitingForSwing
        }

        private NavMeshAgent _agent;
        private IForceableDoor _door;
        private Phase _phase;
        private float _phaseTimer;

        // Per door, not global: a blanket cooldown blinded it to a second door just past the first,
        // and it walked straight through that one.
        private IForceableDoor _cooldownDoor;
        private float _cooldown;
        private float _scanTimer;
        private Vector3 _standPoint;
        private Vector3 _facing;

        private readonly Vector3[] _corners = new Vector3[16];
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public void Initialize(NavMeshAgent agent)
        {
            _agent = agent;
        }

        /// <summary>Runs before the state machine each frame. Finishing here resumes the active state.</summary>
        public void Tick(float deltaTime)
        {
            if (!IsServer || _agent == null || !_agent.isOnNavMesh) return;

            if (_phase == Phase.None)
            {
                TickDetection(deltaTime);
                return;
            }

            if (DoorIsGone())
            {
                Finish();
                return;
            }

            // Someone else opened it before the swipe began and it has finished swinging: nothing
            // left to do. Still swinging, it is waited out like any other leaf in the way.
            if (!IsCommitted && !_door.IsClosed && !_door.IsSwinging)
            {
                Finish();
                return;
            }

            _phaseTimer += deltaTime;

            switch (_phase)
            {
                case Phase.Approaching:
                    UpdateApproach(deltaTime);
                    break;

                case Phase.Aligning:
                    UpdateAlign(deltaTime);
                    break;

                case Phase.Swiping:
                    TurnTowards(_facing, deltaTime);
                    if (_phaseTimer < attackSeconds) return;

                    _door.ForceOpenFrom(transform.position);
                    SetPhase(Phase.WaitingForSwing);
                    break;

                case Phase.WaitingForSwing:
                    TurnTowards(_facing, deltaTime);
                    if (_door.IsSwinging && _phaseTimer < MaxWaitForSwingSeconds) return;

                    Finish();
                    break;
            }
        }

        /// <summary>
        /// Runs after the state machine each frame. A state entered or left this frame may have
        /// touched the agent in its OnEnter/OnExit; this puts the hold back before the agent
        /// moves on it.
        /// </summary>
        public void HoldAgent()
        {
            if (_phase == Phase.None || _agent == null || !_agent.isOnNavMesh) return;

            _agent.updateRotation = false;

            if (_phase == Phase.Approaching)
            {
                _agent.isStopped = false;

                bool lostDestination = !_agent.pathPending &&
                                       (!_agent.hasPath || FlatDistance(_agent.destination, _standPoint) > 0.25f);

                if (lostDestination) _agent.SetDestination(_standPoint);
                return;
            }

            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
        }

        /// <summary>
        /// The active state changed during the walk-up. Drop the door without resuming — the new
        /// state's OnEnter has already set the agent up — and allow an immediate re-scan in case
        /// the new path still runs through it.
        /// </summary>
        public void CancelBeforeSwipe()
        {
            if (_phase == Phase.None || IsCommitted) return;

            _phase = Phase.None;
            _door = null;
            _cooldown = 0f;
            _cooldownDoor = null;
            _scanTimer = 0f;
        }

        private void TickDetection(float deltaTime)
        {
            if (_cooldown > 0f)
            {
                _cooldown -= deltaTime;
                if (_cooldown <= 0f) _cooldownDoor = null;
            }

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f) return;

            _scanTimer = ScanInterval;

            // A pending path does not mean no path: Chase asks for a new one every frame, and the
            // one it is walking is still the one to check.
            if (_agent.isStopped || !_agent.hasPath) return;

            IForceableDoor door = FindClosedDoorOnPath();
            if (door == null) return;

            Begin(door);
        }

        /// <summary>
        /// Walks the path corners out to <see cref="detectDistance"/> looking for a shut leaf.
        ///
        /// <para>It used to cast along the steering direction, which missed a door the path turns
        /// through until the monster was already on top of it, and caught doors it was only walking
        /// past. The path is what actually says it is going through.</para>
        /// </summary>
        private IForceableDoor FindClosedDoorOnPath()
        {
            int cornerCount = _agent.path.GetCornersNonAlloc(_corners);

            Vector3 from = transform.position;
            float budget = detectDistance;

            for (int i = 0; i < cornerCount && budget > 0f; i++)
            {
                Vector3 segment = _corners[i] - from;
                segment.y = 0f;

                float length = segment.magnitude;

                if (length > 0.01f)
                {
                    float castLength = Mathf.Min(length, budget);

                    IForceableDoor door = CastForClosedDoor(from, segment / length, castLength);
                    if (door != null) return door;

                    budget -= castLength;
                }

                from = _corners[i];
            }

            return null;
        }

        private IForceableDoor CastForClosedDoor(Vector3 from, Vector3 direction, float distance)
        {
            Vector3 origin = from + Vector3.up * probeHeight;

            int count = Physics.RaycastNonAlloc(origin, direction, _hits, distance, doorLayers, QueryTriggerInteraction.Ignore);

            IForceableDoor nearest = null;
            float nearestDistance = float.MaxValue;

            // Interactable holds more than doors, and the hits come back in no particular order.
            for (int i = 0; i < count; i++)
            {
                IForceableDoor door = _hits[i].collider.GetComponentInParent<IForceableDoor>();
                if (door == null) continue;

                // Shut, or still swinging open: both leave the leaf across the doorway.
                if (!door.IsClosed && !door.IsSwinging) continue;
                if (door == _cooldownDoor && _cooldown > 0f) continue;
                if (_hits[i].distance >= nearestDistance) continue;

                nearest = door;
                nearestDistance = _hits[i].distance;
            }

            return nearest;
        }

        private void Begin(IForceableDoor door)
        {
            _door = door;

            door.GetApproachPose(transform.position, standOffDistance, out Vector3 standPoint, out _facing);

            // A spot off the navmesh — a doorway tight against a wall — falls back to swiping from
            // where it is rather than walking at something it can never reach.
            _standPoint = NavMesh.SamplePosition(standPoint, out NavMeshHit hit, 1f, _agent.areaMask)
                ? hit.position
                : transform.position;

            SetPhase(Phase.Approaching);

            _agent.isStopped = false;
            _agent.updateRotation = false;
            _agent.SetDestination(_standPoint);
        }

        private void UpdateApproach(float deltaTime)
        {
            Vector3 moving = _agent.desiredVelocity;
            moving.y = 0f;

            if (moving.sqrMagnitude > 0.01f) TurnTowards(moving, deltaTime);

            bool inPosition = FlatDistance(transform.position, _standPoint) <= standTolerance;
            bool pathEnded = !_agent.pathPending && _agent.remainingDistance <= standTolerance;

            if (!inPosition && !pathEnded && _phaseTimer < maxApproachSeconds) return;

            _agent.ResetPath();
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;

            SetPhase(Phase.Aligning);
        }

        private void UpdateAlign(float deltaTime)
        {
            TurnTowards(_facing, deltaTime);

            bool squaredUp = Vector3.Angle(Flat(transform.forward), _facing) <= faceTolerance;

            if (!squaredUp && _phaseTimer < maxAlignSeconds) return;

            // Already opening on its own: no swipe, just let the leaf clear the doorway.
            if (!_door.IsClosed)
            {
                SetPhase(Phase.WaitingForSwing);
                return;
            }

            SetPhase(Phase.Swiping);

            OnDoorHitAnimation?.Invoke();
            PlayHitSoundRpc();
        }

        private void Finish()
        {
            _cooldownDoor = _door;
            _cooldown = retryCooldown;

            _phase = Phase.None;
            _door = null;

            OnForcingFinished?.Invoke();
        }

        private void SetPhase(Phase phase)
        {
            _phase = phase;
            _phaseTimer = 0f;
        }

        /// <summary>A door despawned with its room reads as non-null through the interface.</summary>
        private bool DoorIsGone()
        {
            return _door == null || (_door is UnityEngine.Object unityObject && unityObject == null);
        }

        private void TurnTowards(Vector3 direction, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, faceDoorSpeed * deltaTime);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayHitSoundRpc()
        {
            OnDoorHitSound?.Invoke(transform.position);
        }
    }
}
