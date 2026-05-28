using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Systems
{
    // ИСПРАВЛЕНИЕ: Убрали [BurstCompile] с самой системы, чтобы легально читать синглтоны Unity
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Эта управляемая проверка теперь выполняется безопасно на главном потоке
            if (EnemySwarmManager.Instance == null || !EnemySwarmManager.Instance.HasStateAuthority)
                return;

            var deltaTime = EnemySwarmManager.Instance.Runner.DeltaTime;
            
            // Используем Allocator.TempJob, так как данные пойдут в многопоточную джобу
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var targetQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, TargetableComponent>().Build();
            var targets = targetQuery.ToEntityArray(Allocator.TempJob);
            var targetTransforms = targetQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            var targetPriorities = targetQuery.ToComponentDataArray<TargetableComponent>(Allocator.TempJob);
            var tauntLookup = SystemAPI.GetComponentLookup<TauntComponent>(true);

            // Инициализируем нашу сверхбыструю Burst-джобу данными
            var movementJob = new EnemyMovementJob
            {
                DeltaTime = deltaTime,
                Ecb = ecb.AsParallelWriter(), // ParallelWriter позволяет безопасно писать из разных потоков
                Targets = targets,
                TargetTransforms = targetTransforms,
                TargetPriorities = targetPriorities,
                TauntLookup = tauntLookup
            };

            // Запускаем параллельное вычисление на все ядра процессора
            state.Dependency = movementJob.ScheduleParallel(state.Dependency);
            
            // Гарантируем, что джоба завершится до выполнения команд создания/уничтожения существ
            state.Dependency.Complete();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            targets.Dispose();
            targetTransforms.Dispose();
            targetPriorities.Dispose();
        }
    }

    // ВЫНЕСЕНО: Вся тяжелая симуляция упакована в нативную Burst-джобу
    [BurstCompile]
    public partial struct EnemyMovementJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;

        [ReadOnly] public NativeArray<Entity> Targets;
        [ReadOnly] public NativeArray<LocalTransform> TargetTransforms;
        [ReadOnly] public NativeArray<TargetableComponent> TargetPriorities;
        [ReadOnly] public ComponentLookup<TauntComponent> TauntLookup;

        // Этот метод Burst распараллелит автоматически для каждого моба в игре
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
                float3 targetPos = TargetTransforms[i].Position;
                float dist = math.distance(enemyPos, targetPos);

                float allowedAgroRange = 20f; 
                float currentPriority = TargetPriorities[i].Priority;

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
                    // ИСПРАВЛЕНИЕ: Burst не любит методы Mathf. Заменили на чистую математику дельты
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
                // Для ParallelWriter обязательно передаем chunkIndex, чтобы команды не перемешались
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