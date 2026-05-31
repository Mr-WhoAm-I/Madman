using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Core;
using Unity.Entities;

namespace _Project.Scripts.ECS.Systems.Classes
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct CryoDebuffSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Ищем всех, на ком висит дебафф заморозки
            foreach (var (cryo, entity) in SystemAPI.Query<RefRW<CryoDebuffComponent>>().WithEntityAccess())
            {
                // Уменьшаем таймер
                cryo.ValueRW.Timer -= deltaTime;

                // Если время вышло — снимаем компонент
                if (cryo.ValueRO.Timer <= 0f)
                {
                    ecb.RemoveComponent<CryoDebuffComponent>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}