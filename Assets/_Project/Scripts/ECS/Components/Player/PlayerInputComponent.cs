using Fusion;
using Unity.Entities;
using UnityEngine;

namespace _Project.Scripts.ECS.Components.Player
{
    public struct PlayerInputComponent : IComponentData
    {
        public Vector2 MovementInput;
        public Vector2 AimDirection;
        public NetworkButtons Buttons;
        public NetworkButtons PreviousButtons;
    }
}