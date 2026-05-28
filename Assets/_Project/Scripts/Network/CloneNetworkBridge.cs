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
    [RequireComponent(typeof(Health))]
    public class CloneNetworkBridge : NetworkBehaviour
    {
        public static readonly List<CloneNetworkBridge> ActiveClones = new List<CloneNetworkBridge>();

        [Networked] public PlayerRef OwnerPlayer { get; set; }
        [Networked] private Vector2 NetworkRunDirection { get; set; }
        [Networked] private TickTimer LifeTimer { get; set; }

        private Entity _cloneEntity;
        private EntityManager _entityManager;
        private SchizoidSkillData _skillData;
        private Health _healthComponent;
        
        private float _moveSpeed = 4.5f;
        private Vector2 _initialDirection; // Буфер для передачи вектора из инициализатора в Spawned

        // ИСПРАВЛЕНО: Сигнатура метода теперь принимает вектор направления джойстика
        public void Initialize(PlayerRef owner, SchizoidSkillData data, float2 runDirection)
        {
            OwnerPlayer = owner;
            _skillData = data;
            _initialDirection = new Vector2(runDirection.x, runDirection.y);
            if (data != null)
            {
                _moveSpeed = data.cloneMoveSpeed;
            }
        }

        public override void Spawned()
        {
            ActiveClones.Add(this);
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _healthComponent = GetComponent<Health>();

            float radius = _skillData != null ? _skillData.effectRadius : 6f;
            float duration = _skillData != null ? _skillData.cloneDuration : 4f;

            if (HasStateAuthority && _skillData != null)
            {
                _healthComponent.MaxHealth = _skillData.cloneExplosionDamage * 0.5f;
                _healthComponent.CurrentHealth = _healthComponent.MaxHealth;
                LifeTimer = TickTimer.CreateFromSeconds(Runner, duration);

                // ИСПРАВЛЕНО: Синхронизируем в сеть точное направление, переданное от игрока
                NetworkRunDirection = _initialDirection;
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
                TimeRemaining = duration 
            });

            _entityManager.AddComponentData(_cloneEntity, new HealthLinkComponent { Value = _healthComponent });
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

                Vector3 translation = new Vector3(NetworkRunDirection.x, NetworkRunDirection.y, 0f) * _moveSpeed * Runner.DeltaTime;
                transform.position += translation;
            }

            if (!_entityManager.Exists(_cloneEntity)) return;
            _entityManager.SetComponentData(_cloneEntity, LocalTransform.FromPosition(transform.position));
        }

        private void ExecuteExplosion()
        {
            if (!HasStateAuthority) return;

            float explosionRadius = _skillData != null ? _skillData.cloneExplosionRadius : 3f;
            float explosionDamage = _skillData != null ? _skillData.cloneExplosionDamage : 150f;
            float3 myPos = transform.position;

            var enemyQuery = _entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyTagComponent>()
            );

            var enemyEntities = enemyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < enemyEntities.Length; i++)
            {
                if (math.distance(myPos, enemyTransforms[i].Position) <= explosionRadius)
                {
                    _entityManager.AddComponentData(enemyEntities[i], new TakeDamageComponent { Amount = explosionDamage });
                }
            }

            enemyEntities.Dispose();
            enemyTransforms.Dispose();

            Debug.Log($"[CloneNetworkBridge] Клон Шизоида взорвался! Нанесено {explosionDamage} урона в радиусе {explosionRadius}");
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