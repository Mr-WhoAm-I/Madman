using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    [DefaultExecutionOrder(-10)]
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Networked] public int NetworkArchetypeID { get; set; }
        
        // --- ПЕРЕМЕННЫЕ ДЛЯ СИНХРОНИЗАЦИИ КУЛДАУНА ---
        [Networked] public float NetworkCurrentCooldown { get; set; }
        [Networked] public float NetworkMaxCooldown { get; set; }
        [Networked] public int NetworkCurrentCharges { get; set; }
        [Networked] public int NetworkMaxCharges { get; set; }

        // --- ПЕРЕМЕННЫЕ ДЛЯ СИНХРОНИЗАЦИИ СОСТОЯНИЯ РЫВКА ---
        [Networked] public NetworkBool NetworkIsDashing { get; set; }
        [Networked] public Vector2 NetworkDashDirection { get; set; }
        [Networked] public float NetworkDashTimeLeft { get; set; }
        [Networked] public float NetworkDashSpeed { get; set; }

        public static PlayerNetworkBridge LocalPlayer;
        private Entity _playerEntity;
        private EntityManager _entityManager;
        private ChangeDetector _changeDetector;

        public Entity PlayerEntity => _playerEntity;
        public EntityManager EntityManager => _entityManager;

        public override void Spawned()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            
            _playerEntity = _entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementComponent),
                typeof(LocalTransform),
                typeof(TargetableComponent),
                typeof(SkillStateComponent) 
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = 5f });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.AddComponentData(_playerEntity, new HealthLinkComponent { Value = GetComponent<Health>() });
            _entityManager.AddComponentData(_playerEntity, new PlayerOwnerComponent { Player = Object.InputAuthority });
            
            _entityManager.AddComponentData(_playerEntity, new PlayerBridgeReference { Bridge = this });
            
            if (HasInputAuthority)
            {
                LocalPlayer = this;
                ProfileController.Instance.OnArchetypeChanged += HandleLocalArchetypeChanged;
                
                var mySavedArchetypeID = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
                HandleLocalArchetypeChanged(mySavedArchetypeID);
            }

            ApplyArchetypeStatsToECS();
        }

        private void HandleLocalArchetypeChanged(int newID)
        {
            if (HasStateAuthority)
            {
                NetworkArchetypeID = newID;
            }
            else
            {
                Rpc_SetArchetype(newID);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_entityManager.Exists(_playerEntity)) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(NetworkArchetypeID))
                {
                    ApplyArchetypeStatsToECS();
                }
            }

            // =========================================================================
            // ФАЗА ПУЛЛА: СЕТЬ -> ECS
            // =========================================================================
            
            // ИСПРАВЛЕНИЕ: Пуллим данные в ECS только тогда, когда сеть гарантированно 
            // прислала инициализированные сервером параметры. Защищает от "Нулевого замка".
            if (NetworkMaxCharges > 0)
            {
                var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                skillState.CurrentCooldown = NetworkCurrentCooldown;
                skillState.MaxCooldown = NetworkMaxCooldown;
                skillState.CurrentCharges = NetworkCurrentCharges;
                skillState.MaxCharges = NetworkMaxCharges;
                _entityManager.SetComponentData(_playerEntity, skillState);
            }

            if (NetworkIsDashing)
            {
                if (!_entityManager.HasComponent<DashComponent>(_playerEntity))
                {
                    _entityManager.AddComponent<DashComponent>(_playerEntity);
                }
                
                _entityManager.SetComponentData(_playerEntity, new DashComponent
                {
                    Direction = NetworkDashDirection,
                    Speed = NetworkDashSpeed,
                    TimeLeft = NetworkDashTimeLeft
                });
            }
            else
            {
                if (_entityManager.HasComponent<DashComponent>(_playerEntity))
                {
                    _entityManager.RemoveComponent<DashComponent>(_playerEntity);
                }
            }

            // =========================================================================
            // СБОР КООРДИНАТ И ИНПУТА
            // =========================================================================
            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            ecsTransform.Position = transform.position;
            _entityManager.SetComponentData(_playerEntity, ecsTransform);

            if (!GetInput(out NetworkInputData data)) return;
            var inputComponent = _entityManager.GetComponentData<PlayerInputComponent>(_playerEntity);
                    
            inputComponent.PreviousButtons = inputComponent.Buttons;
            inputComponent.MovementInput = data.MovementInput;
            inputComponent.AimDirection = data.AimDirection;
            inputComponent.Buttons = data.Buttons; 
                    
            _entityManager.SetComponentData(_playerEntity, inputComponent);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void Rpc_SetArchetype(int archetypeID)
        {
            NetworkArchetypeID = archetypeID;
        }

        private void ApplyArchetypeStatsToECS()
        {
            var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);
            
            if (archetypeData == null) 
            {
                Debug.LogWarning($"[NetworkBridge] Ассет архетипа с ID {NetworkArchetypeID} не найден!");
                return;
            }

            var movementComp = _entityManager.GetComponentData<PlayerMovementComponent>(_playerEntity);
            movementComp.MoveSpeed = archetypeData.moveSpeed; 
            _entityManager.SetComponentData(_playerEntity, movementComp);
            
            UpdateArchetypeTag(NetworkArchetypeID);
        }

        private void UpdateArchetypeTag(int archetypeID)
        {
            if (!_entityManager.HasComponent<ArchetypeComponent>(_playerEntity))
                _entityManager.AddComponent<ArchetypeComponent>(_playerEntity);
            _entityManager.SetComponentData(_playerEntity, new ArchetypeComponent { ArchetypeID = archetypeID });

            var archetypeData = ProfileController.Instance.GetArchetypeAsset(archetypeID);
            float skillCooldown = 5f;
            int maxCharges = 1;
            float castDist = 4f;
            float effectRad = 5f;

            if (archetypeData != null && archetypeData.activeSkillData != null) 
            {
                skillCooldown = archetypeData.activeSkillData.cooldown;
                maxCharges = archetypeData.activeSkillData.maxCharges;
                castDist = archetypeData.activeSkillData.castDistance;
                effectRad = archetypeData.activeSkillData.effectRadius;
            }

            // ИСПРАВЛЕНИЕ: Зарождаем истинные данные прямо во Fusion свойствах на Сервере.
            // Это гарантирует, что клиенты получат верные настройки из сети при первом же тике.
            if (HasStateAuthority)
            {
                NetworkMaxCooldown = skillCooldown;
                NetworkCurrentCooldown = 0f;
                NetworkMaxCharges = maxCharges;
                NetworkCurrentCharges = maxCharges;
            }

            // Локальное наполнение ECS для мгновенной готовности
            _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
            { 
                MaxCooldown = skillCooldown, 
                CurrentCooldown = 0f, 
                MaxCharges = maxCharges, 
                CurrentCharges = maxCharges 
            });

            if (!_entityManager.HasComponent<SkillConfigComponent>(_playerEntity))
                _entityManager.AddComponent<SkillConfigComponent>(_playerEntity);
                
            float dashSpd = 0f;
            float dashDur = 0f;
            
            if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericData)
            {
                dashSpd = hystericData.dashSpeed;
                dashDur = hystericData.dashDuration;
            }
                
            _entityManager.SetComponentData(_playerEntity, new SkillConfigComponent
            {
                CastDistance = castDist,
                EffectRadius = effectRad,
                DashSpeed = dashSpd,
                DashDuration = dashDur
            });

            _entityManager.RemoveComponent<HystericTag>(_playerEntity);
            _entityManager.RemoveComponent<ParanoiacTag>(_playerEntity);
            _entityManager.RemoveComponent<MelancholicTag>(_playerEntity);
            _entityManager.RemoveComponent<SchizoidTag>(_playerEntity);

            switch (archetypeID)
            {
                case 0: _entityManager.AddComponent<HystericTag>(_playerEntity); break;
                case 1: _entityManager.AddComponent<ParanoiacTag>(_playerEntity); break;
                case 2: _entityManager.AddComponent<SchizoidTag>(_playerEntity); break;
                case 3: _entityManager.AddComponent<MelancholicTag>(_playerEntity); break;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HasInputAuthority && ProfileController.Instance != null)
            {
                ProfileController.Instance.OnArchetypeChanged -= HandleLocalArchetypeChanged;
            }

            if (_entityManager != default && _entityManager.Exists(_playerEntity))
            {
                _entityManager.DestroyEntity(_playerEntity);
            }
        }
        
        public float GetSkillCooldownPercentage()
        {
            if (!Object || !Object.IsValid) return 0f;

            if (NetworkCurrentCharges < NetworkMaxCharges && NetworkMaxCooldown > 0)
            {
                return NetworkCurrentCooldown / NetworkMaxCooldown;
            }
            return 0f;
        }

        public int GetSkillCharges()
        {
            if (Object == null || !Object.IsValid) return 0;
            return NetworkCurrentCharges;
        }
    }
}