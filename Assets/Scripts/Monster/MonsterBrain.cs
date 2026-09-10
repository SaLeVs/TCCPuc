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
        
        [Tooltip("Seconds the monster keeps a perfect fix on a target after losing sight of it.")]
        [SerializeField] private float trackingGraceSeconds = 3f;

        [Header("Awareness")]
        [Tooltip("Awareness a fully-clear noise adds. Fainter noises add proportionally less, " +
                 "so one distant footstep is never enough on its own.")]
        [SerializeField, Range(0f, 1f)] private float awarenessPerNoise = 0.45f;

        [Tooltip("Awareness lost per second while nothing is heard.")]
        [SerializeField, Min(0f)] private float awarenessDecayPerSecond = 0.12f;

        [Tooltip("Above this, the monster walks over to look (Investigate).")]
        [SerializeField, Range(0f, 1f)] private float suspiciousThreshold = 0.25f;

        [Tooltip("Above this, the monster moves fast to the spot and sweeps it (Search).")]
        [SerializeField, Range(0f, 1f)] private float alertedThreshold = 0.6f;

        public NavMeshAgent NavMeshAgent => navMeshAgent;
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
        public bool ShouldEnterAlert { get; set; }
        
        public Vector3 InvestigationPoint { get; private set; }
        public NoiseType LastHeardNoiseType { get; private set; }

        public float AwarenessValue => _awareness;

        public AwarenessLevel Awareness => _awareness >= alertedThreshold ? AwarenessLevel.Alerted : _awareness >= suspiciousThreshold ? AwarenessLevel.Suspicious : AwarenessLevel.Unaware;
        
        public bool IsTrackingLostTarget => _trackingTimer > 0f && LastKnownTarget != null;
        
        public bool IsHunting => _playersInVision.Count > 0 || IsTrackingLostTarget;
        public bool IsForcingDoor => monsterDoorForcer != null && monsterDoorForcer.IsForcingDoor;

        private StateMachine _stateMachine;
        private State _rootState;
        private string _lastPath;
        private float _trackingTimer;
        private float _awareness;

        
        private void Awake()
        {
            _rootState = new MonsterRoot(null, this);
            StateMachineBuilder stateMachineBuilder = new StateMachineBuilder(_rootState);
            _stateMachine = stateMachineBuilder.Build();
        }
        
        
        public override void OnNetworkSpawn()
        {
            MonsterWander.Initialize(navMeshAgent);
            MonsterChase.Initialize(_playersInVision, navMeshAgent, this);
            MonsterAnimator.Initialize(this);
            MonsterSabotage.Initialize();
            MonsterSearch.Initialize(navMeshAgent);
            MonsterAttack.Initialize(navMeshAgent);

            if (monsterDoorForcer != null)
            {
                monsterDoorForcer.Initialize(navMeshAgent);
            }
            
            if (!IsServer) return;
            
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
            if (IsHunting) return;

            _awareness = Mathf.Clamp01(_awareness + heard.Confidence * awarenessPerNoise);
            
            InvestigationPoint = heard.Position;
            LastHeardNoiseType = heard.Type;

            if (_awareness >= suspiciousThreshold)
            {
                ShouldEnterAlert = true;
            }
        }

        private void TickAwareness(float deltaTime)
        {
            if (IsHunting)
            {
                _awareness = 1f;
                return;
            }

            if (_awareness <= 0f) return;

            _awareness = Mathf.MoveTowards(_awareness, 0f, awarenessDecayPerSecond * deltaTime);
        }
        
        public void ClearAlert()
        {
            _awareness = Mathf.Min(_awareness, suspiciousThreshold * 0.5f);
            ShouldEnterAlert = false;
        }
        
        
        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);
            
            _trackingTimer = 0f;
            ShouldEnterAlert = false;

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
            ShouldEnterAlert = true;
            
            _awareness = 1f;
            InvestigationPoint = LastKnownTargetPosition;

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
            if (statePath != _lastPath)
            {
                Debug.Log($"Monster: State: {statePath}");
                _lastPath = statePath;
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
        }
        
    }
}