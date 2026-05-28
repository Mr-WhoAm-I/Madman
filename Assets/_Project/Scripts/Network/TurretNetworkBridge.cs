using System.Collections.Generic;
using Fusion;
using Unity.Entities;
using Unity.Transforms;
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
        
        [Networked] private TickTimer FireTimer { get; set; }
        [Networked] private TickTimer TauntTimer { get; set; }

        public void Initialize(PlayerRef owner, TurretSkillData data)
        {
            OwnerPlayer = owner;
            _skillData = data;
        }

        public override void Spawned()
        {
            ActiveTurrets.Add(this);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            _healthComponent = GetComponent<Health>();
            
            if (_healthComponent != null && HasStateAuthority && _skillData != null)
            {
                _healthComponent.MaxHealth = _skillData.baseHealth; 
                _healthComponent.CurrentHealth = _skillData.baseHealth;
                IsTaunting = true; 
                TauntTimer = TickTimer.CreateFromSeconds(Runner, _skillData.tauntDuration);
                FireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
            }

            // СОЗДАЕМ ECS-СУЩНОСТЬ
            _turretEntity = _entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(TargetableComponent) 
            );

            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));
            
            // Если турель сразу при спавне таунтит, даем ей приоритет 5, иначе 1
            float initialPriority = IsTaunting ? 5.0f : 1.0f;
            _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = initialPriority });

            // Передаем ссылку на Health в ECS!
            _entityManager.AddComponentData(_turretEntity, new HealthLinkComponent { Value = _healthComponent });
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                // 1. ЖЕСТКАЯ ПРОВЕРКА ВЛАДЕЛЬЦА (Уничтожение при выходе игрока)
                bool isOwnerAlive = false;
                for (int i = PlayerManager.AllActivePlayers.Count - 1; i >= 0; i--)
                {
                    var p = PlayerManager.AllActivePlayers[i];
                    if (p != null && p.Object != null && p.Object.InputAuthority == OwnerPlayer)
                    {
                        isOwnerAlive = true;
                        break;
                    }
                }
                
                if (!isOwnerAlive || _healthComponent == null || _healthComponent.IsDead)
                {
                    Runner.Despawn(Object);
                    return;
                }

                // 2. ОБНОВЛЕНИЕ АГРО
                
                if (IsTaunting && TauntTimer.Expired(Runner))
                {
                    IsTaunting = false; 
                }
                
                var currentPriority = IsTaunting ? 5.0f : 1.0f;
                _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = currentPriority });
            }

            if (!_entityManager.Exists(_turretEntity)) return;
            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));

            UpdateShooting();
        }

        private void UpdateShooting()
        {
            if (!HasStateAuthority || _skillData == null || !_skillData.bulletPrefab.IsValid) return;

            if (FireTimer.Expired(Runner))
            {
                var targetDir = FindClosestEnemyDirection();
                if (targetDir != Vector2.zero)
                {
                    Runner.Spawn(_skillData.bulletPrefab, transform.position, Quaternion.LookRotation(Vector3.forward, targetDir), OwnerPlayer);
                    FireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
                }
            }
        }

        private Vector2 FindClosestEnemyDirection()
        {
            var swarmManager = EnemySwarmManager.Instance;
            if (swarmManager == null || swarmManager.Object == null) return Vector2.zero;

            Vector2 myPos = transform.position;
            Vector2 closestDir = Vector2.zero;
            float closestDist = float.MaxValue;

            for (int i = 0; i < swarmManager.EnemyStates.Length; i++)
            {
                var enemy = swarmManager.EnemyStates[i];
                if (!enemy.IsActive) continue;

                float dist = Vector2.Distance(myPos, enemy.Position);
                if (dist <= _skillData.attackRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closestDir = (enemy.Position - myPos).normalized;
                }
            }
            return closestDir;
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
            // Подстраховка: если сцена выгружается (переход в Хаб), 
            // вычищаем мертвую турель из статического списка
            if (ActiveTurrets.Contains(this))
            {
                ActiveTurrets.Remove(this);
            }
        }
    }
}