using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using _Project.Scripts.ECS.Components.Core;

namespace _Project.Scripts.ECS.Systems.Player
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct LootAnimationSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // Итерируемся по всем монеткам (у которых есть анимация и магнит)
            foreach (var (transform, anim, magnet) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<LootAnimationComponent>, RefRO<MagnetStateComponent>>())
            {
                anim.ValueRW.Timer += deltaTime;
                
                // 2. Покачивание (Синусоида) - работает ТОЛЬКО пока монетка лежит на земле
                if (!magnet.ValueRO.IsPulled)
                {
                    float newY = math.sin(anim.ValueRO.Timer * anim.ValueRO.BobbingSpeed) * anim.ValueRO.BobbingAmount;
                    
                    // Сохраняем X и Z из базовой позиции, а к Y прибавляем покачивание
                    transform.ValueRW.Position = anim.ValueRO.BasePosition + new float3(0f, newY, 0f);
                }
            }
        }
    }
}