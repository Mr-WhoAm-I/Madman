using System.Collections.Generic;
using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;
using Allocator = Unity.Collections.Allocator;

namespace _Project.Scripts.Network
{
    public class IceProjectileNetworkBridge : NetworkBehaviour
    {
        [Networked] private TickTimer LifeTimer { get; set; }
        
        private MelancholicSkillData _skillData;
        private Entity _shooterEntity;
        private float _speed = 12f;
        private bool _hasExploded = false;

        public void Initialize(PlayerRef owner, MelancholicSkillData data, float2 direction, Entity shooterEntity)
        {
            _skillData = data;
            _shooterEntity = shooterEntity;
        }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                LifeTimer = TickTimer.CreateFromSeconds(Runner, 3.0f); // Снаряд живет максимум 3 секунды
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

            transform.position += transform.up * _speed * Runner.DeltaTime;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!HasStateAuthority || _hasExploded) return;

            if (other.TryGetComponent<Health>(out var health))
            {
                // Игнорируем дружественный огонь
                if (Object.InputAuthority == health.Object.InputAuthority) return;
                
                ExecuteChainExplosion();
            }
        }

        private void ExecuteChainExplosion()
        {
            _hasExploded = true;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            float explosionRadius = _skillData != null ? _skillData.effectRadius : 4f;
            float damage = _skillData != null ? _skillData.chainExplosionDamage : 150f;
            int chainTargetsCount = _skillData != null ? _skillData.chainTargetsCount : 3;
            
            float3 myPos = transform.position;

            // Собираем всех врагов на арене
            var enemyQuery = em.CreateEntityQuery(ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<EnemyTagComponent>());
            var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

            List<Entity> explodedEnemies = new List<Entity>();

            // 1. АОЕ ВЗРЫВ (Эпицентр депрессии)
            for (int i = 0; i < enemyEntities.Length; i++)
            {
                float dist = math.distance(myPos, enemyTransforms[i].Position);
                if (dist <= explosionRadius)
                {
                    em.AddComponentData(enemyEntities[i], new TakeDamageComponent 
                    { 
                        Amount = damage, 
                        SourceEntity = _shooterEntity // Передаем ID игрока для активации замедления!
                    });
                    explodedEnemies.Add(enemyEntities[i]);
                }
            }

            // 2. ЦЕПНАЯ РЕАКЦИЯ (Поиск соседей за пределами взрыва)
            float chainMaxDistance = explosionRadius + 6f; // Насколько далеко от эпицентра бьет цепь
            var chainCandidates = new List<(Entity entity, float dist)>();

            for (int i = 0; i < enemyEntities.Length; i++)
            {
                Entity enemy = enemyEntities[i];
                if (explodedEnemies.Contains(enemy)) continue; // Уже получил урон от эпицентра

                float dist = math.distance(myPos, enemyTransforms[i].Position);
                if (dist <= chainMaxDistance)
                {
                    chainCandidates.Add((enemy, dist));
                }
            }

            // Сортируем кандидатов, чтобы цепь била по ближайшим
            chainCandidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            int chainsApplied = 0;
            foreach (var candidate in chainCandidates)
            {
                if (chainsApplied >= chainTargetsCount) break;

                em.AddComponentData(candidate.entity, new TakeDamageComponent 
                { 
                    Amount = damage * 0.5f, // Шипы наносят 50% от базового урона (для баланса)
                    SourceEntity = _shooterEntity
                });
                chainsApplied++;
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();

            Debug.Log($"<color=#00FFFF>[МЕЛАНХОЛИК]</color> Ульта разорвалась! Эпицентр задел: {explodedEnemies.Count}. Шипы ударили: {chainsApplied} врагов.");

            Runner.Despawn(Object);
        }
    }
}