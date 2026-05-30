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
        private float _lifeTime; // Время жизни, переданное от Параноика
        
        [Networked] private TickTimer FireTimer { get; set; }
        [Networked] private TickTimer TauntTimer { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; } // ДОБАВЛЕНО: Сетевой таймер жизни

        public void Initialize(PlayerRef owner, TurretSkillData data, float lifeTime)
        {
            OwnerPlayer = owner;
            _skillData = data;
            _lifeTime = lifeTime;
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
                
                // Запускаем таймер самоуничтожения
                LifeTimer = TickTimer.CreateFromSeconds(Runner, _lifeTime);
            }

            _turretEntity = _entityManager.CreateEntity(
                typeof(LocalTransform),
                typeof(TargetableComponent),
                typeof(TauntComponent)
            );

            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));
            
            var initialPriority = IsTaunting ? 5.0f : 1.0f;
            _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = initialPriority });

            _entityManager.AddComponentData(_turretEntity, new HealthLinkComponent { Value = _healthComponent });
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                var isOwnerAlive = false;
                for (var i = PlayerManager.AllActivePlayers.Count - 1; i >= 0; i--)
                {
                    var p = PlayerManager.AllActivePlayers[i];
                    if (p != null && p.Object != null && p.Object.InputAuthority == OwnerPlayer)
                    {
                        isOwnerAlive = true;
                        break;
                    }
                }
                
                // УСЛОВИЕ УНИЧТОЖЕНИЯ: Владелец мертв, ХП кончилось ИЛИ вышло время жизни!
                if (!isOwnerAlive || _healthComponent == null || _healthComponent.IsDead || LifeTimer.Expired(Runner))
                {
                    ExplodeAndDestroy();
                    return;
                }

                if (IsTaunting && TauntTimer.Expired(Runner))
                {
                    IsTaunting = false; 
                }
            }

            if (!_entityManager.Exists(_turretEntity)) return;
            
            _entityManager.SetComponentData(_turretEntity, LocalTransform.FromPosition(transform.position));

            var currentPriority = IsTaunting ? 5.0f : 1.0f;
            _entityManager.SetComponentData(_turretEntity, new TargetableComponent { Priority = currentPriority });

            var currentRadius = IsTaunting ? (_skillData != null ? _skillData.attackRadius : 10f) : 0f;
            _entityManager.SetComponentData(_turretEntity, new TauntComponent 
            { 
                Radius = currentRadius, 
                TimeRemaining = 0f 
            });

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

        private void ExplodeAndDestroy()
        {
            // ЗАДЕЛ ПОД ПЕРК "Взрывной реактор"
            // В будущем мы здесь сделаем ECS-запрос (как у Меланхолика) и нанесем АоЕ урон.
            Debug.Log("<color=#FF4500>[ТУРЕЛЬ]</color> Самоуничтожение (кончилось ХП или время)!");
            
            Runner.Despawn(Object);
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
            if (ActiveTurrets.Contains(this))
            {
                ActiveTurrets.Remove(this);
            }
        }
    }
}