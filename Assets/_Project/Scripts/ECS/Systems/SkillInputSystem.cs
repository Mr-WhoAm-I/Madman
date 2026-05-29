using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct SkillInputSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (input, skillState, bridgeRef, entity) in 
                     SystemAPI.Query<RefRO<PlayerInputComponent>, RefRO<SkillStateComponent>, PlayerBridgeReference>().WithEntityAccess())
            {
                // ИСПРАВЛЕНИЕ: Убрали IsForward предохранитель. 
                // Системы ввода должны работать на каждом тике, так как инпут Fusion идеально откатывается в прошлое!
                var currentInput = input.ValueRO;
                
                var isSkillButtonPressed = currentInput.Buttons.IsSet(PlayerInputButtons.Skill) && 
                                           !currentInput.PreviousButtons.IsSet(PlayerInputButtons.Skill);

                if (isSkillButtonPressed && skillState.ValueRO.IsReady)
                {
                    if (!SystemAPI.GetComponentLookup<ExecuteSkillRequest>().HasComponent(entity))
                    {
                        ecb.AddComponent(entity, new ExecuteSkillRequest
                        {
                            AimDirection = currentInput.AimDirection,
                            TargetPosition = float3.zero 
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose(); 
        }
    }
}