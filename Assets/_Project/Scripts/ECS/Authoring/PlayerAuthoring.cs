using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using UnityEngine.Serialization;

namespace _Project.Scripts.ECS.Authoring
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [FormerlySerializedAs("MoveSpeed")] public float moveSpeed = 5f; // Скорость по умолчанию

        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                // Добавляем пустой компонент ввода
                AddComponent<PlayerInputComponent>(entity);
                
                // Добавляем компонент скорости, забирая значение из инспектора
                AddComponent(entity, new PlayerMovementComponent 
                { 
                    MoveSpeed = authoring.moveSpeed 
                });
                
                AddComponent(entity, new SkillStateComponent 
                { 
                    MaxCooldown = 5f, 
                    CurrentCooldown = 0f, 
                    MaxCharges = 1, 
                    CurrentCharges = 1 // Со старта игры скилл готов к использованию
                });
            }
        }
    }
}