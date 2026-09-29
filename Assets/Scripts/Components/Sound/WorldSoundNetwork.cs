using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Components.Sound
{
    /// <summary>
    /// Carries world sounds between peers. One lives on an in-scene object in every networked scene.
    ///
    /// <para>The server sends to everyone, itself included. A client plays its own sound at once —
    /// its footsteps should not wait a round trip — and asks the server to pass it on to everybody
    /// else, so nobody hears it twice.</para>
    /// </summary>
    public class WorldSoundNetwork : NetworkBehaviour
    {
        public static WorldSoundNetwork Instance { get; private set; }

        [SerializeField] private SoundLibrarySO library;

        private readonly HashSet<SoundDefinitionSO> _reportedMissing = new();


        public override void OnNetworkSpawn()
        {
            Instance = this;
        }

        internal void Broadcast(SoundDefinitionSO sound, Vector3 position, NetworkObject emitter, float loudness)
        {
            Transform emitterTransform = emitter != null ? emitter.transform : null;

            if (library == null || !library.TryGetId(sound, out ushort id))
            {
                ReportMissing(sound);
                WorldSound.PlayHere(sound, position, emitterTransform, loudness);
                return;
            }

            bool hasEmitter = emitter != null && emitter.IsSpawned;
            NetworkObjectReference emitterReference = hasEmitter ? new NetworkObjectReference(emitter) : default;

            if (IsServer)
            {
                PlayRpc(id, position, loudness, hasEmitter, emitterReference, RpcTarget.Everyone);
                return;
            }

            WorldSound.PlayHere(sound, position, emitterTransform, loudness);
            RelayRpc(id, position, loudness, hasEmitter, emitterReference);
        }

        [Rpc(SendTo.Server)]
        private void RelayRpc(ushort id, Vector3 position, float loudness, bool hasEmitter, NetworkObjectReference emitter,
            RpcParams rpcParams = default)
        {
            // The sender already heard it.
            PlayRpc(id, position, loudness, hasEmitter, emitter,
                RpcTarget.Not(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void PlayRpc(ushort id, Vector3 position, float loudness, bool hasEmitter, NetworkObjectReference emitter,
            RpcParams rpcParams)
        {
            SoundDefinitionSO sound = library.Get(id);
            if (sound == null) return;

            Transform emitterTransform = hasEmitter && emitter.TryGet(out NetworkObject emitterObject)
                ? emitterObject.transform
                : null;

            WorldSound.PlayHere(sound, position, emitterTransform, loudness);
        }

        private void ReportMissing(SoundDefinitionSO sound)
        {
            if (!_reportedMissing.Add(sound)) return;

            Debug.LogError($"{nameof(WorldSoundNetwork)}: '{sound.name}' is not in the Sound Library, so only this " +
                           "machine hears it. Add it to the library (context menu: Collect every Sound Definition).", this);
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
