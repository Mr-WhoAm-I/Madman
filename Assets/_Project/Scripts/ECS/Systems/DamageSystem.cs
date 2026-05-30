using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct DamageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // --- ЛОГИКА 1: Урон игрокам/сетевым объектам ---
            foreach (var (takeDamage, healthLink) in SystemAPI.Query<RefRO<TakeDamageComponent>, HealthLinkComponent>())
            {
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
                enemyHealth.ValueRW.CurrentHealth -= damageAmount;
                
                // === ААА-МЕХАНИКА: ВАМПИРИЗМ ===
                Entity attacker = takeDamage.ValueRO.SourceEntity;
                if (attacker != Entity.Null && SystemAPI.HasComponent<HystericFuryStateTag>(attacker))
                {
                    var config = SystemAPI.GetComponent<SkillConfigComponent>(attacker);
                    
                    // ИСПРАВЛЕНО: Используем state.EntityManager для managed-компонента
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

                // Отложенная смерть
                if (enemyHealth.ValueRO.CurrentHealth <= 0)
                {
                    ecb.AddComponent<DeathTagComponent>(entity);
                }
            }

            // --- ОЧИСТКА: Гарантированно удаляем компонент урона ---
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                ecb.RemoveComponent<TakeDamageComponent>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}