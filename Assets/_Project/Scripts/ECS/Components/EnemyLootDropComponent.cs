using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct EnemyLootDropComponent : IComponentData
    {
        public int Bounty;
    }
}