using System;
using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public struct InputPayload : INetworkSerializable
    {
        public int Tick;
        public DateTime TimeStamp;
        public ulong NetworkObjectId;
        public Vector3 Position;
        public Vector3 InputVector;
        public Vector3 LookVector;
        public bool IsRunning;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref TimeStamp);
            serializer.SerializeValue(ref NetworkObjectId);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref InputVector);
            serializer.SerializeValue(ref LookVector);
            serializer.SerializeValue(ref IsRunning);
        }
    }
}


