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
        [Tooltip("Logs every state change with the awareness level and how long the previous state lasted.")]
        [SerializeField] private bool logStateChanges = true;

        [Header("Path watchdog")]
        [Tooltip("Seconds the agent may try to move without getting anywhere before the active state is told it is stuck.")]
        [SerializeField, Min(0.25f)] private float stuckSeconds = 2f;

        [Tooltip("Metres it has to cover in that time to count as making progress.")]
        [SerializeField, Min(0.05f)] private float stuckMinProgress = 0.4f;

        [Tooltip("Warns when the watchdog finds the monster stuck or its target unreachable.")]
        [SerializeField] private bool logPathStatus = true;

        [Header("Taunt")]
        [Tooltip("Stops to mock a player who dies or is knocked down while it can see them.")]
        [SerializeField] private bool tauntWhenPlayerDowned = true;

        [Tooltip("How long it stands there mocking them.")]
        [SerializeField, Min(0.1f)] private float tauntSeconds = 2.5f;

        [Tooltip("How long a taunt may wait for a swing or a door to finish before it is dropped.")]
        [SerializeField, Min(0f)] private float tauntMaxDelay = 1.5f;

        [Tooltip("How fast it turns to face the player it is mocking.")]
        [SerializeField, Min(0.1f)] private float tauntTurnSpeed = 6f;

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

        /// <summary>Mid-swipe at a door. The state machine is not ticked until it is over.</summary>
        public bool IsDoorSwipeCommitted => monsterDoorForcer != null && monsterDoorForcer.IsCommitted;

        /// <summary>Mocking a downed player. The state machine is not ticked until it is over.</summary>
        public bool IsTaunting => _taunt != null && _taunt.IsTaunting;

        /// <summary>The animator listens here; the taunt itself only exists on the server.</summary>
        public event Action OnTauntAnimation;

        private MonsterTaunt _taunt;

        private StateMachine _stateMachine;
        private State _rootState;
        private State _lastLeaf;
        private float _lastStateChangeTime;
        private float _trackingTimer;

        private MonsterPathWatchdog _pathWatchdog;

        
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

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.OnForcingFinished += MonsterDoorForcer_OnForcingFinished;
            }

            _pathWatchdog = new MonsterPathWatchdog(navMeshAgent, stuckSeconds, stuckMinProgress);

            _taunt = new MonsterTaunt(navMeshAgent, transform, tauntSeconds, tauntMaxDelay, tauntTurnSpeed);
            _taunt.OnTauntAnimation += MonsterTaunt_OnTauntAnimation;
            _taunt.OnFinished += MonsterTaunt_OnFinished;

            PlayerDownedNotifier.OnPlayerDowned += PlayerDownedNotifier_OnPlayerDowned;
        }

        /// <summary>
        /// "In front of it" is what the vision sensor already calls seen. The body is still in the
        /// list at this point: it only drops out on the next scan, once its colliders are off.
        /// </summary>
        private void PlayerDownedNotifier_OnPlayerDowned(GameObject player, bool died)
        {
            if (!tauntWhenPlayerDowned || _taunt == null || player == null) return;
            if (!_playersInVision.Contains(player.transform)) return;

            _taunt.Request(player.transform, Time.time);
        }

        private void MonsterTaunt_OnTauntAnimation() => OnTauntAnimation?.Invoke();

        /// <summary>Same hand-back as after a door: the active state restores itself.</summary>
        private void MonsterTaunt_OnFinished() => _stateMachine.ResumeLeaf();

        private bool IsAnyoneElseInView(Transform except)
        {
            foreach (Transform player in _playersInVision)
            {
                if (player != null && player != except) return true;
            }

            return false;
        }

        /// <summary>
        /// The forcer borrowed the agent and the animation without the active state knowing.
        /// Handing both back to that state is what stops the monster walking off still playing the
        /// swipe, or standing still playing a walk.
        /// </summary>
        private void MonsterDoorForcer_OnForcingFinished()
        {
            _stateMachine.ResumeLeaf();
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

            if (_taunt != null)
            {
                bool busy = MonsterAttack.IsAttacking || IsForcingDoor;
                _taunt.Tick(Time.time, Time.deltaTime, busy, IsAnyoneElseInView(_taunt.Victim));
            }

            // Mocking someone holds everything else still, the same as a committed door swipe.
            if (!IsTaunting)
            {
                TickStateMachineAroundDoor(Time.deltaTime);
            }

            TickPathWatchdog();
            TrackStateChange();
        }

        /// <summary>
        /// Compares the leaf by reference and only builds the readable path when it changed.
        /// Building it every frame to find out whether it changed was a LINQ walk and a string
        /// join sixty times a second, for a log line that fires a few times a minute.
        /// </summary>
        private void TrackStateChange()
        {
            State leaf = _stateMachine.Root.Leaf();
            if (leaf == _lastLeaf) return;

            float heldFor = _lastStateChangeTime > 0f ? Time.time - _lastStateChangeTime : 0f;

            _lastLeaf = leaf;
            _lastStateChangeTime = Time.time;

            if (!logStateChanges) return;

            // Server-only: the state machine does not run anywhere else. If you are testing as a
            // LAN client and see nothing here, that is expected — run as host to read the AI.
            Debug.Log($"Monster: State: {StatePath(leaf)}  (awareness {monsterAwareness.Value:0.00} / {monsterAwareness.Level}, anterior durou {heldFor:0.00}s)");
        }

        /// <summary>
        /// The door forcer and the state machine share one agent, so their order matters.
        ///
        /// <para>The forcer ticks first, so a door finished this frame resumes the active state
        /// before that state updates. The state machine is skipped entirely while a swipe is
        /// committed: letting it run used to un-stop the agent from any OnEnter or OnExit (Chase,
        /// Search, Investigate, Wander all do it) and walk the monster straight through the leaf it
        /// was still swiping at. Before the swipe the state machine runs, and a change of state
        /// drops the door; otherwise the forcer re-asserts its hold after anything the states did.</para>
        /// </summary>
        private void TickStateMachineAroundDoor(float deltaTime)
        {
            if (monsterDoorForcer == null)
            {
                _stateMachine.Tick(deltaTime);
                return;
            }

            monsterDoorForcer.Tick(deltaTime);

            if (monsterDoorForcer.IsCommitted)
            {
                monsterDoorForcer.HoldAgent();
                return;
            }

            State leafBefore = _stateMachine.Root.Leaf();

            _stateMachine.Tick(deltaTime);

            if (monsterDoorForcer.IsForcingDoor && _stateMachine.Root.Leaf() != leafBefore)
            {
                monsterDoorForcer.CancelBeforeSwipe();
                return;
            }

            monsterDoorForcer.HoldAgent();
        }

        private void TickPathWatchdog()
        {
            if (_pathWatchdog == null) return;

            if (!_pathWatchdog.Tick(Time.time, IsForcingDoor || IsTaunting, out PathProblem problem)) return;

            State leaf = _stateMachine.Root.Leaf();

            if (logPathStatus)
            {
                Debug.LogWarning($"Monster: PRESO ({problem}) em {StatePath(leaf)} — {_pathWatchdog.Describe()}", this);
            }

            if (leaf is IPathProblemHandler handler)
            {
                handler.OnPathProblem(problem);
            }
        }

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

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.OnForcingFinished -= MonsterDoorForcer_OnForcingFinished;
            }

            PlayerDownedNotifier.OnPlayerDowned -= PlayerDownedNotifier_OnPlayerDowned;

            if (_taunt != null)
            {
                _taunt.OnTauntAnimation -= MonsterTaunt_OnTauntAnimation;
                _taunt.OnFinished -= MonsterTaunt_OnFinished;
            }
        }
        
    }
}