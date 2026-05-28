using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct SkillStateComponent : IComponentData
    {
        public float MaxCooldown;
        public float CurrentCooldown;
        public int MaxCharges;
        public int CurrentCharges;
        
        // Флаг, указывающий, можно ли сейчас применить навык
        public bool IsReady => CurrentCharges > 0 && CurrentCooldown <= 0f;
    }
}