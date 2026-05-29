using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(SkillInputSystem))]
    public partial struct HystericSkillSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // ИСПРАВЛЕНИЕ: Читаем монолитный сетевой дельта-тайм вместо кадрового рендеринга!
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            var deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (skillState, request, config, input, bridgeRef, entity) in 
                     SystemAPI.Query<RefRW<SkillStateComponent>, RefRO<ExecuteSkillRequest>, RefRO<SkillConfigComponent>, RefRO<PlayerInputComponent>, PlayerBridgeReference>()
                     .WithAll<HystericTag>()
                     .WithEntityAccess())
            {
                // ИСПРАВЛЕНИЕ: Убрали IsForward блокировку симуляции
                skillState.ValueRW.CurrentCharges--;
                
                if (skillState.ValueRW.CurrentCooldown <= 0f)
                {
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                }

                var dashDir = request.ValueRO.AimDirection;
                
                if (math.lengthsq(dashDir) < 0.01f) dashDir = input.ValueRO.MovementInput;
                if (math.lengthsq(dashDir) < 0.01f) dashDir = new float2(1, 0); 
                
                dashDir = math.normalize(dashDir);

                ecb.AddComponent(entity, new Trigger360ShootTag());
                
                ecb.AddComponent(entity, new DashComponent
                {
                    Direction = dashDir,
                    Speed = config.ValueRO.DashSpeed,
                    TimeLeft = config.ValueRO.DashDuration
                });

                ecb.RemoveComponent<ExecuteSkillRequest>(entity);
            }

            foreach (var (dash, bridgeRef, entity) in SystemAPI.Query<RefRW<DashComponent>, PlayerBridgeReference>().WithEntityAccess())
            {
                // ИСПРАВЛЕНИЕ: Таймер уменьшается на фиксированный сетевой deltaTime на каждом тике симуляции!
                dash.ValueRW.TimeLeft -= deltaTime;
                if (dash.ValueRO.TimeLeft <= 0f)
                {
                    ecb.RemoveComponent<DashComponent>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}