using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    partial struct InGameServerSystem : ISystem
    {
        
        public void OnCreate(ref SystemState state)
        {
            // We only want to run this update on the server when we have EntitiesReferences and at least one client in game (NetworkId)
            state.RequireForUpdate<EntitiesReferences>();
            state.RequireForUpdate<NetworkId>(); 
        }
        
        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
            EntitiesReferences entitiesReferences = SystemAPI.GetSingleton<EntitiesReferences>();

            foreach ((RefRO<ReceiveRpcCommandRequest> receiveRpcCommandRequest, Entity entity) in 
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<InGameRequestRpc>().WithEntityAccess())
            {
                entityCommandBuffer.AddComponent<NetworkStreamInGame>(receiveRpcCommandRequest.ValueRO.SourceConnection); 
                Debug.Log("Client connected to Server");
                
                
               Entity playerEntity = entityCommandBuffer.Instantiate(entitiesReferences.playerPrefabEntity);
               
               entityCommandBuffer.SetComponent(playerEntity, LocalTransform.FromPosition(new float3(
                   UnityEngine.Random.Range(-10, 10), 0, 0
                   )));

               NetworkId networkId =  SystemAPI.GetComponent<NetworkId>(receiveRpcCommandRequest.ValueRO.SourceConnection);
               
               entityCommandBuffer.AddComponent(playerEntity, new GhostOwner
               {
                   NetworkId = networkId.Value
               });
               
               // When the connection is destroyed, all entities in the group will be destroyed as well, including the player entity.
               entityCommandBuffer.AppendToBuffer(receiveRpcCommandRequest.ValueRO.SourceConnection, new LinkedEntityGroup
               {
                   Value = playerEntity
               });
               
               entityCommandBuffer.DestroyEntity(entity);
            }
            
            entityCommandBuffer.Playback(state.EntityManager);
        }
    }
}

