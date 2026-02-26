using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public struct InputPayload : INetworkSerializable
    {
        public int Tick;
        public Vector3 InputVector;
        public Vector3 LookVector;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref InputVector);
            serializer.SerializeValue(ref LookVector);
        }
    }
}


