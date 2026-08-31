using System;
using Unity.Collections;
using Unity.Netcode;

namespace Missions.Donations
{
    public struct DonationNetworkState : INetworkSerializable, IEquatable<DonationNetworkState>
    {
        public FixedString64Bytes InstanceId;
        public FixedString64Bytes DonationId;
        public FixedString32Bytes DonorName;
        public FixedString128Bytes Message;
        public float Amount;
        public float Progress;
        public double SpawnTime;
        public double ExpireTime;
        public DonationState State;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref InstanceId);
            serializer.SerializeValue(ref DonationId);
            serializer.SerializeValue(ref DonorName);
            serializer.SerializeValue(ref Message);
            serializer.SerializeValue(ref Amount);
            serializer.SerializeValue(ref Progress);
            serializer.SerializeValue(ref SpawnTime);
            serializer.SerializeValue(ref ExpireTime);
            serializer.SerializeValue(ref State);
        }

        public bool Equals(DonationNetworkState other)
        {
            return InstanceId.Equals(other.InstanceId)
                   && Progress.Equals(other.Progress)
                   && State == other.State
                   && ExpireTime.Equals(other.ExpireTime);
        }
    }
}