namespace Interfaces
{
    public interface IMissionSpawnable
    {
        event System.Action OnSpawnCompleted;
        void RequestSpawn();
    }
}