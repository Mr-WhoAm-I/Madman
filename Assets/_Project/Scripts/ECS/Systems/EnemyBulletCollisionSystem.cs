using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network; 

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct EnemyBulletCollisionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // ИСПРАВЛЕНО: Нам больше не нужно запрашивать Health и Flash. Мы только читаем координаты.
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                for (var i = BulletNetworkMovement.ActiveBullets.Count - 1; i >= 0; i--)
                {
                    var bullet = BulletNetworkMovement.ActiveBullets[i];
                    if (bullet == null || bullet.isHit) continue;

                    var distance = math.distance(transform.ValueRO.Position, bullet.transform.position);

                    if (!(distance < 0.5f)) continue;
                    
                    // AAA-СТАНДАРТ: Мы не трогаем здоровье напрямую! Мы просто вешаем "запрос на урон"
                    // Твоя система DamageSystem (которая обрабатывает взрывы и атаки мобов) сама снимет ХП и убьет врага.
                    ecb.AddComponent(entity, new TakeDamageComponent { 
                        Amount = bullet.damage,
                        SourceEntity = bullet.SourceEntity // Передаем автора пули!
                    });

                    bullet.isHit = true;
                        
                    if (bullet.HasStateAuthority) bullet.Runner.Despawn(bullet.Object);

                    break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}