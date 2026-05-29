using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct SchizoidInstabilitySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            var deltaTime = timeComponent.DeltaTime;

            foreach (var (instability, config, bridgeRef, entity) in 
                     SystemAPI.Query<RefRW<QuantumInstabilityComponent>, RefRO<SkillConfigComponent>, PlayerBridgeReference>().WithEntityAccess())
            {
                if (!bridgeRef.Bridge.Runner.IsForward) continue;

                // 1. Увеличиваем таймер безопасности с момента получения последнего урона.
                // (При получении урона твоя боевая система здоровья должна сбрасывать TimeSinceLastDamage в 0f)
                instability.ValueRW.TimeSinceLastDamage += deltaTime;

                // Проверяем условия для накопления стаков: игрок либо невидим, либо не получал урон более 2 секунд
                var isEntityInvisible = SystemAPI.HasComponent<InvisibilityStateComponent>(entity);
                var isSafeFromDamage = instability.ValueRO.TimeSinceLastDamage >= 2.0f;

                if (isEntityInvisible || isSafeFromDamage)
                {
                    // Если мы еще не достигли лимита (например, макс. 4 стака)
                    if (instability.ValueRO.CurrentStacks < config.ValueRO.InstabilityMaxStacks)
                    {
                        instability.ValueRW.Timer += deltaTime;

                        // Если прошел цикл времени за один стак (например, 1 секунда)
                        if (instability.ValueRO.Timer >= config.ValueRO.InstabilityTimePerStack)
                        {
                            instability.ValueRW.Timer = 0f;
                            instability.ValueRW.CurrentStacks++;
                        }
                    }
                    else
                    {
                        instability.ValueRW.Timer = 0f; // Стаки на максимуме, сбрасываем таймер
                    }
                }
                else
                {
                    // Если игрок в бою и получает урон — прогресс накопления текущего стака замораживается
                    instability.ValueRW.Timer = 0f;
                }
            }
        }
    }
}