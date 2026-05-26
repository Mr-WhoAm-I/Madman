using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct SkillStateComponent : IComponentData
    {
        public float MaxCooldown;    // Базовое время отката (например, 5 секунд)
        public float CurrentCooldown;// Текущий таймер отката
        
        public int MaxCharges;       // Максимальное количество зарядов (у Параноика может быть 2)
        public int CurrentCharges;   // Текущие готовые заряды
    }
}