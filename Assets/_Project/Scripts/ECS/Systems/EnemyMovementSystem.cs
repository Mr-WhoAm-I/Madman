using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial class EnemyMovementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // 1. БЕЗОПАСНАЯ ПРОВЕРКА (Защита от крашей при выходе из сессии)
            if (EnemySwarmManager.Instance == null || EnemySwarmManager.Instance.Object == null || !EnemySwarmManager.Instance.HasStateAuthority) 
                return;

            // 2. ПОЛУЧАЕМ ПРАВИЛЬНОЕ ВРЕМЯ FUSION (Защита от медленного движения)
            float deltaTime = 0.01666f; 
            if (SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComp))
            {
                deltaTime = timeComp.DeltaTime;
            }
            if (deltaTime <= 0f) deltaTime = 0.01666f; // Fallback на стандартный тик

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EnemyMovementComponent>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                var enemyPos = transform.ValueRO.Position;
                float3 targetPos = float3.zero;
                bool hasTarget = false;
                float minDistance = float.MaxValue;
                
                PlayerManager closestPlayer = null;
                TurretNetworkBridge closestTurret = null;

                // 3. ИЩЕМ АКТИВНЫЕ ТУРЕЛИ (ПРИОРИТЕТ)
                // Обратный цикл for защищает от ошибки "Collection was modified" при уничтожении турели
                for (int i = TurretNetworkBridge.ActiveTurrets.Count - 1; i >= 0; i--)
                {
                    var turret = TurretNetworkBridge.ActiveTurrets[i];
                    
                    // Жесткая защита от ссылок на уничтоженные объекты (Unity null check)
                    if (turret == null || turret.Object == null || !turret.IsTaunting) continue;

                    var turretPos = new float3(turret.transform.position.x, turret.transform.position.y, 0f);
                    float dist = math.distance(enemyPos, turretPos);

                    if (dist <= 10f && dist < minDistance) // 10f - радиус агра
                    {
                        minDistance = dist;
                        targetPos = turretPos;
                        hasTarget = true;
                        closestTurret = turret;
                    }
                }

                // 4. ЕСЛИ НЕТ ТУРЕЛЕЙ - ИЩЕМ ИГРОКА
                if (!hasTarget)
                {
                    for (int i = PlayerManager.AllActivePlayers.Count - 1; i >= 0; i--)
                    {
                        var player = PlayerManager.AllActivePlayers[i];
                        if (player == null || player.gameObject == null) continue;

                        var playerPos = new float3(player.transform.position.x, player.transform.position.y, 0f);
                        var dist = math.distance(enemyPos, playerPos);

                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            targetPos = playerPos;
                            hasTarget = true;
                            closestPlayer = player;
                        }
                    }
                }

                // Если вообще никого нет на карте - стоим на месте
                if (!hasTarget) continue;

                var direction = targetPos - enemyPos;
                var distance = math.length(direction); 

                // 5. СТОЛКНОВЕНИЕ И УРОН
                if (distance < 0.5f)
                {
                    if (closestTurret != null && closestTurret.Object != null && closestTurret.HasStateAuthority)
                    {
                        closestTurret.Health -= 10f; 
                    }
                    else if (closestPlayer != null && closestPlayer.gameObject != null)
                    {
                        var targetHealth = closestPlayer.GetComponent<Health>();
                        if (targetHealth != null && targetHealth.HasStateAuthority)
                        {
                            targetHealth.TakeDamage(10f); 
                        }
                    }
                    
                    ecb.DestroyEntity(entity); 
                }
                else
                {
                    direction = math.normalize(direction);
                    transform.ValueRW.Position += direction * movement.ValueRO.Speed * deltaTime;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}