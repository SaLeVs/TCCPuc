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
        [SerializeField] private Transform orientation;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        [SerializeField] private float positionErrorThreshold = 0.5f;
        [SerializeField] private float sensitivity = 20f;

        [SerializeField] private GameObject serverCube;
        [SerializeField] private GameObject clientCube;
        
        private Vector2 _movementInput;
        private Vector2 _cameraLookInput;
        private bool _isRunning;
        
        private float _currentYaw;
        private Vector3 _movementDirection;
        private float _targetSpeed;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;

        
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
        
        private bool _hasServerState;
        
        
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
                inputReader.OnCameraLookEvent += InputReader_OnCameraLookEvent;
                inputReader.OnRunEvent += InputReader_OnRunEvent;
                
            }
            
        }

        
        private void InputReader_OnMoveEvent(Vector2 movementInput) => _movementInput = movementInput;
        private void InputReader_OnCameraLookEvent(Vector2 cameraLookInput) => _cameraLookInput = cameraLookInput;
        private void InputReader_OnRunEvent(bool isRunning) => _isRunning = isRunning;
        
        private void Update()
        {
            _networkTimer.Update(Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Q))
            {
                transform.position += transform.forward * 20f;
            }
            
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
                    LookVector = _cameraLookInput
                };
                
                _clientInputBuffer.Add(inputPayload, bufferIndex);
                
                SendInputToServerRpc(inputPayload);
                
                StatePayload statePayload = ProcessClientMovement(inputPayload);
                
                clientCube.transform.position = new Vector3(statePayload.Position.x, 1f, statePayload.Position.z);
                
                _clientStateBuffer.Add(statePayload, bufferIndex);

                ServerReconciliation();
                
            }
            
        }

        

        [Rpc(SendTo.Server)]
        private void SendInputToServerRpc(InputPayload input)
        {
            _serverInputQueue.Enqueue(input);
            
        }
        
        private void ServerReconciliation()
        {
            if (ShouldReconcile())
            {
                float positionError = 0f;
                int bufferIndex = 0;
                
                StatePayload rewindState = default;
                bufferIndex = _lastServerState.Tick % BUFFER_SIZE;

                if (bufferIndex - 1 < 0) return;
                
                rewindState = IsHost ? _serverStateBuffer.Get(bufferIndex - 1) : _lastServerState;
                positionError = Vector3.Distance(rewindState.Position, _clientStateBuffer.Get(bufferIndex).Position);

                if (positionError > positionErrorThreshold)
                {
                    Reconcile(rewindState);
                }
                
                _lastProcessedState = _lastServerState;
                
            }
            
        }

        private bool ShouldReconcile()
        {
            if (!_hasServerState)
                return false;
            
            return !_lastProcessedState.Equals(_lastServerState);
            
        }
        
        private void Reconcile(StatePayload rewindState)
        {
            transform.position = rewindState.Position;
            transform.rotation = rewindState.Rotation;
            rb.linearVelocity = rewindState.Velocity;
            rb.angularVelocity = rewindState.AngularVelocity;

            if (!rewindState.Equals(_lastServerState)) return;
            
            _clientStateBuffer.Add(rewindState, rewindState.Tick);
            
            int tickToReprocess = _lastServerState.Tick + 1;
            
            
            while (tickToReprocess <= _networkTimer.CurrentTick)
            {
                int bufferIndex = tickToReprocess % BUFFER_SIZE;
                StatePayload statePayload = ProcessClientMovement(_clientInputBuffer.Get(bufferIndex));
                _clientStateBuffer.Add(statePayload, bufferIndex);
                tickToReprocess++;
                
            }
            
        }

        private StatePayload ProcessClientMovement(InputPayload input)
        {
            Move(input.InputVector, input.LookVector);
            
            return new StatePayload
            {
                Tick = input.Tick,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = rb.linearVelocity,
                AngularVelocity = rb.angularVelocity,
            };
            
        }
        
        private void Move(Vector2 moveVector, Vector2 lookVector)
        {
            _currentYaw += lookVector.x * sensitivity * Time.fixedDeltaTime;

            Quaternion yawRotation = Quaternion.Euler(0f, _currentYaw, 0f);

            orientation.rotation = yawRotation;
            transform.rotation = yawRotation;
            
            _targetSpeed = _isRunning ? runSpeed : moveSpeed;
            
            Vector3 desiredVelocityWorld = Vector3.zero;
            
            if (moveVector.sqrMagnitude > 0.0001f)
            {
                desiredVelocityWorld = orientation.forward * moveVector.y + orientation.right * moveVector.x;
                desiredVelocityWorld.y = 0f;
                float inputMag = desiredVelocityWorld.magnitude;
                
                if (inputMag > 0.0001f)
                {
                    desiredVelocityWorld = desiredVelocityWorld.normalized * (_targetSpeed * Mathf.Clamp01(moveVector.magnitude));
                }
                else
                {
                    desiredVelocityWorld = Vector3.zero;
                }
            }
            else
            {
                desiredVelocityWorld = Vector3.zero;
            }
            
            _currentVelocity.x = Mathf.Lerp(_currentVelocity.x, desiredVelocityWorld.x, blendMovementTime * Time.fixedDeltaTime);
            _currentVelocity.y = Mathf.Lerp(_currentVelocity.y, desiredVelocityWorld.z, blendMovementTime * Time.fixedDeltaTime);
            
            _xVelocityDifference = _currentVelocity.x - rb.linearVelocity.x;
            _zVelocityDifference = _currentVelocity.y - rb.linearVelocity.z;

            //  float lerpFraction = _networkTimer.TimeBetweenTick / (1f / Time.deltaTime);
            rb.AddForce(new Vector3(_xVelocityDifference, 0f, _zVelocityDifference), ForceMode.VelocityChange); 
            
        }
        
        private void ProcessServerTick()
        {
            int bufferIndex = -1;

            while (_serverInputQueue.Count > 0)
            {
                InputPayload inputPayload = _serverInputQueue.Dequeue();
                bufferIndex = inputPayload.Tick % BUFFER_SIZE;
                
                StatePayload serverState = SimulateMovement(inputPayload);
                
                serverCube.transform.position = new Vector3(serverState.Position.x, 1f, serverState.Position.z);
                
                _serverStateBuffer.Add(serverState, bufferIndex);
                
            }
            
            if(bufferIndex == -1) return;

            SendStateToClientRpc(_serverStateBuffer.Get(bufferIndex));
            
        }

        private StatePayload SimulateMovement(InputPayload inputPayload)
        {
            Physics.simulationMode = SimulationMode.Script;
            Move(inputPayload.InputVector, inputPayload.LookVector);
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
                bool wasNoState = !_hasServerState;

                _lastServerState = statePayload;
                _hasServerState = true;

                if (wasNoState)
                {
                    _lastProcessedState = statePayload;
                }
                    
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
