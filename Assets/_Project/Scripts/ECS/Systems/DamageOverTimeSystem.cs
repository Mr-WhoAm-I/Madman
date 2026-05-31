using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateBefore(typeof(DamageSystem))] // Наносим яд до основной обработки урона
    public partial struct DamageOverTimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent)) return;
            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // 1. ТОКСИЧНОЕ ОБЛАКО
            foreach (var (cloud, transform, entity) in SystemAPI.Query<RefRW<ToxicCloudComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                cloud.ValueRW.LifeTime -= deltaTime;
                if (cloud.ValueRO.LifeTime <= 0f)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                foreach (var (enemyHealth, enemyTransform, enemyEntity) in SystemAPI.Query<RefRW<EnemyHealthComponent>, RefRO<LocalTransform>>().WithEntityAccess())
                {
                    if (math.distance(transform.ValueRO.Position, enemyTransform.ValueRO.Position) <= cloud.ValueRO.Radius)
                    {
                        enemyHealth.ValueRW.CurrentHealth -= cloud.ValueRO.DPS * deltaTime;
                        CheckDeathForHarvest(ref state, ecb, enemyHealth.ValueRO, enemyEntity, cloud.ValueRO.OwnerEntity);
                    }
                }
            }

            // 2. ИСТЯЗАНИЕ (Яд от пуль)
            foreach (var (poison, enemyHealth, entity) in SystemAPI.Query<RefRO<PoisonDebuffComponent>, RefRW<EnemyHealthComponent>>().WithEntityAccess())
            {
                enemyHealth.ValueRW.CurrentHealth -= poison.ValueRO.DPS * deltaTime;
                CheckDeathForHarvest(ref state, ecb, enemyHealth.ValueRO, entity, poison.ValueRO.OwnerEntity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // Вспомогательный метод для проверки смерти от яда и прока Кровавой жатвы
        private void CheckDeathForHarvest(ref SystemState state, EntityCommandBuffer ecb, EnemyHealthComponent health, Entity enemyEntity, Entity ownerEntity)
        {
            if (health.CurrentHealth <= 0)
            {
                ecb.AddComponent<DeathTagComponent>(enemyEntity);
                
                if (ownerEntity != Entity.Null && state.EntityManager.HasComponent<SkillConfigComponent>(ownerEntity))
                {
                    var config = state.EntityManager.GetComponentData<SkillConfigComponent>(ownerEntity);
                    if (config.KillCooldownReduction > 0f && state.EntityManager.HasComponent<SkillStateComponent>(ownerEntity))
                    {
                        var skill = state.EntityManager.GetComponentData<SkillStateComponent>(ownerEntity);
                        skill.CurrentCooldown = math.max(0f, skill.CurrentCooldown - config.KillCooldownReduction);
                        state.EntityManager.SetComponentData(ownerEntity, skill);
                    }
                }
            }
        }
    }
}