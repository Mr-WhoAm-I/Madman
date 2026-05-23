using Unity.Entities;
using Unity.Transforms;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(PlayerMovementSystem))] // Гарантируем, что выполняется после расчета движения
    public partial class PlayerTransformSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Работает без Burst, так как мы обращаемся к Unity (Transform)
            foreach (var (localTransform, sync) in SystemAPI.Query<RefRO<LocalTransform>, PlayerTransformSync>())
            {
                if (sync.Value != null)
                {
                    sync.Value.position = localTransform.ValueRO.Position;
                }
            }
        }
    }
}