// Путь: Assets/_Project/Scripts/ECS/Systems/Player/SkillInputSystem.cs
using _Project.Scripts.ECS.Components.Combat; // <-- НОВОЕ
using _Project.Scripts.ECS.Components.Player;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Systems.Player
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct SkillInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (input, skillState, mana, config, entity) in 
                     SystemAPI.Query<RefRO<PlayerInputComponent>, RefRO<SkillStateComponent>, RefRW<ManaComponent>, RefRO<SkillConfigComponent>>().WithEntityAccess())
            {
                var currentInput = input.ValueRO;
                
                var isSkillButtonPressed = currentInput.Buttons.IsSet(PlayerInputButtons.Skill) && 
                                           !currentInput.PreviousButtons.IsSet(PlayerInputButtons.Skill);

                // ПРОВЕРКА МАНЫ: Добавили проверку mana.ValueRO.CurrentMana >= config.ValueRO.ManaCost
                if (!isSkillButtonPressed || !skillState.ValueRO.IsReady ||
                    !(mana.ValueRO.CurrentMana >= config.ValueRO.ManaCost)) continue;
                if (SystemAPI.GetComponentLookup<ExecuteSkillRequest>().HasComponent(entity)) continue;
                // ТРАТА МАНЫ: Списываем ману и обновляем таймер задержки регена прямо в ECS
                mana.ValueRW.CurrentMana -= config.ValueRO.ManaCost;
                mana.ValueRW.RegenCooldownTimer = config.ValueRO.ManaRegenCooldown;

                ecb.AddComponent(entity, new ExecuteSkillRequest
                {
                    AimDirection = currentInput.AimDirection,
                    TargetPosition = float3.zero
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}