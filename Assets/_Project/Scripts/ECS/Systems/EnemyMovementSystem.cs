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
            // Если Менеджера еще нет, или мы КЛИЕНТ — отключаем самостоятельное движение!
            if (EnemySwarmManager.Instance == null || !EnemySwarmManager.Instance.HasStateAuthority)
            {
                return; // Лоботомия: Клиент больше не двигает врагов сам
            }

            // Если список игроков пуст (никто еще не заспавнился), врагам некуда идти
            if (PlayerManager.AllActivePlayers.Count == 0) return;

            var deltaTime = SystemAPI.Time.DeltaTime;
            
            // Буфер для удаления врагов (камикадзе)
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EnemyMovementComponent>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                var enemyPos = transform.ValueRO.Position;
                var closestPlayerPos = float3.zero;
                var minDistance = float.MaxValue;
                PlayerManager closestPlayer = null;

                // 1. ИЩЕМ БЛИЖАЙШЕГО ИГРОКА ИЗ СПИСКА
                foreach (var player in PlayerManager.AllActivePlayers)
                {
                    if (player == null) continue; // Защита от отключившихся игроков
                    
                    var playerPos = new float3(player.transform.position.x, player.transform.position.y, 0f);
                    var dist = math.distance(enemyPos, playerPos);

                    if (!(dist < minDistance)) continue;
                    minDistance = dist;
                    closestPlayerPos = playerPos;
                    closestPlayer = player; // Запоминаем, кого именно мы преследуем
                }

                // Если по какой-то причине цель не найдена, стоим на месте
                if (closestPlayer == null) continue;

                var direction = closestPlayerPos - enemyPos;
                var distance = math.length(direction); 

                // 2. ПРОВЕРКА СТОЛКНОВЕНИЯ С БЛИЖАЙШИМ ИГРОКОМ
                if (distance < 0.5f)
                {
                    // Теперь мы берем Health именно у того игрока, к которому подошли, а не статичный!
                    var targetHealth = closestPlayer.GetComponent<Health>();
                    
                    if (targetHealth != null && targetHealth.HasStateAuthority)
                    {
                        targetHealth.TakeDamage(10f); // Наносим 10 урона
                    }
                    
                    // Приказываем движку уничтожить этого врага
                    ecb.DestroyEntity(entity);
                }
                // 3. ДВИЖЕНИЕ
                else
                {
                    direction = math.normalize(direction);
                    transform.ValueRW.Position += direction * movement.ValueRO.Speed * deltaTime;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}