using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public struct PlayerStateData : INetworkSerializable
    {
        public int tick;
        public Vector3 position;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref position);
        }
    }
}