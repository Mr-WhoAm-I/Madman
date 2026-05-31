using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Enemies
{
    public struct EnemyLootDropComponent : IComponentData
    {
        public int Bounty;
    }
}