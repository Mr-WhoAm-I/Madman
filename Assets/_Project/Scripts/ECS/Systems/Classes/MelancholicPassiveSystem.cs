using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network.Managers;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems.Classes
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(Enemies.EnemyMovementSystem))]
    [UpdateAfter(typeof(Combat.EnemyBulletCollisionSystem))]
    [UpdateBefore(typeof(Combat.DamageSystem))] 
    public partial struct MelancholicPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var swarmManager = EnemySwarmManager.Instance;
            if (swarmManager == null || swarmManager.Runner == null || !swarmManager.Runner.IsForward) return;

            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent)) return;

            float deltaTime = timeComponent.DeltaTime;
            
            // Вычисляем точное сетевое время симуляции через тики Fusion
            float elapsedTime = swarmManager.Runner.Tick * swarmManager.Runner.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // === МЕХАНИКА: АУРА УНЫНИЯ (Глобальный тик раз в 2 секунды) ===
            bool isAuraTick = math.floor(elapsedTime / 2.0f) > math.floor((elapsedTime - deltaTime) / 2.0f);
            if (isAuraTick)
            {
                foreach (var (config, playerTransform, playerEntity) in SystemAPI.Query<RefRO<SkillConfigComponent>, RefRO<LocalTransform>>().WithAll<MelancholicTag>().WithEntityAccess())
                {
                    if (config.ValueRO.AuraRadius > 0f)
                    {
                        foreach (var (enemyHealth, enemyTransform, enemyEntity) in SystemAPI.Query<RefRO<EnemyHealthComponent>, RefRO<LocalTransform>>().WithEntityAccess())
                        {
                            if (math.distance(playerTransform.ValueRO.Position, enemyTransform.ValueRO.Position) <= config.ValueRO.AuraRadius)
                            {
                                ApplyApathyStack(ref state, ecb, enemyEntity, playerEntity, config.ValueRO, "попал в Ауру уныния");
                            }
                        }
                    }
                }
            }

            // =========================================================================
            // 1. ПЕРЕХВАТ СОБЫТИЙ УРОНА
            // =========================================================================
            foreach (var (takeDamage, victimEntity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                Entity attacker = takeDamage.ValueRO.SourceEntity;

                bool isMelancholicAttacking = attacker != Entity.Null && SystemAPI.HasComponent<MelancholicTag>(attacker);
                bool isMelancholicAttacked = SystemAPI.HasComponent<MelancholicTag>(victimEntity);

                if (isMelancholicAttacking || isMelancholicAttacked)
                {
                    Entity debuffTarget = isMelancholicAttacking ? victimEntity : attacker;
                    Entity melancholicEntity = isMelancholicAttacking ? attacker : victimEntity;

                    if (debuffTarget == Entity.Null || !SystemAPI.HasComponent<EnemyTagComponent>(debuffTarget)) continue;
                    if (!SystemAPI.HasComponent<SkillConfigComponent>(melancholicEntity)) continue;

                    var config = SystemAPI.GetComponent<SkillConfigComponent>(melancholicEntity);

                    if (isMelancholicAttacking)
                    {
                        ecb.AddComponent(debuffTarget, new FrostSlowComponent
                        {
                            SpeedMultiplier = config.FrostSlowMultiplier,
                            TimeRemaining = 1.5f 
                        });
                    }

                    string reason = isMelancholicAttacking ? "получил пулю" : "ударил Меланхолика";
                    ApplyApathyStack(ref state, ecb, debuffTarget, melancholicEntity, config, reason);
                }
            }

            // =========================================================================
            // 2. ОБСЛУЖИВАНИЕ ТАЙМЕРОВ ДЕБАФФОВ
            // =========================================================================
            foreach (var (slow, entity) in SystemAPI.Query<RefRW<FrostSlowComponent>>().WithEntityAccess())
            {
                slow.ValueRW.TimeRemaining -= deltaTime;
                if (slow.ValueRO.TimeRemaining <= 0f)
                {
                    ecb.RemoveComponent<FrostSlowComponent>(entity);
                }
            }

            foreach (var (apathy, entity) in SystemAPI.Query<RefRW<ApathyDebuffComponent>>().WithEntityAccess())
            {
                if (apathy.ValueRO.FreezeTimer > 0f)
                {
                    apathy.ValueRW.FreezeTimer -= deltaTime;
                    if (apathy.ValueRO.FreezeTimer <= 0f)
                    {
                        Debug.Log($"<color=#87CEFA>[ОТТЕПЕЛЬ]</color> Враг {entity.Index} оттаял! Стаки сброшены.");
                        ecb.RemoveComponent<ApathyDebuffComponent>(entity);
                        ecb.RemoveComponent<FrostVulnerabilityComponent>(entity); // Снимаем уязвимость
                    }
                }
                else if (apathy.ValueRO.DebuffLifeTimer > 0f)
                {
                    apathy.ValueRW.DebuffLifeTimer -= deltaTime;
                    if (apathy.ValueRO.DebuffLifeTimer <= 0f)
                    {
                        ecb.RemoveComponent<ApathyDebuffComponent>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // Вынес наложение стаков в отдельный метод, чтобы вызывать и от пуль, и от Ауры
        private void ApplyApathyStack(ref SystemState state, EntityCommandBuffer ecb, Entity debuffTarget, Entity melancholicEntity, SkillConfigComponent config, string reason)
        {
            int currentStacks = 0;
            float freezeTimer = 0f;

            if (SystemAPI.HasComponent<ApathyDebuffComponent>(debuffTarget))
            {
                var existingApathy = SystemAPI.GetComponent<ApathyDebuffComponent>(debuffTarget);
                currentStacks = existingApathy.CurrentStacks;
                freezeTimer = existingApathy.FreezeTimer;
            }

            if (freezeTimer <= 0f)
            {
                currentStacks++;
                float lifeTimer = 5.0f; 

                if (currentStacks >= config.ApathyMaxStacks)
                {
                    freezeTimer = config.FreezeDuration; 
                    Debug.Log($"<color=#0000FF><b>[ЗАМОРОЗКА!]</b></color> Враг {debuffTarget.Index} накопил {currentStacks} стаков и ПРЕВРАТИЛСЯ В ЛЕД!");

                    // === МЕХАНИКА: ХРУПКИЙ ЛЕД (Уязвимость) ===
                    if (config.FrostVulnerability > 0f)
                    {
                        ecb.AddComponent(debuffTarget, new FrostVulnerabilityComponent { Multiplier = config.FrostVulnerability });
                        Debug.Log($"<color=#E0FFFF>[ХРУПКИЙ ЛЕД]</color> Враг {debuffTarget.Index} уязвим! +{config.FrostVulnerability * 100}% урона.");
                    }

                    // === МЕХАНИКА: ЛЕДЯНОЙ ДОСПЕХ ===
                    if (melancholicEntity != Entity.Null && config.ShieldPerFreeze > 0f && SystemAPI.HasComponent<EnergyShieldComponent>(melancholicEntity))
                    {
                        var shield = SystemAPI.GetComponent<EnergyShieldComponent>(melancholicEntity);
                        float restored = shield.MaxShield * config.ShieldPerFreeze;
                        shield.CurrentShield = math.min(shield.MaxShield, shield.CurrentShield + restored);
                        ecb.SetComponent(melancholicEntity, shield);
                        Debug.Log($"<color=#00FFFF>[ЛЕДЯНОЙ ДОСПЕХ]</color> Восстановлено {restored} ед. щита за заморозку!");
                    }
                }
                else
                {
                    // Логируем только если это не от спама Ауры
                    if (reason != "попал в Ауру уныния")
                    {
                        Debug.Log($"<color=#ADD8E6>[АПАТИЯ]</color> Враг {debuffTarget.Index} {reason}. Стаки: {currentStacks} / {config.ApathyMaxStacks}");
                    }
                }

                ecb.AddComponent(debuffTarget, new ApathyDebuffComponent
                {
                    CurrentStacks = currentStacks,
                    FreezeTimer = freezeTimer,
                    DebuffLifeTimer = lifeTimer
                });
            }
        }
    }
}