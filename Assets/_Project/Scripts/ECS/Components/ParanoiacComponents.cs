using Fusion;
using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    // Тег для всех, кого могут бить враги (Игроки и Турели)
    public struct TargetableTag : IComponentData { }

    // Компонент Агра (теперь с таймером)
    public struct TauntComponent : IComponentData
    {
        public float Radius;
        public float TimeRemaining; // Сколько секунд еще работает агро
    }

    public struct PlayerOwnerComponent : IComponentData
    {
        public PlayerRef Player;
    }

    // Компонент, чтобы ECS знал, какой у игрока класс
    public struct ArchetypeComponent : IComponentData
    {
        public int ArchetypeID;
    }
}