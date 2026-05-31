using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    // Универсальный щит. Может висеть на ком угодно (Игрок, Моб, Турель).
    public struct EnergyShieldComponent : IComponentData
    {
        public float CurrentShield;
        public float MaxShield;
        
        // Таймер, который отсчитывает время с момента последнего получения урона
        public float OutOfCombatTimer; 
    }
}