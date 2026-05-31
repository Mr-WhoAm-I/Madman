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
    [RequireComponent(typeof(Health))]
    public class CloneNetworkBridge : NetworkBehaviour
    {
        public static readonly List<CloneNetworkBridge> ActiveClones = new();

        [Networked] public PlayerRef OwnerPlayer { get; set; }
        [Networked] private Vector2 NetworkRunDirection { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }
        [Networked] private NetworkBool IsMiniClone { get; set; } // Флаг для мини-версий

        private Entity _cloneEntity;
        private EntityManager _entityManager;
        private SchizoidSkillData _skillData;
        private Health _healthComponent;
        
        private float _moveSpeed = 4.5f;
        private Vector2 _initialDirection; 
        public Entity CloneEntity => _cloneEntity;
        // ИСПРАВЛЕНО: Добавлен параметр isMini
        public void Initialize(PlayerRef owner, SchizoidSkillData data, float2 runDirection, bool isMini = false)
        {
            OwnerPlayer = owner;
            _skillData = data;
            _initialDirection = new Vector2(runDirection.x, runDirection.y);
            IsMiniClone = isMini;
            
            if (data != null)
            {
                _moveSpeed = data.cloneMoveSpeed;
            }
        }

        // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Чтение конфига владельца клона ---
        private bool GetOwnerConfig(out SkillConfigComponent config, out Entity ownerEntity)
        {
            config = default;
            ownerEntity = Entity.Null;
            if (_entityManager == default) return false;
            
            var query = _entityManager.CreateEntityQuery(typeof(PlayerOwnerComponent), typeof(SkillConfigComponent));
            using var owners = query.ToComponentDataArray<PlayerOwnerComponent>(Allocator.Temp);
            using var configs = query.ToComponentDataArray<SkillConfigComponent>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i].Player == OwnerPlayer)
                {
                    config = configs[i];
                    ownerEntity = entities[i];
                    return true;
                }
            }
            return false;
        }

        public override void Spawned()
        {
            ActiveClones.Add(this);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _healthComponent = GetComponent<Health>();

            var radius = _skillData != null ? _skillData.effectRadius : 6f;
            var duration = _skillData != null ? _skillData.cloneDuration : 4f;

            if (HasStateAuthority && _skillData != null)
            {
                // Мини-клоны в 2 раза слабее и живут в 2 раза меньше
                float hpMult = IsMiniClone ? 0.25f : 0.5f;
                _healthComponent.MaxHealth = _skillData.cloneExplosionDamage * hpMult;
                _healthComponent.CurrentHealth = _healthComponent.MaxHealth;
                LifeTimer = TickTimer.CreateFromSeconds(Runner, IsMiniClone ? duration * 0.5f : duration);

                NetworkRunDirection = _initialDirection;
            }

            // Визуальное уменьшение для мини-клонов
            if (IsMiniClone)
            {
                transform.localScale = Vector3.one * 0.5f;
                radius *= 0.5f; 
            }

            _cloneEntity = _entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(TargetableComponent),
                typeof(TauntComponent)
            );

            _entityManager.SetComponentData(_cloneEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_cloneEntity, new TargetableComponent { Priority = 6.0f });
            
            _entityManager.SetComponentData(_cloneEntity, new TauntComponent 
            { 
                Radius = radius, 
                TimeRemaining = IsMiniClone ? duration * 0.5f : duration 
            });

            _entityManager.AddComponentData(_cloneEntity, new HealthLinkComponent { Value = _healthComponent });
            bool hasConfig = GetOwnerConfig(out var config, out var ownerEntity);
            _entityManager.AddComponentData(_cloneEntity, new CloneComponent { OwnerEntity = hasConfig ? ownerEntity : Entity.Null });
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (LifeTimer.Expired(Runner) || _healthComponent == null || _healthComponent.IsDead)
                {
                    ExecuteExplosion();
                    Runner.Despawn(Object);
                    return;
                }

                var translation = new Vector3(NetworkRunDirection.x, NetworkRunDirection.y, 0f) * _moveSpeed * Runner.DeltaTime;
                transform.position += translation;
            }

            if (!_entityManager.Exists(_cloneEntity)) return;
            _entityManager.SetComponentData(_cloneEntity, LocalTransform.FromPosition(transform.position));
        }

        private void ExecuteExplosion()
        {
            if (!HasStateAuthority) return;

            bool hasConfig = GetOwnerConfig(out var config, out var ownerEntity);

            var explosionRadius = _skillData != null ? _skillData.cloneExplosionRadius : 3f;
            var explosionDamage = _skillData != null ? _skillData.cloneExplosionDamage : 150f;

            if (hasConfig)
            {
                // === МЕХАНИКА: МНОЖИТЕЛЬ РАДИУСА ===
                if (config.CloneRadiusMult > 0f) explosionRadius *= config.CloneRadiusMult;
            }

            // У мини-клонов радиус и урон меньше
            if (IsMiniClone)
            {
                explosionRadius *= 0.5f;
                explosionDamage *= 0.5f;
            }

            float3 myPos = transform.position;

            var enemyQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyTagComponent>()
            );

            var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            
            int hitCount = 0;

            for (var i = 0; i < enemyEntities.Length; i++)
            {
                if (math.distance(myPos, enemyTransforms[i].Position) <= explosionRadius)
                {
                    // ВАЖНО: Передаем SourceEntity, чтобы работал перк Кровавая жатва
                    _entityManager.AddComponentData(enemyEntities[i], new TakeDamageComponent 
                    { 
                        Amount = explosionDamage,
                        SourceEntity = ownerEntity != Entity.Null ? ownerEntity : Entity.Null
                    });
                    hitCount++;
                }
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();

            Debug.Log($"<color=#00FFFF>[КЛОН]</color> Взрыв! Радиус: {explosionRadius}. Урон: {explosionDamage}. Задето: {hitCount}");

            // Спавн облака и мини-клонов доступен ТОЛЬКО основному клону
            if (hasConfig && !IsMiniClone)
            {
                // === МЕХАНИКА: ТОКСИЧНАЯ ЛИЧНОСТЬ ===
                if (config.CloneToxicCloudDPS > 0f)
                {
                    var cloudEntity = _entityManager.CreateEntity(typeof(LocalTransform), typeof(ToxicCloudComponent));
                    _entityManager.SetComponentData(cloudEntity, LocalTransform.FromPosition(myPos));
                    _entityManager.SetComponentData(cloudEntity, new ToxicCloudComponent
                    {
                        DPS = config.CloneToxicCloudDPS,
                        Radius = explosionRadius,
                        LifeTime = 5f, // Облако висит 5 секунд
                        OwnerEntity = ownerEntity != Entity.Null ? ownerEntity : Entity.Null
                    });
                    
                    Debug.Log($"<color=#32CD32>[ТОКСИЧНАЯ ЛИЧНОСТЬ]</color> Оставлено ядовитое облако (DPS: {config.CloneToxicCloudDPS})!");
                }

                // === МЕХАНИКА: МИНИ-КЛОНЫ ===
                if (config.MiniClones > 0)
                {
                    Debug.Log($"<color=#00FFFF>[МИНИ-КЛОНЫ]</color> Разделение на {config.MiniClones} осколка!");
                    float angleStep = 360f / config.MiniClones;
                    for (int i = 0; i < config.MiniClones; i++)
                    {
                        float angle = i * angleStep;
                        Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                        // Спавним новый префаб и передаем ему направление и флаг IsMiniClone = true
                        Runner.Spawn(_skillData.clonePrefab, transform.position, Quaternion.identity, OwnerPlayer, (runner, obj) =>
                        {
                            var cloneBridge = obj.GetComponent<CloneNetworkBridge>();
                            if (cloneBridge != null)
                            {
                                cloneBridge.Initialize(OwnerPlayer, _skillData, dir, true);
                            }
                        });
                    }
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActiveClones.Remove(this);
            if (_entityManager != default && _entityManager.Exists(_cloneEntity))
            {
                _entityManager.DestroyEntity(_cloneEntity);
            }
        }

        private void OnDestroy()
        {
            if (ActiveClones.Contains(this))
            {
                ActiveClones.Remove(this);
            }
        }
    }
}