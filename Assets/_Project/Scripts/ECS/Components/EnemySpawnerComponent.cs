using Unity.Entities;

namespace _Project.Scripts.ECS.Components
{
    public struct EnemySpawnerComponent : IComponentData
    {
        public Entity EnemyPrefab; // Оптимизированная ссылка на префаб
        public float SpawnInterval; // Как часто спавнить
        public double NextSpawnTime; // Время следующего спавна
    }
}