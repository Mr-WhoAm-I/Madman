using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    // Хранит номинал осколка/монеты
    public struct LootComponent : IComponentData
    {
        public int Value;
    }

    // Состояние магнетизма
    public struct MagnetStateComponent : IComponentData
    {
        public bool IsPulled;
        public Entity TargetEntity; // К кому летит осколок
    }
}