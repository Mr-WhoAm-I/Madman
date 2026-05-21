using System.Collections;
using System.Collections.Generic;
using Fusion;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Authoring;

namespace _Project.Scripts.Gameplay
{
    public class WaveManager : NetworkBehaviour
    {
        public static WaveManager Instance;

        [Header("Сценарий уровня")]
        public WaveScenarioData levelScenario;

        // Эти переменные синхронизируются по сети, чтобы Клиенты знали текущую волну
        [Networked] private int CurrentWaveIndex { get; set; }
        [Networked] public NetworkBool IsShopPhase { get; set; }

        private EntityManager _entityManager;
        private Entity _registryEntity;

        private void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            // Только Сервер командует волнами! Клиенты просто смотрят.
            if (!HasStateAuthority) return;

            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            StartCoroutine(InitializeRegistryCoroutine());
        }

        // ReSharper disable Unity.PerformanceAnalysis
        private IEnumerator InitializeRegistryCoroutine()
        {
            // Ждем полсекунды, чтобы ECS гарантированно успел запечь все сущности
            yield return new WaitForSeconds(0.5f); 

            // Ищем наш Реестр Врагов в мире ECS
            var query = _entityManager.CreateEntityQuery(typeof(EnemyPrefabElement));
            
            // ИСПРАВЛЕНИЕ: Явно проверяем, не пустой ли запрос, и берем сущность напрямую.
            // Это избавляет нас от бага с невидимым System.Object!
            if (!query.IsEmpty)
            {
                _registryEntity = query.GetSingletonEntity();
                Debug.Log("[WaveManager] Реестр врагов найден. Запуск Сценария!");
                StartCoroutine(WaveRoutine());
            }
            else
            {
                Debug.LogError("[WaveManager] ОШИБКА: Не найден EnemyRegistry на сцене!");
            }
        }

        private IEnumerator WaveRoutine()
        {
            while (CurrentWaveIndex < levelScenario.waves.Count)
            {
                var currentWave = levelScenario.waves[CurrentWaveIndex];
                IsShopPhase = false;

                Debug.Log($"[WaveManager] Подготовка к: {currentWave.waveName}");
                yield return new WaitForSeconds(currentWave.delayBeforeWave);

                Debug.Log($"[WaveManager] НАЧАЛО ВОЛНЫ: {currentWave.waveName}");

                // Запускаем спавн всех пачек в этой волне ПАРАЛЛЕЛЬНО
                List<Coroutine> batchCoroutines = new();
                foreach (var batch in currentWave.spawnBatches)
                {
                    batchCoroutines.Add(StartCoroutine(ProcessBatch(batch)));
                }

                // Ждем, пока все пачки не закончат выходить из разломов
                foreach (var c in batchCoroutines) yield return c;

                Debug.Log($"[WaveManager] Все враги вышли на арену. Ждем зачистки...");

                // Пауза до тех пор, пока количество живых врагов не станет равно 0
                yield return new WaitUntil(() => GetAliveEnemiesCount() == 0);

                Debug.Log($"[WaveManager] ВОЛНА {currentWave.waveName} ОТБИТА!");

                if (currentWave.hasShopAfterWave)
                {
                    IsShopPhase = true;
                    Debug.Log("[WaveManager] ФАЗА МАГАЗИНА. Отдыхаем...");
                    // В будущем тут будет ожидание нажатия кнопки "Готов" от команды
                    yield return new WaitForSeconds(5f); 
                }

                CurrentWaveIndex++;
            }

            Debug.Log("[WaveManager] ПОБЕДА! Сценарий полностью завершен.");
        }

        private IEnumerator ProcessBatch(SpawnBatch batch)
        {
            if (batch.enemyDefinition == null) yield break;

            int spawnedCount = 0;
            FixedString64Bytes targetName = new FixedString64Bytes(batch.enemyDefinition.name);
            Entity prefabEntity = Entity.Null;

            // Ищем нужного врага в Реестре
            var buffer = _entityManager.GetBuffer<EnemyPrefabElement>(_registryEntity);
            foreach (var element in buffer)
            {
                if (element.EnemyName == targetName)
                {
                    prefabEntity = element.PrefabEntity;
                    break;
                }
            }

            if (prefabEntity == Entity.Null)
            {
                Debug.LogError($"[WaveManager] Враг {batch.enemyDefinition.name} не найден в Реестре!");
                yield break;
            }

            // Ищем физическую Зону Спавна по ID
            if (!SpawnZone.AllZones.TryGetValue(batch.spawnZoneID, out SpawnZone zone))
            {
                Debug.LogError($"[WaveManager] Разлом с ID {batch.spawnZoneID} не найден на арене!");
                yield break;
            }

            // ПОРЦИОННЫЙ СПАВН
            while (spawnedCount < batch.totalAmount)
            {
                int toSpawn = Mathf.Min(batch.spawnAtOnce, batch.totalAmount - spawnedCount);

                for (int i = 0; i < toSpawn; i++)
                {
                    Vector3 spawnPos = zone.GetRandomPoint();
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
            // Клонируем врага
            var newEnemy = _entityManager.Instantiate(prefab);
            
            // Ставим его на точку разлома
            _entityManager.SetComponentData(newEnemy, LocalTransform.FromPosition(position.x, position.y, 0));

            // НАКАЧИВАЕМ ХАРАКТЕРИСТИКАМИ ПО УРОВНЮ!
            float finalHealth = data.GetHealthForLevel(level);
            if (_entityManager.HasComponent<EnemyHealthComponent>(newEnemy))
            {
                _entityManager.SetComponentData(newEnemy, new EnemyHealthComponent { CurrentHealth = finalHealth });
            }
        }

        private int GetAliveEnemiesCount()
        {
            // Считаем всех сущностей с тегом врага
            var query = _entityManager.CreateEntityQuery(typeof(EnemyTagComponent));
            return query.CalculateEntityCount();
        }
    }
}