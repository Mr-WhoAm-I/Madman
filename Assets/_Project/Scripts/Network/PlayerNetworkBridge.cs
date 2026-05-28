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
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Networked] public int NetworkArchetypeID { get; set; }
        
        // --- ПЕРЕМЕННЫЕ ДЛЯ СИНХРОНИЗАЦИИ КУЛДАУНА ---
        [Networked] public float NetworkCurrentCooldown { get; set; }
        [Networked] public float NetworkMaxCooldown { get; set; }
        [Networked] public int NetworkCurrentCharges { get; set; }
        [Networked] public int NetworkMaxCharges { get; set; }

        public static PlayerNetworkBridge LocalPlayer;
        private Entity _playerEntity;
        private EntityManager _entityManager;
        private ChangeDetector _changeDetector;

        public override void Spawned()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            
            _playerEntity = _entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementComponent),
                typeof(LocalTransform),
                typeof(PlayerTransformSync),
                typeof(TargetableComponent),
                typeof(SkillStateComponent) 
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = 5f });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_playerEntity, new PlayerTransformSync { Value = transform });
            _entityManager.SetComponentData(_playerEntity, new TargetableComponent { Priority = 1.0f });
            _entityManager.AddComponentData(_playerEntity, new HealthLinkComponent { Value = GetComponent<Health>() });
            _entityManager.AddComponentData(_playerEntity, new PlayerOwnerComponent { Player = Object.InputAuthority });
            
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

            // --- СИНХРОНИЗАЦИЯ НАВЫКОВ (СЕРВЕР -> КЛИЕНТ) ---
            if (HasStateAuthority)
            {
                // Сервер читает из ECS и отдает в сеть
                var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                NetworkCurrentCooldown = skillState.CurrentCooldown;
                NetworkMaxCooldown = skillState.MaxCooldown;
                NetworkCurrentCharges = skillState.CurrentCharges;
                NetworkMaxCharges = skillState.MaxCharges;
            }
            else
            {
                // Клиент покорно принимает актуальные таймеры сервера
                var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                skillState.CurrentCooldown = NetworkCurrentCooldown;
                skillState.MaxCooldown = NetworkMaxCooldown;
                skillState.CurrentCharges = NetworkCurrentCharges;
                skillState.MaxCharges = NetworkMaxCharges;
                _entityManager.SetComponentData(_playerEntity, skillState);
            }

            // --- ФИЗИКА И ИНПУТ ---
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
            
            if (_entityManager.HasComponent<Trigger360ShootTag>(_playerEntity))
            {
                if (HasStateAuthority)
                {
                    var weapon = GetComponent<PlayerWeapon>();
                    if (weapon != null)
                    {
                        // Достаем актуальный архетип
                        var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);
                        
                        // Проверяем, что в слоте activeSkillData действительно лежит навык Истерика
                        if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericSkill)
                        {
                            weapon.ShootTornado360(hystericSkill.bulletCount);
                        }
                        else 
                        {
                            // Фолбек на случай, если данные в Инспекторе не настроили
                            weapon.ShootTornado360(8); 
                        }
                    }
                }
                _entityManager.RemoveComponent<Trigger360ShootTag>(_playerEntity);
            }
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

            Debug.Log($"[NetworkBridge] Применены статы архетипа {archetypeData.archetypeName}. Скорость: {movementComp.MoveSpeed}");
        }

        private void UpdateArchetypeTag(int archetypeID)
        {
            if (!_entityManager.HasComponent<ArchetypeComponent>(_playerEntity))
                _entityManager.AddComponent<ArchetypeComponent>(_playerEntity);
            _entityManager.SetComponentData(_playerEntity, new ArchetypeComponent { ArchetypeID = archetypeID });

            // Базовые параметры навыка выставляем только на сервере
            if (HasStateAuthority)
            {
                var archetypeData = ProfileController.Instance.GetArchetypeAsset(archetypeID);
                float skillCooldown = archetypeData != null && archetypeData.activeSkillData != null 
                    ? archetypeData.activeSkillData.cooldown 
                    : 5f; 

                _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
                { 
                    MaxCooldown = skillCooldown, 
                    CurrentCooldown = 0f, 
                    MaxCharges = 1, 
                    CurrentCharges = 1 
                });
            }

            _entityManager.RemoveComponent<HystericTag>(_playerEntity);
            _entityManager.RemoveComponent<ParanoiacTag>(_playerEntity);
            _entityManager.RemoveComponent<MelancholicTag>(_playerEntity);
            _entityManager.RemoveComponent<SchizoidTag>(_playerEntity);

            switch (archetypeID)
            {
                case 0:
                    _entityManager.AddComponent<HystericTag>(_playerEntity);
                    break;
                case 1:
                    _entityManager.AddComponent<ParanoiacTag>(_playerEntity);
                    break;
                case 2:
                    _entityManager.AddComponent<SchizoidTag>(_playerEntity);
                    break;
                case 3:
                    _entityManager.AddComponent<MelancholicTag>(_playerEntity);
                    break;
                default:
                    Debug.LogWarning($"[NetworkBridge] Неизвестный ID архетипа: {archetypeID}");
                    break;
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
            // ЗАЩИТА: Проверяем, существует ли еще сетевой объект
            if (!Object || !Object.IsValid) return 0f;

            if (NetworkCurrentCharges < NetworkMaxCharges && NetworkMaxCooldown > 0)
            {
                return NetworkCurrentCooldown / NetworkMaxCooldown;
            }
            return 0f;
        }

        public int GetSkillCharges()
        {
            // ЗАЩИТА
            if (Object == null || !Object.IsValid) return 0;
            
            return NetworkCurrentCharges;
        }
    }
}