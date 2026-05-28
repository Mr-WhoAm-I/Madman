using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (EnemySwarmManager.Instance == null || !EnemySwarmManager.Instance.HasStateAuthority)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // Кэшируем Query всех целей для максимальной скорости
            var targetQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, TargetableComponent>().Build();
            var targets = targetQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var targetTransforms = targetQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            var targetPriorities = targetQuery.ToComponentDataArray<TargetableComponent>(Unity.Collections.Allocator.Temp);

            foreach (var (transform, movement, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<EnemyMovementComponent>>().WithEntityAccess().WithAll<EnemyTagComponent>())
            {
                float3 enemyPos = transform.ValueRO.Position;
                Entity bestTarget = Entity.Null;
                float3 bestTargetPos = float3.zero;
                float maxPriority = -1f;
                float minDistance = 20f; 

                // 1. ПОИСК ЛУЧШЕЙ ЦЕЛИ
                for (int i = 0; i < targets.Length; i++)
                {
                    float3 targetPos = targetTransforms[i].Position;
                    float dist = math.distance(enemyPos, targetPos);

                    // Проверяем только тех, кто в радиусе агра (например, 20f)
                    if (dist < 20f) 
                    {
                        float currentPriority = targetPriorities[i].Priority;
                        bool isBetterTarget = false;

                        // Если приоритет строго выше — переключаемся на эту цель
                        if (currentPriority > maxPriority)
                        {
                            isBetterTarget = true;
                        }
                        // Если приоритеты РАВНЫ (например, 1.0 и 1.0), выбираем того, кто БЛИЖЕ
                        else if (Mathf.Approximately(currentPriority, maxPriority) && dist < minDistance)
                        {
                            isBetterTarget = true;
                        }

                        if (isBetterTarget)
                        {
                            maxPriority = currentPriority;
                            minDistance = dist;
                            bestTarget = targets[i];
                            bestTargetPos = targetPos;
                        }
                    }
                }

                if (bestTarget == Entity.Null) continue;

                // 2. ДВИЖЕНИЕ И АТАКА
                var direction = bestTargetPos - enemyPos;
                var distance = math.length(direction);

                if (distance < 0.8f) // Дистанция атаки
                {
                    // Враг просто кидает "запрос на урон" в найденную цель
                    ecb.AddComponent(bestTarget, new TakeDamageComponent { Amount = 10f });
                    
                    ecb.DestroyEntity(entity); // Враг самоуничтожился при атаке
                }
                else
                {
                    direction = math.normalize(direction);
                    transform.ValueRW.Position += direction * movement.ValueRO.Speed * deltaTime;
                }
            }

            targets.Dispose();
            targetTransforms.Dispose();
            targetPriorities.Dispose();
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}