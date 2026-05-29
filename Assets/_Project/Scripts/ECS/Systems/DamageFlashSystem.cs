using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [BurstCompile]
    public partial struct DamageFlashSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Ищем всех, у кого есть таймер мигания и цвет
            foreach (var (flash, color) in SystemAPI.Query<RefRW<DamageFlashComponent>, RefRW<URPMaterialPropertyBaseColor>>())
            {
                if (flash.ValueRO.Timer > 0f)
                {
                    // Уменьшаем таймер
                    flash.ValueRW.Timer -= deltaTime;
                    // Делаем цвет чисто белым (R=1, G=1, B=1, Alpha=1)
                    color.ValueRW.Value = new float4(1f, 1f, 1f, 1f);
                }
                else
                {
                    // Возвращаем оригинальный красный цвет
                    color.ValueRW.Value = new float4(1f, 0f, 0f, 1f);
                }
            }
        }
    }
}