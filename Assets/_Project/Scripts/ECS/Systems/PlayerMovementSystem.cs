using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, input, movement) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerInputComponent>, RefRO<PlayerMovementComponent>>())
            {
                float3 moveDirection = new float3(input.ValueRO.MovementVector.x, input.ValueRO.MovementVector.y, 0f);
                
                // Исправленная строка: обращаемся напрямую к Position через ValueRW (Read-Write)
                transform.ValueRW.Position += moveDirection * movement.ValueRO.MoveSpeed * deltaTime;
            }
        }
    }
}