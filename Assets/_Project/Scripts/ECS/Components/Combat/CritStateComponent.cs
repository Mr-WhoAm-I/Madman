using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    // Хранит состояние рандома для конкретного игрока или моба
    public struct CritStateComponent : IComponentData
    {
        public int NonCritStreak; // Серия выстрелов без крита
    }
}