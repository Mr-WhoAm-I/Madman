using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct SkillConfigComponent : IComponentData
    {
        public float CastDistance;
        public float EffectRadius;
        
        // --- Новые параметры для рывка ---
        public float DashSpeed;
        public float DashDuration;
    }
}