using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network.Core
{
    public enum PlayerInputButtons
    {
        Skill = 0,
        UseConsumable1 = 1,
        UseConsumable2 = 2
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 MovementInput;
        public Vector2 AimDirection;
        public NetworkButtons Buttons; // Специальная структура Fusion для кнопок
        public byte SelectedAmmoType; // 0 = Physical, 1 = Fire, 2 = Cryo, 3 = Toxic
    }
}