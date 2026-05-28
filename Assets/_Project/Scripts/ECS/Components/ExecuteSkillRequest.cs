using Unity.Entities;
using Unity.Mathematics;

namespace _Project.Scripts.ECS.Components
{
    // Компонент-запрос. Добавляется на один кадр, когда игрок нажал кнопку скилла
    public struct ExecuteSkillRequest : IComponentData
    {
        // Нам нужно знать, куда смотрел/целился игрок в момент нажатия
        public float2 AimDirection;
        // Позиция курсора (полезно для спавна турели или телепорта)
        public float3 TargetPosition; 
    }
}