using System;
using System.Collections.Generic;
using Components;
using Components.Perception;
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
    public class Door : NetworkBehaviour, IInteractable, IForceableDoor
    {
        public static Action<Vector3> OnDoorBlockedSound;

        [Header("References")]
        [SerializeField] private Transform doorPivot;

        [Tooltip("The main leaf's kinematic Rigidbody. Its transform must sit on the hinge — that is the point it turns round.")]
        [SerializeField] private Rigidbody doorRigidbody;

        [SerializeField] private NavMeshObstacle navMeshObstacle;

        [Tooltip("Optional. The noise a player makes opening or closing this door. Deliberately " +
                 "not emitted when the monster forces the door — it would chase its own racket.")]
        [SerializeField] private NoiseEmitter useNoise;

        [Header("Impact")]
        [Tooltip("What this door does to a player it hits while swinging. Leave empty for a door that never knocks anyone over.")]
        [SerializeField] private ImpactProfileSO impactProfile;

        [Header("Obstruction")]
        [Tooltip("Bodies the leaf will not sweep through. It refuses to close on any of them, and " +
                 "refuses to open into one it cannot knock down. Empty means Player + Monster.")]
        [SerializeField] private LayerMask occupantLayers;

        [Tooltip("Metres added round the leaf when checking its path, so a body just brushing the " +
                 "edge still counts as in the way.")]
        [SerializeField, Min(0f)] private float obstructionMargin = 0.05f;

        [Tooltip("Largest gap, in degrees, between the poses checked along a swing.")]
        [SerializeField, Range(2f, 30f)] private float sweepSampleDegrees = 10f;

        [Header("NavMesh")]
        [Tooltip("Metres either side of the leaf where the doorway's navmesh connection is tested, " +
                 "and where the fallback link starts and ends when that connection is missing.")]
        [SerializeField, Min(0.3f)] private float navMeshCheckReach = 0.9f;

        [Tooltip("Metres of the open leaf, from the hinge, left out of the navmesh carve. That end " +
                 "lies against the wall where the navmesh is eroded anyway, and carving it as well " +
                 "cut through the doorway strip beside it.")]
        [SerializeField, Min(0f)] private float openLeafHingeClearance = 0.45f;

        [Header("Angles")]
        [Tooltip("The main leaf's local Y rotation when the door is shut — the rotation the leaf has " +
                 "in the prefab, usually 0. How the whole door is turned in the scene is the root's " +
                 "rotation and never goes here.")]
        [SerializeField] private float closedAngle;

        [Tooltip("The main leaf's local Y rotation fully open one way. The door picks this side or " +
                 "side B on its own, so it always swings away from whoever opens it.")]
        [FormerlySerializedAs("openAngle")]
        [SerializeField] private float openSideAAngle = 100f;

        [Tooltip("The main leaf's local Y rotation fully open the other way — normally side A's angle, negated.")]
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

        public Vector3 Position => DoorwayCentre;
        public bool IsClosed => _state.Value == DoorState.Closed;
        public bool IsSwinging => !Mathf.Approximately(_currentAngle, _targetAngle);

        /// <summary>Server-side: the monster is holding this door and it will not answer players yet.</summary>
        public bool IsLocked => Time.time < _lockedUntilTime;

        private const float CloseRetrySeconds = 0.2f;

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

        /// <summary>
        /// One moving leaf. Its box is kept in the leaf's own rotation frame so its pose at any
        /// angle can be rebuilt for a query without moving the real one.
        /// </summary>
        private sealed class Leaf
        {
            public Rigidbody Body;
            public float ClosedYaw;
            public float Mirror;

            public bool HasBox;
            public Vector3 BoxOffset;
            public Quaternion BoxRotation;
            public Vector3 BoxHalfExtents;

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
        /// </summary>
        private void CreateOpenLeafObstacle()
        {
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

            Debug.Log($"{name}: vao desconectado no NavMesh em {centre} — link de reserva criado atravessando a porta.", this);
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

            Debug.Log($"Door interacted by {playerInteractor.name} at position {playerInteractor.transform.position}");
            RequestToggleServerRpc(playerInteractor.transform.position);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void RequestToggleServerRpc(Vector3 playerPosition)
        {
            // The monster is holding this door — either it just forced it and shutting it straight
            // back would be a door-slamming contest, or it sabotaged the whole floor. Rattle it so
            // the refusal reads as the door being held, not as the input being dropped.
            if (IsLocked)
            {
                PlayBlockedSoundRpc();
                return;
            }

            bool moved = _state.Value == DoorState.Closed
                ? TryOpenAwayFrom(playerPosition)
                : TryClose();

            // Someone is in the way. Same rattle: the door is stuck on them.
            if (!moved)
            {
                PlayBlockedSoundRpc();
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

            _state.Value = SideAwayFrom(fromPosition);
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

        [Rpc(SendTo.ClientsAndHost)]
        private void PlayBlockedSoundRpc()
        {
            // Positional, so anyone nearby hears someone failing to get through.
            OnDoorBlockedSound?.Invoke(transform.position);
        }

        private bool TryOpenAwayFrom(Vector3 fromPosition)
        {
            if (!IsServer || _state.Value != DoorState.Closed) return false;

            DoorState away = SideAwayFrom(fromPosition);
            float openAngle = AngleFor(away);

            // Players on the far side are fine — the swing knocks them over, which is the point of
            // the impact profile. The monster, or anyone the profile cannot knock down, is not:
            // the leaf would pass through them. No trying the other side either — that swing comes
            // straight back at whoever opened it.
            if (ScanSweep(_currentAngle, openAngle, SwingSide(openAngle)) == Occupant.Solid) return false;

            _state.Value = away;
            return true;
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
        }

        private void FixedUpdate()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                TryCloseWhenClear();
                UpdateOpenLeafObstacle();
            }

            if (!IsSwinging) return;

            float step = Mathf.Max(1f, openDegreesPerSecond) * Time.fixedDeltaTime;
            float next = Mathf.MoveTowards(_currentAngle, _targetAngle, step);

            if (IsServer && !ResolveOccupants(next)) return;

            ApplyAngle(next);

            if (!IsSwinging)
            {
                _swingSign = 0f;
                _hitThisSwing.Clear();
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
                PlayBlockedSoundRpc();

                if (IsLocked) _closeWhenClear = true;

                return false;
            }

            _knockableBuffer.Clear();
            ScanLeafAt(nextAngle, _knockableBuffer, SwingSide(_targetAngle));

            foreach (PlayerKnockdown knockdown in _knockableBuffer)
            {
                Leaf leaf = NearestLeaf(knockdown.transform.position, nextAngle);

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

        private void KnockDown(Leaf leaf, PlayerKnockdown knockdown)
        {
            Vector3 hinge = leaf.Body.position;
            Vector3 radial = knockdown.transform.position - hinge;
            radial.y = 0f;

            if (radial.sqrMagnitude < 0.0001f) return;

            // Linear speed of the leaf at the player: angular speed times the lever arm.
            float leafSpeed = openDegreesPerSecond * Mathf.Deg2Rad * radial.magnitude;
            if (leafSpeed < impactProfile.minimumSpeed) return;

            // The leaf sweeps perpendicular to the lever arm, so that is where it throws the player.
            Vector3 direction = Vector3.Cross(Vector3.up * (_swingSign * leaf.Mirror), radial).normalized;

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
                    leaf.Body.transform.localRotation = Quaternion.Euler(0f, YawFor(leaf, angle), 0f);
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

            Quaternion toLeafSpace = Quaternion.Inverse(leafTransform.rotation);

            leaf.BoxOffset = toLeafSpace * (box.TransformPoint(leafBox.center) - leafTransform.position);
            leaf.BoxRotation = toLeafSpace * box.rotation;

            Vector3 scale = box.lossyScale;
            leaf.BoxHalfExtents = Vector3.Scale(leafBox.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * 0.5f;

            leaf.HasBox = true;
        }

        private Quaternion LeafRotationAt(Leaf leaf, float angle)
        {
            Transform parent = leaf.Body.transform.parent;
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;

            return parentRotation * Quaternion.Euler(0f, YawFor(leaf, angle), 0f);
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
        private Occupant ScanSweep(float fromAngle, float toAngle, float side)
        {
            float span = Mathf.Abs(toAngle - fromAngle);
            int steps = Mathf.Max(1, Mathf.CeilToInt(span / sweepSampleDegrees));

            Occupant worst = Occupant.None;

            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(fromAngle, toAngle, i / (float)steps);

                Occupant occupant = ScanLeafAt(angle, null, side);
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
        private Occupant ScanLeafAt(float angle, List<PlayerKnockdown> knockable, float side)
        {
            if (!_hasLeafBox) return Occupant.None;

            Occupant worst = Occupant.None;

            Vector3 doorwayCentre = DoorwayCentre;
            Vector3 normal = FlatNormal();

            foreach (Leaf leaf in _leaves)
            {
                if (!leaf.HasBox) continue;

                Occupant occupant = ScanOneLeafAt(leaf, angle, knockable, side, doorwayCentre, normal);
                if (occupant > worst) worst = occupant;
            }

            return worst;
        }

        private Occupant ScanOneLeafAt(Leaf leaf, float angle, List<PlayerKnockdown> knockable, float side,
            Vector3 doorwayCentre, Vector3 normal)
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

                if (knockdown != null && CanKnockDown(knockdown))
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
