using Unity.Entities;

namespace _Project.Scripts.ECS.Components.BuffsAndDebuffs
{
    // Компонент поджога (Damage over Time)
    public struct BurningDebuffComponent : IComponentData
    {
        public float Timer;           // Таймер до следующего тика урона
        public float TickRate;        // Задержка между тиками (например, 0.5 сек)
        public float DamagePerTick;   // Урон за тик
        public int TicksRemaining;    // Сколько тиков осталось
        public Entity SourceEntity;   // Кто поджег (чтобы опыт/фраг пошел игроку)
    }
    
    // Компонент заморозки (Контроль толпы)
    public struct CryoDebuffComponent : IComponentData
    {
        public float Timer;           // Таймер до разморозки
        public float OriginalSpeed;   // Базовая скорость врага для восстановления
    }
}