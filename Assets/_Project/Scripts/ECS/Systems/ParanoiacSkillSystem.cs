using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(SkillInputSystem))] // Обрабатываем строго после сбора инпута
    public partial struct ParanoiacSkillSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Запрос включает в себя все необходимые компоненты для безопасного расчета в сети
            foreach (var (skillState, request, config, transform, bridgeRef, entity) in 
                     SystemAPI.Query<RefRW<SkillStateComponent>, RefRO<ExecuteSkillRequest>, RefRO<SkillConfigComponent>, RefRO<LocalTransform>, PlayerBridgeReference>()
                     .WithAll<ParanoiacTag>()
                     .WithEntityAccess())
            {
                // КРИТИЧЕСКИЙ ПРЕДОХРАНИТЕЛЬ: Изменяем заряды и создаем команды спавна
                // только при движении в реальное будущее, защищая клиента от лавины ресимуляций
                if (!bridgeRef.Bridge.Runner.IsForward) continue;

                // 1. Потребляем заряд навыка и активируем перезарядку
                skillState.ValueRW.CurrentCharges--;
                if (skillState.ValueRW.CurrentCooldown <= 0f)
                {
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                }

                // 2. Вычисляем направление броска на основе вектора прицеливания джойстика
                var throwDir = new float3(request.ValueRO.AimDirection.x, request.ValueRO.AimDirection.y, 0);
                
                // Защита от броска "под себя", если джойстик вернул нулевой вектор
                if (math.lengthsq(throwDir) < 0.01f) throwDir = new float3(0, 1, 0);
                
                // УБРАЛИ ХАРДКОД: Дистанция броска теперь динамически берется из ScriptableObject (через конфиг)
                var targetPosition = transform.ValueRO.Position + (math.normalize(throwDir) * config.ValueRO.CastDistance);

                // 3. Добавляем команду спавна турели прямо на сущность этого игрока
                ecb.AddComponent(entity, new SpawnTurretCommand
                {
                    Position = targetPosition
                });

                // 4. Удаляем запрос инпута, чтобы симуляция не повторилась на следующем тике
                ecb.RemoveComponent<ExecuteSkillRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}