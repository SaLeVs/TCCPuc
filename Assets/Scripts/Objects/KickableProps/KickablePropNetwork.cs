using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    /// <summary>
    /// Keeps every <see cref="KickableProp"/> in the same place for every peer, over named messages,
    /// so the props need no NetworkObject of their own.
    ///
    /// <para>Every peer keeps simulating its copy, so a kick answers at once. The peer that drives
    /// what touched a prop last (its own player; on the server the monster, doors, ragdolls) becomes
    /// its authority and streams its motion; the others follow that stream and snap to where it came
    /// to rest. The server settles who the authority is: each change bumps the prop's epoch, and a
    /// state from an older epoch is ignored, so two players kicking the same cup end up agreeing.</para>
    ///
    /// <para>Created on its own by the first prop; does nothing while no session is running.</para>
    /// </summary>
    public class KickablePropNetwork : MonoBehaviour
    {
        private const string StateMessage = "KickableProp.State";
        private const string AckMessage = "KickableProp.Ack";
        private const string SnapshotMessage = "KickableProp.Snapshot";

        private const float SendInterval = 0.05f;
        // Waits for the last prop of a scene or room to register before asking the server where they are.
        private const float SnapshotDelay = 0.5f;

        // Slower than this for RestAfter seconds is at rest, even if the physics keeps it awake.
        private const float StillLinearSpeed = 0.03f;
        private const float StillAngularSpeed = 0.15f;
        private const float RestAfter = 0.75f;

        private const float MinSendDistance = 0.002f;
        private const float MinSendAngle = 0.5f;
        // A follower that settles further than this from where its authority left it is put back.
        private const float RestTolerance = 0.02f;
        private const float RestAngleTolerance = 2f;

        // Keeps an unreliable message under one packet.
        private const int MaxUnreliableEntries = 14;
        private const int EntrySize = 4 + 8 + 2 + 2 + 1 + 12 + 16 + 12 + 12;
        private const int MaxReliableSize = 256 * 1024;
        private const int MaxEntriesPerMessage = 4096;
        // The epoch every peer starts each prop on, with the server as its authority.
        private const ushort InitialEpoch = 1;

        private const byte FlagRest = 1;
        private const byte FlagClaim = 2;
        // Where the server has the prop now, not a new step: taken even at the sequence already seen.
        private const byte FlagSnapshot = 4;

        private struct PropState
        {
            public uint Id;
            public ulong Authority;
            public ushort Epoch;
            public ushort Seq;
            public byte Flags;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }

        private static readonly List<KickableProp> Props = new List<KickableProp>();
        private static readonly List<KickableProp> Unresolved = new List<KickableProp>();
        private static readonly Dictionary<uint, KickableProp> ById = new Dictionary<uint, KickableProp>();
        private static readonly Dictionary<uint, PropState> PendingStates = new Dictionary<uint, PropState>();

        private static KickablePropNetwork _instance;
        private static bool _isQuitting;

        /// <summary>A session is running and the props are being kept in sync.</summary>
        public static bool IsActive { get; private set; }

        private readonly List<PropState> _streamBatch = new List<PropState>();
        private readonly List<PropState> _reliableBatch = new List<PropState>();
        private readonly List<PropState> _relayStream = new List<PropState>();
        private readonly List<PropState> _relayReliable = new List<PropState>();
        private readonly List<ulong> _targets = new List<ulong>();

        private NetworkManager _networkManager;
        private CustomMessagingManager _messaging;
        private float _sendTimer;
        private float _snapshotDueTime = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Props.Clear();
            Unresolved.Clear();
            ById.Clear();
            PendingStates.Clear();
            _instance = null;
            _isQuitting = false;
            IsActive = false;

            Application.quitting -= OnApplicationQuitting;
            Application.quitting += OnApplicationQuitting;
        }

        private static void OnApplicationQuitting()
        {
            _isQuitting = true;
        }

        internal static void Add(KickableProp prop)
        {
            if (Props.Contains(prop)) return;

            Props.Add(prop);
            Unresolved.Add(prop);
            prop.IsRegistered = false;
            prop.IsUnsynced = false;
            ResetState(prop);

            EnsureInstance();
        }

        internal static void Remove(KickableProp prop)
        {
            Props.Remove(prop);
            Unresolved.Remove(prop);

            if (prop.IsRegistered && ById.TryGetValue(prop.Id, out KickableProp registered) && registered == prop)
            {
                ById.Remove(prop.Id);
            }

            prop.IsRegistered = false;
            ResetState(prop);
        }

        /// <summary>
        /// Something this peer drives touched the prop: this peer streams it from now on. A client
        /// starts at once and the server confirms it with a new epoch; the server takes it straight
        /// away.
        /// </summary>
        internal static void Claim(KickableProp prop)
        {
            if (_instance == null || !IsActive || !prop.IsRegistered || prop.IsLocalAuthority) return;

            NetworkManager networkManager = _instance._networkManager;

            prop.IsLocalAuthority = true;
            prop.Authority = networkManager.LocalClientId;
            prop.PendingClaim = true;
            prop.StillTime = 0f;

            if (networkManager.IsServer)
            {
                prop.Epoch++;
            }
        }

        private static void EnsureInstance()
        {
            if (_instance != null || _isQuitting || !Application.isPlaying) return;

            GameObject host = new GameObject(nameof(KickablePropNetwork));
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<KickablePropNetwork>();
        }

        private static void ResetState(KickableProp prop)
        {
            prop.Authority = NetworkManager.ServerClientId;
            prop.Epoch = InitialEpoch;
            prop.LastSeq = 0;
            prop.SendSeq = 0;
            prop.IsLocalAuthority = false;
            prop.PendingClaim = false;
            prop.IsStreaming = false;
            prop.StillTime = 0f;
            prop.WasSleeping = true;
        }

        private void OnDestroy()
        {
            Deactivate();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            bool isRunning = networkManager != null && networkManager.IsListening &&
                             networkManager.CustomMessagingManager != null &&
                             (networkManager.IsServer || networkManager.IsConnectedClient);

            if (!isRunning)
            {
                if (IsActive) Deactivate();
                return;
            }

            if (!IsActive || networkManager != _networkManager || networkManager.CustomMessagingManager != _messaging)
            {
                Activate(networkManager);
            }

            ResolveUnregistered();

            if (!_networkManager.IsServer && _snapshotDueTime >= 0f && Time.unscaledTime >= _snapshotDueTime)
            {
                _snapshotDueTime = -1f;
                RequestSnapshot();
            }

            _sendTimer += Time.unscaledDeltaTime;
            if (_sendTimer < SendInterval) return;

            float elapsed = _sendTimer;
            _sendTimer = 0f;
            Tick(elapsed);
        }

        private void Activate(NetworkManager networkManager)
        {
            // A new session: whatever was known about the last one no longer holds.
            if (IsActive) Deactivate();

            _networkManager = networkManager;
            _messaging = networkManager.CustomMessagingManager;

            _messaging.RegisterNamedMessageHandler(StateMessage, OnStateMessage);
            _messaging.RegisterNamedMessageHandler(AckMessage, OnAckMessage);
            _messaging.RegisterNamedMessageHandler(SnapshotMessage, OnSnapshotRequest);
            _networkManager.OnClientDisconnectCallback += OnClientDisconnect;

            IsActive = true;
        }

        private void Deactivate()
        {
            if (_networkManager != null)
            {
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnect;

                if (_messaging != null && _networkManager.CustomMessagingManager == _messaging)
                {
                    _messaging.UnregisterNamedMessageHandler(StateMessage);
                    _messaging.UnregisterNamedMessageHandler(AckMessage);
                    _messaging.UnregisterNamedMessageHandler(SnapshotMessage);
                }
            }

            _networkManager = null;
            _messaging = null;
            _snapshotDueTime = -1f;
            IsActive = false;

            ById.Clear();
            PendingStates.Clear();
            Unresolved.Clear();

            foreach (KickableProp prop in Props)
            {
                if (prop == null) continue;

                prop.IsRegistered = false;
                prop.IsUnsynced = false;
                ResetState(prop);
                Unresolved.Add(prop);
            }
        }

        /// <summary>Gives an id to every prop whose id is known by now (a prop in a room waits for the room to spawn).</summary>
        private void ResolveUnregistered()
        {
            for (int i = Unresolved.Count - 1; i >= 0; i--)
            {
                KickableProp prop = Unresolved[i];

                if (prop == null)
                {
                    Unresolved.RemoveAt(i);
                    continue;
                }

                if (!prop.TryResolveId(out uint id)) continue;

                Unresolved.RemoveAt(i);

                if (ById.TryGetValue(id, out KickableProp other) && other != null && other != prop)
                {
                    prop.IsUnsynced = true;
                    Debug.LogWarning($"[{nameof(KickablePropNetwork)}] '{prop.name}' has the same id as '{other.name}'; it is not synced.", prop);
                    continue;
                }

                ById[id] = prop;
                prop.IsRegistered = true;
                ResetState(prop);

                Rigidbody body = prop.Body;
                prop.RestPosition = body.position;
                prop.RestRotation = body.rotation;
                // Where it was placed counts as sent: settling onto its shelf at load sends nothing.
                prop.LastSentPosition = body.position;
                prop.LastSentRotation = body.rotation;
                prop.WasSleeping = body.IsSleeping();

                if (_networkManager.IsServer)
                {
                    prop.IsLocalAuthority = true;
                }
                else
                {
                    _snapshotDueTime = Time.unscaledTime + SnapshotDelay;
                }

                if (PendingStates.TryGetValue(id, out PropState pending))
                {
                    PendingStates.Remove(id);
                    ApplyIncoming(prop, pending);
                }
            }
        }

        private void Tick(float elapsed)
        {
            _streamBatch.Clear();
            _reliableBatch.Clear();

            for (int i = 0; i < Props.Count; i++)
            {
                KickableProp prop = Props[i];
                if (prop == null || !prop.IsRegistered) continue;

                bool sleeping = prop.Body.IsSleeping();

                if (prop.IsLocalAuthority)
                {
                    TickAuthority(prop, sleeping, elapsed);
                }
                else
                {
                    TickFollower(prop, sleeping);
                }

                prop.WasSleeping = prop.Body.IsSleeping();
            }

            if (_networkManager.IsServer)
            {
                BuildTargets(_networkManager.LocalClientId);
                SendStates(_streamBatch, NetworkDelivery.UnreliableSequenced, 0, true);
                SendStates(_reliableBatch, NetworkDelivery.ReliableFragmentedSequenced, 0, true);
            }
            else
            {
                SendStates(_streamBatch, NetworkDelivery.UnreliableSequenced, NetworkManager.ServerClientId, false);
                SendStates(_reliableBatch, NetworkDelivery.ReliableFragmentedSequenced, NetworkManager.ServerClientId, false);
            }
        }

        private void TickAuthority(KickableProp prop, bool sleeping, float elapsed)
        {
            Rigidbody body = prop.Body;

            if (!sleeping)
            {
                bool isStill = body.linearVelocity.sqrMagnitude < StillLinearSpeed * StillLinearSpeed &&
                               body.angularVelocity.sqrMagnitude < StillAngularSpeed * StillAngularSpeed;
                prop.StillTime = isStill ? prop.StillTime + elapsed : 0f;

                // A cup rocking on its rim can stay awake for long; past this it is at rest for everyone.
                if (prop.StillTime >= RestAfter)
                {
                    // Sleep keeps the velocity it had, and the body creeps on with it once woken.
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.Sleep();
                    sleeping = true;
                }
            }

            if (prop.PendingClaim)
            {
                prop.PendingClaim = false;
                prop.IsStreaming = !sleeping;
                _reliableBatch.Add(Capture(prop, (byte)(FlagClaim | (sleeping ? FlagRest : 0))));
                return;
            }

            if (!sleeping)
            {
                bool hasMoved = (body.position - prop.LastSentPosition).sqrMagnitude > MinSendDistance * MinSendDistance ||
                                Quaternion.Angle(body.rotation, prop.LastSentRotation) > MinSendAngle;

                if (hasMoved)
                {
                    prop.IsStreaming = true;
                    _streamBatch.Add(Capture(prop, 0));
                }
            }
            else if (prop.IsStreaming)
            {
                prop.IsStreaming = false;
                _reliableBatch.Add(Capture(prop, FlagRest));
            }
        }

        /// <summary>
        /// A follower that settled on its own away from where its authority left it was moved by
        /// something only this peer saw (a remote player's copy, which runs a little behind).
        /// It goes back.
        /// </summary>
        private static void TickFollower(KickableProp prop, bool sleeping)
        {
            // IsStreaming on a follower: its authority is still sending, it hasn't come to rest.
            if (!sleeping || prop.WasSleeping || prop.IsStreaming) return;

            Rigidbody body = prop.Body;
            bool isAway = (body.position - prop.RestPosition).sqrMagnitude > RestTolerance * RestTolerance ||
                          Quaternion.Angle(body.rotation, prop.RestRotation) > RestAngleTolerance;
            if (!isAway) return;

            PlaceAtRest(prop, prop.RestPosition, prop.RestRotation);
        }

        private PropState Capture(KickableProp prop, byte flags)
        {
            Rigidbody body = prop.Body;
            bool isRest = (flags & FlagRest) != 0;

            prop.SendSeq++;
            prop.LastSentPosition = body.position;
            prop.LastSentRotation = body.rotation;

            if (isRest)
            {
                prop.RestPosition = body.position;
                prop.RestRotation = body.rotation;
            }

            if (_networkManager.IsServer)
            {
                prop.LastSeq = prop.SendSeq;
            }

            return new PropState
            {
                Id = prop.Id,
                Authority = _networkManager.LocalClientId,
                Epoch = prop.Epoch,
                Seq = prop.SendSeq,
                Flags = flags,
                Position = body.position,
                Rotation = body.rotation,
                Velocity = isRest ? Vector3.zero : body.linearVelocity,
                AngularVelocity = isRest ? Vector3.zero : body.angularVelocity
            };
        }

        private void OnStateMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsActive) return;

            reader.ReadValueSafe(out ushort count);
            if (count > MaxEntriesPerMessage) return;

            bool isServer = _networkManager.IsServer;
            _relayStream.Clear();
            _relayReliable.Clear();

            for (int i = 0; i < count; i++)
            {
                ReadState(ref reader, out PropState state);

                if (isServer)
                {
                    HandleClientState(senderClientId, state);
                }
                else
                {
                    HandleServerState(state);
                }
            }

            if (!isServer || (_relayStream.Count == 0 && _relayReliable.Count == 0)) return;

            BuildTargets(senderClientId);
            SendStates(_relayStream, NetworkDelivery.UnreliableSequenced, 0, true);
            SendStates(_relayReliable, NetworkDelivery.ReliableFragmentedSequenced, 0, true);
        }

        /// <summary>Server: a client's state is taken if that client is the authority, or is claiming the prop.</summary>
        private void HandleClientState(ulong senderClientId, PropState state)
        {
            if (senderClientId == _networkManager.LocalClientId) return;
            if (!ById.TryGetValue(state.Id, out KickableProp prop) || prop == null) return;

            bool isClaim = (state.Flags & FlagClaim) != 0;

            if (isClaim)
            {
                // The last claim to reach the server wins; the earlier claimer gets the new epoch
                // through the relay and lets go.
                if (prop.Authority != senderClientId)
                {
                    prop.Authority = senderClientId;
                    prop.Epoch++;
                    prop.IsLocalAuthority = false;
                    prop.PendingClaim = false;
                }

                SendAck(senderClientId, prop.Id, prop.Epoch);
            }
            else if (prop.Authority != senderClientId || !IsSeqNewer(state.Seq, prop.LastSeq))
            {
                return;
            }

            prop.LastSeq = state.Seq;
            ApplyFollow(prop, state);

            state.Authority = senderClientId;
            state.Epoch = prop.Epoch;
            state.Flags = (byte)(state.Flags & ~FlagClaim);

            if (isClaim || (state.Flags & FlagRest) != 0)
            {
                _relayReliable.Add(state);
            }
            else
            {
                _relayStream.Add(state);
            }
        }

        /// <summary>Client: a state from the server, kept for later if the prop isn't here yet.</summary>
        private void HandleServerState(PropState state)
        {
            if (ById.TryGetValue(state.Id, out KickableProp prop) && prop != null)
            {
                ApplyIncoming(prop, state);
                return;
            }

            if (PendingStates.TryGetValue(state.Id, out PropState kept) && !IsNewer(state, kept.Epoch, kept.Seq)) return;
            PendingStates[state.Id] = state;
        }

        private void ApplyIncoming(KickableProp prop, PropState state)
        {
            if (state.Authority == _networkManager.LocalClientId) return;

            int epochDelta = (short)(state.Epoch - prop.Epoch);
            if (epochDelta < 0) return;

            if (epochDelta == 0)
            {
                // Same epoch while claiming: a state from the authority this peer is taking over from.
                if (prop.IsLocalAuthority) return;

                // An untouched prop's snapshot carries the sequence the client already has (none sent yet).
                bool isSnapshot = (state.Flags & FlagSnapshot) != 0;
                if (isSnapshot ? IsSeqNewer(prop.LastSeq, state.Seq) : !IsSeqNewer(state.Seq, prop.LastSeq)) return;
            }

            prop.Authority = state.Authority;
            prop.Epoch = state.Epoch;
            prop.LastSeq = state.Seq;
            prop.IsLocalAuthority = false;
            prop.PendingClaim = false;

            ApplyFollow(prop, state);
        }

        private static void ApplyFollow(KickableProp prop, PropState state)
        {
            prop.StillTime = 0f;

            if ((state.Flags & FlagRest) != 0)
            {
                prop.IsStreaming = false;
                PlaceAtRest(prop, state.Position, state.Rotation);
                return;
            }

            prop.IsStreaming = true;

            Rigidbody body = prop.Body;
            body.position = state.Position;
            body.rotation = state.Rotation;
            prop.transform.SetPositionAndRotation(state.Position, state.Rotation);
            body.linearVelocity = state.Velocity;
            body.angularVelocity = state.AngularVelocity;
            body.WakeUp();
        }

        private static void PlaceAtRest(KickableProp prop, Vector3 position, Quaternion rotation)
        {
            prop.RestPosition = position;
            prop.RestRotation = rotation;

            Rigidbody body = prop.Body;
            body.position = position;
            body.rotation = rotation;
            prop.transform.SetPositionAndRotation(position, rotation);
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private void SendAck(ulong clientId, uint id, ushort epoch)
        {
            FastBufferWriter writer = new FastBufferWriter(sizeof(uint) + sizeof(ushort), Allocator.Temp);

            try
            {
                writer.WriteValueSafe(id);
                writer.WriteValueSafe(epoch);
                _messaging.SendNamedMessage(AckMessage, clientId, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>Client: the server confirmed a claim. A newer epoch already seen from someone else means it was overtaken.</summary>
        private void OnAckMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsActive || _networkManager.IsServer) return;

            reader.ReadValueSafe(out uint id);
            reader.ReadValueSafe(out ushort epoch);

            if (!ById.TryGetValue(id, out KickableProp prop) || prop == null) return;
            if ((short)(epoch - prop.Epoch) < 0) return;

            prop.Epoch = epoch;
            prop.Authority = _networkManager.LocalClientId;
            prop.IsLocalAuthority = true;
        }

        private void RequestSnapshot()
        {
            FastBufferWriter writer = new FastBufferWriter(sizeof(byte), Allocator.Temp);

            try
            {
                writer.WriteValueSafe((byte)0);
                _messaging.SendNamedMessage(SnapshotMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }
            finally
            {
                writer.Dispose();
            }
        }

        /// <summary>
        /// Server: tells a client where every prop is, for one that joined or loaded a room late.
        /// All of them, not only the kicked ones: a client's copy can settle differently while its
        /// scene loads (a cup slipping into a bin), and the server's pose is the one that counts.
        /// </summary>
        private void OnSnapshotRequest(ulong senderClientId, FastBufferReader reader)
        {
            if (!IsActive || !_networkManager.IsServer || senderClientId == _networkManager.LocalClientId) return;

            _reliableBatch.Clear();

            foreach (KickableProp prop in Props)
            {
                if (prop == null || !prop.IsRegistered) continue;

                Rigidbody body = prop.Body;
                bool isRest = body.IsSleeping();

                _reliableBatch.Add(new PropState
                {
                    Id = prop.Id,
                    Authority = prop.Authority,
                    Epoch = prop.Epoch,
                    Seq = prop.LastSeq,
                    Flags = (byte)(FlagSnapshot | (isRest ? FlagRest : 0)),
                    Position = body.position,
                    Rotation = body.rotation,
                    Velocity = isRest ? Vector3.zero : body.linearVelocity,
                    AngularVelocity = isRest ? Vector3.zero : body.angularVelocity
                });
            }

            SendStates(_reliableBatch, NetworkDelivery.ReliableFragmentedSequenced, senderClientId, false);
            _reliableBatch.Clear();
        }

        /// <summary>Server: the props a client that left was streaming go back to the server.</summary>
        private void OnClientDisconnect(ulong clientId)
        {
            if (!IsActive || !_networkManager.IsServer || clientId == _networkManager.LocalClientId) return;

            foreach (KickableProp prop in Props)
            {
                if (prop == null || !prop.IsRegistered || prop.Authority != clientId) continue;

                prop.Authority = _networkManager.LocalClientId;
                prop.Epoch++;
                prop.IsLocalAuthority = true;
                prop.PendingClaim = true;
            }
        }

        /// <summary>Server: every connected client but this peer and <paramref name="excluded"/>.</summary>
        private void BuildTargets(ulong excluded)
        {
            _targets.Clear();

            IReadOnlyList<ulong> clients = _networkManager.ConnectedClientsIds;
            for (int i = 0; i < clients.Count; i++)
            {
                ulong clientId = clients[i];
                if (clientId == _networkManager.LocalClientId || clientId == excluded) continue;
                _targets.Add(clientId);
            }
        }

        private void SendStates(List<PropState> states, NetworkDelivery delivery, ulong clientId, bool toTargets)
        {
            if (states.Count == 0) return;
            if (toTargets && _targets.Count == 0) return;

            bool isUnreliable = delivery == NetworkDelivery.Unreliable || delivery == NetworkDelivery.UnreliableSequenced;
            int chunk = isUnreliable ? MaxUnreliableEntries : MaxEntriesPerMessage;

            for (int start = 0; start < states.Count; start += chunk)
            {
                int count = Mathf.Min(chunk, states.Count - start);
                int size = sizeof(ushort) + count * EntrySize;
                FastBufferWriter writer = new FastBufferWriter(size, Allocator.Temp, Mathf.Max(size, isUnreliable ? size : MaxReliableSize));

                try
                {
                    writer.WriteValueSafe((ushort)count);

                    for (int i = start; i < start + count; i++)
                    {
                        WriteState(ref writer, states[i]);
                    }

                    if (toTargets)
                    {
                        _messaging.SendNamedMessage(StateMessage, _targets, writer, delivery);
                    }
                    else
                    {
                        _messaging.SendNamedMessage(StateMessage, clientId, writer, delivery);
                    }
                }
                finally
                {
                    writer.Dispose();
                }
            }
        }

        private static void WriteState(ref FastBufferWriter writer, in PropState state)
        {
            writer.WriteValueSafe(state.Id);
            writer.WriteValueSafe(state.Authority);
            writer.WriteValueSafe(state.Epoch);
            writer.WriteValueSafe(state.Seq);
            writer.WriteValueSafe(state.Flags);
            writer.WriteValueSafe(state.Position);
            writer.WriteValueSafe(state.Rotation);
            writer.WriteValueSafe(state.Velocity);
            writer.WriteValueSafe(state.AngularVelocity);
        }

        private static void ReadState(ref FastBufferReader reader, out PropState state)
        {
            state = default;
            reader.ReadValueSafe(out state.Id);
            reader.ReadValueSafe(out state.Authority);
            reader.ReadValueSafe(out state.Epoch);
            reader.ReadValueSafe(out state.Seq);
            reader.ReadValueSafe(out state.Flags);
            reader.ReadValueSafe(out state.Position);
            reader.ReadValueSafe(out state.Rotation);
            reader.ReadValueSafe(out state.Velocity);
            reader.ReadValueSafe(out state.AngularVelocity);
        }

        private static bool IsSeqNewer(ushort seq, ushort than)
        {
            return (short)(seq - than) > 0;
        }

        private static bool IsNewer(PropState state, ushort epoch, ushort seq)
        {
            int epochDelta = (short)(state.Epoch - epoch);
            return epochDelta > 0 || (epochDelta == 0 && IsSeqNewer(state.Seq, seq));
        }
    }
}
