using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Authoring
{
    // ECS-компонент, который будет хранить ссылку на префаб
    public struct LootRegistryComponent : IComponentData
    {
        public Entity FragmentPrefab;
    }

    public class LootRegistryAuthoring : MonoBehaviour
    {
        public GameObject FragmentPrefab; // Сюда мы перетащим твой префаб монетки в инспекторе

        class Baker : Baker<LootRegistryAuthoring>
        {
            public override void Bake(LootRegistryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new LootRegistryComponent 
                {
                    // Превращаем GameObject префаб в ECS Entity префаб
                    FragmentPrefab = GetEntity(authoring.FragmentPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}