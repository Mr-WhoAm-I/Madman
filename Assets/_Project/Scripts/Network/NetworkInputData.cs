using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    // Определяем наши кнопки. Пока что только одна - Атака.
    public enum PlayerInputButtons
    {
        Attack = 0
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 MovementInput;
        public NetworkButtons Buttons; // Специальная структура Fusion для кнопок
    }
}