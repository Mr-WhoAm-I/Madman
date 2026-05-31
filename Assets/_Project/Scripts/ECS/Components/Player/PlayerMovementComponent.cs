using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Player
{
    public struct PlayerMovementComponent : IComponentData
    {
        public float MoveSpeed;
    }
}