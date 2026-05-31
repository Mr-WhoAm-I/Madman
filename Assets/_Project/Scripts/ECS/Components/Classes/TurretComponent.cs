using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Classes
{
    public struct TurretComponent : IComponentData
    {
        public Entity OwnerEntity; // Ссылка на Параноика
        public float CryoMultiplier; 
        public float CryoDuration;
    }
}