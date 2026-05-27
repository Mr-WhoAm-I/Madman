using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using _Project.Scripts.Network;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [BurstCompile]
    public partial struct ParanoiacSkillSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Используем ECB для создания сущностей-запросов внутри Job-ов или Burst-систем
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // ФИЛЬТР: Только сущности с ParanoiacTag
            foreach (var (input, skillState, transform, archetype) in SystemAPI.Query<RefRO<PlayerInputComponent>, RefRW<SkillStateComponent>, RefRO<LocalTransform>, RefRO<ArchetypeComponent>>().WithAll<ParanoiacTag>())
            {
                // Читаем биты кнопки (с учетом Server-Authoritative Input)
                bool isSkillPressed = (input.ValueRO.Buttons.Bits & (1 << (int)PlayerInputButtons.Skill)) != 0;
                bool wasPrevPressed = (input.ValueRO.PreviousButtons.Bits & (1 << (int)PlayerInputButtons.Skill)) != 0;
                bool justPressed = isSkillPressed && !wasPrevPressed;

                if (justPressed && skillState.ValueRO.CurrentCharges > 0 && skillState.ValueRO.CurrentCooldown <= 0f)
                {
                    // 1. Запускаем кулдаун
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                    skillState.ValueRW.CurrentCharges--;

                    // 2. Вычисляем точку установки турели (бросаем на 4 метра по направлению прицела)
                    float3 throwDir = new float3(input.ValueRO.AimDirection.x, input.ValueRO.AimDirection.y, 0);
                    
                    // Защита от броска без направления (например, если вектор равен нулю)
                    if (math.lengthsq(throwDir) < 0.1f) throwDir = new float3(0, 1, 0);
                    
                    float3 targetPosition = transform.ValueRO.Position + (math.normalize(throwDir) * 4f);

                    // 3. Создаем ECS-запрос на спавн
                    var requestEntity = ecb.CreateEntity();
                    ecb.AddComponent(requestEntity, new SpawnTurretRequest
                    {
                        Position = targetPosition,
                        ArchetypeID = archetype.ValueRO.ArchetypeID // Передаем ID параноика
                    });
                }
            }

            // Применяем изменения
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}