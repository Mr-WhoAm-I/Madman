using Unity.Entities;

namespace _Project.Scripts.ECS.Components.BuffsAndDebuffs
{
    public struct ToxicCloudComponent : IComponentData
    {
        public float DPS;
        public float Radius;
        public float LifeTime;
        public Entity OwnerEntity; // Владелец (для Кровавой Жатвы)
    }
}