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

        [Header("Path watchdog")]
        [Tooltip("Seconds the agent may try to move without getting anywhere before the active state is told it is stuck.")]
        [SerializeField, Min(0.25f)] private float stuckSeconds = 2f;

        [Tooltip("Metres it has to cover in that time to count as making progress.")]
        [SerializeField, Min(0.05f)] private float stuckMinProgress = 0.4f;

        [Tooltip("Logs every stuck or unreachable report, and every time the path turns partial or invalid.")]
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
        private string _lastPath;
        private float _trackingTimer;

        private MonsterPathWatchdog _pathWatchdog;
        private NavMeshPathStatus _lastLoggedPathStatus = NavMeshPathStatus.PathComplete;
        private float _nextPathStatusLogTime;

        
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

            Debug.Log($"Monster: {(died ? "matou" : "derrubou")} {player.name} na frente dele — vai zoar", this);
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

            LogPathStatusChange();

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

        /// <summary>Logs the moment the path stops being complete, throttled — Chase re-paths every frame.</summary>
        private void LogPathStatusChange()
        {
            if (!logPathStatus || !navMeshAgent.isOnNavMesh) return;
            if (navMeshAgent.pathPending || !navMeshAgent.hasPath) return;

            NavMeshPathStatus status = navMeshAgent.pathStatus;
            if (status == _lastLoggedPathStatus) return;
            if (Time.time < _nextPathStatusLogTime) return;

            _lastLoggedPathStatus = status;
            _nextPathStatusLogTime = Time.time + 1f;

            if (status == NavMeshPathStatus.PathComplete) return;

            Debug.Log($"Monster: caminho {status} em {StatePath(_stateMachine.Root.Leaf())} — {_pathWatchdog.Describe()}", this);
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