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

            // --- ЛОГИКА 1: Нанесение урона игрокам/сетевым объектам через HealthLink ---
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

            // --- ЛОГИКА 2: Нанесение урона врагам через ECS EnemyHealthComponent ---
            // ИСПРАВЛЕНО: Добавлено WithEntityAccess()
            foreach (var (takeDamage, enemyHealth, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>, RefRW<EnemyHealthComponent>>().WithEntityAccess())
            {
                enemyHealth.ValueRW.CurrentHealth -= takeDamage.ValueRO.Amount;
                
                // AAA-стандарт: Отложенная смерть. Если ХП <= 0, вешаем тег смерти.
                if (enemyHealth.ValueRO.CurrentHealth <= 0)
                {
                    ecb.AddComponent<DeathTagComponent>(entity);
                }
            }

            // --- ОЧИСТКА: Гарантированно удаляем компонент урона у ВСЕХ, чтобы он не залипал ---
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                ecb.RemoveComponent<TakeDamageComponent>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}