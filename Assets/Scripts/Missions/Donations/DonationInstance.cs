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

        public DonationState State;
        public float Progress;

        public bool IsExpired(double now) => ExpireTime > 0 && now >= ExpireTime;
    }
}