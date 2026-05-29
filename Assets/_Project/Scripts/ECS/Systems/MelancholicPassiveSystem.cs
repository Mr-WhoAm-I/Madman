using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    // КРИТИЧЕСКИ ВАЖНО: Мы обязаны успеть прочитать теги урона ДО того, 
    // как твоя DamageSystem отнимет здоровье и удалит компонент TakeDamageComponent.
    [UpdateBefore(typeof(DamageSystem))] 
    public partial struct MelancholicPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =========================================================================
            // 1. ПЕРЕХВАТ СОБЫТИЙ УРОНА (РЕАКТИВНАЯ МАГИЯ)
            // =========================================================================
            foreach (var (takeDamage, victimEntity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                Entity attacker = takeDamage.ValueRO.SourceEntity;

                // СИТУАЦИЯ А: Меланхолик наносит урон (Стрельба) -> Вешаем микро-замедление на жертву
                if (attacker != Entity.Null && SystemAPI.HasComponent<MelancholicTag>(attacker))
                {
                    if (SystemAPI.HasComponent<SkillConfigComponent>(attacker))
                    {
                        var config = SystemAPI.GetComponent<SkillConfigComponent>(attacker);
                        
                        ecb.AddComponent(victimEntity, new FrostSlowComponent
                        {
                            SpeedMultiplier = config.FrostSlowMultiplier,
                            TimeRemaining = 1.5f // Легкое замедление держится 1.5 секунды
                        });
                    }
                }

                // СИТУАЦИЯ Б: Меланхолик получает урон (Пассивка "Тяжесть бытия") -> Вешаем стак Апатии на обидчика
                if (SystemAPI.HasComponent<MelancholicTag>(victimEntity) && attacker != Entity.Null)
                {
                    if (SystemAPI.HasComponent<EnemyTagComponent>(attacker) && SystemAPI.HasComponent<SkillConfigComponent>(victimEntity))
                    {
                        var config = SystemAPI.GetComponent<SkillConfigComponent>(victimEntity);
                        
                        int currentStacks = 0;
                        float freezeTimer = 0f;

                        // Проверяем, есть ли уже дебафф на враге
                        if (SystemAPI.HasComponent<ApathyDebuffComponent>(attacker))
                        {
                            var existingApathy = SystemAPI.GetComponent<ApathyDebuffComponent>(attacker);
                            currentStacks = existingApathy.CurrentStacks;
                            freezeTimer = existingApathy.FreezeTimer;
                        }

                        // Накидываем стак только если враг еще не превращен в ледяную глыбу
                        if (freezeTimer <= 0f)
                        {
                            currentStacks++;
                            float lifeTimer = 5.0f; // Стаки спадают через 5 секунд, если моб перестанет бить

                            if (currentStacks >= config.ApathyMaxStacks)
                            {
                                freezeTimer = config.FreezeDuration; // ПОЛНАЯ ЗАМОРОЗКА!
                            }

                            ecb.AddComponent(attacker, new ApathyDebuffComponent
                            {
                                CurrentStacks = currentStacks,
                                FreezeTimer = freezeTimer,
                                DebuffLifeTimer = lifeTimer
                            });
                        }
                    }
                }
            }

            // =========================================================================
            // 2. ОБСЛУЖИВАНИЕ ТАЙМЕРОВ ДЕБАФФОВ
            // =========================================================================

            // А. Затухание ледяного замедления от выстрелов
            foreach (var (slow, entity) in SystemAPI.Query<RefRW<FrostSlowComponent>>().WithEntityAccess())
            {
                slow.ValueRW.TimeRemaining -= deltaTime;
                if (slow.ValueRO.TimeRemaining <= 0f)
                {
                    ecb.RemoveComponent<FrostSlowComponent>(entity);
                }
            }

            // Б. Контроль Апатии (Заморозка и спадение стаков)
            foreach (var (apathy, entity) in SystemAPI.Query<RefRW<ApathyDebuffComponent>>().WithEntityAccess())
            {
                if (apathy.ValueRO.FreezeTimer > 0f)
                {
                    apathy.ValueRW.FreezeTimer -= deltaTime;
                    if (apathy.ValueRO.FreezeTimer <= 0f)
                    {
                        // Заморозка истекла — враг оттаивает, стаки сбрасываются
                        ecb.RemoveComponent<ApathyDebuffComponent>(entity);
                    }
                }
                else if (apathy.ValueRO.DebuffLifeTimer > 0f)
                {
                    apathy.ValueRW.DebuffLifeTimer -= deltaTime;
                    if (apathy.ValueRO.DebuffLifeTimer <= 0f)
                    {
                        // Время жизни стаков вышло — они испаряются
                        ecb.RemoveComponent<ApathyDebuffComponent>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}