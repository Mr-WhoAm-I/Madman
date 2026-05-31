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
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // ИСПРАВЛЕНО: Теперь читаем EnemyLootDropComponent
            foreach (var (transform, lootDrop, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyLootDropComponent>>().WithAll<EnemyTagComponent, DeathTagComponent>().WithEntityAccess())
            {
                // 1. СПАВН ЛОКАЛЬНОГО ЛУТА
                var lootEntity = ecb.CreateEntity();
                ecb.AddComponent(lootEntity, LocalTransform.FromPosition(transform.ValueRO.Position));
                
                // Берем награду конкретно этого врага!
                ecb.AddComponent(lootEntity, new LootComponent { Value = lootDrop.ValueRO.Bounty }); 
                
                ecb.AddComponent(lootEntity, new MagnetStateComponent { IsPulled = false, TargetEntity = Entity.Null });
                
                // 2. УДАЛЕНИЕ ВРАГА
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}