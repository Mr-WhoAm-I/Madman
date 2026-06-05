using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Gameplay;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Combat
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct EnemyBulletCollisionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                for (var i = BulletNetworkMovement.ActiveBullets.Count - 1; i >= 0; i--)
                {
                    var bullet = BulletNetworkMovement.ActiveBullets[i];
                    
                    // Пропускаем удаляющиеся пули
                    if (bullet == null || bullet.isDespawning) continue;
                    
                    // Предотвращаем двойной урон от пробивных пуль по одной цели
                    if (bullet.HitEntities.Contains(entity)) continue;

                    var distance = math.distance(transform.ValueRO.Position, bullet.transform.position);
                    if (distance >= 0.5f) continue;
                    
                    // Попадание! Запоминаем врага
                    bullet.HitEntities.Add(entity);

                    // Передаем автора пули и её стихию!
                    ecb.AddComponent(entity, new TakeDamageComponent { 
                        Amount = bullet.damage,
                        SourceEntity = bullet.SourceEntity,
                        Element = bullet.currentElement,
                        IsCritical = bullet.isCritical // <- ПЕРЕДАЕМ ФЛАГ!
                    });

                    // Если пуля не пробивная - уничтожаем
                    if (!bullet.pierceEnemies)
                    {
                        bullet.isDespawning = true;
                        if (bullet.HasStateAuthority) bullet.Runner.Despawn(bullet.Object);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}