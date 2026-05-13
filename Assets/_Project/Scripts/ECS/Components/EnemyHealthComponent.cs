using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct EnemyHealthComponent : IComponentData
    {
        public float CurrentHealth;
    }
}