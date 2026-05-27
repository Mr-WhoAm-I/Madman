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
            
            // 1. ДОБАВЛЯЕМ typeof(SkillStateComponent) В СПИСОК
            _playerEntity = _entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementComponent),
                typeof(LocalTransform),
                typeof(PlayerTransformSync),
                typeof(SkillStateComponent) // <--- ВОТ СЮДА
            );

            _entityManager.SetComponentData(_playerEntity, new PlayerMovementComponent { MoveSpeed = 5f });
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(transform.position));
            _entityManager.SetComponentData(_playerEntity, new PlayerTransformSync { Value = transform });
            
            // 2. ИНИЦИАЛИЗИРУЕМ ЗНАЧЕНИЯ (перенесли логику из PlayerAuthoring)
            _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
            { 
                MaxCooldown = 5f, 
                CurrentCooldown = 0f, 
                MaxCharges = 1, 
                CurrentCharges = 1 // Со старта игры скилл готов к использованию
            });

            _entityManager.AddComponentData(_playerEntity, new PlayerOwnerComponent { Player = Object.InputAuthority });
            
            // Если это НАШ локальный игрок
            if (HasInputAuthority)
            {
                LocalPlayer = this;
                ProfileController.Instance.OnArchetypeChanged += HandleLocalArchetypeChanged;
                
                // Отправляем текущий сохраненный профиль
                var mySavedArchetypeID = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
                HandleLocalArchetypeChanged(mySavedArchetypeID);
            }

            // ПРИНУДИТЕЛЬНАЯ ИНИЦИАЛИЗАЦИЯ
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
                // Удаляем тег и на сервере, и на клиенте (чтобы не стрелять вечно)
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

        // Логика конечного автомата (State Machine) на тегах
        private void UpdateArchetypeTag(int archetypeID)
        {
            if (!_entityManager.HasComponent<ArchetypeComponent>(_playerEntity))
                _entityManager.AddComponent<ArchetypeComponent>(_playerEntity);
            _entityManager.SetComponentData(_playerEntity, new ArchetypeComponent { ArchetypeID = archetypeID });
            
            // 1. Очищаем старые теги, чтобы избежать наслоения классов
            _entityManager.RemoveComponent<HystericTag>(_playerEntity);
            _entityManager.RemoveComponent<ParanoiacTag>(_playerEntity);
            _entityManager.RemoveComponent<MelancholicTag>(_playerEntity);
            _entityManager.RemoveComponent<SchizoidTag>(_playerEntity);

            // 2. Вешаем актуальный тег.
            // ВАЖНО: Проверь, какие именно ID (0, 1, 2, 3) соответствуют твоим классам 
            // в массиве ProfileController, и подставь правильные значения!
            switch (archetypeID)
            {
                case 0: // Предположим, 0 - это Истерик
                    _entityManager.AddComponent<HystericTag>(_playerEntity);
                    break;
                case 1: // Предположим, 1 - это Параноик
                    _entityManager.AddComponent<ParanoiacTag>(_playerEntity);
                    
                    // Пример: Параноик может иметь другие базовые настройки кулдауна
                    _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
                    { 
                        MaxCooldown = 8f, // Турель откатывается дольше
                        CurrentCooldown = 0f, 
                        MaxCharges = 1, 
                        CurrentCharges = 1 
                    });
                    break;
                case 2:  // Шизоид
                    _entityManager.AddComponent<SchizoidTag>(_playerEntity);
                    break;
                case 3: // Меланхолик
                    _entityManager.AddComponent<MelancholicTag>(_playerEntity);
                    break;
                default:
                    Debug.LogWarning($"[NetworkBridge] Неизвестный ID архетипа: {archetypeID}");
                    break;
            }
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