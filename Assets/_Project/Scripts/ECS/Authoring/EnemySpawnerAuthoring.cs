using Unity.Entities;
using UnityEngine;
using _Project.Scripts.ECS.Components;
using UnityEngine.Serialization;

namespace _Project.Scripts.ECS.Authoring
{
    public class EnemySpawnerAuthoring : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public float spawnInterval = 0.5f; // По умолчанию спавним каждые полсекунды

        class Baker : Baker<EnemySpawnerAuthoring>
        {
            public override void Bake(EnemySpawnerAuthoring authoring)
            {
                // Spawner должен иметь позицию, поэтому Dynamic
                var entity = GetEntity(TransformUsageFlags.Dynamic); 
                
                AddComponent(entity, new EnemySpawnerComponent
                {
                    // Превращаем GameObject в Entity прямо во время запекания
                    EnemyPrefab = GetEntity(authoring.enemyPrefab, TransformUsageFlags.Dynamic),
                    SpawnInterval = authoring.spawnInterval,
                    NextSpawnTime = 0
                });
            }
        }
    }
}