using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    // INetworkStruct говорит Фотону, что эту структуру можно передавать по сети
    public struct EnemyNetworkState : INetworkStruct
    {
        public NetworkBool IsActive; // Жив ли враг (нужно ли его рисовать)
        public Vector2 Position;     // Где он находится
        public float Health;           // Текущее здоровье (для мигания или полосок ХП)
    }
}