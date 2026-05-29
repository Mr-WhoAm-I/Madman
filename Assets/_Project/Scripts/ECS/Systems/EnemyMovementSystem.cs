using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (EnemySwarmManager.Instance == null || !EnemySwarmManager.Instance.HasStateAuthority)
                return;

            var deltaTime = EnemySwarmManager.Instance.Runner.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var targetQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, TargetableComponent>().Build();
            var targets = targetQuery.ToEntityArray(Allocator.TempJob);
            var targetTransforms = targetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var targetPriorities = targetQuery.ToComponentDataArray<TargetableComponent>(Allocator.TempJob);
            var tauntLookup = SystemAPI.GetComponentLookup<TauntComponent>(true);

            // ДОБАВЛЕНО: Считываем массивы дебаффов для передачи в Job
            var apathyLookup = SystemAPI.GetComponentLookup<ApathyDebuffComponent>(true);
            var slowLookup = SystemAPI.GetComponentLookup<FrostSlowComponent>(true);

            var movementJob = new EnemyMovementJob
            {
                DeltaTime = deltaTime,
                Ecb = ecb.AsParallelWriter(), 
                Targets = targets,
                TargetTransforms = targetTransforms,
                TargetPriorities = targetPriorities,
                TauntLookup = tauntLookup,
                ApathyLookup = apathyLookup,
                SlowLookup = slowLookup
            };

            state.Dependency = movementJob.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            targets.Dispose();
            targetTransforms.Dispose();
            targetPriorities.Dispose();
        }
    }

    [BurstCompile]
    public partial struct EnemyMovementJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;

        [ReadOnly] public NativeArray<Entity> Targets;
        [ReadOnly] public NativeArray<LocalTransform> TargetTransforms;
        [ReadOnly] public NativeArray<TargetableComponent> TargetPriorities;
        [ReadOnly] public ComponentLookup<TauntComponent> TauntLookup;

        // ДОБАВЛЕНО: Lookup-таблицы для проверки дебаффов на конкретном враге
        [ReadOnly] public ComponentLookup<ApathyDebuffComponent> ApathyLookup;
        [ReadOnly] public ComponentLookup<FrostSlowComponent> SlowLookup;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref LocalTransform transform, in EnemyMovementComponent movement, in EnemyTagComponent enemyTag)
        {
            // --- 1. ПРОВЕРКА НА ЗАМОРОЗКУ (АПАТИЯ) ---
            if (ApathyLookup.HasComponent(entity))
            {
                if (ApathyLookup[entity].FreezeTimer > 0f)
                {
                    // Враг во льду! Пропускаем логику поиска, движения и атаки.
                    return;
                }
            }

            var enemyPos = transform.Position;
            var bestTarget = Entity.Null;
            var bestTargetPos = float3.zero;
            var maxPriority = -1f;
            var minDistance = 20f; 

            // 1. ПОИСК ЛУЧШЕЙ ЦЕЛИ
            for (var i = 0; i < Targets.Length; i++)
            {
                var currentPriority = TargetPriorities[i].Priority;

                if (currentPriority <= 0.001f) continue;

                var targetPos = TargetTransforms[i].Position;
                var dist = math.distance(enemyPos, targetPos);
                var allowedAgroRange = 20f; 

                if (currentPriority > 1.0f && TauntLookup.HasComponent(Targets[i]))
                {
                    allowedAgroRange = TauntLookup[Targets[i]].Radius;
                }

                if (dist < allowedAgroRange) 
                {
                    var isBetterTarget = false;

                    if (currentPriority > maxPriority)
                    {
                        isBetterTarget = true;
                    }
                    else if (math.abs(currentPriority - maxPriority) < 0.001f && dist < minDistance)
                    {
                        isBetterTarget = true;
                    }

                    if (isBetterTarget)
                    {
                        maxPriority = currentPriority;
                        minDistance = dist;
                        bestTarget = Targets[i];
                        bestTargetPos = targetPos;
                    }
                }
            }

            if (bestTarget == Entity.Null) return;

            // --- 2. РАСЧЕТ ДВИЖЕНИЯ С УЧЕТОМ ЗАМЕДЛЕНИЯ ---
            var currentSpeed = movement.Speed;
            if (SlowLookup.HasComponent(entity))
            {
                currentSpeed *= SlowLookup[entity].SpeedMultiplier;
            }

            var direction = bestTargetPos - enemyPos;
            var distance = math.length(direction);

            // 3. ДВИЖЕНИЕ И АТАКА
            if (distance < 0.8f) 
            {
                // ИСПРАВЛЕНО: Передаем свою сущность (entity) как источник урона
                Ecb.AddComponent(chunkIndex, bestTarget, new TakeDamageComponent { 
                    Amount = 10f, 
                    SourceEntity = entity 
                });
                Ecb.DestroyEntity(chunkIndex, entity); 
            }
            else
            {
                direction = math.normalize(direction);
                transform.Position += direction * currentSpeed * DeltaTime;
            }
        }
    }
}