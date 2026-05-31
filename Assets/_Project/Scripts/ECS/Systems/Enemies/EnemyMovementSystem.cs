using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Managers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Enemies
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

            var apathyLookup = SystemAPI.GetComponentLookup<ApathyDebuffComponent>(true);
            var slowLookup = SystemAPI.GetComponentLookup<FrostSlowComponent>(true);
            // ДОБАВЛЕНО: Lookup для Крио-заморозки от турели
            var cryoLookup = SystemAPI.GetComponentLookup<CryoDebuffComponent>(true);

            var movementJob = new EnemyMovementJob
            {
                DeltaTime = deltaTime,
                Ecb = ecb.AsParallelWriter(), 
                Targets = targets,
                TargetTransforms = targetTransforms,
                TargetPriorities = targetPriorities,
                TauntLookup = tauntLookup,
                ApathyLookup = apathyLookup,
                SlowLookup = slowLookup,
                CryoLookup = cryoLookup // ПЕРЕДАЕМ В JOB
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

        [ReadOnly] public ComponentLookup<ApathyDebuffComponent> ApathyLookup;
        [ReadOnly] public ComponentLookup<FrostSlowComponent> SlowLookup;
        // ДОБАВЛЕНО: Таблица Крио-заморозки
        [ReadOnly] public ComponentLookup<CryoDebuffComponent> CryoLookup;

        public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndex, ref LocalTransform transform, in EnemyMovementComponent movement, in EnemyTagComponent enemyTag)
        {
            if (ApathyLookup.HasComponent(entity))
            {
                if (ApathyLookup[entity].FreezeTimer > 0f)
                {
                    return;
                }
            }

            var enemyPos = transform.Position;
            var bestTarget = Entity.Null;
            var bestTargetPos = float3.zero;
            var maxPriority = -1f;
            var minDistance = 20f; 

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
            
            // ДОБАВЛЕНО: Применяем замедление от турели (они могут стакаться с другими замедлениями)
            if (CryoLookup.HasComponent(entity))
            {
                currentSpeed *= CryoLookup[entity].SpeedMultiplier;
            }

            var direction = bestTargetPos - enemyPos;
            var distance = math.length(direction);

            if (distance < 0.8f) 
            {
                Ecb.AddComponent(chunkIndex, bestTarget, new TakeDamageComponent { 
                    Amount = 10f, 
                    SourceEntity = entity 
                });
            }
            else
            {
                direction = math.normalize(direction);
                transform.Position += direction * currentSpeed * DeltaTime;
            }
        }
    }
}