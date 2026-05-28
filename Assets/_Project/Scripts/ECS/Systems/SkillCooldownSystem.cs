using Unity.Entities;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct SkillCooldownSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // ИСПРАВЛЕНИЕ: Читаем сетевой DeltaTime синглтона
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;

            foreach (var (skillState, bridgeRef) in SystemAPI.Query<RefRW<SkillStateComponent>, PlayerBridgeReference>())
            {
                // ИСПРАВЛЕНИЕ: Убрали IsForward блокировку. Кулдаун симулируется детерминированно
                if (skillState.ValueRO.CurrentCooldown > 0f)
                {
                    skillState.ValueRW.CurrentCooldown -= deltaTime;
                    
                    if (skillState.ValueRW.CurrentCooldown <= 0f)
                    {
                        skillState.ValueRW.CurrentCooldown = 0f;
                        
                        if (skillState.ValueRO.CurrentCharges < skillState.ValueRO.MaxCharges)
                        {
                            skillState.ValueRW.CurrentCharges++; 
                            
                            if (skillState.ValueRO.CurrentCharges < skillState.ValueRO.MaxCharges)
                            {
                                skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;
                            }
                        }
                    }
                }
            }
        }
    }
}