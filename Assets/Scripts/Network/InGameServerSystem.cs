using Unity.Burst;
using Unity.Entities;

namespace Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    partial struct InGameServerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            
        }
    }
}

