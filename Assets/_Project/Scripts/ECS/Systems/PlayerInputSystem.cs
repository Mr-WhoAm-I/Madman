using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.UI;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PlayerInputSystem : SystemBase
    {
        private PlayerControls _controls;

        protected override void OnCreate()
        {
            _controls = new PlayerControls();
            _controls.Enable();
        }

        protected override void OnUpdate()
        {
            var moveInput = Vector2.zero;

            if (HUDManager.Instance == null || !HUDManager.Instance.IsInteractionSuspended)
            {
                moveInput = _controls.Gameplay.Move.ReadValue<Vector2>();
            }

            foreach (var inputComp in SystemAPI.Query<RefRW<PlayerInputComponent>>())
            {
                inputComp.ValueRW.MovementVector = new Unity.Mathematics.float2(moveInput.x, moveInput.y);
            }
        }

        protected override void OnDestroy()
        {
            _controls.Disable();
        }
    }
}