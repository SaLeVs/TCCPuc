using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    partial struct InGameServerSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EntitiesReferences>();
        }
        
        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach ((RefRO<ReceiveRpcCommandRequest> receiveRpcCommandRequest, Entity entity) in 
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<InGameRequestRpc>().WithEntityAccess())
            {
                entityCommandBuffer.AddComponent<NetworkStreamInGame>(receiveRpcCommandRequest.ValueRO.SourceConnection); 
                Debug.Log("Client connected to Server");
                entityCommandBuffer.DestroyEntity(entity);
            }
            
            entityCommandBuffer.Playback(state.EntityManager);
        }
    }
}

