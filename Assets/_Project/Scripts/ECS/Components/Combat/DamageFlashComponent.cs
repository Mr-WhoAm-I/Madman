using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    public struct DamageFlashComponent : IComponentData
    {
        public float Timer;
    }
}