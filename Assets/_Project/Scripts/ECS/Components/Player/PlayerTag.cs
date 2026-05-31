using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Player
{
    // Пустой компонент-тег (Tag Component). 
    // Занимает 0 байт в памяти, используется только для фильтрации в запросах (Query).
    public struct PlayerTag : IComponentData
    {
    }
}