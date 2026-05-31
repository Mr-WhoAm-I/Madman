using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components
{
    // Буферный компонент, так как при смерти может взорваться сразу несколько мобов
    [InternalBufferCapacity(8)]
    public struct SpawnShrapnelCommand : IBufferElementData
    {
        public float3 Position;
        public Entity TargetEnemy; // В кого летит осколок (может быть Entity.Null)
    }
}