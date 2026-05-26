using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network; // Для PlayerInputButtons

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))] // Должно работать ДО движения
    [BurstCompile]
    public partial struct HystericSkillSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (input, skillState, entity) in SystemAPI.Query<RefRO<PlayerInputComponent>, RefRW<SkillStateComponent>>().WithEntityAccess())
            {
                // Проверяем: нажата сейчас И не была нажата в прошлом кадре
                bool wasPressed = input.ValueRO.Buttons.IsSet(PlayerInputButtons.Skill) && 
                                 !input.ValueRO.PreviousButtons.IsSet(PlayerInputButtons.Skill);

                // Если нажали и есть заряды
                if (wasPressed && skillState.ValueRO.CurrentCharges > 0)
                {
                    // 1. Тратим заряд ульты
                    skillState.ValueRW.CurrentCharges--;
                    if (skillState.ValueRO.CurrentCooldown <= 0f)
                        skillState.ValueRW.CurrentCooldown = skillState.ValueRO.MaxCooldown;

                    // 2. Вектор рывка (если никуда не целимся, рвем вправо)
                    Vector2 dashDir = input.ValueRO.AimDirection == Vector2.zero ? new Vector2(1, 0) : input.ValueRO.AimDirection.normalized;

                    // 3. Вешаем рывок на игрока
                    SystemAPI.AddComponent(entity, new DashComponent 
                    { 
                        Timer = 0.25f, // Четверть секунды летим
                        Speed = 25f,   // Очень быстро
                        Direction = dashDir
                    });

                    // 4. Вешаем флажок для спавна пуль
                    SystemAPI.AddComponent<Trigger360ShootTag>(entity);
                }
            }
        }
    }
}