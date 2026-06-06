using System.Collections.Generic;
using _Project.Scripts.Data.Skills;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.ECS.Components.Skills;
using Fusion;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Allocator = Unity.Collections.Allocator;

namespace _Project.Scripts.Network.Bridges
{
    public class IceProjectileNetworkBridge : NetworkBehaviour
    {
        [Networked] private TickTimer LifeTimer { get; set; }
        
        private MelancholicSkillData _skillData;
        private Entity _shooterEntity;
        
        private float _speed;
        private float _maxDistance;
        private bool _hasExploded = false;
        private Vector3 _startPos;
        
        private bool _isShard = false;
        private Entity _targetEntity = Entity.Null;

        // Инициализация основного снаряда
        public void InitializeMain(PlayerRef owner, MelancholicSkillData data, float2 direction, Entity shooterEntity)
        {
            _skillData = data;
            _shooterEntity = shooterEntity;
            _startPos = transform.position;
            _isShard = false;
            
            // Читаем параметры из конфига
            _maxDistance = data != null ? data.projectileMaxDistance : 15f;
            _speed = data != null ? data.projectileSpeed : 12f;
        }

        // Инициализация самонаводящегося шипа
        public void InitializeShard(PlayerRef owner, MelancholicSkillData data, Entity shooterEntity, Entity target)
        {
            _skillData = data;
            _shooterEntity = shooterEntity;
            _isShard = true;
            _targetEntity = target;
            
            // Читаем параметры из конфига
            _speed = data != null ? data.shardSpeed : 18f;
            
            transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); 
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, 5.0f); // Защитный фолбэк на случай улета за карту
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (_hasExploded || LifeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            float3 currentPos = transform.position;

            // =======================================================
            // ЛОГИКА 1: САМОНАВОДЯЩИЙСЯ ШИП
            // =======================================================
            if (_isShard)
            {
                if (em.Exists(_targetEntity) && em.HasComponent<LocalTransform>(_targetEntity))
                {
                    float3 targetPos = em.GetComponentData<LocalTransform>(_targetEntity).Position;
                    Vector3 dir = math.normalize(targetPos - currentPos);
                    
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                    transform.position += dir * _speed * Runner.DeltaTime;

                    if (math.distance(currentPos, targetPos) < 0.8f)
                    {
                        HitTarget(_targetEntity);
                    }
                }
                else
                {
                    transform.position += transform.up * _speed * Runner.DeltaTime;
                }
            }
            // =======================================================
            // ЛОГИКА 2: ОСНОВНОЙ СНАРЯД (ЭПИЦЕНТР)
            // =======================================================
            else 
            {
                transform.position += transform.up * _speed * Runner.DeltaTime;

                if (Vector3.Distance(_startPos, transform.position) >= _maxDistance)
                {
                    ExecuteChainExplosion();
                    return;
                }

                var enemyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyTagComponent>());
                var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
                var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                bool hit = false;
                for (int i = 0; i < enemyEntities.Length; i++)
                {
                    if (math.distance(currentPos, enemyTransforms[i].Position) < 0.8f) 
                    {
                        hit = true;
                        break;
                    }
                }
                
                enemyEntities.Dispose();
                enemyTransforms.Dispose();

                if (hit)
                {
                    ExecuteChainExplosion();
                }
            }
        }

        private void HitTarget(Entity target)
        {
            _hasExploded = true;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // Вычисляем урон осколка через множитель
            float dmg = _skillData != null ? (_skillData.chainExplosionDamage * _skillData.shardDamageMultiplier) : 75f;
            
            if (em.Exists(target))
            {
                em.AddComponentData(target, new TakeDamageComponent 
                { 
                    Amount = dmg, 
                    SourceEntity = _shooterEntity
                });
            }
            
            Runner.Despawn(Object);
        }

        private void ExecuteChainExplosion()
        {
            _hasExploded = true;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            // Читаем базовые параметры из ScriptableObject
            float explosionRadius = _skillData != null ? _skillData.chainExplosionRadius : 4f;
            float damage = _skillData != null ? _skillData.chainExplosionDamage : 150f;
            int chainTargetsCount = _skillData != null ? _skillData.chainTargetsCount : 3;
            float shardSearchRad = _skillData != null ? _skillData.shardSearchRadius : 8f;
            
            // === МЕХАНИКА: МОГУЧАЯ ЦЕПЬ (Чтение из ECS) ===
            if (_shooterEntity != Entity.Null && em.Exists(_shooterEntity) && em.HasComponent<SkillConfigComponent>(_shooterEntity))
            {
                var config = em.GetComponentData<SkillConfigComponent>(_shooterEntity);
                
                // Проверяем, купил ли игрок бафф (значение в конфиге стало больше стартовой базы)
                if (config.ChainTargetsCount > chainTargetsCount)
                {
                    Debug.Log($"<color=#00FFFF>[МОГУЧАЯ ЦЕПЬ]</color> Цепная реакция усилена! Осколков будет: {config.ChainTargetsCount}");
                }
                
                // Берем максимальное значение для подстраховки: либо стартовую базу, либо прокачанную базу из конфига
                chainTargetsCount = math.max(chainTargetsCount, config.ChainTargetsCount);
            }

            float3 myPos = transform.position;

            var enemyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyTagComponent>());
            var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            List<Entity> explodedEnemies = new List<Entity>();

            // 1. АОЕ ВЗРЫВ ЭПИЦЕНТРА
            for (int i = 0; i < enemyEntities.Length; i++)
            {
                float dist = math.distance(myPos, enemyTransforms[i].Position);
                if (dist <= explosionRadius)
                {
                    em.AddComponentData(enemyEntities[i], new TakeDamageComponent 
                    { 
                        Amount = damage, 
                        SourceEntity = _shooterEntity
                    });
                    explodedEnemies.Add(enemyEntities[i]);
                }
            }

            // 2. ПОИСК ЦЕЛЕЙ И ВЫЛЕТ САМОНАВОДЯЩИХСЯ ШИПОВ
            float chainMaxDistance = explosionRadius + shardSearchRad; 
            var chainCandidates = new List<(Entity entity, float dist)>();

            for (int i = 0; i < enemyEntities.Length; i++)
            {
                Entity enemy = enemyEntities[i];
                if (explodedEnemies.Contains(enemy)) continue;

                float dist = math.distance(myPos, enemyTransforms[i].Position);
                if (dist <= chainMaxDistance)
                {
                    chainCandidates.Add((enemy, dist));
                }
            }

            chainCandidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            int spawnedShards = 0;
            int candidateIndex = 0;

            // Спавним осколки ТОЛЬКО если есть хотя бы 1 цель
            if (chainCandidates.Count > 0)
            {
                // Крутим цикл, пока не выстрелим ВСЕ доступные осколки
                while (spawnedShards < chainTargetsCount)
                {
                    // Используем остаток от деления (%), чтобы идти по кругу списка врагов
                    var candidate = chainCandidates[candidateIndex % chainCandidates.Count];

                    if (_skillData != null)
                    {
                        Runner.Spawn(_skillData.iceProjectilePrefab, transform.position, Quaternion.identity, Object.InputAuthority, (runner, obj) =>
                        {
                            var shardBridge = obj.GetComponent<IceProjectileNetworkBridge>();
                            if (shardBridge != null)
                            {
                                shardBridge.InitializeShard(Object.InputAuthority, _skillData, _shooterEntity, candidate.entity);
                            }
                        });
                    }

                    spawnedShards++;
                    candidateIndex++;
                }
            }
            else
            {
                Debug.Log($"<color=#87CEFA>[МОГУЧАЯ ЦЕПЬ]</color> Нет целей для отскока. Осколки не выпущены.");
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();

            Runner.Despawn(Object);
        }
    }
}