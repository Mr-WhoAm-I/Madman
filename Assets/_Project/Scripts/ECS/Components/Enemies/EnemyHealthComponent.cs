using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Enemies
{
    public struct EnemyHealthComponent : IComponentData
    {
        public float CurrentHealth;
    }
}