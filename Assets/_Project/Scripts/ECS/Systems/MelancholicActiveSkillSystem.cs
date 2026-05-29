using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(SkillInputSystem))]
    public partial struct MelancholicActiveSkillSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (skillState, request, transform, bridgeRef, entity) in 
                     SystemAPI.Query<RefRW<SkillStateComponent>, RefRO<ExecuteSkillRequest>, RefRO<LocalTransform>, PlayerBridgeReference>()
                         .WithAll<MelancholicTag>()
                         .WithEntityAccess())
            {
                // Защита от сетевых откатов
                if (!bridgeRef.Bridge.Runner.IsForward) continue;

                skillState.ValueRW.CurrentCharges--;
                if (skillState.ValueRW.CurrentCooldown <= 0f)
                {
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                }

                // Считываем вектор прицеливания
                float2 aimDir = request.ValueRO.AimDirection;
                if (math.lengthsq(aimDir) < 0.01f) aimDir = new float2(0f, 1f); // Дефолт вверх, если брошен тапом
                else aimDir = math.normalize(aimDir);

                // Отдаем мосту команду на спавн ульты
                ecb.AddComponent(entity, new SpawnIceProjectileCommand
                {
                    CastDirection = aimDir
                });

                ecb.RemoveComponent<ExecuteSkillRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}