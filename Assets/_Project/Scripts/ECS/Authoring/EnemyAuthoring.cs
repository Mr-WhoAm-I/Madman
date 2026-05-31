using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using Unity.Mathematics;
using Unity.Rendering;

namespace _Project.Scripts.ECS.Authoring
{
    public class EnemyAuthoring : MonoBehaviour
    {
        public float speed = 2f; // Скорость врагов по умолчанию
        public float maxHealth = 50f;

        class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTagComponent>(entity);
                
                // Добавляем компонент движения
                AddComponent(entity, new EnemyMovementComponent { Speed = authoring.speed });
                AddComponent(entity, new EnemyHealthComponent { CurrentHealth = authoring.maxHealth });
                AddComponent(entity, new DamageFlashComponent { Timer = 0f });
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(1f, 0f, 0f, 1f) });
                
            }
        }
    }
}