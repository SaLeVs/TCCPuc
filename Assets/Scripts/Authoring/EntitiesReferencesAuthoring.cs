using Unity.Entities;
using UnityEngine;

public class EntitiesReferencesAuthoring : MonoBehaviour
{
    public GameObject playerPrefab;
}

public class EntitiesReferencesAuthoringBaker : Baker<EntitiesReferencesAuthoring>
{
    public override void Bake(EntitiesReferencesAuthoring authoring)
    {
        Entity entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new EntitiesReferences
        {
            playerPrefabEntity = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic)
        });
    }
}

public struct EntitiesReferences : IComponentData
{
    public Entity playerPrefabEntity;
    
}