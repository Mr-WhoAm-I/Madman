using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Skills;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems.Classes
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct HystericPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (healthLink, config, entity) in SystemAPI.Query<HealthLinkComponent, RefRO<SkillConfigComponent>>().WithAll<HystericTag>().WithEntityAccess())
            {
                var health = healthLink.Value;
                if (health == null || health.Object == null || !health.Object.IsValid) continue;

                float hpPercent = health.CurrentHealth / health.MaxHealth;
                bool isFurious = hpPercent <= config.ValueRO.FuryHealthThreshold && health.CurrentHealth > 0;
                
                // === ОБРАБОТКА ТАЙМЕРА ПЕРЕГРУЗКИ ===
                if (SystemAPI.HasComponent<OverloadTimerComponent>(entity))
                {
                    var overload = SystemAPI.GetComponent<OverloadTimerComponent>(entity);
                    overload.Value -= deltaTime;
                    
                    if (overload.Value > 0)
                    {
                        isFurious = true; // Насильно держим Ярость, пока есть время
                        SystemAPI.SetComponent(entity, overload); // Сохраняем новое время
                    }
                    else
                    {
                        ecb.RemoveComponent<OverloadTimerComponent>(entity); // Время вышло — удаляем таймер
                    }
                }

                bool hasTag = SystemAPI.HasComponent<HystericFuryStateTag>(entity);

                if (isFurious && !hasTag)
                {
                    ecb.AddComponent<HystericFuryStateTag>(entity);
                    Debug.Log($"<color=#FF4500>[ИСТЕРИК]</color> Активирована ДВОЙНАЯ ЯРОСТЬ!");
                }
                else if (!isFurious && hasTag)
                {
                    ecb.RemoveComponent<HystericFuryStateTag>(entity);
                    Debug.Log($"<color=#32CD32>[ИСТЕРИК]</color> Ярость спала.");
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}