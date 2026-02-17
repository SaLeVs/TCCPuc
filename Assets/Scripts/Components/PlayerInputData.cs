using Unity.Netcode;
using UnityEngine;

namespace Components
{
    public struct PlayerInputData : INetworkSerializable
    {
        public int tick;
        public Vector2 movementInput;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tick);
            serializer.SerializeValue(ref movementInput);
        }
    }
}