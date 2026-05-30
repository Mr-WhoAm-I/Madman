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
    // ААА-СТАНДАРТ ДЛЯ FUSION 2: Используем нативный порядок выполнения Unity, 
    // чтобы шлюз всегда выполнял пулл данных в ECS раньше, чем физика бега или оружия начнет их читать.
    [DefaultExecutionOrder(-10)]
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Networked] public int NetworkArchetypeID { get; set; }
        
        // --- ПЕРЕМЕННЫЕ ДЛЯ СИНХРОНИЗАЦИИ КУЛДАУНА ---
        [Networked] public float NetworkCurrentCooldown { get; set; }
        [Networked] public float NetworkMaxCooldown { get; set; }
        [Networked] public int NetworkCurrentCharges { get; set; }
        [Networked] public int NetworkMaxCharges { get; set; }

        // --- ПЕРЕМЕННЫЕ ДЛЯ СИНХРОНИЗАЦИИ СОСТОЯНИЯ РЫВКА (ИСТЕРИК) ---
        [Networked] public NetworkBool NetworkIsDashing { get; set; }
        [Networked] public Vector2 NetworkDashDirection { get; set; }
        [Networked] public float NetworkDashTimeLeft { get; set; }
        [Networked] public float NetworkDashSpeed { get; set; }

        public static PlayerNetworkBridge LocalPlayer;
        private Entity _playerEntity;
        private EntityManager _entityManager;
        private ChangeDetector _changeDetector;
        private SpriteRenderer _spriteRenderer;

        public Entity PlayerEntity => _playerEntity;
        public EntityManager EntityManager => _entityManager;

        public override void Spawned()
        {
            _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            
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
            
            // Инициализируем базовый приоритет игрока равным 1.0f.
            _entityManager.SetComponentData(_playerEntity, new TargetableComponent { Priority = 1.0f });
            
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
            if (NetworkMaxCharges > 0)
            {
                var skillState = _entityManager.GetComponentData<SkillStateComponent>(_playerEntity);
                skillState.CurrentCooldown = NetworkCurrentCooldown;
                skillState.MaxCooldown = NetworkMaxCooldown;
                skillState.CurrentCharges = NetworkCurrentCharges;
                skillState.MaxCharges = NetworkMaxCharges;
                _entityManager.SetComponentData(_playerEntity, skillState);
            }

            var targetable = _entityManager.GetComponentData<TargetableComponent>(_playerEntity);
            if (_entityManager.HasComponent<InvisibilityStateComponent>(_playerEntity))
            {
                targetable.Priority = 0.0f; // Полный инвиз, мобы теряют из виду!
            }
            else
            {
                targetable.Priority = 1.0f; // Обычное состояние, игрока можно атаковать
            }
            _entityManager.SetComponentData(_playerEntity, targetable);
            
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
            // СЕТЕВАЯ ФАБРИКА: ОБРАБОТКА КОМАНДЫ НА СПАВН ТУРЕТИ (ПАРАНОИК)
            // =========================================================================
            if (_entityManager.HasComponent<SpawnTurretCommand>(_playerEntity))
            {
                if (HasStateAuthority)
                {
                    var command = _entityManager.GetComponentData<SpawnTurretCommand>(_playerEntity);
                    var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);
                    
                    // ИСПРАВЛЕНО: Теперь читаем настройки из ParanoiacSkillData
                    if (archetypeData != null && archetypeData.activeSkillData is ParanoiacSkillData paranoiacSkill)
                    {
                        var turretCombat = paranoiacSkill.turretCombatSettings;
                        if (turretCombat != null)
                        {
                            var activeCount = 0;
                            TurretNetworkBridge oldestTurret = null;
                            
                            for (var i = 0; i < TurretNetworkBridge.ActiveTurrets.Count; i++)
                            {
                                if (TurretNetworkBridge.ActiveTurrets[i].OwnerPlayer == Object.InputAuthority)
                                {
                                    activeCount++;
                                    if (oldestTurret == null) 
                                    {
                                        oldestTurret = TurretNetworkBridge.ActiveTurrets[i];
                                    }
                                }
                            }

                            // Читаем лимит турелей из профиля Параноика
                            if (activeCount >= paranoiacSkill.maxTurrets && oldestTurret != null)
                            {
                                Runner.Despawn(oldestTurret.Object);
                            }

                            Runner.Spawn(turretCombat.turretPrefab, command.Position, Quaternion.identity, Object.InputAuthority, (runner, obj) => 
                            {
                                var turretBridge = obj.GetComponent<TurretNetworkBridge>();
                                if (turretBridge != null)
                                {
                                    // Передаем боевые настройки и время жизни турели!
                                    turretBridge.Initialize(Object.InputAuthority, turretCombat, paranoiacSkill.turretLifeTime);
                                }
                            });
                        }
                    }
                }
                
                _entityManager.RemoveComponent<SpawnTurretCommand>(_playerEntity);
            }

            // =========================================================================
            // СЕТЕВАЯ ФАБРИКА: ОБРАБОТКА КОМАНДЫ НА СПАВН КЛОНА (ШИЗОИД) (ДОБАВЛЕНО)
            // =========================================================================
            if (_entityManager.HasComponent<SpawnCloneCommand>(_playerEntity))
            {
                if (HasStateAuthority)
                {
                    var command = _entityManager.GetComponentData<SpawnCloneCommand>(_playerEntity);
                    var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);

                    if (archetypeData != null && archetypeData.activeSkillData is SchizoidSkillData schizoidSkill)
                    {
                        Runner.Spawn(schizoidSkill.clonePrefab, command.SpawnPosition, Quaternion.identity, Object.InputAuthority, (runner, obj) =>
                        {
                            var cloneBridge = obj.GetComponent<CloneNetworkBridge>();
                            if (cloneBridge != null)
                            {
                                // ИСПРАВЛЕНО: Передаем высчитанное направление из инпута прямо в инициализатор моста клона!
                                cloneBridge.Initialize(Object.InputAuthority, schizoidSkill, command.RunDirection);
                            }
                        });
                    }
                }

                _entityManager.RemoveComponent<SpawnCloneCommand>(_playerEntity);
            }
            
            // =========================================================================
            // СЕТЕВАЯ ФАБРИКА: ОБРАБОТКА КОМАНДЫ НА СПАВН УЛЬТЫ МЕЛАНХОЛИКА
            // =========================================================================
            if (_entityManager.HasComponent<SpawnIceProjectileCommand>(_playerEntity))
            {
                if (HasStateAuthority)
                {
                    var command = _entityManager.GetComponentData<SpawnIceProjectileCommand>(_playerEntity);
                    var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);

                    if (archetypeData != null && archetypeData.activeSkillData is MelancholicSkillData melancholicSkill)
                    {
                        // Разворачиваем снаряд по вектору джойстика
                        float angle = Mathf.Atan2(command.CastDirection.y, command.CastDirection.x) * Mathf.Rad2Deg - 90f;
                        Quaternion rotation = Quaternion.Euler(0, 0, angle);

                        // Спавним с небольшим смещением вперед, чтобы не задеть самого себя
                        Vector3 spawnPos = transform.position + new Vector3(command.CastDirection.x, command.CastDirection.y, 0f) * 0.5f;

                        Runner.Spawn(melancholicSkill.iceProjectilePrefab, spawnPos, rotation, Object.InputAuthority, (runner, obj) =>
                        {
                            var iceBridge = obj.GetComponent<IceProjectileNetworkBridge>();
                            if (iceBridge != null)
                            {
                                iceBridge.InitializeMain(Object.InputAuthority, melancholicSkill, command.CastDirection, _playerEntity);
                            }
                        });
                    }
                }

                _entityManager.RemoveComponent<SpawnIceProjectileCommand>(_playerEntity);
            }

            // =========================================================================
            // СБОР КООРДИНАТ И ИНПУТА В ECS
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
            var skillCooldown = 5f;
            var maxCharges = 1;
            var castDist = 4f;
            var effectRad = 5f;

            if (archetypeData != null && archetypeData.activeSkillData != null) 
            {
                skillCooldown = archetypeData.activeSkillData.cooldown;
                maxCharges = archetypeData.activeSkillData.maxCharges;
                castDist = archetypeData.activeSkillData.castDistance;
                effectRad = archetypeData.activeSkillData.effectRadius;
            }

            if (HasStateAuthority)
            {
                NetworkMaxCooldown = skillCooldown;
                NetworkCurrentCooldown = 0f;
                NetworkMaxCharges = maxCharges;
                NetworkCurrentCharges = maxCharges;
            }

            _entityManager.SetComponentData(_playerEntity, new SkillStateComponent 
            { 
                MaxCooldown = skillCooldown, 
                CurrentCooldown = 0f, 
                MaxCharges = maxCharges, 
                CurrentCharges = maxCharges 
            });

            if (!_entityManager.HasComponent<SkillConfigComponent>(_playerEntity))
                _entityManager.AddComponent<SkillConfigComponent>(_playerEntity);
                
            // Сборка динамических параметров под конкретные классы
            var dashSpd = 0f;
            float dashDur = 0.2f;
            
            // Параметры Истерика
            float furyThreshold = 0.3f;
            float furySpeedMult = 1.5f;
            float furyLifesteal = 0f;
            int tornadoMult = 1;
            
            // Параметры Параноика
            float shieldCap = 100f;
            float shieldRecharge = 5f;
            float auraRad = 3f;
            int maxTurrets = 1;
            float turretTime = 15f;
            
            // Параметры Шизоида 
            var instabilityTime = 1f;
            var instabilityMax = 4;
            var instabilityDmg = 0.2f;
            var invisDuration = 4f;
            var cloneExplosionDmg = 150f;
            var cloneExplosionRad = 3f;
            
            // Параметры Меланхолика 
            var frostSlow = 1f;
            var apathyMax = 3;
            var freezeDur = 2f;
            var chainTargets = 3;
            var chainDmg = 150f;

            if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericData)
            {
                dashSpd = hystericData.dashSpeed;
                dashDur = hystericData.dashDuration;
                
                furyThreshold = hystericData.furyHealthThreshold;
                furySpeedMult = hystericData.furySpeedMultiplier;
                furyLifesteal = hystericData.furyLifesteal;
                tornadoMult = hystericData.tornadoBulletMultiplier;
            }
            else if (archetypeData != null && archetypeData.activeSkillData is ParanoiacSkillData paranoiacData)
            {
                shieldCap = paranoiacData.shieldCapacity;
                shieldRecharge = paranoiacData.shieldRechargeTime;
                auraRad = paranoiacData.shieldAuraRadius;
                maxTurrets = paranoiacData.maxTurrets;
                turretTime = paranoiacData.turretLifeTime;
            }
            else if (archetypeData != null && archetypeData.activeSkillData is SchizoidSkillData schizoidData)
            {
                instabilityTime = schizoidData.timePerInstabilityStack;
                instabilityMax = schizoidData.maxInstabilityStacks;
                instabilityDmg = schizoidData.damageMultiplierPerStack;
                invisDuration = schizoidData.invisibilityDuration;
                cloneExplosionDmg = schizoidData.cloneExplosionDamage;
                cloneExplosionRad = schizoidData.cloneExplosionRadius;
            }
            else if (archetypeData != null && archetypeData.activeSkillData is MelancholicSkillData melancholicData)
            {
                // Переводим 0.2f (20% замедления) в множитель скорости 0.8f
                frostSlow = 1.0f - melancholicData.slowPercentage; 
                apathyMax = melancholicData.apathyStacksToFreeze;
                freezeDur = melancholicData.freezeDuration;
                chainTargets = melancholicData.chainTargetsCount;
                chainDmg = melancholicData.chainExplosionDamage;
            }
                
            _entityManager.SetComponentData(_playerEntity, new SkillConfigComponent
            {
                CastDistance = castDist,
                EffectRadius = effectRad,
                
                // Наполнение конфига Истерика
                DashSpeed = dashSpd,
                DashDuration = dashDur,
                FuryHealthThreshold = furyThreshold,
                FurySpeedMultiplier = furySpeedMult,
                FuryLifesteal = furyLifesteal,
                TornadoBulletMultiplier = tornadoMult,
                
                // Наполнение конфига Параноика
                ShieldCapacity = shieldCap,
                ShieldRechargeTime = shieldRecharge,
                ShieldAuraRadius = auraRad,
                MaxTurrets = maxTurrets,
                TurretLifeTime = turretTime,
                
                // Наполнение конфига Шизоида
                InstabilityTimePerStack = instabilityTime,
                InstabilityMaxStacks = instabilityMax,
                InstabilityDamagePerStack = instabilityDmg,
                InvisibilityDuration = invisDuration,
                CloneExplosionDamage = cloneExplosionDmg,
                CloneExplosionRadius = cloneExplosionRad,
                
                // Наполнение конфига Меланхолика
                FrostSlowMultiplier = frostSlow,
                ApathyMaxStacks = apathyMax,
                FreezeDuration = freezeDur,
                ChainTargetsCount = chainTargets,
                ChainExplosionDamage = chainDmg
            });

            _entityManager.RemoveComponent<HystericTag>(_playerEntity);
            _entityManager.RemoveComponent<ParanoiacTag>(_playerEntity);
            _entityManager.RemoveComponent<MelancholicTag>(_playerEntity);
            _entityManager.RemoveComponent<SchizoidTag>(_playerEntity);
            
            // Чистим пассивные компоненты других классов при смене архетипа
            if (_entityManager.HasComponent<QuantumInstabilityComponent>(_playerEntity))
                _entityManager.RemoveComponent<QuantumInstabilityComponent>(_playerEntity);
            if (_entityManager.HasComponent<InvisibilityStateComponent>(_playerEntity))
                _entityManager.RemoveComponent<InvisibilityStateComponent>(_playerEntity);
            if  (_entityManager.HasComponent<EnergyShieldComponent>(_playerEntity))
                _entityManager.RemoveComponent<EnergyShieldComponent>(_playerEntity);

            switch (archetypeID)
            {
                case 0: 
                    _entityManager.AddComponent<HystericTag>(_playerEntity); 
                    break;
                case 1: 
                    _entityManager.AddComponent<ParanoiacTag>(_playerEntity); 
                    // Выдаем щит Параноику!
                    _entityManager.AddComponentData(_playerEntity, new EnergyShieldComponent 
                    { 
                        MaxShield = shieldCap, 
                        CurrentShield = shieldCap, 
                        OutOfCombatTimer = 0f 
                    });
                    break;
                case 2: 
                    _entityManager.AddComponent<SchizoidTag>(_playerEntity);
                    _entityManager.AddComponentData(_playerEntity, new QuantumInstabilityComponent
                    {
                        CurrentStacks = 0,
                        Timer = 0f,
                        TimeSinceLastDamage = 10f
                    });
                    break;
                case 3: 
                    _entityManager.AddComponent<MelancholicTag>(_playerEntity); 
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
        
        private void Update()
        {
            if (_spriteRenderer == null) return;

            // Проверяем, есть ли на нашей ECS-сущности компонент невидимости
            if (_entityManager.Exists(_playerEntity) && _entityManager.HasComponent<InvisibilityStateComponent>(_playerEntity))
            {
                // Если невидим — делаем спрайт полупрозрачным (альфа 0.3f)
                var c = _spriteRenderer.color;
                c.a = 0.3f;
                _spriteRenderer.color = c;
            }
            else
            {
                // Когда инвиз спал — возвращаем полную видимость
                var c = _spriteRenderer.color;
                c.a = 1.0f;
                _spriteRenderer.color = c;
            }
        }
    }
}