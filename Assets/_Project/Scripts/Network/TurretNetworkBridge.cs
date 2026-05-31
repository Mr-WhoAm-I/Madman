using System.Collections.Generic;
using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    public class TurretNetworkBridge : NetworkBehaviour
    {
        public static readonly List<TurretNetworkBridge> ActiveTurrets = new List<TurretNetworkBridge>();
        
        [Networked] public PlayerRef OwnerPlayer { get; set; }
        [Networked] public NetworkBool IsTaunting { get; set; } 
        
        private Entity _turretEntity;
        private EntityManager _entityManager;
        private TurretSkillData _skillData;
        private Health _healthComponent;
        private float _lifeTime; 
        
        [Networked] private TickTimer FireTimer { get; set; }
        [Networked] private TickTimer TauntTimer { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }

        private float _healLogTimer; // Чтобы лог лечения не спамил каждый кадр

        public void Initialize(PlayerRef owner, TurretSkillData data, float lifeTime)
        {
            OwnerPlayer = owner;
            _skillData = data;
            _lifeTime = lifeTime;
        }

        private SkillConfigComponent GetOwnerConfig(out Entity ownerEntity)
        {
            ownerEntity = Entity.Null;
            if (_entityManager == default) return default; 
            
            var query = _entityManager.CreateEntityQuery(typeof(PlayerOwnerComponent), typeof(SkillConfigComponent));
            using var owners = query.ToComponentDataArray<PlayerOwnerComponent>(Unity.Collections.Allocator.Temp);
            using var configs = query.ToComponentDataArray<SkillConfigComponent>(Unity.Collections.Allocator.Temp);
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i].Player == OwnerPlayer)
                {
                    ownerEntity = entities[i];
                    return configs[i];
                }
            }
            return default;
        }

        public override void Spawned()
        {
            ActiveTurrets.Add(this);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _healthComponent = GetComponent<Health>();

            var config = GetOwnerConfig(out Entity ownerEntity);
            
            // Защита от дефолтного 0
            int maxTurrets = config.MaxTurrets <= 0 ? 1 : config.MaxTurrets; 
            
            Debug.Log($"<color=#FFA500>[ТУРЕЛЬ]</color> Спавн. Лимит владельца: {maxTurrets}. Базовое ХП: {_skillData.baseHealth}");

            if (_healthComponent != null && HasStateAuthority && _skillData != null)
            {
                // === ПЕРК: ЗАПАСНЫЕ ДЕТАЛИ (ШТРАФ ЗДОРОВЬЯ) ===
                float healthMultiplier = 1.0f;
                if (maxTurrets > 1)
                {
                    healthMultiplier = _skillData.sparePartsHealthMult;
                    Debug.Log($"<color=#FFA500>[ЗАПАСНЫЕ ДЕТАЛИ]</color> Сработал штраф прочности. Множитель ХП: {healthMultiplier}");
                }
                
                _healthComponent.MaxHealth = _skillData.baseHealth * healthMultiplier; 
                _healthComponent.CurrentHealth = _healthComponent.MaxHealth;
                
                IsTaunting = true; 
                TauntTimer = TickTimer.CreateFromSeconds(Runner, _skillData.tauntDuration);
                FireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
                LifeTimer = TickTimer.CreateFromSeconds(Runner, _lifeTime);

                // === ПЕРК: ЗАПАСНЫЕ ДЕТАЛИ (КОНТРОЛЬ ЛИМИТА) ===
                EnforceTurretLimit(maxTurrets);
            }

            _turretEntity = _entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(TargetableComponent),
                typeof(TauntComponent),
                typeof(TurretComponent)
            );

            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_turretEntity, new TurretComponent 
            { 
                OwnerEntity = ownerEntity,
                CryoMultiplier = _skillData.cryoSlowMultiplier,
                CryoDuration = _skillData.cryoDuration
            });
            
            var initialPriority = IsTaunting ? 5.0f : 1.0f;
            _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = initialPriority });
            _entityManager.AddComponentData(_turretEntity, new HealthLinkComponent { Value = _healthComponent });
        }

        private void EnforceTurretLimit(int maxAllowed)
        {
            List<TurretNetworkBridge> myTurrets = new List<TurretNetworkBridge>();
            foreach (var t in ActiveTurrets)
            {
                if (t.OwnerPlayer == OwnerPlayer) myTurrets.Add(t);
            }

            Debug.Log($"<color=#FFA500>[ТУРЕЛЬ]</color> Контроль лимита: на арене {myTurrets.Count} турелей из {maxAllowed}.");

            if (myTurrets.Count > maxAllowed)
            {
                Debug.Log($"<color=#FF0000>[ЗАПАСНЫЕ ДЕТАЛИ]</color> Превышен лимит! Детонируем самую старую турель.");
                if (myTurrets[0] != this) 
                {
                    myTurrets[0].ExplodeAndDestroy();
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            var config = GetOwnerConfig(out var ownerEntity);

            if (HasStateAuthority)
            {
                var isOwnerAlive = false;
                for (var i = PlayerManager.AllActivePlayers.Count - 1; i >= 0; i--)
                {
                    var p = PlayerManager.AllActivePlayers[i];
                    if (p != null && p.Object != null && p.Object.InputAuthority == OwnerPlayer)
                    {
                        isOwnerAlive = true;
                        
                        // === ПЕРК: ПОЛЕВОЙ МЕДИК ===
                        if (config.TurretHealAura > 0f)
                        {
                            if (Vector3.Distance(transform.position, p.transform.position) <= _skillData.healAuraRadius)
                            {
                                p.GetComponent<Health>().Heal(config.TurretHealAura * Runner.DeltaTime);
                                
                                // Логируем лечение раз в секунду
                                _healLogTimer += Runner.DeltaTime;
                                if (_healLogTimer >= 1f)
                                {
                                    Debug.Log($"<color=#32CD32>[ПОЛЕВОЙ МЕДИК]</color> Аура восстанавливает {config.TurretHealAura} ХП/сек союзникам!");
                                    _healLogTimer = 0f;
                                }
                            }
                        }
                        break;
                    }
                }
                
                if (!isOwnerAlive || _healthComponent == null || _healthComponent.IsDead || LifeTimer.Expired(Runner))
                {
                    ExplodeAndDestroy();
                    return;
                }

                if (IsTaunting && TauntTimer.Expired(Runner)) IsTaunting = false; 
            }

            if (!_entityManager.Exists(_turretEntity)) return;
            
            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = IsTaunting ? 5.0f : 1.0f });
            _entityManager.SetComponentData(_turretEntity, new TauntComponent { Radius = IsTaunting ? (_skillData != null ? _skillData.attackRadius : 10f) : 0f, TimeRemaining = 0f });

            UpdateShooting(ownerEntity, config);
        }

        private void UpdateShooting(Entity ownerEntity, SkillConfigComponent config)
        {
            if (!HasStateAuthority || _skillData == null || !_skillData.bulletPrefab.IsValid) return;

            if (FireTimer.Expired(Runner))
            {
                var targetDir = FindClosestEnemyDirection();
                if (targetDir != Vector2.zero)
                {
                    if (config.TurretCryo)
                    {
                        Debug.Log($"<color=#00FFFF>[КРИО-СНАРЯДЫ]</color> Выстрел с эффектом заморозки!");
                    }

                    Runner.Spawn(_skillData.bulletPrefab, transform.position, Quaternion.LookRotation(Vector3.forward, targetDir), OwnerPlayer, (runner, obj) =>
                    {
                        var bullet = obj.GetComponent<BulletNetworkMovement>();
                        if (bullet != null)
                        {
                            bullet.InitNetworkState(_skillData.bulletLifeTime, _skillData.bulletDamage, _skillData.bulletSpeed, _turretEntity); 
                        }
                    });
                    
                    FireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
                }
            }
        }

        private Vector2 FindClosestEnemyDirection()
        {
            var swarmManager = EnemySwarmManager.Instance;
            if (swarmManager == null || swarmManager.Object == null) return Vector2.zero;

            Vector2 myPos = transform.position;
            var MyClosestDir = Vector2.zero;
            var closestDist = float.MaxValue;

            for (var i = 0; i < swarmManager.EnemyStates.Length; i++)
            {
                var enemy = swarmManager.EnemyStates[i];
                if (!enemy.IsActive) continue;

                var dist = Vector2.Distance(myPos, enemy.Position);
                if (dist <= _skillData.attackRadius && dist < closestDist)
                {
                    closestDist = dist;
                    MyClosestDir = (enemy.Position - myPos).normalized;
                }
            }
            return MyClosestDir;
        }

        public void ExplodeAndDestroy()
        {
            var config = GetOwnerConfig(out var ownerEntity);

            // === ПЕРК: ВЗРЫВНОЙ РЕАКТОР ===
            if (config.TurretExplode && HasStateAuthority)
            {
                float explosionRadius = _skillData.explosionRadius;
                float explosionDamage = _skillData.explosionDamage;

                var query = _entityManager.CreateEntityQuery(typeof(EnemyHealthComponent), typeof(LocalTransform));
                using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                using var transforms = query.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
                
                int hitCount = 0;
                for(int i = 0; i < entities.Length; i++) 
                {
                    if (math.distance(transforms[i].Position, transform.position) <= explosionRadius) 
                    {
                        _entityManager.AddComponentData(entities[i], new TakeDamageComponent 
                        { 
                            Amount = explosionDamage, 
                            SourceEntity = ownerEntity 
                        });
                        hitCount++;
                    }
                }
                
                Debug.Log($"<color=#FF4500>[ВЗРЫВНОЙ РЕАКТОР]</color> Детонация турели! Задето врагов: {hitCount}. Урон: {explosionDamage}");
            }
            
            if (HasStateAuthority) Runner.Despawn(Object);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActiveTurrets.Remove(this);
            if (_entityManager != default && _entityManager.Exists(_turretEntity))
            {
                _entityManager.DestroyEntity(_turretEntity);
            }
        }

        private void OnDestroy()
        {
            if (ActiveTurrets.Contains(this)) ActiveTurrets.Remove(this);
        }
    }
}