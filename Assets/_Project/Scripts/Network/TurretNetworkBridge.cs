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

        [Networked] public float Health { get; set; }
        [Networked] public float MaxHealth { get; set; }
        [Networked] public PlayerRef OwnerPlayer { get; set; }
        [Networked] public NetworkBool IsTaunting { get; set; } 
        
        private Entity _turretEntity;
        private EntityManager _entityManager;
        private TurretSkillData _skillData;
        
        [Networked] private TickTimer _fireTimer { get; set; }
        [Networked] private TickTimer _tauntTimer { get; set; }

        public void Initialize(PlayerRef owner, TurretSkillData data)
        {
            OwnerPlayer = owner;
            _skillData = data;
        }

        public override void Spawned()
        {
            ActiveTurrets.Add(this);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (HasStateAuthority && _skillData != null)
            {
                MaxHealth = _skillData.baseHealth; 
                Health = MaxHealth;
                IsTaunting = true; 
                _tauntTimer = TickTimer.CreateFromSeconds(Runner, _skillData.tauntDuration);
                _fireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
            }

            _turretEntity = _entityManager.CreateEntity(
                typeof(TargetableTag),
                typeof(LocalTransform)
            );

            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));
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
                
                if (!isOwnerAlive || Health <= 0)
                {
                    Runner.Despawn(Object);
                    return;
                }

                // 2. ОБНОВЛЕНИЕ АГРО
                if (IsTaunting && _tauntTimer.Expired(Runner))
                {
                    IsTaunting = false; 
                }
            }

            if (!_entityManager.Exists(_turretEntity)) return;
            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));

            UpdateShooting();
        }

        private void UpdateShooting()
        {
            if (!HasStateAuthority || _skillData == null || !_skillData.bulletPrefab.IsValid) return;

            if (_fireTimer.Expired(Runner))
            {
                var targetDir = FindClosestEnemyDirection();
                if (targetDir != Vector2.zero)
                {
                    Runner.Spawn(_skillData.bulletPrefab, transform.position, Quaternion.LookRotation(Vector3.forward, targetDir), OwnerPlayer);
                    _fireTimer = TickTimer.CreateFromSeconds(Runner, 1f / _skillData.fireRate);
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