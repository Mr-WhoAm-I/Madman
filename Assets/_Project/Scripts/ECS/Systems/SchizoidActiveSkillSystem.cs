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

                // --- ЧТЕНИЕ ПЕРКОВ: ДЛИТЕЛЬНОСТЬ И ПАРКУР ---
                float totalInvisDuration = config.ValueRO.InvisibilityDuration + config.ValueRO.InvisDuration;
                float speedMult = config.ValueRO.InvisSpeedMult > 0f ? config.ValueRO.InvisSpeedMult : 1.0f;

                ecb.AddComponent(entity, new InvisibilityStateComponent
                {
                    TimeRemaining = totalInvisDuration,
                    SpeedMultiplier = speedMult, 
                    IsFirstShotBonusActive = true 
                });
                
                if (config.ValueRO.InvisDuration > 0f)
                {
                    UnityEngine.Debug.Log($"<color=#9400D3>[ИНВИЗ]</color> Длительность увеличена до {totalInvisDuration} сек!");
                }
                
                if (config.ValueRO.InvisSpeedMult > 0f)
                {
                    UnityEngine.Debug.Log($"<color=#9400D3>[ПАРКУР]</color> Скорость в инвизе увеличена в {speedMult} раз!");
                }

                // ИСПРАВЛЕНО: Рассчитываем вектор направления на основе инпута джойстика
                var aimDir = request.ValueRO.AimDirection;
                
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
                var deltaTime = timeComponent.DeltaTime;

                foreach (var (invis, bridgeRef, entity) in SystemAPI.Query<RefRW<InvisibilityStateComponent>, PlayerBridgeReference>().WithEntityAccess())
                {
                    if (!bridgeRef.Bridge.Runner.IsForward) continue;

                    invis.ValueRW.TimeRemaining -= deltaTime;
                    if (invis.ValueRO.TimeRemaining <= 0f)
                    {
                        // Если таймер вышел естественно — передаем бафф на следующий выстрел!
                        if (invis.ValueRO.IsFirstShotBonusActive)
                        {
                            ecb.AddComponent<ShadowStrikeBuffComponent>(entity);
                            UnityEngine.Debug.Log("<color=#9400D3>[ИНВИЗ]</color> Инвиз спал. Готов Удар из тени!");
                        }
                        
                        ecb.RemoveComponent<InvisibilityStateComponent>(entity);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}