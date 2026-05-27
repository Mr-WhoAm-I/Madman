using Unity.Burst;
using Unity.Collections;
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
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            var deltaTime = timeComponent.DeltaTime;
            
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, input, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerInputComponent>, RefRO<PlayerMovementComponent>>().WithEntityAccess())
            {
                // Если на игроке висит рывок — двигаем его по вектору рывка
                if (SystemAPI.HasComponent<DashComponent>(entity))
                {
                    var dash = SystemAPI.GetComponent<DashComponent>(entity);
                    
                    var dashMove = new float3(dash.Direction.x, dash.Direction.y, 0f);
                    transform.ValueRW.Position += dashMove * dash.Speed * deltaTime;

                    dash.Timer -= deltaTime;
                    
                    // Если время рывка вышло - удаляем его
                    if (dash.Timer <= 0)
                    {
                        ecb.RemoveComponent<DashComponent>(entity);
                    }
                    else
                    {
                        SystemAPI.SetComponent(entity, dash);
                    }
                }
                else 
                {
                    // Твоя классическая логика движения
                    var moveDirection = new float3(input.ValueRO.MovementInput.x, input.ValueRO.MovementInput.y, 0f);
                    transform.ValueRW.Position += moveDirection * movement.ValueRO.MoveSpeed * deltaTime;
                }
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}