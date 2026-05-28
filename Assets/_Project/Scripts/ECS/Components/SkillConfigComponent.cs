using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct SkillConfigComponent : IComponentData
    {
        // Общая база расстояний и радиусов
        public float CastDistance;
        public float EffectRadius;
        
        // Параметры Истерика
        public float DashSpeed;
        public float DashDuration;

        // Параметры Шизоида (Новые поля)
        public float InstabilityTimePerStack;
        public int InstabilityMaxStacks;
        public float InstabilityDamagePerStack;
        public float InvisibilityDuration;
        public float CloneExplosionDamage;
        public float CloneExplosionRadius;
    }
}