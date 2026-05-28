using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    // Вешаем этот компонент на игрока, клона и турель
    public struct TargetableComponent : IComponentData 
    {
        // Можно добавить вес цели, если хочешь, чтобы враги предпочитали кого-то больше
        public float Priority; 
    }
}