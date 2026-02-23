using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public struct InputPayload : INetworkSerializable
    {
        public int Tick;
        public Vector3 InputVector;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref InputVector);
        }
    }
}


