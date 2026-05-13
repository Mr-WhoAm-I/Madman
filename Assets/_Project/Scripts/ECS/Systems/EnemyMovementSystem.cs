using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network; 

namespace _Project.Scripts.ECS.Systems
{
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            var targetPos = new float3(
                PlayerNetworkMovement.LocalPlayerPosition.x, 
                PlayerNetworkMovement.LocalPlayerPosition.y, 
                0f);

            // Создаем буфер команд (EntityCommandBuffer). 
            // Мы не можем удалять врагов прямо во время цикла foreach, движок выдаст ошибку.
            // Поэтому мы записываем "приказы об уничтожении" в этот буфер, чтобы выполнить их позже.
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Обрати внимание: мы добавили 'entity' в параметры, чтобы знать, кого удалять
            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EnemyMovementComponent>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                var direction = targetPos - transform.ValueRO.Position;
                var distance = math.length(direction); // Наша оптимизированная математика

                // 1. ПРОВЕРКА СТОЛКНОВЕНИЯ (Дистанция меньше 0.5 юнитов)
                if (distance < 0.5f)
                {
                    // Проверяем, жив ли еще игрок и имеем ли мы право менять его здоровье (только Сервер!)
                    if (PlayerNetworkMovement.LocalPlayerHealth != null && PlayerNetworkMovement.LocalPlayerHealth.HasStateAuthority)
                    {
                        PlayerNetworkMovement.LocalPlayerHealth.TakeDamage(10f); // Наносим 10 урона
                    }
                    
                    // Приказываем движку уничтожить этого врага (камикадзе)
                    ecb.DestroyEntity(entity);
                }
                // 2. ДВИЖЕНИЕ (Если еще не добежали)
                else
                {
                    direction = math.normalize(direction);
                    transform.ValueRW.Position += direction * movement.ValueRO.Speed * deltaTime;
                }
            }

            // Выполняем все записанные приказы об уничтожении
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}