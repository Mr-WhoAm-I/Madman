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

            var movementJob = new EnemyMovementJob
            {
                DeltaTime = deltaTime,
                Ecb = ecb.AsParallelWriter(), 
                Targets = targets,
                TargetTransforms = targetTransforms,
                TargetPriorities = targetPriorities,
                TauntLookup = tauntLookup
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

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref LocalTransform transform, in EnemyMovementComponent movement, in EnemyTagComponent enemyTag)
        {
            float3 enemyPos = transform.Position;
            Entity bestTarget = Entity.Null;
            float3 bestTargetPos = float3.zero;
            float maxPriority = -1f;
            float minDistance = 20f; 

            // 1. ПОИСК ЛУЧШЕЙ ЦЕЛИ
            for (int i = 0; i < Targets.Length; i++)
            {
                float currentPriority = TargetPriorities[i].Priority;

                // АБСОЛЮТНЫЙ ИНВИЗ: Если приоритет цели равен 0 (или сброшен инвизом),
                // вражеский ИИ полностью слепнет к этой сущности и идет мимо!
                if (currentPriority <= 0.001f) continue;

                float3 targetPos = TargetTransforms[i].Position;
                float dist = math.distance(enemyPos, targetPos);
                float allowedAgroRange = 20f; 

                if (currentPriority > 1.0f && TauntLookup.HasComponent(Targets[i]))
                {
                    allowedAgroRange = TauntLookup[Targets[i]].Radius;
                }

                if (dist < allowedAgroRange) 
                {
                    bool isBetterTarget = false;

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

            // 2. ДВИЖЕНИЕ И АТАКА
            var direction = bestTargetPos - enemyPos;
            var distance = math.length(direction);

            if (distance < 0.8f) 
            {
                Ecb.AddComponent(chunkIndex, bestTarget, new TakeDamageComponent { Amount = 10f });
                Ecb.DestroyEntity(chunkIndex, entity); 
            }
            else
            {
                direction = math.normalize(direction);
                transform.Position += direction * movement.Speed * DeltaTime;
            }
        }
    }
}