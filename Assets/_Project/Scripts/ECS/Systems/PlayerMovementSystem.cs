using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [BurstCompile]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Пытаемся получить время сети. Если его еще нет — выходим (чтобы не было крашей).
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            var deltaTime = timeComponent.DeltaTime;

            foreach (var (transform, input, movement) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerInputComponent>, RefRO<PlayerMovementComponent>>())
            {
                var moveDirection = new float3(input.ValueRO.MovementInput.x, input.ValueRO.MovementInput.y, 0f);
                transform.ValueRW.Position += moveDirection * movement.ValueRO.MoveSpeed * deltaTime;
            }
        }
    }
}