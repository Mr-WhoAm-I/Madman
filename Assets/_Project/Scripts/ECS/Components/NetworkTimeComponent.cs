using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct NetworkTimeComponent : IComponentData
    {
        public float DeltaTime;
    }
}