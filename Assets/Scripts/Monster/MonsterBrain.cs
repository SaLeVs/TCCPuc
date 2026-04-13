using System.Collections.Generic;
using System.Linq;
using Components;
using Monster.HSM;
using Monster.MonsterStates;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Monster
{
    public class MonsterBrain : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private VisionSensor visionSensor;
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private MonsterWander monsterWander;
        [SerializeField] private MonsterSabotage monsterSabotage;
        [SerializeField] private MonsterChase monsterChase;
        
        public Animator Animator => animator;
        public NavMeshAgent NavMeshAgent => navMeshAgent;
        public MonsterWander MonsterWander => monsterWander;
        public MonsterSabotage MonsterSabotage => monsterSabotage;
        public MonsterChase MonsterChase => monsterChase;
        
        public readonly List<Transform> _playersInVision = new();
        
        private StateMachine _stateMachine;
        private State _rootState;
        
        private string _lastPath;

        
        
        private void Awake()
        {
            _rootState = new MonsterRoot(null, this);
            StateMachineBuilder stateMachineBuilder = new StateMachineBuilder(_rootState);
            _stateMachine = stateMachineBuilder.Build();
        }
        
        public override void OnNetworkSpawn()
        {
            MonsterSabotage.Initialize();
            MonsterWander.Initialize(NavMeshAgent);
            MonsterChase.Initialize(_playersInVision);

            if (!IsServer) return;
            
            _stateMachine.Start();
            visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit += VisionSensor_OnTargetExit;
        }
        
        private void VisionSensor_OnTargetEnter(GameObject player)
        {
            _playersInVision.Add(player.transform);
        }
        
        private void VisionSensor_OnTargetExit(GameObject player)
        {
            _playersInVision.Remove(player.transform);
        }
        
        private void Update()
        {
            if (!IsServer) return;
            
            _stateMachine.Tick(Time.deltaTime);
            
            string statePath = StatePath(_stateMachine.Root.Leaf());
            
            if (statePath != _lastPath)
            {
                Debug.Log($"State: {statePath}");
                _lastPath = statePath;
            }
        }
        
        private static string StatePath(State state)
        {
            return string.Join(" > ", state.PathToRoot().Reverse().Select(node => node.GetType().Name));
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            
            visionSensor.OnTargetEnter -= VisionSensor_OnTargetEnter;
            visionSensor.OnTargetExit -= VisionSensor_OnTargetExit;
        }
    }
}