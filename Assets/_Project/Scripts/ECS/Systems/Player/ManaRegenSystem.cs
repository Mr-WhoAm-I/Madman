// Путь: Assets/_Project/Scripts/ECS/Systems/Player/ManaRegenSystem.cs
using Unity.Burst;
using Unity.Entities;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core; // Для NetworkAuthorityComponent
using _Project.Scripts.ECS.Components.Skills;

namespace _Project.Scripts.ECS.Systems.Player
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ManaRegenSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // ВМЕСТО PlayerBridgeReference запрашиваем чистый NetworkAuthorityComponent
            foreach (var (mana, config, auth) in SystemAPI.Query<RefRW<ManaComponent>, RefRO<SkillConfigComponent>, RefRO<NetworkAuthorityComponent>>())
            {
                // Burst счастлив: мы проверяем чистый bool флаг, а не обращаемся к классу Photon
                if (!auth.ValueRO.HasStateAuthority) continue;

                if (mana.ValueRO.RegenCooldownTimer > 0f)
                {
                    mana.ValueRW.RegenCooldownTimer -= deltaTime;
                    continue;
                }

                if (mana.ValueRO.CurrentMana < config.ValueRO.BaseMaxMana)
                {
                    mana.ValueRW.CurrentMana += config.ValueRO.ManaRegenRate * deltaTime;
                    
                    if (mana.ValueRO.CurrentMana > config.ValueRO.BaseMaxMana)
                    {
                        mana.ValueRW.CurrentMana = config.ValueRO.BaseMaxMana;
                    }
                    
                    // УБРАНО: Мы больше не пушим данные в мост отсюда.
                }
            }
        }
    }
}