using _Project.Scripts.ECS.Authoring; // Для доступа к LootRegistryComponent
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Enemies;
using Unity.Entities;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Combat
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(DamageSystem))] 
    public partial struct EnemyDeathSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // ИЩЕМ РЕЕСТР ПРЕФАБОВ: Если на сцене нет LootRegistryComponent, прерываемся
            if (!SystemAPI.TryGetSingleton<LootRegistryComponent>(out var registry))
                return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (transform, lootDrop, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLootDropComponent>>().WithAll<EnemyTagComponent, DeathTagComponent>().WithEntityAccess())
            {
                // 1. СПАВН ПРЕФАБА С ВИЗУАЛОМ (Вместо ecb.CreateEntity())
                var lootEntity = ecb.Instantiate(registry.FragmentPrefab);
                
                // 2. Устанавливаем позицию монетки туда, где умер враг
                ecb.SetComponent(lootEntity, LocalTransform.FromPosition(transform.ValueRO.Position));
                
                ecb.SetComponent(lootEntity, new LootAnimationComponent 
                {
                    BobbingSpeed = 4f,
                    BobbingAmount = 0.15f,
                    BasePosition = transform.ValueRO.Position, // Качаемся вокруг точки смерти врага
                    Timer = 0f
                });
                ecb.SetComponent(lootEntity, new LootComponent { Value = lootDrop.ValueRO.Bounty });
                ecb.SetComponent(lootEntity, new MagnetStateComponent { IsPulled = false, TargetEntity = Entity.Null });
                
                // 4. УДАЛЕНИЕ МЕРТВОГО ВРАГА
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}