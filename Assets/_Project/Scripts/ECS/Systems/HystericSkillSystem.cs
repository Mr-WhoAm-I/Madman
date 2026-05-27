using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network; 

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))] 
    [BurstCompile]
    public partial struct HystericSkillSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Создаем буфер команд для структурных изменений (добавление компонентов)
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (input, skillState, entity) in SystemAPI.Query<RefRO<PlayerInputComponent>, RefRW<SkillStateComponent>>().WithEntityAccess())
            {
                var buttons = input.ValueRO.Buttons; // Копируем состояние кнопок в локальную переменную
                var prevButtons = input.ValueRO.PreviousButtons;
                
                const int skillIndex = (int)PlayerInputButtons.Skill;

                var wasPressed = buttons.IsSet(skillIndex) && !prevButtons.IsSet(skillIndex);

                if (!wasPressed || skillState.ValueRO.CurrentCharges <= 0) continue;
                skillState.ValueRW.CurrentCharges--;
                if (skillState.ValueRO.CurrentCooldown <= 0f)
                    skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;

                var dashDir = input.ValueRO.AimDirection == Vector2.zero ? new Vector2(1, 0) : input.ValueRO.AimDirection.normalized;

                // Добавляем компоненты через буфер
                ecb.AddComponent(entity, new DashComponent 
                { 
                    Timer = 0.25f, 
                    Speed = 25f,   
                    Direction = dashDir
                });

                ecb.AddComponent<Trigger360ShootTag>(entity);
            }
            
            // Выполняем все накопленные команды разом
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}