using _Project.Scripts.ECS.Components;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(EnemyMovementSystem))]
    [UpdateAfter(typeof(EnemyBulletCollisionSystem))]
    [UpdateBefore(typeof(DamageSystem))] 
    public partial struct MelancholicPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // === ИСПРАВЛЕНО: Безопасная проверка на null для Runner ===
            var swarmManager = Network.EnemySwarmManager.Instance;
            if (swarmManager == null || swarmManager.Runner == null || !swarmManager.Runner.IsForward) return;

            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =========================================================================
            // 1. ПЕРЕХВАТ СОБЫТИЙ УРОНА (ОБНОВЛЕННАЯ ЛОГИКА)
            // =========================================================================
            foreach (var (takeDamage, victimEntity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                Entity attacker = takeDamage.ValueRO.SourceEntity;

                bool isMelancholicAttacking = attacker != Entity.Null && SystemAPI.HasComponent<MelancholicTag>(attacker);
                bool isMelancholicAttacked = SystemAPI.HasComponent<MelancholicTag>(victimEntity);

                // Если Меланхолик участвует в бою (бьет сам ИЛИ бьют его)
                if (isMelancholicAttacking || isMelancholicAttacked)
                {
                    // Определяем, кто получает дебафф, а кто является источником навыка
                    Entity debuffTarget = isMelancholicAttacking ? victimEntity : attacker;
                    Entity melancholicEntity = isMelancholicAttacking ? attacker : victimEntity;

                    if (debuffTarget == Entity.Null || !SystemAPI.HasComponent<EnemyTagComponent>(debuffTarget)) continue;
                    if (!SystemAPI.HasComponent<SkillConfigComponent>(melancholicEntity)) continue;

                    var config = SystemAPI.GetComponent<SkillConfigComponent>(melancholicEntity);

                    // Если Меланхолик стреляет — дополнительно накладываем замедление
                    if (isMelancholicAttacking)
                    {
                        ecb.AddComponent(debuffTarget, new FrostSlowComponent
                        {
                            SpeedMultiplier = config.FrostSlowMultiplier,
                            TimeRemaining = 1.5f 
                        });
                    }

                    // НАЧИСЛЕНИЕ СТАКА АПАТИИ (И за выстрел, и за получение урона)
                    int currentStacks = 0;
                    float freezeTimer = 0f;

                    if (SystemAPI.HasComponent<ApathyDebuffComponent>(debuffTarget))
                    {
                        var existingApathy = SystemAPI.GetComponent<ApathyDebuffComponent>(debuffTarget);
                        currentStacks = existingApathy.CurrentStacks;
                        freezeTimer = existingApathy.FreezeTimer;
                    }

                    // Если враг еще не заморожен полностью — добавляем стак
                    if (freezeTimer <= 0f)
                    {
                        currentStacks++;
                        float lifeTimer = 5.0f; // Время до спадения стаков

                        if (currentStacks >= config.ApathyMaxStacks)
                        {
                            freezeTimer = config.FreezeDuration; 
                            Debug.Log($"<color=#0000FF><b>[ЗАМОРОЗКА!]</b></color> Враг {debuffTarget.Index} накопил {currentStacks} стаков и ПРЕВРАТИЛСЯ В ЛЕД на {freezeTimer} сек!");
                        }
                        else
                        {
                            string reason = isMelancholicAttacking ? "получил пулю" : "ударил Меланхолика";
                            Debug.Log($"<color=#ADD8E6>[АПАТИЯ]</color> Враг {debuffTarget.Index} {reason}. Стаки: {currentStacks} / {config.ApathyMaxStacks}");
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
    }
}