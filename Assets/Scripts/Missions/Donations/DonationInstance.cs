namespace Missions.Donations
{
    public class DonationInstance
    {
        public string InstanceId;
        public DonationDefinition Definition;
        public string DonorName;
        public float Amount;
        public double SpawnTime;
        public double ExpireTime;

        /// <summary>The player it is addressed to: their voice reads it out, so whoever is near them hears it.</summary>
        public ulong RecipientClientId;
        public string RecipientName;

        public DonationState State;
        public float Progress;

        public bool IsExpired(double now) => ExpireTime > 0 && now >= ExpireTime;
    }
}
