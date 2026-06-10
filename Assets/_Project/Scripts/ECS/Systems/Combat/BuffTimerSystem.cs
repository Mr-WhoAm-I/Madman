using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using Unity.Burst;
using Unity.Entities;

namespace _Project.Scripts.ECS.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct BuffTimerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent)) return;
            float deltaTime = timeComponent.DeltaTime;
            
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (buff, entity) in SystemAPI.Query<RefRW<ActiveBuffComponent>>().WithEntityAccess())
            {
                buff.ValueRW.Timer -= deltaTime;
                if (buff.ValueRO.Timer <= 0f)
                {
                    ecb.RemoveComponent<ActiveBuffComponent>(entity);
                    // Здесь можно вызвать экшен для UI: "Бафф спал"
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}