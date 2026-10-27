using System;
using System.Collections;
using System.Collections.Generic;
using Components;
using Components.Perception;
using Components.Sound;
using Enums;
using Interfaces;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;

namespace Objects
{
    [RequireComponent(typeof(NavMeshObstacle))]
    public class Door : NetworkBehaviour, IDoorwayInteractable, IInteractionRange, IForceableDoor
    {
        /// <summary>Someone failed to get this door to move. Raised on every client, with where the door is.</summary>
        public static event Action<Vector3> OnDoorBlocked;

        [Header("References")]
        [SerializeField] private Transform doorPivot;

        [Tooltip("The main leaf's kinematic Rigidbody. Its transform must sit on the hinge — that is the point it turns round.")]
        [SerializeField] private Rigidbody doorRigidbody;

        [SerializeField] private NavMeshObstacle navMeshObstacle;

        [Tooltip("Optional. Solid only while the door is shut and still, and only once nobody is inside " +
                 "it: the gap under a counter flap, which the flap alone leaves open to anyone " +
                 "crouching, and which the monster's probe would otherwise pass straight under.")]
        [SerializeField] private BoxCollider closedBlocker;

        [Tooltip("Optional. The noise a player makes opening or closing this door. Deliberately " +
                 "not emitted when the monster forces the door — it would chase its own racket.")]
        [SerializeField] private NoiseEmitter useNoise;

        [Tooltip("The rattle of a door that won't move: held by the monster, or stuck on someone.")]
        [SerializeField] private SoundDefinitionSO blockedSound;

        [Tooltip("The latch and creak as the door starts to swing open — whoever opens it, the monster included.")]
        [SerializeField] private SoundDefinitionSO openSound;

        [Tooltip("The thud of the leaf landing in the frame. Played when the swing ends, not when it starts, " +
                 "so a close that bounces off someone never sounds like it shut.")]
        [SerializeField] private SoundDefinitionSO closeSound;

        [Header("Impact")]
        [Tooltip("What this door does to a player it hits while swinging. Leave empty for a door that never knocks anyone over.")]
        [SerializeField] private ImpactProfileSO impactProfile;

        [Header("Obstruction")]
        [Tooltip("Bodies the leaf will not sweep through. A player closing the door shoves the players " +
                 "in its path out of the way; anything else, or a sabotage close, still stops it. It " +
                 "refuses to open into one it cannot knock down. Empty means Player + Monster.")]
        [SerializeField] private LayerMask occupantLayers;

        [Tooltip("Metres added round the leaf when checking its path, so a body just brushing the " +
                 "edge still counts as in the way.")]
        [SerializeField, Min(0f)] private float obstructionMargin = 0.05f;

        [Tooltip("Largest gap, in degrees, between the poses checked along a swing.")]
        [SerializeField, Range(2f, 30f)] private float sweepSampleDegrees = 10f;

        [Header("Shove")]
        [Tooltip("What stops a player being shoved out of a closing leaf's way. Empty means " +
                 "Default + Scenary + Walls + Props + Doors + MainStage.")]
        [SerializeField] private LayerMask shoveBlockingLayers;

        [Tooltip("Seconds the shove takes. The leaf waits this long before it starts to close.")]
        [SerializeField, Range(0.05f, 0.5f)] private float shoveSeconds = 0.15f;

        [Header("NavMesh")]
        [Tooltip("Metres either side of the leaf where the doorway's navmesh connection is tested, " +
                 "and where the fallback link starts and ends when that connection is missing.")]
        [SerializeField, Min(0.3f)] private float navMeshCheckReach = 0.9f;

        [Tooltip("Metres of the open leaf, from the hinge, left out of the navmesh carve. That end " +
                 "lies against the wall where the navmesh is eroded anyway, and carving it as well " +
                 "cut through the doorway strip beside it.")]
        [SerializeField, Min(0f)] private float openLeafHingeClearance = 0.45f;

        [Header("Hinge")]
        [Tooltip("The axis the leaves turn round, in their parent's space. Up for a door. Right or " +
                 "Forward for a flap hinged along one edge, like a counter top that lifts.")]
        [SerializeField] private HingeAxis hingeAxis = HingeAxis.Up;

        [Tooltip("On: it opens to whichever side is away from whoever opens it. Off: it only ever " +
                 "opens to side A, the way a counter flap only lifts.")]
        [SerializeField] private bool opensBothWays = true;

        [Header("Angles")]
        [Tooltip("The main leaf's local rotation round the hinge axis when the door is shut — the " +
                 "rotation the leaf has in the prefab, usually 0. How the whole door is turned in the " +
                 "scene is the root's rotation and never goes here.")]
        [SerializeField] private float closedAngle;

        [Tooltip("The main leaf's local rotation round the hinge axis fully open one way. With both " +
                 "ways on, the door picks this side or side B on its own, so it always swings away " +
                 "from whoever opens it.")]
        [FormerlySerializedAs("openAngle")]
        [SerializeField] private float openSideAAngle = 100f;

        [Tooltip("The main leaf's local rotation fully open the other way — normally side A's angle, negated.")]
        [SerializeField] private float openSideBAngle = -100f;

        [Header("Double door")]
        [Tooltip("The other leaves that open and close with the main one. Leave empty for a single door.")]
        [SerializeField] private ExtraLeaf[] extraLeaves = Array.Empty<ExtraLeaf>();

        /// <summary>A leaf that follows the main one — the other half of a double door.</summary>
        [Serializable]
        private class ExtraLeaf
        {
            [Tooltip("This leaf's kinematic Rigidbody. Its transform must sit on its hinge.")]
            public Rigidbody leafRigidbody;

            [Tooltip("This leaf's own local Y rotation when the door is shut. The left half of a " +
                     "double door modelled as a mirror of the right one is usually 180.")]
            public float closedAngle;

            [Tooltip("-1 turns it the opposite way to the main leaf, which is what makes both halves " +
                     "of a double door open to the same side. 1 turns it the same way.")]
            public float mirror = -1f;
        }

        [Header("Settings")]
        [SerializeField] private float openDegreesPerSecond = 300f;

        [Tooltip("How long the door refuses to be touched after the monster forces it, so nobody can shut it back in its face on a loop.")]
        [SerializeField] private float monsterLockSeconds = 3f;

        [Tooltip("How far away a player can open or close it from. Further than other things: a door " +
                 "has to be caught on the run, with a monster behind.")]
        [SerializeField, Min(0.5f)] private float interactionRange = 2.5f;

        private enum HingeAxis
        {
            Up,
            Right,
            Forward
        }

        public float InteractionRange => interactionRange;

        public Vector3 Position => DoorwayCentre;
        public bool IsClosed => _state.Value == DoorState.Closed;
        public bool IsSwinging => !Mathf.Approximately(_currentAngle, _targetAngle);

        /// <summary>Server-side: the monster is holding this door and it will not answer players yet.</summary>
        public bool IsLocked => Time.time < _lockedUntilTime;

        private const float CloseRetrySeconds = 0.2f;

        private const ulong NoClient = ulong.MaxValue;

        // The shove's path is checked from above step height, so a floor lip or a threshold does not
        // count as a wall.
        private const float ShoveStepUp = 0.3f;

        // Extra distance past the leaf, so a shoved player ends clear of it and not touching it.
        private const float ShoveSkin = 0.02f;

        // Whoever shuts the door while already heading out of its path at this speed or more is let
        // go on their own, as long as the leaf has to wait no longer than this for them.
        private const float LeavingSpeed = 1f;
        private const float MaxLeavingWait = 0.3f;

        // A flap's path is walked rather than worked out: the body is tried this far along at a
        // time, up to the last distance, against every pose the flap passes through.
        private const float SweptClearanceStep = 0.05f;
        private const float MaxSweptClearance = 2.5f;

        // Added round the shut leaves when the crosshair looks for the doorway.
        private const float DoorwayTargetPadding = 0.02f;

        private static readonly WaitForFixedUpdate WaitForFixed = new WaitForFixedUpdate();

        private const float NavMeshSampleRadius = 0.6f;

        // A path through this doorway is about the straight line between the two test points; one
        // that goes round through some other door is metres longer.
        private const float ThroughDoorwaySlack = 1.5f;

        private static NavMeshPath _connectionProbe;
        private static readonly Vector3[] ProbeCorners = new Vector3[32];

        private NavMeshLinkInstance _fallbackLink;

        private readonly NetworkVariable<DoorState> _state = new NetworkVariable<DoorState>(DoorState.Closed);

        // Source of truth for the swing on every peer, as the main leaf's local Y rotation — every
        // other leaf's is worked out from it. The leaves are driven towards _targetAngle on the
        // physics step with MoveRotation everywhere: writing the Transform instead, as clients used to,
        // teleports the collider, and a teleported leaf does not push a player out of the way — it
        // appears inside them and the solver ejects them to whichever side is nearer.
        private float _currentAngle;
        private float _targetAngle;

        // +1 / -1 while swinging, so an impact knows which way the leaf is travelling.
        private float _swingSign;

        private float _lockedUntilTime;

        // Server-side. A sabotage that found someone in the doorway keeps trying until it clears.
        private bool _closeWhenClear;
        private float _nextCloseAttemptTime;

        // Where a close that gets bounced goes back to.
        private DoorState _lastOpenState = DoorState.OpenSideA;

        // Server-side. Whoever opened the door for the swing in progress — never knocked down by it.
        private ulong _openerClientId = NoClient;

        // Server-side. Players being shoved out of the way of a close a player asked for. The leaf
        // ignores them until it shuts: the server's copy of a remote player lags behind where their
        // own machine has already moved them.
        private readonly HashSet<PlayerKnockdown> _shoved = new HashSet<PlayerKnockdown>();

        // Server-side. When the close waiting on those shoves starts; negative when none is waiting.
        private float _closeAfterShoveTime = -1f;

        private readonly List<PlayerKnockdown> _inTheWay = new List<PlayerKnockdown>();
        private readonly List<float> _shoveSides = new List<float>();
        private readonly RaycastHit[] _shoveHits = new RaycastHit[16];

        // Owner-side. The shove moving this machine's own player, if any.
        private Coroutine _shoveRoutine;

        /// <summary>
        /// One moving leaf. Its box is kept in the leaf's own rotation frame so its pose at any
        /// angle can be rebuilt for a query without moving the real one.
        /// </summary>
        private sealed class Leaf
        {
            public Rigidbody Body;
            public float ClosedYaw;
            public float Mirror;

            public BoxCollider Box;
            public bool HasBox;
            public Vector3 BoxOffset;
            public Quaternion BoxRotation;
            public Vector3 BoxHalfExtents;

            // Flat distance from the hinge to the far edge, and half the leaf's thickness.
            public float Reach;
            public float HalfThickness;

            // Server-side. Carves the open leaf out of the navmesh so the monster walks round it.
            public NavMeshObstacle OpenObstacle;
        }

        // The main leaf first, then any extras.
        private readonly List<Leaf> _leaves = new List<Leaf>();

        // At least one leaf has a box to query with.
        private bool _hasLeafBox;

        private readonly Collider[] _overlapBuffer = new Collider[16];
        private readonly List<PlayerKnockdown> _knockableBuffer = new List<PlayerKnockdown>();
        private readonly HashSet<PlayerKnockdown> _hitThisSwing = new HashSet<PlayerKnockdown>();

        private enum Occupant
        {
            None,

            /// <summary>A player the impact profile can knock over — the leaf may swing through them.</summary>
            Knockable,

            /// <summary>Anything the leaf must not pass through.</summary>
            Solid
        }

        private void Awake()
        {
            if (occupantLayers.value == 0)
            {
                occupantLayers = LayerMask.GetMask("Player", "Monster");
            }

            if (shoveBlockingLayers.value == 0)
            {
                shoveBlockingLayers = LayerMask.GetMask("Default", "Scenary", "Walls", "Props", "Doors", "MainStage");
            }

            AddLeaf(doorRigidbody, closedAngle, 1f);

            if (extraLeaves != null)
            {
                foreach (ExtraLeaf extra in extraLeaves)
                {
                    if (extra == null || extra.leafRigidbody == null) continue;

                    AddLeaf(extra.leafRigidbody, extra.closedAngle, extra.mirror < 0f ? -1f : 1f);
                }
            }
        }

        private void AddLeaf(Rigidbody body, float closedYaw, float mirror)
        {
            if (body == null) return;

            // The only continuous mode a kinematic body supports. It lets the leaf's own motion be
            // swept, so a fast swing meets a player instead of skipping past their surface.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Leaf leaf = new Leaf { Body = body, ClosedYaw = closedYaw, Mirror = mirror };
            CacheLeafGeometry(leaf);

            _leaves.Add(leaf);
            _hasLeafBox |= leaf.HasBox;
        }

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += OnStateChanged;

            // The doorway always stays on the navmesh. Carving it away is what used to make a
            // closed door unreachable — the monster now walks up to it and forces it open instead.
            navMeshObstacle.enabled = false;

            _targetAngle = AngleFor(_state.Value);
            ApplyAngle(_targetAngle, snap: true);

            // Only the server has a monster to walk anything.
            if (!IsServer) return;

            NavMeshRebuildNotifier.OnRebuilt += NavMeshRebuildNotifier_OnRebuilt;

            // Scene-placed doors already have the baked navmesh round them. A door in a room that
            // SpawnRooms just placed has nothing under its room side until the rebuild, so a
            // missing side is not worth a warning yet.
            EnsureDoorwayConnected(warnIfNoNavMesh: false);

            CreateOpenLeafObstacle();
        }

        /// <summary>
        /// An open leaf sticks most of a metre out from the wall, and the navmesh has no idea it is
        /// there — the doors are left out of the bake so a shut one never blocks the doorway. The
        /// monster walked straight through open leaves, above all the one it had just forced,
        /// which swings out on exactly the side it is heading into.
        ///
        /// <para>A carving obstacle on the leaf, switched on only while the door stands open and
        /// still: shut, it would carve the doorway itself, and moving, the carve lags behind.</para>
        ///
        /// <para>A flap needs none: it lifts over its own hinge line and leaves nothing standing out
        /// into the way.</para>
        /// </summary>
        private void CreateOpenLeafObstacle()
        {
            if (!IsVerticalHinge) return;

            foreach (Leaf leaf in _leaves)
            {
                leaf.OpenObstacle = CreateOpenLeafObstacle(leaf.Body);
            }
        }

        private NavMeshObstacle CreateOpenLeafObstacle(Rigidbody body)
        {
            BoxCollider leafBox = body.GetComponent<BoxCollider>();
            if (leafBox == null) return null;

            Vector3 centre = leafBox.center;
            Vector3 size = leafBox.size;

            // The long horizontal axis runs from the hinge, at the leaf's origin, out to the latch.
            int axis = Mathf.Abs(size.x) >= Mathf.Abs(size.z) ? 0 : 2;

            float scale = Mathf.Abs(leafBox.transform.lossyScale[axis]);
            float clearance = scale > 0.0001f ? openLeafHingeClearance / scale : 0f;

            float length = Mathf.Abs(size[axis]);
            float kept = Mathf.Max(0.05f, length - clearance);
            float outward = centre[axis] < 0f ? -1f : 1f;

            centre[axis] += outward * (length - kept) * 0.5f;
            size[axis] = kept;

            NavMeshObstacle obstacle = leafBox.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = centre;
            obstacle.size = size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            obstacle.enabled = false;

            return obstacle;
        }

        private void UpdateOpenLeafObstacle()
        {
            bool carve = _state.Value != DoorState.Closed && !IsSwinging;

            foreach (Leaf leaf in _leaves)
            {
                if (leaf.OpenObstacle == null || leaf.OpenObstacle.enabled == carve) continue;

                leaf.OpenObstacle.enabled = carve;
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateChanged;

            NavMeshRebuildNotifier.OnRebuilt -= NavMeshRebuildNotifier_OnRebuilt;
            RemoveFallbackLink();
        }

        private void NavMeshRebuildNotifier_OnRebuilt()
        {
            if (!IsServer) return;

            EnsureDoorwayConnected(warnIfNoNavMesh: true);
        }

        /// <summary>
        /// Makes sure the monster can path through this doorway, and links it if it cannot.
        ///
        /// <para>The navmesh is eroded by the agent radius from both jambs, and a doorway about as
        /// wide as the leaf leaves a strip only a voxel or two across. Whether that strip survives
        /// depends on how the doorway lines up with the voxel grid — and SpawnRooms drops rooms in
        /// random slots and rebuilds at runtime, so it changed from door to door and from match to
        /// match. A doorway that lost it cut the room off: every path in ended at the frame, and the
        /// monster stood there. The link is only added where the strip is actually missing.</para>
        /// </summary>
        private void EnsureDoorwayConnected(bool warnIfNoNavMesh)
        {
            RemoveFallbackLink();

            if (!_hasLeafBox) return;

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID,
                areaMask = NavMesh.AllAreas
            };

            Vector3 centre = DoorwayCentre;
            Vector3 reach = FlatNormal() * navMeshCheckReach;

            bool hasFront = NavMesh.SamplePosition(centre + reach, out NavMeshHit front, NavMeshSampleRadius, filter);
            bool hasBack = NavMesh.SamplePosition(centre - reach, out NavMeshHit back, NavMeshSampleRadius, filter);

            if (!hasFront || !hasBack)
            {
                if (warnIfNoNavMesh)
                {
                    Debug.LogWarning($"{name}: sem NavMesh {(hasFront ? "atras" : "na frente")} da porta em {centre} — " +
                                     "o monstro nao tem como usar esta porta.", this);
                }

                return;
            }

            if (IsConnectedThroughDoorway(front.position, back.position, filter)) return;

            _fallbackLink = NavMesh.AddLink(new NavMeshLinkData
            {
                startPosition = front.position,
                endPosition = back.position,
                width = 0f,
                bidirectional = true,
                costModifier = -1f,
                area = 0,
                agentTypeID = filter.agentTypeID
            });
        }

        private static bool IsConnectedThroughDoorway(Vector3 from, Vector3 to, NavMeshQueryFilter filter)
        {
            _connectionProbe ??= new NavMeshPath();

            if (!NavMesh.CalculatePath(from, to, filter, _connectionProbe)) return false;
            if (_connectionProbe.status != NavMeshPathStatus.PathComplete) return false;

            int count = _connectionProbe.GetCornersNonAlloc(ProbeCorners);

            // Filled the buffer: a path that winding is not going straight through a doorway.
            if (count >= ProbeCorners.Length) return false;

            float length = 0f;

            for (int i = 1; i < count; i++)
            {
                length += Vector3.Distance(ProbeCorners[i - 1], ProbeCorners[i]);
            }

            return length <= Vector3.Distance(from, to) + ThroughDoorwaySlack;
        }

        private void RemoveFallbackLink()
        {
            if (NavMesh.IsLinkValid(_fallbackLink))
            {
                NavMesh.RemoveLink(_fallbackLink);
            }

            _fallbackLink = default;
        }

        /// <summary>
        /// Says which door it is, once, as soon as the scene loads. A door with no NetworkObject
        /// above it never spawns, and the first sign used to be an RpcException when someone tried
        /// to open it.
        /// </summary>
        private void Start()
        {
            if (GetComponentInParent<NetworkObject>(true) != null) return;

            Debug.LogError(NotNetworkedMessage, this);
        }

        private string NotNetworkedMessage =>
            $"{name}: esta porta não tem NetworkObject acima dela, então não entra na rede e não abre. " +
            "Com a cena aberta, rode Tools > TCC > Portas > Adicionar NetworkObject nas portas sem rede e salve.";

        public bool CanInteract(GameObject interactor) => true;

        public bool Interact(GameObject playerInteractor)
        {
            if (!IsSpawned)
            {
                Debug.LogError(NotNetworkedMessage, this);
                return false;
            }

            // Where the player looks decides, when they stand right in the doorway, which way a
            // closing leaf shoves them: backwards, away from the door they are looking at.
            UnityEngine.Camera view = UnityEngine.Camera.main;
            Vector3 facing = view != null ? view.transform.forward : playerInteractor.transform.forward;

            // Sent from the closer's own machine: the server's copy of a remote body is kinematic
            // and carries no velocity, and whether they are already on their way out decides
            // whether they need shoving at all.
            Vector3 velocity = playerInteractor.TryGetComponent(out Rigidbody body) ? body.linearVelocity : Vector3.zero;

            RequestToggleServerRpc(playerInteractor.transform.position, facing, velocity);
            return true;
        }

        /// <summary>
        /// Where <paramref name="ray"/> enters the doorway: the space the leaves fill when shut,
        /// whether or not they are there now. A ray that starts inside it is not looking at it — a
        /// player standing in the doorway is looking somewhere else.
        /// </summary>
        public bool TryRaycastDoorway(Ray ray, float maxDistance, out float distance, out Collider leaf)
        {
            distance = float.MaxValue;
            leaf = null;

            foreach (Leaf candidate in _leaves)
            {
                if (!candidate.HasBox) continue;

                LeafBoxPose(candidate, closedAngle, out Vector3 centre, out Quaternion rotation);

                Quaternion toBox = Quaternion.Inverse(rotation);
                Vector3 extents = candidate.BoxHalfExtents + Vector3.one * DoorwayTargetPadding;

                if (!RayEntersBox(toBox * (ray.origin - centre), toBox * ray.direction, extents, out float enter)) continue;
                if (enter > maxDistance || enter >= distance) continue;

                distance = enter;
                leaf = candidate.Box;
            }

            return leaf != null;
        }

        /// <summary>Slab test against a box centred on the origin. False when the ray starts inside it.</summary>
        private static bool RayEntersBox(Vector3 origin, Vector3 direction, Vector3 extents, out float enter)
        {
            float near = float.MinValue;
            float far = float.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 0.000001f)
                {
                    if (Mathf.Abs(origin[axis]) > extents[axis])
                    {
                        enter = 0f;
                        return false;
                    }

                    continue;
                }

                float first = (-extents[axis] - origin[axis]) / direction[axis];
                float second = (extents[axis] - origin[axis]) / direction[axis];

                near = Mathf.Max(near, Mathf.Min(first, second));
                far = Mathf.Min(far, Mathf.Max(first, second));
            }

            enter = near;
            return near > 0f && near <= far;
        }

        [Rpc(SendTo.Server)]
        private void RequestToggleServerRpc(Vector3 playerPosition, Vector3 playerFacing, Vector3 playerVelocity,
            RpcParams rpcParams = default)
        {
            // The monster is holding this door — either it just forced it and shutting it straight
            // back would be a door-slamming contest, or it sabotaged the whole floor. Rattle it so
            // the refusal reads as the door being held, not as the input being dropped.
            if (IsLocked)
            {
                RattleBlocked();
                return;
            }

            // Already shoving people out of the way of a close: pressing again is the same close.
            if (_closeAfterShoveTime >= 0f) return;

            ulong sender = rpcParams.Receive.SenderClientId;

            bool moved = _state.Value == DoorState.Closed
                ? TryOpenAwayFrom(playerPosition, sender)
                : TryCloseShoving(sender, playerFacing, playerVelocity);

            // Someone is in the way. Same rattle: the door is stuck on them.
            if (!moved)
            {
                RattleBlocked();
                return;
            }

            if (useNoise != null) useNoise.Emit();
        }

        /// <summary>
        /// Server-side open with no interactor behind it — what the monster uses after shouldering
        /// the door. Never closes: forcing a door only ever opens it.
        ///
        /// <para>Skips the obstruction check on purpose. The monster is the one thing that gets
        /// through regardless, and anyone on the far side takes the door in the face.</para>
        /// </summary>
        public void ForceOpenFrom(Vector3 fromPosition)
        {
            if (!IsServer) return;

            // Hold the door shut to interaction whether or not it was already open: the monster is
            // right there either way. Max, not assignment: this used to cut a twelve-second
            // sabotage hold down to three.
            _lockedUntilTime = Mathf.Max(_lockedUntilTime, Time.time + monsterLockSeconds);
            _closeWhenClear = false;

            if (_state.Value != DoorState.Closed) return;

            _openerClientId = NoClient;
            _state.Value = SideAwayFrom(fromPosition);
            WorldSound.Play(openSound, DoorwayCentre, NetworkObject);
        }

        /// <summary>
        /// The monster's door sabotage: slam it shut and hold it. Nothing calls Restore on a
        /// timer, so the hold has to expire by itself — players get the door back when it does.
        /// Every door on the map gets this at once, so it must never close on whoever happens to be
        /// standing in a doorway; those doors wait and shut the moment it clears.
        /// </summary>
        public void CloseAndLock(float seconds)
        {
            if (!IsServer) return;

            _lockedUntilTime = Time.time + seconds;

            if (_state.Value == DoorState.Closed) return;

            _closeWhenClear = !TryClose();
        }

        public void ClearLock()
        {
            if (!IsServer) return;

            _lockedUntilTime = 0f;
            _closeWhenClear = false;
        }

        public void GetApproachPose(Vector3 fromPosition, float standOff, out Vector3 standPoint, out Vector3 facing)
        {
            Vector3 centre = DoorwayCentre;
            Vector3 normal = FlatNormal();

            // The side of the doorway plane the caller is on is the side it stands on.
            float side = Vector3.Dot(normal, fromPosition - centre) >= 0f ? 1f : -1f;

            standPoint = centre + normal * (side * standOff);
            facing = -normal * side;
        }

        /// <summary>Server-side: the rattle for everyone near the door, and the event for every client.</summary>
        private void RattleBlocked()
        {
            WorldSound.Play(blockedSound, transform.position, NetworkObject);
            NotifyBlockedRpc();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void NotifyBlockedRpc()
        {
            OnDoorBlocked?.Invoke(transform.position);
        }

        private bool TryOpenAwayFrom(Vector3 fromPosition, ulong openerClientId)
        {
            if (!IsServer || _state.Value != DoorState.Closed) return false;

            DoorState away = SideAwayFrom(fromPosition);
            float openAngle = AngleFor(away);

            // Players on the far side are fine — the swing knocks them over, which is the point of
            // the impact profile. The monster, or anyone the profile cannot knock down, is not:
            // the leaf would pass through them. No trying the other side either — that swing comes
            // straight back at whoever opened it.
            if (ScanSweep(_currentAngle, openAngle, SwingSide(openAngle)) == Occupant.Solid) return false;

            _openerClientId = openerClientId;
            _state.Value = away;
            WorldSound.Play(openSound, DoorwayCentre, NetworkObject);
            return true;
        }

        /// <summary>
        /// A player asked to close the door. Players in the leaf's path no longer stop it — a
        /// refusal nobody could see the reason for. Each one is shoved out of the way, to the side
        /// they are on, and the leaf follows once the shove is over. The monster in the doorway, or
        /// a player with a wall on both sides, still makes it rattle.
        ///
        /// <para>Whoever shut it while already heading out of its path is not shoved: the shove
        /// took over their run for a moment, which in a chase is the worst moment for it. The leaf
        /// waits just as long as they need to get clear on their own.</para>
        /// </summary>
        private bool TryCloseShoving(ulong closerClientId, Vector3 closerFacing, Vector3 closerVelocity)
        {
            if (_state.Value == DoorState.Closed) return true;

            _inTheWay.Clear();
            Occupant occupant = ScanSweep(_currentAngle, closedAngle, 0f, _inTheWay, shoving: true);

            if (occupant == Occupant.Solid) return false;

            if (occupant == Occupant.None)
            {
                _state.Value = DoorState.Closed;
                return true;
            }

            // The way out of the leaf's path: deeper into the side the open leaf stands in, for a
            // door. A flap stands on neither side, so out of either end of the passage under it.
            Vector3 intoSwing;

            if (IsVerticalHinge)
            {
                float swingSide = SwingSide(_currentAngle);
                if (swingSide == 0f) return false;

                intoSwing = FlatNormal() * swingSide;
            }
            else
            {
                intoSwing = FlatNormal();
            }

            // Every shove is worked out before any is sent: if one player has nowhere to go, the
            // door stays open and nobody is moved for nothing. 0 marks someone leaving on their own.
            _shoveSides.Clear();
            float wait = 0f;

            foreach (PlayerKnockdown player in _inTheWay)
            {
                if (!player.TryGetComponent(out CapsuleCollider capsule)) return false;

                bool isCloser = player.OwnerClientId == closerClientId;
                Vector3 facing = isCloser ? closerFacing : Vector3.zero;
                float preferred = ShoveSideFor(capsule, intoSwing, facing);

                if (isCloser && IsLeavingOnTheirOwn(capsule, intoSwing, preferred, closerVelocity, out float leaveSeconds))
                {
                    _shoveSides.Add(0f);
                    wait = Mathf.Max(wait, leaveSeconds);
                    continue;
                }

                if (CanShove(capsule, intoSwing, preferred)) _shoveSides.Add(preferred);
                else if (CanShove(capsule, intoSwing, -preferred)) _shoveSides.Add(-preferred);
                else return false;

                wait = Mathf.Max(wait, shoveSeconds);
            }

            for (int i = 0; i < _inTheWay.Count; i++)
            {
                PlayerKnockdown player = _inTheWay[i];

                // Leaving or shoved, the leaf ignores them until it shuts: the server's copy of a
                // remote body is behind where their own machine already has it.
                _shoved.Add(player);

                if (_shoveSides[i] == 0f) continue;

                ShoveRpc(player.NetworkObject, intoSwing, _shoveSides[i],
                    RpcTarget.Single(player.OwnerClientId, RpcTargetUse.Temp));
            }

            _closeAfterShoveTime = Time.time + wait;
            return true;
        }

        /// <summary>
        /// True when <paramref name="velocity"/> already carries the body out of the leaf's path,
        /// the way the shove would have, fast enough that the leaf only waits a moment for it.
        /// <paramref name="seconds"/> is how long it needs.
        /// </summary>
        private bool IsLeavingOnTheirOwn(CapsuleCollider capsule, Vector3 intoSwing, float side, Vector3 velocity,
            out float seconds)
        {
            seconds = 0f;

            velocity.y = 0f;
            float speed = Vector3.Dot(velocity, intoSwing * side);
            if (speed < LeavingSpeed) return false;

            seconds = ShoveDistance(capsule, intoSwing, side) / speed;
            return seconds <= MaxLeavingWait;
        }

        /// <summary>
        /// +1 to shove a player deeper into the side the leaves open to, -1 out to the other side of
        /// the doorway: the side they are on. Someone standing in the doorway itself is on neither,
        /// so the closer, who is looking at the door, goes backwards — into the room they are
        /// shutting themselves in.
        /// </summary>
        private float ShoveSideFor(CapsuleCollider capsule, Vector3 intoSwing, Vector3 facing)
        {
            Vector3 offset = capsule.bounds.center - DoorwayCentre;
            offset.y = 0f;

            float across = Vector3.Dot(offset, intoSwing);

            facing.y = 0f;
            float look = facing.sqrMagnitude > 0.0001f ? Vector3.Dot(facing.normalized, intoSwing) : 0f;

            bool inDoorway = Mathf.Abs(across) < DoorwayHalfDepth + BodyRadius(capsule) + obstructionMargin;

            if (inDoorway && Mathf.Abs(look) > 0.2f) return look > 0f ? -1f : 1f;

            return across >= 0f ? 1f : -1f;
        }

        private bool CanShove(CapsuleCollider capsule, Vector3 intoSwing, float side)
        {
            float distance = ShoveDistance(capsule, intoSwing, side);
            if (distance <= 0f) return true;

            return FreeShoveDistance(capsule, intoSwing * side, distance + ShoveSkin) >= distance;
        }

        /// <summary>
        /// How far a body must move along <paramref name="intoSwing"/> times <paramref name="side"/>
        /// to be clear of every leaf's closing sweep. Into the swing side the sweep is a half-disc
        /// round each hinge, as wide as the leaf; on the other side it is only the shut leaf's
        /// thickness.
        /// </summary>
        private float ShoveDistance(CapsuleCollider capsule, Vector3 intoSwing, float side)
        {
            if (!IsVerticalHinge) return SweptClearance(capsule, intoSwing * side);

            Vector3 centre = capsule.bounds.center;
            float radius = BodyRadius(capsule);
            float needed = 0f;

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                Vector3 fromHinge = centre - leaf.Body.position;
                fromHinge.y = 0f;

                float across = Vector3.Dot(fromHinge, intoSwing);
                float alongSquared = Mathf.Max(0f, fromHinge.sqrMagnitude - across * across);
                float reach = leaf.Reach + radius + obstructionMargin;

                // Past the leaf's far edge: it never reaches them.
                if (alongSquared >= reach * reach) continue;

                needed = Mathf.Max(needed, side > 0f
                    ? Mathf.Sqrt(reach * reach - alongSquared) - across
                    : across + leaf.HalfThickness + radius + obstructionMargin);
            }

            return needed;
        }

        /// <summary>
        /// How far a body must move along <paramref name="direction"/> to be clear of every pose the
        /// leaves pass through on the way shut. A lifting flap sweeps nothing so simple as a door's
        /// half-disc, so its path is walked a few centimetres at a time instead.
        /// </summary>
        private float SweptClearance(CapsuleCollider capsule, Vector3 direction)
        {
            Transform body = capsule.transform;

            for (float distance = 0f; distance < MaxSweptClearance; distance += SweptClearanceStep)
            {
                if (OverlapsClosingSweep(capsule, body.position + direction * distance, body.rotation))
                {
                    continue;
                }

                return distance > 0f ? distance + obstructionMargin : 0f;
            }

            return MaxSweptClearance;
        }

        private bool OverlapsClosingSweep(CapsuleCollider capsule, Vector3 position, Quaternion rotation)
        {
            float span = Mathf.Abs(closedAngle - _currentAngle);
            int steps = Mathf.Max(1, Mathf.CeilToInt(span / sweepSampleDegrees));

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                Vector3 hinge = leaf.Body.transform.position;

                for (int i = 0; i <= steps; i++)
                {
                    float angle = Mathf.Lerp(_currentAngle, closedAngle, i / (float)steps);

                    if (Physics.ComputePenetration(capsule, position, rotation,
                            leaf.Box, hinge, LeafRotationAt(leaf, angle), out _, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>How far the body can travel along <paramref name="direction"/> before the level stops it.</summary>
        private float FreeShoveDistance(CapsuleCollider capsule, Vector3 direction, float distance)
        {
            Bounds bounds = capsule.bounds;
            float radius = Mathf.Max(0.05f, BodyRadius(capsule) - ShoveSkin);

            Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y + radius + ShoveStepUp, bounds.center.z);
            Vector3 top = new Vector3(bounds.center.x, Mathf.Max(bottom.y, bounds.max.y - radius), bounds.center.z);

            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, _shoveHits, distance,
                shoveBlockingLayers, QueryTriggerInteraction.Ignore);

            float free = distance;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _shoveHits[i];

                // Something they already overlap is not something the shove runs into, and this
                // door's own leaves are about to swing away. Only the leaves: a counter carries its
                // flap's Door on the counter itself, and the counter does not move.
                if (hit.distance <= 0f) continue;
                if (IsOwnLeaf(hit.collider)) continue;

                free = Mathf.Min(free, hit.distance);
            }

            return free;
        }

        private bool IsOwnLeaf(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null) return false;

            foreach (Leaf leaf in _leaves)
            {
                if (leaf.Body == body) return true;
            }

            return false;
        }

        private static float BodyRadius(CapsuleCollider capsule)
        {
            Vector3 extents = capsule.bounds.extents;
            return Mathf.Min(extents.x, extents.z);
        }

        private float LeafHalfThickness => _leaves.Count > 0 ? _leaves[0].HalfThickness : 0f;

        // How far either side of the doorway plane counts as standing in the doorway itself. A
        // flap's passage has no such plane: the middle of it is the doorway.
        private float DoorwayHalfDepth => IsVerticalHinge ? LeafHalfThickness : 0f;

        /// <summary>
        /// Runs on the shoved player's own machine, which moves its own body. Worked out again from
        /// where the body really is, which the server only sees a moment late.
        /// </summary>
        [Rpc(SendTo.SpecifiedInParams)]
        private void ShoveRpc(NetworkObjectReference player, Vector3 intoSwing, float side, RpcParams rpcParams)
        {
            if (!player.TryGet(out NetworkObject playerObject) || !playerObject.IsOwner) return;
            if (!playerObject.TryGetComponent(out Rigidbody body)) return;
            if (!playerObject.TryGetComponent(out CapsuleCollider capsule)) return;

            Vector3 direction = intoSwing * side;
            float distance = ShoveDistance(capsule, intoSwing, side);
            if (distance <= 0f) return;

            distance = FreeShoveDistance(capsule, direction, distance + ShoveSkin);

            if (_shoveRoutine != null) StopCoroutine(_shoveRoutine);
            _shoveRoutine = StartCoroutine(Shove(body, direction * distance));
        }

        /// <summary>
        /// PlayerMovement rewrites the body's velocity every physics step, so a push as a force would
        /// be undone at once. The shove places the body instead, each step from where it started, so
        /// input in between never adds more than a single step's worth.
        /// </summary>
        private IEnumerator Shove(Rigidbody body, Vector3 offset)
        {
            Vector3 start = body.position;
            float elapsed = 0f;

            while (elapsed < shoveSeconds)
            {
                yield return WaitForFixed;

                if (body == null) break;

                elapsed += Time.fixedDeltaTime;

                // Eased out: it hits hard and settles.
                float t = Mathf.Clamp01(elapsed / shoveSeconds);
                Vector3 target = start + offset * (1f - (1f - t) * (1f - t));
                target.y = body.position.y;

                body.MovePosition(target);
            }

            _shoveRoutine = null;
        }

        /// <summary>Server-side: the shoves are over, so the leaf goes.</summary>
        private void CloseAfterShove()
        {
            if (_closeAfterShoveTime < 0f || Time.time < _closeAfterShoveTime) return;

            _closeAfterShoveTime = -1f;

            if (_state.Value == DoorState.Closed) return;

            // The monster got there first and is holding it open.
            if (IsLocked)
            {
                _shoved.Clear();
                return;
            }

            _state.Value = DoorState.Closed;
        }

        private bool TryClose()
        {
            if (_state.Value == DoorState.Closed) return true;

            // Nobody at all may be in the path of a closing leaf. It is what pinned players against
            // the frame, and a body the solver cannot resolve gets ejected — usually straight
            // through the leaf.
            if (ScanSweep(_currentAngle, closedAngle, 0f) != Occupant.None) return false;

            _state.Value = DoorState.Closed;
            return true;
        }

        private DoorState SideAwayFrom(Vector3 fromPosition)
        {
            if (!opensBothWays) return DoorState.OpenSideA;

            Vector3 toOpener = fromPosition - doorPivot.position;
            toOpener.y = 0f;

            float side = Vector3.Dot(doorPivot.forward, toOpener.normalized);

            return side > 0f ? DoorState.OpenSideB : DoorState.OpenSideA;
        }

        private float AngleFor(DoorState state)
        {
            switch (state)
            {
                case DoorState.OpenSideA: return openSideAAngle;
                case DoorState.OpenSideB: return openSideBAngle;
                default: return closedAngle;
            }
        }

        private void OnStateChanged(DoorState previous, DoorState current)
        {
            if (current == DoorState.Closed && previous != DoorState.Closed)
            {
                _lastOpenState = previous;
            }

            _targetAngle = AngleFor(current);
            _swingSign = Mathf.Sign(_targetAngle - _currentAngle);
            _hitThisSwing.Clear();

            // Back open, bounced or forced: whoever was shoved for the close counts again.
            if (current != DoorState.Closed) _shoved.Clear();
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                TryCloseWhenClear();
                CloseAfterShove();
                UpdateOpenLeafObstacle();
            }

            UpdateClosedBlocker();

            if (!IsSwinging) return;

            float step = Mathf.Max(1f, openDegreesPerSecond) * Time.fixedDeltaTime;
            float next = Mathf.MoveTowards(_currentAngle, _targetAngle, step);

            if (IsServer && !ResolveOccupants(next)) return;

            ApplyAngle(next);

            if (!IsSwinging)
            {
                _swingSign = 0f;
                _hitThisSwing.Clear();
                _shoved.Clear();

                // Every peer runs the swing; only the server's copy sends the sound.
                if (IsServer && Mathf.Approximately(_targetAngle, closedAngle))
                {
                    WorldSound.Play(closeSound, DoorwayCentre, NetworkObject);
                }
            }
        }

        /// <summary>
        /// Server-side, once per physics step of a swing, against the pose the leaf is about to
        /// take. Returns false when the step must not be applied.
        ///
        /// <para>Replaces the OnCollisionEnter relay. On the server a remote player's body is a
        /// kinematic copy, and kinematic against kinematic raises no collision callbacks — so the
        /// door only ever knocked down the host. A query sees every player.</para>
        /// </summary>
        private bool ResolveOccupants(float nextAngle)
        {
            bool closing = Mathf.Approximately(_targetAngle, closedAngle);

            if (closing)
            {
                // Someone stepped into the doorway after the close began: bounce back open rather
                // than crush them into the frame.
                if (ScanLeafAt(nextAngle, null, 0f) == Occupant.None) return true;

                _state.Value = _lastOpenState;
                RattleBlocked();

                if (IsLocked) _closeWhenClear = true;

                return false;
            }

            _knockableBuffer.Clear();
            ScanLeafAt(nextAngle, _knockableBuffer, SwingSide(_targetAngle));

            foreach (PlayerKnockdown knockdown in _knockableBuffer)
            {
                Leaf leaf = NearestLeaf(knockdown.transform.position, nextAngle);

                // Whoever opened the door is never knocked down by it, whatever the geometry says
                // about a body that ran after the leaf and caught up with it.
                if (knockdown.OwnerClientId == _openerClientId) continue;

                // Touching the leaf is not being hit by it. Whoever opened the door and ran after
                // it catches up with the back of a leaf that is moving away from them.
                if (!IsInFrontOfSwing(leaf, knockdown.transform.position, nextAngle)) continue;
                if (!_hitThisSwing.Add(knockdown)) continue;

                KnockDown(leaf, knockdown);
            }

            // Opening is never stopped: the monster is kinematic and the leaf simply passes it,
            // and anything else was refused before the swing started.
            return true;
        }

        /// <summary>
        /// True when <paramref name="position"/> is on the face of the leaf that is travelling into
        /// it — the side the door actually hits. The leaf's plane runs through the hinge along the
        /// leaf, and it sweeps towards whichever side its motion points to.
        /// </summary>
        private bool IsInFrontOfSwing(Leaf leaf, Vector3 position, float angle)
        {
            if (leaf == null || !leaf.HasBox) return true;

            if (!IsVerticalHinge) return IsInFrontOfFlap(leaf, position, angle);

            Vector3 along = leaf.BoxOffset;
            along.y = 0f;

            if (along.sqrMagnitude < 0.0001f || _swingSign == 0f) return true;

            // A mirrored leaf turns the other way for the same change of angle.
            Vector3 leafAxis = LeafRotationAt(leaf, angle) * along.normalized;
            Vector3 travel = Vector3.Cross(Vector3.up * (_swingSign * leaf.Mirror), leafAxis);

            Vector3 fromHinge = position - leaf.Body.position;
            fromHinge.y = 0f;

            return Vector3.Dot(fromHinge, travel) > 0f;
        }

        /// <summary><see cref="IsInFrontOfSwing"/> for a leaf turning round any axis.</summary>
        private bool IsInFrontOfFlap(Leaf leaf, Vector3 position, float angle)
        {
            Vector3 axis = HingeAxisWorld(leaf);

            Vector3 leafAxis = LeafRotationAt(leaf, angle) * leaf.BoxOffset;
            leafAxis -= axis * Vector3.Dot(leafAxis, axis);

            if (leafAxis.sqrMagnitude < 0.0001f || _swingSign == 0f) return true;

            Vector3 travel = Vector3.Cross(axis * (_swingSign * leaf.Mirror), leafAxis.normalized);

            return Vector3.Dot(position - leaf.Body.position, travel) > 0f;
        }

        private void KnockDown(Leaf leaf, PlayerKnockdown knockdown)
        {
            Vector3 hinge = leaf.Body.position;
            Vector3 radial = knockdown.transform.position - hinge;

            // The lever arm is measured square to the hinge: across the floor for a door, up off
            // the counter for a flap.
            Vector3 axis = IsVerticalHinge ? Vector3.up : HingeAxisWorld(leaf);

            if (IsVerticalHinge) radial.y = 0f;
            else radial -= axis * Vector3.Dot(radial, axis);

            if (radial.sqrMagnitude < 0.0001f) return;

            // Linear speed of the leaf at the player: angular speed times the lever arm.
            float leafSpeed = openDegreesPerSecond * Mathf.Deg2Rad * radial.magnitude;
            if (leafSpeed < impactProfile.minimumSpeed) return;

            // The leaf sweeps perpendicular to the lever arm, so that is where it throws the player.
            Vector3 direction = Vector3.Cross(axis * (_swingSign * leaf.Mirror), radial).normalized;

            knockdown.ApplyImpact(impactProfile, direction);
        }

        /// <summary>The leaf whose middle is closest to <paramref name="position"/> — the one that hit them.</summary>
        private Leaf NearestLeaf(Vector3 position, float angle)
        {
            Leaf nearest = _leaves.Count > 0 ? _leaves[0] : null;
            float nearestDistance = float.MaxValue;

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                LeafBoxPose(leaf, angle, out Vector3 centre, out _);

                Vector3 offset = position - centre;
                offset.y = 0f;

                float distance = offset.sqrMagnitude;
                if (distance >= nearestDistance) continue;

                nearest = leaf;
                nearestDistance = distance;
            }

            return nearest;
        }

        /// <summary>
        /// Every peer: the blocker goes the moment the door starts to open, and comes back once it
        /// has shut and nobody is inside it — someone crouched under a flap that came down over
        /// them would otherwise be thrown out of a collider appearing round them.
        /// </summary>
        private void UpdateClosedBlocker()
        {
            if (closedBlocker == null) return;

            bool shut = _state.Value == DoorState.Closed && !IsSwinging;

            if (!shut)
            {
                if (closedBlocker.enabled) closedBlocker.enabled = false;
                return;
            }

            if (closedBlocker.enabled || IsBlockerOccupied()) return;

            closedBlocker.enabled = true;
        }

        private bool IsBlockerOccupied()
        {
            Transform box = closedBlocker.transform;
            Vector3 scale = box.lossyScale;
            Vector3 halfExtents = Vector3.Scale(closedBlocker.size,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;

            return Physics.CheckBox(box.TransformPoint(closedBlocker.center), halfExtents, box.rotation,
                occupantLayers, QueryTriggerInteraction.Ignore);
        }

        private void TryCloseWhenClear()
        {
            if (!_closeWhenClear) return;

            if (!IsLocked || _state.Value == DoorState.Closed)
            {
                _closeWhenClear = false;
                return;
            }

            if (Time.time < _nextCloseAttemptTime) return;

            _nextCloseAttemptTime = Time.time + CloseRetrySeconds;

            if (TryClose())
            {
                _closeWhenClear = false;
            }
        }

        private void ApplyAngle(float angle, bool snap = false)
        {
            _currentAngle = angle;

            foreach (Leaf leaf in _leaves)
            {
                if (snap)
                {
                    leaf.Body.transform.localRotation = LeafLocalRotation(leaf, angle);
                    leaf.Body.rotation = leaf.Body.transform.rotation;
                    continue;
                }

                leaf.Body.MoveRotation(LeafRotationAt(leaf, angle));
            }
        }

        // ---- Leaf geometry ------------------------------------------------------------------

        /// <summary>
        /// A leaf's local Y rotation when the main leaf is at <paramref name="angle"/>: its own
        /// shut rotation, plus the main leaf's swing — reversed for a mirrored leaf.
        /// </summary>
        private float YawFor(Leaf leaf, float angle)
        {
            return leaf.ClosedYaw + leaf.Mirror * (angle - closedAngle);
        }

        private bool IsVerticalHinge => hingeAxis == HingeAxis.Up;

        private Vector3 HingeAxisLocal
        {
            get
            {
                switch (hingeAxis)
                {
                    case HingeAxis.Right: return Vector3.right;
                    case HingeAxis.Forward: return Vector3.forward;
                    default: return Vector3.up;
                }
            }
        }

        /// <summary>
        /// A leaf's local rotation when the main leaf is at <paramref name="angle"/>: turned that far
        /// round the hinge axis. Euler for the upright axis, exactly as doors always were.
        /// </summary>
        private Quaternion LeafLocalRotation(Leaf leaf, float angle)
        {
            float turn = YawFor(leaf, angle);

            return IsVerticalHinge
                ? Quaternion.Euler(0f, turn, 0f)
                : Quaternion.AngleAxis(turn, HingeAxisLocal);
        }

        /// <summary>The hinge axis in world space, through the leaf's own origin.</summary>
        private Vector3 HingeAxisWorld(Leaf leaf)
        {
            Transform parent = leaf.Body.transform.parent;
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;

            return parentRotation * HingeAxisLocal;
        }

        /// <summary>Middle of the doorway at floor height — between both halves of a double door.</summary>
        private Vector3 DoorwayCentre
        {
            get
            {
                Vector3 sum = Vector3.zero;
                int count = 0;

                foreach (Leaf leaf in _leaves)
                {
                    if (!leaf.HasBox) continue;

                    LeafBoxPose(leaf, closedAngle, out Vector3 centre, out _);
                    sum += centre;
                    count++;
                }

                if (count == 0) return transform.position;

                Vector3 middle = sum / count;
                middle.y = doorPivot.position.y;

                return middle;
            }
        }

        private Vector3 FlatNormal()
        {
            Vector3 normal = doorPivot.forward;
            normal.y = 0f;

            return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
        }

        private void CacheLeafGeometry(Leaf leaf)
        {
            BoxCollider leafBox = leaf.Body.GetComponent<BoxCollider>();

            if (leafBox == null)
            {
                Debug.LogWarning($"{name}: the door leaf {leaf.Body.name} has no BoxCollider, so nothing can check whether the doorway is clear.", this);
                return;
            }

            Transform leafTransform = leaf.Body.transform;
            Transform box = leafBox.transform;

            leaf.Box = leafBox;

            Quaternion toLeafSpace = Quaternion.Inverse(leafTransform.rotation);

            leaf.BoxOffset = toLeafSpace * (box.TransformPoint(leafBox.center) - leafTransform.position);
            leaf.BoxRotation = toLeafSpace * box.rotation;

            Vector3 scale = box.lossyScale;
            leaf.BoxHalfExtents = Vector3.Scale(leafBox.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;

            // A leaf stands upright, so of its two flat axes the longer runs from the hinge to the
            // latch and the shorter is its thickness.
            Vector3 flatOffset = new Vector3(leaf.BoxOffset.x, 0f, leaf.BoxOffset.z);
            leaf.Reach = flatOffset.magnitude + Mathf.Max(leaf.BoxHalfExtents.x, leaf.BoxHalfExtents.z);
            leaf.HalfThickness = Mathf.Min(leaf.BoxHalfExtents.x, leaf.BoxHalfExtents.z);

            leaf.HasBox = true;
        }

        private Quaternion LeafRotationAt(Leaf leaf, float angle)
        {
            Transform parent = leaf.Body.transform.parent;
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;

            return parentRotation * LeafLocalRotation(leaf, angle);
        }

        private void LeafBoxPose(Leaf leaf, float angle, out Vector3 centre, out Quaternion rotation)
        {
            Quaternion leafRotation = LeafRotationAt(leaf, angle);

            // The hinge is the leaf's own origin, so it stays put while the leaf turns.
            centre = leaf.Body.transform.position + leafRotation * leaf.BoxOffset;
            rotation = leafRotation * leaf.BoxRotation;
        }

        /// <summary>
        /// Which side of the doorway plane the leaves end up on at <paramref name="openAngle"/>:
        /// +1 along the door's forward, -1 against it. Worked out from the main leaf itself rather
        /// than from the sign of the angle, which depends on which way the leaf was modelled. The
        /// other half of a double door, mirrored, opens to the same side.
        /// </summary>
        private float SwingSide(float openAngle)
        {
            // A flap lifts over its hinge line and ends up on neither side of the passage.
            if (!IsVerticalHinge) return 0f;

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                LeafBoxPose(leaf, openAngle, out Vector3 centre, out _);

                return Vector3.Dot(FlatNormal(), centre - DoorwayCentre) >= 0f ? 1f : -1f;
            }

            return 0f;
        }

        /// <summary>
        /// Checks every pose from <paramref name="fromAngle"/> to <paramref name="toAngle"/>, not
        /// counting the pose the leaf is already in — whatever is touching it now is not in the
        /// way of it moving off.
        /// </summary>
        /// <param name="side">See <see cref="ScanLeafAt"/>.</param>
        /// <param name="players">See <see cref="ScanLeafAt"/>'s knockable.</param>
        /// <param name="shoving">See <see cref="ScanLeafAt"/>.</param>
        private Occupant ScanSweep(float fromAngle, float toAngle, float side,
            List<PlayerKnockdown> players = null, bool shoving = false)
        {
            float span = Mathf.Abs(toAngle - fromAngle);
            int steps = Mathf.Max(1, Mathf.CeilToInt(span / sweepSampleDegrees));

            Occupant worst = Occupant.None;

            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromAngle, toAngle, i / (float)steps);

                Occupant occupant = ScanLeafAt(angle, players, side, shoving);
                if (occupant > worst) worst = occupant;

                if (worst == Occupant.Solid) break;
            }

            return worst;
        }

        /// <summary>
        /// What the leaves would overlap with the main leaf at <paramref name="angle"/>. Players
        /// they can knock down are added to <paramref name="knockable"/> when one is given.
        /// </summary>
        /// <param name="side">
        /// 0 counts everyone. +1 or -1 counts only bodies on that side of the doorway plane — an
        /// opening leaf only ever moves into one side, and whoever opened it, leaning on it from the
        /// other side near the hinge, used to count as in its way and get knocked over by it.
        /// </param>
        /// <param name="shoving">
        /// Every standing player counts as one the leaf can deal with — a close a player asked for
        /// shoves them aside — not only those the impact profile can knock down.
        /// </param>
        private Occupant ScanLeafAt(float angle, List<PlayerKnockdown> knockable, float side, bool shoving = false)
        {
            if (!_hasLeafBox) return Occupant.None;

            Occupant worst = Occupant.None;

            Vector3 doorwayCentre = DoorwayCentre;
            Vector3 normal = FlatNormal();

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                Occupant occupant = ScanOneLeafAt(leaf, angle, knockable, side, doorwayCentre, normal, shoving);
                if (occupant > worst) worst = occupant;
            }

            return worst;
        }

        private Occupant ScanOneLeafAt(Leaf leaf, float angle, List<PlayerKnockdown> knockable, float side,
            Vector3 doorwayCentre, Vector3 normal, bool shoving)
        {
            LeafBoxPose(leaf, angle, out Vector3 centre, out Quaternion rotation);

            Vector3 halfExtents = leaf.BoxHalfExtents + Vector3.one * obstructionMargin;

            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, _overlapBuffer, rotation,
                occupantLayers, QueryTriggerInteraction.Ignore);

            Occupant worst = Occupant.None;

            for (int i = 0; i < count; i++)
            {
                if (side != 0f && Vector3.Dot(normal, _overlapBuffer[i].bounds.center - doorwayCentre) * side < 0f) continue;

                PlayerKnockdown knockdown = _overlapBuffer[i].GetComponentInParent<PlayerKnockdown>();

                // Already being shoved out of this close's way.
                if (knockdown != null && _shoved.Contains(knockdown)) continue;

                bool movable = knockdown != null && (shoving ? !knockdown.IsKnockedDown : CanKnockDown(knockdown));

                if (movable)
                {
                    if (knockable != null && !knockable.Contains(knockdown)) knockable.Add(knockdown);
                    if (worst == Occupant.None) worst = Occupant.Knockable;
                    continue;
                }

                worst = Occupant.Solid;
            }

            return worst;
        }

        private bool CanKnockDown(PlayerKnockdown knockdown)
        {
            return impactProfile != null && impactProfile.knocksDown && !knockdown.IsKnockedDown;
        }
    }
}
