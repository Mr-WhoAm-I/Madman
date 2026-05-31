using _Project.Scripts.ECS.Components.Enemies;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Enemies
{
    [BurstCompile]
    public partial struct EnemySpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Получаем текущее время игры
            var currentTime = SystemAPI.Time.ElapsedTime;

            // Ищем все спавнеры на сцене
            foreach (var (spawner, transform) in SystemAPI.Query<RefRW<EnemySpawnerComponent>, RefRO<LocalTransform>>())
            {
                // Если пришло время спавнить
                if (currentTime >= spawner.ValueRO.NextSpawnTime)
                {
                    // Мгновенно клонируем Entity в памяти
                    var newEnemy = state.EntityManager.Instantiate(spawner.ValueRO.EnemyPrefab);
                    
                    // Генерируем случайную позицию в радиусе 5 юнитов от спавнера
                    var randomSeed = (uint)(currentTime * 10000) + 1; // Уникальный сид (seed)
                    var random = new Random(randomSeed);
                    var randomOffset = new float3(random.NextFloat(-5f, 5f), random.NextFloat(-5f, 5f), 0);
                    
                    // Перемещаем нового врага в эту точку
                    state.EntityManager.SetComponentData(newEnemy, LocalTransform.FromPosition(transform.ValueRO.Position + randomOffset));

                    // Задаем время для следующего врага
                    spawner.ValueRW.NextSpawnTime = currentTime + spawner.ValueRO.SpawnInterval;
                }
            }
        }
    }
}