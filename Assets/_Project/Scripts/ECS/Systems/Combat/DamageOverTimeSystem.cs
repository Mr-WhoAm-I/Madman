using _Project.Scripts.Data.Weapons; // Нужно для WeaponElementalType
using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.ECS.Components.Skills;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Combat
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateBefore(typeof(DamageSystem))] 
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

            // 2. ИСТЯЗАНИЕ (Яд от пуль клона)
            foreach (var (poison, enemyHealth, transform, entity) in SystemAPI.Query<RefRO<PoisonDebuffComponent>, RefRW<EnemyHealthComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float poisonTick = poison.ValueRO.DPS * deltaTime;
                enemyHealth.ValueRW.CurrentHealth -= poisonTick;
                
                // Для яда не спавним цифры каждый кадр, так как это DPS, иначе экран утонет в цифрах
                CheckDeathForHarvest(ref state, ecb, enemyHealth.ValueRO, entity, poison.ValueRO.OwnerEntity);
            }

            // ==========================================
            // 3. ГОРЕНИЕ (Элементальные огненные пули)
            // ==========================================
            foreach (var (burn, enemyHealth, transform, entity) in SystemAPI.Query<RefRW<BurningDebuffComponent>, RefRW<EnemyHealthComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                burn.ValueRW.Timer -= deltaTime;
                if (burn.ValueRO.Timer <= 0f)
                {
                    // Сбрасываем таймер для следующего тика
                    burn.ValueRW.Timer = burn.ValueRO.TickRate;
                    burn.ValueRW.TicksRemaining -= 1;

                    float tickDamage = burn.ValueRO.DamagePerTick;
                    enemyHealth.ValueRW.CurrentHealth -= tickDamage;

                    // ВЫЗЫВАЕМ ОТРИСОВКУ ОРАНЖЕВОЙ ЦИФРЫ (Крит всегда false для горения)
                    DamageSystem.OnEnemyDamaged?.Invoke(transform.ValueRO.Position, tickDamage, WeaponElementalType.Fire, false);

                    CheckDeathForHarvest(ref state, ecb, enemyHealth.ValueRO, entity, burn.ValueRO.SourceEntity);

                    // Если тики горения закончились - тушим врага
                    if (burn.ValueRO.TicksRemaining <= 0)
                    {
                        ecb.RemoveComponent<BurningDebuffComponent>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

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