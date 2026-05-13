using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
{
    [BurstCompile]
    public partial struct EnemySpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Получаем текущее время игры
            double currentTime = SystemAPI.Time.ElapsedTime;

            // Ищем все спавнеры на сцене
            foreach (var (spawner, transform) in SystemAPI.Query<RefRW<EnemySpawnerComponent>, RefRO<LocalTransform>>())
            {
                // Если пришло время спавнить
                if (currentTime >= spawner.ValueRO.NextSpawnTime)
                {
                    // Мгновенно клонируем Entity в памяти
                    Entity newEnemy = state.EntityManager.Instantiate(spawner.ValueRO.EnemyPrefab);
                    
                    // Генерируем случайную позицию в радиусе 5 юнитов от спавнера
                    uint randomSeed = (uint)(currentTime * 10000) + 1; // Уникальный сид (seed)
                    var random = new Random(randomSeed);
                    float3 randomOffset = new float3(random.NextFloat(-5f, 5f), random.NextFloat(-5f, 5f), 0);
                    
                    // Перемещаем нового врага в эту точку
                    state.EntityManager.SetComponentData(newEnemy, LocalTransform.FromPosition(transform.ValueRO.Position + randomOffset));

                    // Задаем время для следующего врага
                    spawner.ValueRW.NextSpawnTime = currentTime + spawner.ValueRO.SpawnInterval;
                }
            }
        }
    }
}