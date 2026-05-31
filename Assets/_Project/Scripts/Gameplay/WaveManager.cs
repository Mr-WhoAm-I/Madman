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
using _Project.Scripts.Network;
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
        [Header("Тайминги фаз")]
        [Tooltip("Сколько секунд дается игрокам на сбор лута после убийства последнего врага")]
        public float timeToCollectLoot = 5f;
        
        [Tooltip("Сколько секунд длится фаза магазина до принудительного начала следующей волны")]
        public float shopPhaseDuration = 30f;

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

                // === НОВАЯ ЛОГИКА ФАЗ ===
                if (currentWave.hasShopAfterWave)
                {
                    Debug.Log($"[WaveManager] Ждем {timeToCollectLoot} сек. для сбора лута...");
                    yield return new WaitForSeconds(timeToCollectLoot);

                    IsShopPhase = true;
                    Rpc_SetShopUIState(true);
                    Debug.Log($"[WaveManager] ФАЗА МАГАЗИНА НАЧАТА! Длительность: {shopPhaseDuration} сек.");

                    yield return new WaitForSeconds(shopPhaseDuration);

                    IsShopPhase = false;
                    Rpc_SetShopUIState(false);
                    Debug.Log("[WaveManager] ФАЗА МАГАЗИНА ОКОНЧЕНА! Подготовка к следующей волне...");
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

            // --- ДОБАВЛЕНО: Выдаем врагу его персональную награду ---
            // (Позже мы можем умножать baseBounty на множитель волны CurrentWaveIndex)
            int finalBounty = data.baseBounty; 
            _entityManager.AddComponentData(newEnemy, new EnemyLootDropComponent { Bounty = finalBounty });
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
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SetShopUIState(NetworkBool isOpen)
        {
            if (HUDManager.Instance == null) return;

            if (isOpen)
            {
                // --- ИНИЦИАЛИЗАЦИЯ СЕССИИ МАГАЗИНА ---
                int maxRerolls = 1; // Базовое количество рероллов (1 бесплатный)
                
                if (PlayerNetworkBridge.LocalPlayer != null && PlayerNetworkBridge.LocalPlayer.Object.IsValid)
                {
                    var player = PlayerNetworkBridge.LocalPlayer;
                    if (player.EntityManager.Exists(player.PlayerEntity))
                    {
                        var config = player.EntityManager.GetComponentData<SkillConfigComponent>(player.PlayerEntity);
                        // Если игрок прокачал перк на рероллы, прибавляем их к базовому
                        maxRerolls += config.MaxRerolls; 
                    }
                }
                
                if (LocalShopManager.Instance != null)
                {
                    LocalShopManager.Instance.OnShopPhaseStarted(maxRerolls);
                }
                // ------------------------------------

                HUDManager.Instance.OpenWindow(UIWindowType.Shop);
            }
            else
            {
                HUDManager.Instance.CloseCurrentWindow(); 
            }
        }
    }
}