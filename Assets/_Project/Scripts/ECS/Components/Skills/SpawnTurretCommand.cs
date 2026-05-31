using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components.Skills
{
    // Компонент-команда. Добавляется на сущность игрока на один сетевой тик,
    // когда система Параноика приняла решение установить турель.
    public struct SpawnTurretCommand : IComponentData
    {
        public float3 Position; // Точка физического спавна турели на карте
    }
}