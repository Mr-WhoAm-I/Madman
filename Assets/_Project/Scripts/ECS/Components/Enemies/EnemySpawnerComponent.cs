using Unity.Entities;

namespace _Project.Scripts.ECS.Components.Enemies
{
    public struct EnemySpawnerComponent : IComponentData
    {
        public Entity EnemyPrefab; // Оптимизированная ссылка на префаб
        public float SpawnInterval; // Как часто спавнить
        public double NextSpawnTime; // Время следующего спавна
    }
}