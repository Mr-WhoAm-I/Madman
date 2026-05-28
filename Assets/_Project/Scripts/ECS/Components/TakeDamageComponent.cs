using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct TakeDamageComponent : IComponentData
    {
        public float Amount; // Сколько урона нанести
    }
}