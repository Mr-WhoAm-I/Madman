using Unity.Entities;
using UnityEngine; // Добавлено для дебаг логов
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct DamageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =======================================================================
            // 0. ПЕРЕХВАТ УРОНА ЩИТАМИ (Работает для ЛЮБОЙ сущности с щитом)
            // =======================================================================
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRW<TakeDamageComponent>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnergyShieldComponent>(entity))
                {
                    var shield = SystemAPI.GetComponent<EnergyShieldComponent>(entity);
                    
                    // Сбрасываем таймер "Вне боя" при любом попадании
                    shield.OutOfCombatTimer = 0f;

                    if (shield.CurrentShield > 0f)
                    {
                        float incomingDamage = takeDamage.ValueRO.Amount;

                        if (shield.CurrentShield >= incomingDamage)
                        {
                            // Щит выдержал весь урон
                            shield.CurrentShield -= incomingDamage;
                            takeDamage.ValueRW.Amount = 0f; // Обнуляем урон, чтобы ХП не пострадало
                            
                            Debug.Log($"<color=#00FFFF>[ЩИТ]</color> Урон {incomingDamage} полностью поглощен! Остаток щита: {shield.CurrentShield}");
                        }
                        else
                        {
                            // Щит пробит! Вычитаем остаток урона
                            takeDamage.ValueRW.Amount -= shield.CurrentShield;
                            Debug.Log($"<color=#00FFFF>[ЩИТ ПРОБИТ]</color> Поглощено {shield.CurrentShield} урона. В здоровье прошло: {takeDamage.ValueRO.Amount}");
                            
                            shield.CurrentShield = 0f;
                        }
                    }
                    
                    SystemAPI.SetComponent(entity, shield);
                }
            }

            // --- ЛОГИКА 1: Урон игрокам/сетевым объектам ---
            foreach (var (takeDamage, healthLink) in SystemAPI.Query<RefRO<TakeDamageComponent>, HealthLinkComponent>())
            {
                // ИГНОРИРУЕМ, ЕСЛИ УРОН БЫЛ ПОГЛОЩЕН ЩИТОМ
                if (takeDamage.ValueRO.Amount <= 0f) continue; 

                if (healthLink.Value != null && healthLink.Value.Object != null && healthLink.Value.Object.IsValid)
                {
                    if (healthLink.Value.HasStateAuthority)
                    {
                        healthLink.Value.TakeDamage(takeDamage.ValueRO.Amount);
                    }
                }
            }

            // --- ЛОГИКА 2: Урон врагам и ВАМПИРИЗМ ---
            foreach (var (takeDamage, enemyHealth, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>, RefRW<EnemyHealthComponent>>().WithEntityAccess())
            {
                float damageAmount = takeDamage.ValueRO.Amount;
                
                // ИГНОРИРУЕМ, ЕСЛИ УРОН БЫЛ ПОГЛОЩЕН ЩИТОМ (например, если мобам тоже дадим щиты)
                if (damageAmount > 0f)
                {
                    enemyHealth.ValueRW.CurrentHealth -= damageAmount;
                    
                    // === ААА-МЕХАНИКА: ВАМПИРИЗМ ===
                    Entity attacker = takeDamage.ValueRO.SourceEntity;
                    if (attacker != Entity.Null && SystemAPI.HasComponent<HystericFuryStateTag>(attacker))
                    {
                        var config = SystemAPI.GetComponent<SkillConfigComponent>(attacker);
                        
                        if (config.FuryLifesteal > 0f && state.EntityManager.HasComponent<HealthLinkComponent>(attacker))
                        {
                            var attackerHealthLink = state.EntityManager.GetComponentObject<HealthLinkComponent>(attacker);
                            if (attackerHealthLink.Value != null && attackerHealthLink.Value.HasStateAuthority)
                            {
                                float healAmount = damageAmount * config.FuryLifesteal;
                                attackerHealthLink.Value.Heal(healAmount);
                            }
                        }
                    }

                    if (enemyHealth.ValueRO.CurrentHealth <= 0)
                    {
                        ecb.AddComponent<DeathTagComponent>(entity);
                    }
                }
            }

            // --- ОЧИСТКА ---
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                ecb.RemoveComponent<TakeDamageComponent>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}