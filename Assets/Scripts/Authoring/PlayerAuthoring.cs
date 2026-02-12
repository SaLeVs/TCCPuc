using Unity.Entities;
using UnityEngine;

namespace Components
{
    public class PlayerAuthoring : MonoBehaviour
    {
    
    }
    
    public class PlayerAuthoringBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Player());
        }
    }
    
    public struct Player : IComponentData
    {
    
    }
}

