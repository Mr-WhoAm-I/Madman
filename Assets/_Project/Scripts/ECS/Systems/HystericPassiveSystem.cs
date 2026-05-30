using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct HystericPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (healthLink, config, entity) in SystemAPI.Query<HealthLinkComponent, RefRO<SkillConfigComponent>>().WithAll<HystericTag>().WithEntityAccess())
            {
                var health = healthLink.Value;
                // Проверяем, жив ли объект и привязан ли он
                if (health == null || health.Object == null || !health.Object.IsValid) continue;

                // Высчитываем процент здоровья
                float hpPercent = health.CurrentHealth / health.MaxHealth;
                
                // Проверяем условие ярости (ХП ниже порога, но игрок еще жив)
                bool isFurious = hpPercent <= config.ValueRO.FuryHealthThreshold && health.CurrentHealth > 0;
                bool hasTag = SystemAPI.HasComponent<HystericFuryStateTag>(entity);

                // Если пора впасть в ярость, а тега еще нет
                if (isFurious && !hasTag)
                {
                    ecb.AddComponent<HystericFuryStateTag>(entity);
                    Debug.Log($"<color=#FF4500>[ИСТЕРИК]</color> Здоровье упало до {hpPercent:P0}. Активирована ДВОЙНАЯ ЯРОСТЬ!");
                }
                // Если отхилились выше порога, а тег висит
                else if (!isFurious && hasTag)
                {
                    ecb.RemoveComponent<HystericFuryStateTag>(entity);
                    Debug.Log($"<color=#32CD32>[ИСТЕРИК]</color> Здоровье восстановлено до {hpPercent:P0}. Ярость спала.");
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}