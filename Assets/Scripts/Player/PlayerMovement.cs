using System.Collections.Generic;
using UnityEngine;
using Inputs;
using Network;
using Unity.Netcode;

namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        
        private Vector2 _movementInput;
        private bool _isRunning;
        
        private Vector3 _movementDirection;
        private float _currentSpeed;

        
        // Prediction and Reconciliation
        private NetworkTimer _networkTimer;
        private const float SERVER_TICK_RATE = 60f;
        private const int BUFFER_SIZE = 1024;

        private Buffer<InputPayload> _clientInputBuffer;
        private Buffer<StatePayload> _clientStateBuffer;
        
        private StatePayload _lastServerState;
        private StatePayload _lastProcessedState;

        private Buffer<StatePayload> _serverStateBuffer;
        private Queue<InputPayload> _serverInputQueue;
        
        
        private void Awake()
        {
            _networkTimer = new NetworkTimer(SERVER_TICK_RATE);
            
            _clientInputBuffer = new Buffer<InputPayload>(BUFFER_SIZE);
            _clientStateBuffer = new Buffer<StatePayload>(BUFFER_SIZE);
            
            _serverStateBuffer = new Buffer<StatePayload>(BUFFER_SIZE);
            _serverInputQueue = new Queue<InputPayload>();
            
        }
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent += InputReader_OnMoveEvent;
                inputReader.OnRunEvent += InputReader_OnRunEvent;
                
            }
            
        }
        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        private void InputReader_OnRunEvent(bool isRunning) => _isRunning = isRunning;
        

        private void Update()
        {
            _networkTimer.Update(Time.deltaTime);
            
        }
        
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                while (_networkTimer.ShouldTick())
                {
                    ProcessClientTick();
                    ProcessServerTick();
                }
            }
            
        }
        
        private void ProcessClientTick()
        {
            if (IsClient)
            {
                int currentTick = _networkTimer.CurrentTick;
                int bufferIndex = currentTick % BUFFER_SIZE;
                
                InputPayload inputPayload = new InputPayload
                {
                    Tick = currentTick,
                    InputVector = _movementInput,
                };
                
                _clientInputBuffer.Add(inputPayload, bufferIndex);
                
                SendInputToServerRpc(inputPayload);
                
                StatePayload statePayload = ProcessClientMovement(inputPayload);
                _clientStateBuffer.Add(statePayload, bufferIndex);
                
            }
            
        }
        
        [Rpc(SendTo.Server)]
        private void SendInputToServerRpc(InputPayload input)
        {
            _serverInputQueue.Enqueue(input);
            
        }
        
        private StatePayload ProcessClientMovement(InputPayload input)
        {
            Move(input.InputVector);
            
            return new StatePayload
            {
                Tick = input.Tick,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = rb.linearVelocity,
                AngularVelocity = rb.angularVelocity,
                
            };
            
        }
        
        private void Move(Vector2 inputVector)
        {
            Debug.Log($"Moving with input: {inputVector}");
        }
        
        private void ProcessServerTick()
        {
            int bufferIndex = -1;

            while (_serverInputQueue.Count > 0)
            {
                InputPayload inputPayload = _serverInputQueue.Dequeue();
                bufferIndex = inputPayload.Tick % BUFFER_SIZE;
                
                StatePayload serverState = SimulateMovement(inputPayload);
                _serverStateBuffer.Add(serverState, bufferIndex);
            }
            
            if(bufferIndex == -1) return;

            SendStateToClientRpc(_serverStateBuffer.Get(bufferIndex));
        }

        private StatePayload SimulateMovement(InputPayload inputPayload)
        {
            Physics.simulationMode = SimulationMode.Script;
            Move(inputPayload.InputVector);
            Physics.Simulate(Time.fixedDeltaTime);
            Physics.simulationMode = SimulationMode.FixedUpdate;

            return new StatePayload
            {
                Tick = inputPayload.Tick,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = rb.linearVelocity,
                AngularVelocity = rb.angularVelocity,
            };
            
        }

        [Rpc(SendTo.ClientsAndHost)] 
        private void SendStateToClientRpc(StatePayload statePayload)
        {
            if (IsOwner)
            {
                _lastServerState = statePayload;
                
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnMoveEvent -= InputReader_OnMoveEvent;
                inputReader.OnRunEvent -= InputReader_OnRunEvent;
                
            }
            
        }
        
    }
}
