using Unity.Burst;
using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    // Обязательно помещаем в нашу группу Fusion, чтобы таймеры не рассинхронизировались с сетью
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [BurstCompile]
    public partial struct SkillCooldownSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Берем время, которое мы прокинули из Fusion в прошлом баг-фиксе
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            var deltaTime = timeComponent.DeltaTime;

            // Проходимся по всем сущностям, у которых есть состояние скилла
            foreach (var skillState in SystemAPI.Query<RefRW<SkillStateComponent>>())
            {
                // Если зарядов меньше, чем должно быть — крутим таймер отката
                if (skillState.ValueRO.CurrentCharges >= skillState.ValueRO.MaxCharges) continue;
                skillState.ValueRW.CurrentCooldown -= deltaTime;

                // Таймер дошел до нуля — скилл перезарядился
                if (!(skillState.ValueRO.CurrentCooldown <= 0f)) continue;
                skillState.ValueRW.CurrentCharges++;
                        
                // Если есть еще недостающие заряды (например, восстановили 1 из 2),
                // запускаем таймер заново. Иначе просто обнуляем.
                skillState.ValueRW.CurrentCooldown = skillState.ValueRO.CurrentCharges < skillState.ValueRO.MaxCharges ? skillState.ValueRO.MaxCooldown : 0f;
            }
        }
    }
}