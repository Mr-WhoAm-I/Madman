using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    // Указываем, что система должна обновляться до основной логики движения
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        private PlayerControls _controls;

        protected override void OnCreate()
        {
            // Инициализируем наш сгенерированный класс управления
            _controls = new PlayerControls();
            _controls.Enable();
        }

        protected override void OnUpdate()
        {
            // Читаем значения из карты Gameplay (наши кнопки WASD)
            var moveInput = _controls.Gameplay.Move.ReadValue<Vector2>();

            // Пробегаемся по всем сущностям, у которых есть PlayerInputComponent
            // SystemAPI.Query - это современный способ фильтрации в DOTS
            foreach (var inputComp in SystemAPI.Query<RefRW<PlayerInputComponent>>())
            {
                // Записываем вектор движения из Input System в наш ECS компонент
                inputComp.ValueRW.MovementVector = new Unity.Mathematics.float2(moveInput.x, moveInput.y);
            }
        }

        protected override void OnDestroy()
        {
            _controls.Disable();
        }
    }
}