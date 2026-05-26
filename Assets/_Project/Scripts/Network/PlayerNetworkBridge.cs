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
                typeof(PlayerTransformSync) 
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = 5f });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_playerEntity, new PlayerTransformSync { Value = transform });

            // 1. Если это НАШ локальный игрок
            if (HasInputAuthority)
            {
                // Подписываемся на смену одежды в гардеробе
                ProfileController.Instance.OnArchetypeChanged += HandleLocalArchetypeChanged;
                
                // Отправляем текущий сохраненный профиль
                int mySavedArchetypeID = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
                HandleLocalArchetypeChanged(mySavedArchetypeID);
            }

            // 2. ПРИНУДИТЕЛЬНАЯ ИНИЦИАЛИЗАЦИЯ (Решает проблему скорости "0" при старте)
            // Применяем статы сразу при спавне, так как ChangeDetector может не поймать начальное значение
            ApplyArchetypeStatsToECS();
        }

        // Вызывается, когда мы локально поменяли класс в UI (или при спавне)
        private void HandleLocalArchetypeChanged(int newID)
        {
            if (HasStateAuthority)
            {
                // Хост имеет право менять Networked переменные напрямую
                NetworkArchetypeID = newID;
            }
            else
            {
                // Клиент должен вежливо попросить сервер (Хоста) сделать это
                Rpc_SetArchetype(newID);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_entityManager.Exists(_playerEntity)) return;

            // --- ОТСЛЕЖИВАНИЕ ИЗМЕНЕНИЙ СТАТОВ ПО СЕТИ ---
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(NetworkArchetypeID))
                {
                    ApplyArchetypeStatsToECS();
                }
            }

            // 1. ROLLBACK-СИНХРОНИЗАЦИЯ
            var ecsTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            ecsTransform.Position = transform.position;
            _entityManager.SetComponentData(_playerEntity, ecsTransform);

            // 2. ИНПУТ
            if (!GetInput(out NetworkInputData data)) return;
            if (!_entityManager.Exists(_playerEntity)) return;
            var inputComponent = _entityManager.GetComponentData<PlayerInputComponent>(_playerEntity);
                    
            inputComponent.PreviousButtons = inputComponent.Buttons;
            inputComponent.MovementInput = data.MovementInput;
            inputComponent.AimDirection = data.AimDirection;
            inputComponent.Buttons = data.Buttons; // Пробрасываем кнопки
                    
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
                Debug.LogWarning($"[NetworkBridge] Ассет архетипа с ID {NetworkArchetypeID} не найден! Проверь массив в ProfileController.");
                return;
            }

            var movementComp = _entityManager.GetComponentData<PlayerMovementComponent>(_playerEntity);
            movementComp.MoveSpeed = archetypeData.moveSpeed; 
            _entityManager.SetComponentData(_playerEntity, movementComp);
            
            Debug.Log($"[NetworkBridge] Применены статы архетипа {archetypeData.archetypeName}. Скорость: {movementComp.MoveSpeed}");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Убираем за собой: отписываемся от события, чтобы избежать утечек памяти
            if (HasInputAuthority && ProfileController.Instance != null)
            {
                ProfileController.Instance.OnArchetypeChanged -= HandleLocalArchetypeChanged;
            }

            if (_entityManager != default && _entityManager.Exists(_playerEntity))
            {
                _entityManager.DestroyEntity(_playerEntity);
            }
        }
        
        // Возвращает процент отката от 0 до 1 (удобно для Image.fillAmount в Unity UI)
        public float GetSkillCooldownPercentage()
        {
            if (!_entityManager.Exists(_playerEntity)) return 0f; // 0 значит скилл готов, затемнение не нужно
            var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                
            // Если скилл в откате (зарядов нет), считаем процент
            if (skillState.CurrentCharges < skillState.MaxCharges && skillState.MaxCooldown > 0)
            {
                return skillState.CurrentCooldown / skillState.MaxCooldown;
            }
            return 0f; // 0 значит скилл готов, затемнение не нужно
        }

        // Возвращает количество зарядов (удобно, чтобы написать циферку "2" на иконке у Параноика)
        public int GetSkillCharges()
        {
            return _entityManager.Exists(_playerEntity) ? _entityManager.GetComponentData<SkillStateComponent>(_playerEntity).CurrentCharges : 0;
        }
    }
}