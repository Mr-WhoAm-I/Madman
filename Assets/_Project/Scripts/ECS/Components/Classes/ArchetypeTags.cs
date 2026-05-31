using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Classes
{
    // Теги не содержат данных, они используются исключительно как фильтры для систем
    public struct HystericTag : IComponentData { }
    
    public struct ParanoiacTag : IComponentData { }
    
    public struct MelancholicTag : IComponentData { }
    
    public struct SchizoidTag : IComponentData { }
}