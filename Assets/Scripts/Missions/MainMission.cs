using Unity.Netcode;

namespace Missions
{
    public abstract class MainMission : NetworkBehaviour
    {
        public abstract void StartMission();
        public abstract void AbortMission();
    }
}