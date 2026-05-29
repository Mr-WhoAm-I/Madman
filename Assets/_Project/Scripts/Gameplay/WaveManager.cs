using System.Collections;
using System.Linq;
using Fusion;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Authoring;
using _Project.Scripts.Core;
using _Project.Scripts.UI;

namespace _Project.Scripts.Gameplay
{
    public class WaveManager : NetworkBehaviour
    {
        public static WaveManager Instance;

        [Header("Сценарий уровня")]
        public WaveScenarioData levelScenario;

        [Networked] private int CurrentWaveIndex { get; set; }
        [Networked] public NetworkBool IsShopPhase { get; set; }

        private EntityManager _entityManager;
        private Entity _registryEntity;
        
        // Кэшируем ссылку на контроллер профиля для быстрой работы
        private ProfileController _profileController;

        private void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            // Кешируем один раз при старте сцены, чтобы не искать каждый раз
            _profileController = ProfileController.Instance;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetupBattleLayout();
            }
            if (!HasStateAuthority) return;

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            StartCoroutine(InitializeRegistryCoroutine());
        }

        // Отключаем паранойю Rider для методов инициализации
        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator InitializeRegistryCoroutine()
        {
            yield return new WaitForSeconds(0.5f); 

            var query = _entityManager.CreateEntityQuery(typeof(EnemyPrefabElement));

            if (query.IsEmpty) yield break;
            _registryEntity = query.GetSingletonEntity();
            Debug.Log("[WaveManager] Реестр врагов найден. Запуск Сценария!");
            StartCoroutine(WaveRoutine());
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator WaveRoutine()
        {
            while (CurrentWaveIndex < levelScenario.waves.Count)
            {
                var currentWave = levelScenario.waves[CurrentWaveIndex];
                IsShopPhase = false;

                yield return new WaitForSeconds(currentWave.delayBeforeWave);

                var batchCoroutines = currentWave.spawnBatches.Select(batch => StartCoroutine(ProcessBatch(batch))).ToList();
                Debug.Log($"[WaveManager] НАЧАЛО ВОЛНЫ: {currentWave.waveName}");
                
                foreach (var c in batchCoroutines) yield return c;

                yield return new WaitUntil(() => GetAliveEnemiesCount() == 0);

                Debug.Log($"[WaveManager] ВОЛНА {currentWave.waveName} ОТБИТА!");
                Rpc_GrantExperience(currentWave.waveXpReward);

                if (currentWave.hasShopAfterWave)
                {
                    IsShopPhase = true;
                    yield return new WaitForSeconds(5f); 
                }

                CurrentWaveIndex++;
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator ProcessBatch(SpawnBatch batch)
        {
            // Быстрая проверка на null (ReferenceEquals) не дергает C++ API Unity
            if (ReferenceEquals(batch.enemyDefinition, null)) yield break;

            var spawnedCount = 0;
            var targetName = new FixedString64Bytes(batch.enemyDefinition.name);
            var prefabEntity = Entity.Null;

            var buffer = _entityManager.GetBuffer<EnemyPrefabElement>(_registryEntity);
            // ДОБАВИТЬ:
            foreach (var element in buffer)
            {
                if (element.EnemyName != targetName) continue;
                prefabEntity = element.PrefabEntity;
                break;
            }

            if (prefabEntity == Entity.Null) yield break;

            if (!SpawnZone.AllZones.TryGetValue(batch.spawnZoneID, out var zone)) yield break;

            while (spawnedCount < batch.totalAmount)
            {
                var toSpawn = Mathf.Min(batch.spawnAtOnce, batch.totalAmount - spawnedCount);

                var spawnPos = zone.GetRandomPoint();
                for (var i = 0; i < toSpawn; i++)
                {
                    SpawnEnemyECS(prefabEntity, spawnPos, batch.enemyDefinition, batch.enemyLevel);
                    spawnedCount++;
                }

                if (spawnedCount < batch.totalAmount)
                {
                    yield return new WaitForSeconds(batch.spawnDelay);
                }
            }
        }

        private void SpawnEnemyECS(Entity prefab, Vector3 position, EnemyDefinitionData data, int level)
        {
            var newEnemy = _entityManager.Instantiate(prefab);
            _entityManager.SetComponentData(newEnemy, LocalTransform.FromPosition(position.x, position.y, 0));

            var finalHealth = data.GetHealthForLevel(level);
            if (_entityManager.HasComponent<EnemyHealthComponent>(newEnemy))
            {
                _entityManager.SetComponentData(newEnemy, new EnemyHealthComponent { CurrentHealth = finalHealth });
            }
        }

        private int GetAliveEnemiesCount()
        {
            var query = _entityManager.CreateEntityQuery(typeof(EnemyTagComponent));
            return query.CalculateEntityCount();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void Rpc_GrantExperience(float xpAmount)
        {
            _profileController?.AddExperience(xpAmount);
        }
    }
}