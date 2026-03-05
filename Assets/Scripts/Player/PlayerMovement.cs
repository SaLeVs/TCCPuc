using System;
using System.Collections.Generic;
using Components;
using UnityEngine;
using Inputs;
using Network;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Transform orientation;
        [SerializeField] private NetworkTransform playerNetworkTransform;

        [SerializeField] private float moveSpeed;
        [SerializeField] private float runSpeed;
        [SerializeField] private float blendMovementTime = 8.9f;
        
        [SerializeField] private float sensitivity = 20f;
        [SerializeField] private float staminaMax;
        [SerializeField] private float staminaDrainPerSecond;
        [SerializeField] private float staminaGainPerSecond;
        [SerializeField] private float staminaCooldownThreshold = 5f;
        

        [SerializeField] private GameObject serverCube;
        [SerializeField] private GameObject clientCube;
        [SerializeField] private float positionErrorThreshold = 0.5f;
        [SerializeField] private float reconciliationCooldown;
        [SerializeField] private float extrapolationLimitInMilliseconds = 0.5f;
        [SerializeField] private float extrapolationMultiplier = 1.2f;
        
        public float SimulationYaw => _simulationYaw; 
        
        private Vector2 _movementInput;
        private Vector2 _cameraLookInput;
        private bool _isRunning;
        
        private float _simulationYaw;  
        private float _visualYaw;
        
        private Vector3 _movementDirection;
        private float _targetSpeed;
        
        private Vector2 _currentVelocity;
        private float _xVelocityDifference;
        private float _zVelocityDifference;
        
        private float _stamina;
        private bool _isExhausted;
        
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
        private CountdownTimer _reconciliationTimer;
        
        // Handle Lag and Extrapolation
        private StatePayload _extrapolationState;
        private CountdownTimer _extrapolationCooldownTimer;
        
        private void Awake()
        {
            _networkTimer = new NetworkTimer(SERVER_TICK_RATE);
            
            _clientInputBuffer = new Buffer<InputPayload>(BUFFER_SIZE);
            _clientStateBuffer = new Buffer<StatePayload>(BUFFER_SIZE);
            
            _serverStateBuffer = new Buffer<StatePayload>(BUFFER_SIZE);
            _serverInputQueue = new Queue<InputPayload>();
            
            _stamina = staminaMax;
            _isExhausted = false;
            
            _reconciliationTimer = new CountdownTimer(reconciliationCooldown);
            _extrapolationCooldownTimer = new CountdownTimer(extrapolationLimitInMilliseconds);
            
            _extrapolationState = new StatePayload
            {
                Position = default,
                Rotation = default,
                Velocity = default,
                AngularVelocity = default
            };
            
            _reconciliationTimer.OnTimerStart += () =>
            {
                _extrapolationCooldownTimer.Stop();
            };

            _extrapolationCooldownTimer.OnTimerStart += () =>
            {
                _reconciliationTimer.Stop();
                SwitchAuthorityMode(NetworkTransform.AuthorityModes.Server);
            };
            
            _extrapolationCooldownTimer.OnTimerStop += () =>
            {
                _extrapolationState = default;
                SwitchAuthorityMode(NetworkTransform.AuthorityModes.Owner);
            };
            
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

        private void SwitchAuthorityMode(NetworkTransform.AuthorityModes authorityMode)
        {
            bool shouldPlayerSync = authorityMode == NetworkTransform.AuthorityModes.Owner;
            
            playerNetworkTransform.AuthorityMode = authorityMode;
            playerNetworkTransform.SyncPositionX = shouldPlayerSync;
            playerNetworkTransform.SyncPositionY = shouldPlayerSync;
            playerNetworkTransform.SyncPositionZ = shouldPlayerSync;
            
        }
        
        private void Update()
        {
            _networkTimer.Update(Time.deltaTime);
            _reconciliationTimer.Tick(Time.deltaTime);
            _extrapolationCooldownTimer.Tick(Time.deltaTime);

            if (Input.GetKeyDown(KeyCode.Q))
            {
                transform.position += transform.forward * 20f;
            }
            
        }
        
        private void FixedUpdate()
        {
            while (_networkTimer.ShouldTick())
            {
                ProcessClientTick();
                ProcessServerTick();
                
            }

            Extrapolate();
        }
        
        private void ProcessClientTick()
        {
            if(!IsClient || !IsOwner) return;
            
            int currentTick = _networkTimer.CurrentTick;
            int bufferIndex = currentTick % BUFFER_SIZE;
                
            InputPayload inputPayload = new InputPayload
            {
                Tick = currentTick,
                TimeStamp = DateTime.Now,
                NetworkObjectId = NetworkObjectId,
                InputVector = _movementInput,
                Position = transform.position,
                LookVector = _cameraLookInput,
                IsRunning = _isRunning
            };
                
            _clientInputBuffer.Add(inputPayload, bufferIndex);
                
            SendInputToServerRpc(inputPayload);
                
            StatePayload statePayload = ProcessClientMovement(inputPayload);
                
            clientCube.transform.position = new Vector3(statePayload.Position.x, 1f, statePayload.Position.z);
                
            _clientStateBuffer.Add(statePayload, bufferIndex);

            ServerReconciliation();
            
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
                    _reconciliationTimer.Start();
                }
                
                _lastProcessedState = _lastServerState;
                
            }
            
        }

        private bool ShouldReconcile()
        {
            if (!_hasServerState && !_reconciliationTimer.IsRunning && !_extrapolationCooldownTimer.IsRunning)
                return false;
            
            return !_lastProcessedState.Equals(_lastServerState);
            
        }
        
        private void Reconcile(StatePayload rewindState)
        {
            transform.position = rewindState.Position;
            transform.rotation = rewindState.Rotation;
            rb.linearVelocity = rewindState.Velocity;
            rb.angularVelocity = rewindState.AngularVelocity;
            _stamina = rewindState.Stamina;
            
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
            float tickTime = 1f / SERVER_TICK_RATE;
            
            Move(input.InputVector);
            Look(input.LookVector);
            UpdateStamina(input.IsRunning, tickTime);
            
            return new StatePayload
            {
                Tick = input.Tick,
                NetworkObjectId = input.NetworkObjectId,
                Position = transform.position,
                Rotation = transform.rotation,
                Velocity = rb.linearVelocity,
                AngularVelocity = rb.angularVelocity,
                Stamina = _stamina
            };
            
        }

        private void Look(Vector3 inputLookVector)
        {
            _simulationYaw += inputLookVector.x * sensitivity * Time.fixedDeltaTime;

            Quaternion yawRotation = Quaternion.Euler(0f, _simulationYaw, 0f);
    
            orientation.rotation = yawRotation;
            transform.rotation = yawRotation;
        }

        private void Move(Vector2 moveVector)
        {
            bool canRun = _isRunning && !_isExhausted;

            _targetSpeed = canRun ? runSpeed : moveSpeed;
            
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

        private void UpdateStamina(bool isRunning, float tickTime)
        {
            bool canRun = isRunning && !_isExhausted;

            if (canRun)
            {
                _stamina -= staminaDrainPerSecond * tickTime;

                if (_stamina <= 0f)
                {
                    _stamina = 0f;
                    _isExhausted = true;
                }
            }
            else
            {
                _stamina += staminaGainPerSecond * tickTime;
                _stamina = Mathf.Min(_stamina, staminaMax);

                if (_isExhausted && _stamina >= staminaCooldownThreshold)
                {
                    _isExhausted = false;
                }
            }
        }
        
        private void ProcessServerTick()
        {
            if(!IsServer) return;
            
            int bufferIndex = -1;
            
            InputPayload inputPayload = default;
            
            while (_serverInputQueue.Count > 0)
            {
                inputPayload = _serverInputQueue.Dequeue();
                bufferIndex = inputPayload.Tick % BUFFER_SIZE;
                
                StatePayload serverState = SimulateMovement(inputPayload);
                
                serverCube.transform.position = new Vector3(serverState.Position.x, 1f, serverState.Position.z);
                
                _serverStateBuffer.Add(serverState, bufferIndex);
                
            }
            
            if(bufferIndex == -1) return;

            SendStateToClientRpc(_serverStateBuffer.Get(bufferIndex));
            ProcessExtrapolation(_serverStateBuffer.Get(bufferIndex), CalculateLatencyInMilliseconds(inputPayload));
        }

        

        private StatePayload SimulateMovement(InputPayload inputPayload)
        {
            Physics.simulationMode = SimulationMode.Script;
            
            float tickTime = 1f / SERVER_TICK_RATE;
            
            Move(inputPayload.InputVector);
            Look(inputPayload.LookVector);
            UpdateStamina(inputPayload.IsRunning, tickTime);
            
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
        
        private void ProcessExtrapolation(StatePayload lastState, float latency)
        {
            if(latency < extrapolationLimitInMilliseconds && latency > Time.fixedDeltaTime)
            {
                float axisLenght = latency * lastState.AngularVelocity.magnitude * Mathf.Deg2Rad;
                Quaternion angularRotation = Quaternion.AngleAxis(axisLenght,lastState.AngularVelocity);
                
                if(_extrapolationState.Position != default)
                {
                    lastState = _extrapolationState;

                    Vector3 positionCorrection = lastState.Velocity * (1 + latency * extrapolationMultiplier);
                    _extrapolationState.Position = positionCorrection;
                    _extrapolationState.Rotation = angularRotation * lastState.Rotation;
                    _extrapolationState.Velocity = lastState.Velocity;
                    _extrapolationState.AngularVelocity = lastState.AngularVelocity;
                    _extrapolationCooldownTimer.Start();
                }
                else
                {
                    _extrapolationCooldownTimer.Stop();
                }
                
            }
            
        }

        private void Extrapolate()
        {
            if (IsServer && _extrapolationCooldownTimer.IsRunning)
            {
                transform.position +=  new Vector3(_extrapolationState.Position.x, 0f, _extrapolationState.Position.z);
            }
        }
        
        private static float CalculateLatencyInMilliseconds(InputPayload inputPayload)
        {
            return (DateTime.Now - inputPayload.TimeStamp).Milliseconds / 1000f;
            
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
