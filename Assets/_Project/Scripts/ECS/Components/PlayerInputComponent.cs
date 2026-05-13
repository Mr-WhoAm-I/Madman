using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components
{
    public struct PlayerInputComponent : IComponentData
    {
        public float2 MovementVector;
    }
}