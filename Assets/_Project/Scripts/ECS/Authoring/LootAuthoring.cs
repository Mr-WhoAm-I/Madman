using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components.Core;

namespace _Project.Scripts.ECS.Authoring
{
    public class LootAuthoring : MonoBehaviour
    {
        // Этот класс "запекает" (Bake) наш GameObject с компонентами в чистую ECS-сущность
        class Baker : Baker<LootAuthoring>
        {
            public override void Bake(LootAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Добавляем компоненты лута (со значениями по умолчанию, реальные значения выдаст враг при смерти)
                AddComponent(entity, new LootComponent { Value = 1 });
                AddComponent(entity, new MagnetStateComponent { IsPulled = false, TargetEntity = Entity.Null });
                AddComponent(entity, new LootAnimationComponent());
            }
        }
    }
}