using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct DamageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Ищем все сущности, на которых висит запрос на урон (TakeDamageComponent) 
            // И у которых есть ссылка на скрипт здоровья (HealthLinkComponent)
            foreach (var (takeDamage, healthLink, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>, HealthLinkComponent>().WithEntityAccess())
            {
                // Проверяем, что ссылка на скрипт не пустая и объект жив в сети
                if (healthLink.Value != null && healthLink.Value.Object != null && healthLink.Value.Object.IsValid)
                {
                    // Если мы на сервере, наносим реальный урон
                    if (healthLink.Value.HasStateAuthority)
                    {
                        healthLink.Value.TakeDamage(takeDamage.ValueRO.Amount);
                    }
                }

                // Удаляем компонент урона, чтобы не наносить его каждый кадр бесконечно
                ecb.RemoveComponent<TakeDamageComponent>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}