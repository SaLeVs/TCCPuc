using Unity.Netcode;
using UnityEngine;

namespace Network
{
    public class StatePayload : INetworkSerializable
    {
        public int Tick;
        public ulong NetworkObjectId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float Stamina;
        
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Tick);
            serializer.SerializeValue(ref NetworkObjectId);
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Velocity);
            serializer.SerializeValue(ref AngularVelocity);
            serializer.SerializeValue(ref Stamina);
        }
    }
}

