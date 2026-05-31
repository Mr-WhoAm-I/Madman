using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Combat
{
    // Компонент-метка. Означает, что сущность должна умереть в этом кадре.
    public struct DeathTagComponent : IComponentData { }
}