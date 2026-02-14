using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace Network
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    partial struct InGameClientSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkId>(); 
        }
        
        // [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp); // We use an EntityCommandBuffer to make structural changes
            
            // NetworkStreamInGame component is added to the player entity when the client has finished loading the game and is ready to receive game data
            foreach ((RefRO<NetworkId> networkId, Entity entity) in SystemAPI.Query<RefRO<NetworkId>>().WithNone<NetworkStreamInGame>().WithEntityAccess())
            {
                entityCommandBuffer.AddComponent<NetworkStreamInGame>(entity);
                Debug.Log($"Setup client in game: {networkId.ValueRO.Value}");
                
                Entity rpcEntity = entityCommandBuffer.CreateEntity();
                entityCommandBuffer.AddComponent(rpcEntity, new InGameRequestRpc());
                entityCommandBuffer.AddComponent(rpcEntity, new SendRpcCommandRequest());
            }

            entityCommandBuffer.Playback(state.EntityManager);
        }
    }
    
    public struct InGameRequestRpc : IRpcCommand
    {
        // Here we can add any data we want to send from the client to the server when the client is ready to receive game data.
    }
}

