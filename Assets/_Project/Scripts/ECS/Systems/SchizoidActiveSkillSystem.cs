using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(SkillInputSystem))]
    public partial struct SchizoidActiveSkillSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (skillState, request, config, transform, bridgeRef, entity) in 
                     SystemAPI.Query<RefRW<SkillStateComponent>, RefRO<ExecuteSkillRequest>, RefRO<SkillConfigComponent>, RefRO<LocalTransform>, PlayerBridgeReference>()
                     .WithAll<SchizoidTag>()
                     .WithEntityAccess())
            {
                if (!bridgeRef.Bridge.Runner.IsForward) continue;

                skillState.ValueRW.CurrentCharges--;
                if (skillState.ValueRW.CurrentCooldown <= 0f)
                {
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                }

                ecb.AddComponent(entity, new InvisibilityStateComponent
                {
                    TimeRemaining = config.ValueRO.InvisibilityDuration,
                    SpeedMultiplier = 1.0f, 
                    IsFirstShotBonusActive = true 
                });

                // ИСПРАВЛЕНО: Рассчитываем вектор направления на основе инпута джойстика
                float2 aimDir = request.ValueRO.AimDirection;
                
                // Защита: если джойстик в нейтральном положении, клон побежит строго вверх
                if (math.lengthsq(aimDir) < 0.01f) aimDir = new float2(0f, 1f);
                else aimDir = math.normalize(aimDir);

                // Передаем позицию и вектор направления в команду мосту
                ecb.AddComponent(entity, new SpawnCloneCommand
                {
                    SpawnPosition = transform.ValueRO.Position,
                    RunDirection = aimDir 
                });

                ecb.RemoveComponent<ExecuteSkillRequest>(entity);
            }

            if (SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
            {
                float deltaTime = timeComponent.DeltaTime;

                foreach (var (invis, bridgeRef, entity) in SystemAPI.Query<RefRW<InvisibilityStateComponent>, PlayerBridgeReference>().WithEntityAccess())
                {
                    if (!bridgeRef.Bridge.Runner.IsForward) continue;

                    invis.ValueRW.TimeRemaining -= deltaTime;
                    if (invis.ValueRO.TimeRemaining <= 0f)
                    {
                        ecb.RemoveComponent<InvisibilityStateComponent>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}