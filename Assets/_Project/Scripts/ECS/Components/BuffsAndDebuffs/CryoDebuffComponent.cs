using Unity.Entities;

namespace _Project.Scripts.ECS.Components.BuffsAndDebuffs
{
    public struct CryoDebuffComponent : IComponentData
    {
        public float SpeedMultiplier;
        public float Timer;
    }
}