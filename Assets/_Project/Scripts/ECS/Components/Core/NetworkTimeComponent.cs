using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Core
{
    public struct NetworkTimeComponent : IComponentData
    {
        public float DeltaTime;
    }
}