using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    // Определяем наши кнопки. Пока что только одна - Атака.
    public enum PlayerInputButtons
    {
        Skill = 0,
        Interact = 1
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 MovementInput;
        public Vector2 AimDirection;
        public NetworkButtons Buttons; // Специальная структура Fusion для кнопок
    }
}