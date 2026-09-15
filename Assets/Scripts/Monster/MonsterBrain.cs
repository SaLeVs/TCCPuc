using System;
using System.Collections.Generic;
using System.Linq;
using Components;
using Components.Perception;
using Monster.HSM;
using Monster.MonsterStates;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterBrain : NetworkBehaviour
    {
        public event Action<Transform> OnPlayerEnterInVision;
        public event Action<Transform> OnPlayerExitInVision;
        
        [SerializeField] private NavMeshAgent navMeshAgent;
        
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private HearingSensor hearingSensor;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        [SerializeField] private MonsterChase monsterChase;
        [SerializeField] private MonsterAttack monsterAttack;
        [SerializeField] private MonsterAnimator monsterAnimator;
        [SerializeField] private MonsterSearch monsterSearch;
        [SerializeField] private MonsterDoorForcer monsterDoorForcer;
        [SerializeField] private MonsterAwareness monsterAwareness;
        [SerializeField] private MonsterInvestigate monsterInvestigate;
        
        [Tooltip("Seconds the monster keeps a perfect fix on a target after losing sight of it.")]
        [SerializeField] private float trackingGraceSeconds = 3f;

        [Header("Debug")]
        [Tooltip("Logs any frame where the monster moves further than it physically could. " +
                 "Runs on every peer — turn it on as a LAN client to tell a network problem " +
                 "(client logs jumps, host does not) from a local one (both log them).")]
        [SerializeField] private bool logMovementJumps;

        [Tooltip("A jump is anything faster than this, in metres per second.")]
        [SerializeField, Min(1f)] private float jumpSpeedThreshold = 12f;

        [Tooltip("Warns when the state machine changes state again too soon after the last one. " +
                 "Rapid back-and-forth here is the usual cause of the monster twitching in place.")]
        [SerializeField] private bool logRapidStateChanges = true;

        [Tooltip("A state that lasts less than this many seconds is flagged.")]
        [SerializeField, Min(0.05f)] private float rapidStateSeconds = 0.75f;

        [Tooltip("Warns when the monster swings its facing further than it should in one frame.")]
        [SerializeField] private bool logFacingFlips;

        [Tooltip("Degrees of turn in a single frame that counts as a flip.")]
        [SerializeField, Min(1f)] private float facingFlipDegrees = 40f;

        public NavMeshAgent NavMeshAgent => navMeshAgent;
        public MonsterAwareness MonsterAwareness => monsterAwareness;
        public MonsterInvestigate MonsterInvestigate => monsterInvestigate;
        public MonsterWander MonsterWander => monsterWander;
        public MonsterSabotage MonsterSabotage => monsterSabotage;
        public MonsterChase MonsterChase => monsterChase;
        public MonsterAttack MonsterAttack => monsterAttack;
        public MonsterAnimator MonsterAnimator => monsterAnimator;
        public MonsterSearch MonsterSearch => monsterSearch;
        public MonsterDoorForcer MonsterDoorForcer => monsterDoorForcer;
        
        
        public readonly List<Transform> _playersInVision = new();
        public Vector3 LastKnownTargetPosition { get; private set; }
        public Transform LastKnownTarget { get; private set; }

        public bool IsTrackingLostTarget => _trackingTimer > 0f && LastKnownTarget != null;
        
        public bool IsHunting => _playersInVision.Count > 0 || IsTrackingLostTarget;
        public bool IsForcingDoor => monsterDoorForcer != null && monsterDoorForcer.IsForcingDoor;

        private StateMachine _stateMachine;
        private State _rootState;
        private string _lastPath;
        private float _trackingTimer;

        
        private void Awake()
        {
            _rootState = new MonsterRoot(null, this);
            StateMachineBuilder stateMachineBuilder = new StateMachineBuilder(_rootState);
            _stateMachine = stateMachineBuilder.Build();
        }
        
        
        public override void OnNetworkSpawn()
        {
            // Only the server drives the agent. Left enabled on a client it keeps snapping the
            // transform onto the local navmesh while NetworkTransform writes the server's
            // position into the same transform — the two fight every frame, which reads as the
            // monster being shoved around and never quite moving at its own speed.
            if (!IsServer && navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }

            MonsterWander.Initialize(navMeshAgent);
            MonsterChase.Initialize(_playersInVision, navMeshAgent, this);
            MonsterAnimator.Initialize(this);
            MonsterSabotage.Initialize();
            MonsterSearch.Initialize(navMeshAgent);
            MonsterAttack.Initialize(navMeshAgent);

            if (monsterInvestigate != null)
            {
                monsterInvestigate.Initialize(navMeshAgent);
            }

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Initialize(navMeshAgent);
            }
            
            if (!IsServer) return;

            // Awareness is not optional the way the door forcer is — every Alert transition
            // reads it. Fail here with a clear message instead of a null ref mid-chase.
            if (monsterAwareness == null || monsterInvestigate == null)
            {
                Debug.LogError($"{name}: MonsterBrain is missing MonsterAwareness or MonsterInvestigate. The monster will not react to anything it hears.", this);
                return;
            }

            _stateMachine.Start();
            visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit += VisionSensor_OnTargetExit;

            if (hearingSensor != null)
            {
                hearingSensor.OnNoiseHeard += HearingSensor_OnNoiseHeard;
            }
        }


        private void HearingSensor_OnNoiseHeard(HeardNoise heard)
        {
            // Vision outranks hearing. While it can see you — or is still coasting on a fix it
            // just lost — a noise tells it nothing it does not already know, and letting sound
            // rewrite the destination would pull it off an active chase.
            if (IsHunting) return;

            monsterAwareness.RegisterNoise(heard);
        }

        private void TickAwareness(float deltaTime)
        {
            // Seeing someone pins the meter, so losing them drops straight into Alerted
            // rather than into a lazy Investigate.
            if (IsHunting) monsterAwareness.PinToMax();
            else monsterAwareness.Tick(deltaTime);
        }


        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);

            _trackingTimer = 0f;

            OnPlayerEnterInVision?.Invoke(player.transform);
        }

        private void VisionSensor_OnTargetExit(GameObject player)
        {
            _playersInVision.Remove(player.transform);

            if (_playersInVision.Count == 0)
            {
                LastKnownTargetPosition = player.transform.position;
                LastKnownTarget = player.transform;

                _trackingTimer = trackingGraceSeconds;
            }

            OnPlayerExitInVision?.Invoke(player.transform);
        }

        private void TickTracking(float deltaTime)
        {
            if (_trackingTimer <= 0f) return;

            if (LastKnownTarget == null)
            {
                GoCold();
                return;
            }
            
            LastKnownTargetPosition = LastKnownTarget.position;

            _trackingTimer -= deltaTime;
            if (_trackingTimer > 0f) return;

            GoCold();
        }

        private void GoCold()
        {
            _trackingTimer = 0f;

            monsterAwareness.RegisterLostSight(LastKnownTargetPosition);

            MonsterChase.ForgetTarget();
        }

        private void Update()
        {
            if (!IsServer) return;

            TickTracking(Time.deltaTime);
            TickAwareness(Time.deltaTime);
            
            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Tick(Time.deltaTime);
            }

            _stateMachine.Tick(Time.deltaTime);

            string statePath = StatePath(_stateMachine.Root.Leaf());
            if (statePath == _lastPath) return;

            float heldFor = _lastStateChangeTime > 0f ? Time.time - _lastStateChangeTime : 0f;

            _lastPath = statePath;
            _lastStateChangeTime = Time.time;

            // Server-only: the state machine does not run anywhere else. If you are testing as a
            // LAN client and see nothing here, that is expected — run as host to read the AI.
            Debug.Log($"Monster: State: {statePath}  (awareness {monsterAwareness.Value:0.00} / {monsterAwareness.Level}, anterior durou {heldFor:0.00}s)");

            if (!logRapidStateChanges) return;

            bool wasRapid = heldFor > 0f && heldFor < rapidStateSeconds;

            if (!wasRapid)
            {
                _rapidChangeStreak = 0;
                return;
            }

            _rapidChangeStreak++;

            Debug.LogWarning($"Monster: TROCA RAPIDA DE ESTADO — durou so {heldFor:0.00}s " +
                             $"({_rapidChangeStreak} seguidas). Agora: {statePath}", this);
        }
        
        /// <summary>
        /// Runs on every peer, unlike <see cref="Update"/>. On a client the transform is written
        /// by NetworkTransform interpolation, so a jump logged here and not on the host means the
        /// updates are arriving late or dropping — a network problem, not an AI one.
        /// </summary>
        private void LateUpdate()
        {
            if (!logMovementJumps && !logFacingFlips) return;

            Vector3 position = transform.position;
            Vector3 forward = transform.forward;

            if (!_hasLastLoggedPosition)
            {
                _lastLoggedPosition = position;
                _lastLoggedForward = forward;
                _hasLastLoggedPosition = true;
                return;
            }

            float travelled = Vector3.Distance(_lastLoggedPosition, position);
            float turned = Vector3.Angle(_lastLoggedForward, forward);

            _lastLoggedPosition = position;
            _lastLoggedForward = forward;

            if (Time.deltaTime <= 0f) return;

            string peer = IsServer ? "HOST" : "CLIENT";

            if (logMovementJumps)
            {
                float speed = travelled / Time.deltaTime;

                if (speed >= jumpSpeedThreshold)
                {
                    Debug.Log($"Monster: {peer} SALTO de {travelled:0.00}m num frame ({speed:0} m/s)");
                }
            }

            if (!logFacingFlips) return;
            if (turned < facingFlipDegrees) return;

            // A big turn in one frame is either the rotation code fighting itself or a path that
            // doubled back — both look identical on screen, but only the second moves the agent.
            Debug.LogWarning($"Monster: {peer} GIROU {turned:0}graus num frame " +
                             $"(andou {travelled:0.00}m) — estado {_lastPath}", this);
        }

        private Vector3 _lastLoggedPosition;
        private bool _hasLastLoggedPosition;
        private Vector3 _lastLoggedForward;
        private float _lastStateChangeTime;
        private int _rapidChangeStreak;

        private static string StatePath(State state)
        {
            return string.Join(" > ", state.PathToRoot().Reverse().Select(node => node.GetType().Name));
        }

        
        public override void OnNetworkDespawn()
        {
            MonsterChase.Uninitialize(_playersInVision, navMeshAgent, this);
            MonsterAnimator.Uninitialize(this);
            MonsterSabotage.Uninitialize();
            
            if (!IsServer) return;
            
            visionSensor.OnTargetEnter -= VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit -= VisionSensor_OnTargetExit;

            if (hearingSensor != null)
            {
                hearingSensor.OnNoiseHeard -= HearingSensor_OnNoiseHeard;
            }
        }
        
    }
}