using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Player;
using _Project.Scripts.ECS.Components.Skills;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Systems.Classes
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(Player.SkillInputSystem))]
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

                // === АКТИВАЦИЯ ПЕРЕГРУЗКИ ===
                if (config.ValueRO.ForceFuryOnUltimate)
                {
                    ecb.AddComponent(entity, new OverloadTimerComponent 
                    { 
                        Value = config.ValueRO.OverloadDuration 
                    });
                }

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