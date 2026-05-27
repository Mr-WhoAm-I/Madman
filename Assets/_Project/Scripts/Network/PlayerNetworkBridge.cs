using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.Network
{
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Networked] public int NetworkArchetypeID { get; set; }

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
                typeof(SkillStateComponent) 
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = 5f });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_playerEntity, new PlayerTransformSync { Value = transform });
            
            // МЫ УБРАЛИ ОТСЮДА ХАРДКОД SkillStateComponent! 
            // Он теперь задается динамически внутри ApplyArchetypeStatsToECS() -> UpdateArchetypeTag()

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

            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            ecsTransform.Position = transform.position;
            _entityManager.SetComponentData(_playerEntity, ecsTransform);

            if (!GetInput(out NetworkInputData data)) return;
            if (!_entityManager.Exists(_playerEntity)) return;
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
                        weapon.ShootTornado360();
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

            // --- НОВОЕ: ЧИТАЕМ КУЛДАУН ИЗ SCRIPTABLE OBJECT ---
            var archetypeData = ProfileController.Instance.GetArchetypeAsset(archetypeID);
            float skillCooldown = archetypeData != null && archetypeData.activeSkillData != null 
                ? archetypeData.activeSkillData.cooldown 
                : 5f; // Значение по умолчанию, если навык не назначен

            // Устанавливаем SkillStateComponent динамически для любого класса
            _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
            { 
                MaxCooldown = skillCooldown, 
                CurrentCooldown = 0f, 
                MaxCharges = 1, 
                CurrentCharges = 1 
            });
            // ---------------------------------------------------
            
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
                    // Хардкод 8f удален! Теперь кулдаун берется из CerberusTurretData.asset
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
            if (!_entityManager.Exists(_playerEntity)) return 0f;
            var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                
            if (skillState.CurrentCharges < skillState.MaxCharges && skillState.MaxCooldown > 0)
            {
                return skillState.CurrentCooldown / skillState.MaxCooldown;
            }
            return 0f;
        }

        public int GetSkillCharges()
        {
            return _entityManager.Exists(_playerEntity) ? _entityManager.GetComponentData<SkillStateComponent>(_playerEntity).CurrentCharges : 0;
        }
    }
}