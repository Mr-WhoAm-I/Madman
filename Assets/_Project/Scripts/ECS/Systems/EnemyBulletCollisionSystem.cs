using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network; 

namespace _Project.Scripts.ECS.Systems
{
    public partial struct EnemyBulletCollisionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Обрати внимание: мы добавили RefRW для Health и Flash
            foreach (var (transform, health, flash, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyHealthComponent>, RefRW<DamageFlashComponent>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                for (var i = BulletNetworkMovement.ActiveBullets.Count - 1; i >= 0; i--)
                {
                    var bullet = BulletNetworkMovement.ActiveBullets[i];
                    if (bullet == null || bullet.isHit) continue;

                    var distance = math.distance(transform.ValueRO.Position, bullet.transform.position);

                    if (!(distance < 0.5f)) continue;
                    // 1. Отнимаем здоровье
                    health.ValueRW.CurrentHealth -= bullet.damage;
                    bullet.isHit = true;
                        
                    if (bullet.HasStateAuthority) bullet.Runner.Despawn(bullet.Object);

                    // 2. Проверяем, жив ли враг
                    if (health.ValueRO.CurrentHealth <= 0)
                    {
                        ecb.DestroyEntity(entity); // Убиваем
                    }
                    else
                    {
                        flash.ValueRW.Timer = 0.1f; // Включаем таймер мигания на 0.1 сек
                    }

                    break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}