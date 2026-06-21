namespace Missions
{
    public interface IMissionOwnerAware
    {
        int ItemId { get; }
        void SetOwnershipSelector(MissionsManagerBase manager);
    }
}