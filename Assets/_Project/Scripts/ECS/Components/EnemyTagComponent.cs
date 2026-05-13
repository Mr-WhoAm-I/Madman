using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    // Пустая структура-метка (Tag). Помогает движку быстро находить всех врагов.
    public struct EnemyTagComponent : IComponentData { }
}