using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct DamageFlashComponent : IComponentData
    {
        public float Timer;
    }
}