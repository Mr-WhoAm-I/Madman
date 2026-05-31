using Fusion;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.Data;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Gameplay;

namespace _Project.Scripts.Network
{
    // ААА-СТАНДАРТ ДЛЯ FUSION 2: Используем нативный порядок выполнения Unity, 
    // чтобы шлюз всегда выполнял пулл данных в ECS раньше, чем физика бега или оружия начнет их читать.
    [DefaultExecutionOrder(-10)]
    public class PlayerNetworkBridge : NetworkBehaviour
    {
        [Networked] public int NetworkArchetypeID { get; set; }
        [Networked] public int NetworkCurrency { get; set; }
        [Networked, Capacity(32)] 
        public NetworkLinkedList<NetworkString<_32>> PurchasedUpgrades { get; }
        
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
        
                    if (archetypeData != null && archetypeData.activeSkillData is ParanoiacSkillData paranoiacSkill)
                    {
                        var turretCombat = paranoiacSkill.turretCombatSettings;
                        if (turretCombat != null)
                        {
                            Runner.Spawn(turretCombat.turretPrefab, command.Position, Quaternion.identity, Object.InputAuthority, (runner, obj) => 
                            {
                                var turretBridge = obj.GetComponent<TurretNetworkBridge>();
                                if (turretBridge != null)
                                {
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
            // СЕТЕВАЯ ФАБРИКА: ОБРАБОТКА КОМАНДЫ НА СПАВН ОСКОЛКОВ (ОСКОЛОЧНЫЙ ВЗРЫВ)
            // =========================================================================
            if (_entityManager.HasBuffer<SpawnShrapnelCommand>(_playerEntity))
            {
                if (HasStateAuthority)
                {
                    var buffer = _entityManager.GetBuffer<SpawnShrapnelCommand>(_playerEntity);
                    if (buffer.Length > 0)
                    {
                        var archetypeData = ProfileController.Instance.GetArchetypeAsset(NetworkArchetypeID);

                        if (archetypeData != null && archetypeData.activeSkillData is MelancholicSkillData melancholicSkill)
                        {
                            // Распределяем осколки веером (равномерно по кругу)
                            float angleStep = 360f / buffer.Length; 
                            
                            for (int i = 0; i < buffer.Length; i++)
                            {
                                var command = buffer[i];
                                        
                                // Вычисляем угол для текущего осколка, чтобы они красиво разлетались в разные стороны
                                float currentAngle = i * angleStep;
                                Quaternion rotation = Quaternion.Euler(0, 0, currentAngle);
                                
                                Vector3 spawnPos = new Vector3(command.Position.x, command.Position.y, 0f);

                                Runner.Spawn(melancholicSkill.iceProjectilePrefab, spawnPos, rotation, Object.InputAuthority, (runner, obj) =>
                                {
                                    var shardBridge = obj.GetComponent<IceProjectileNetworkBridge>();
                                    if (shardBridge != null)
                                    {
                                        // Передаем команду и Entity.Null. Шип полетит прямо по заданному rotation!
                                        shardBridge.InitializeShard(Object.InputAuthority, melancholicSkill, _playerEntity, command.TargetEnemy);
                                    }
                                });
                            }
                        }
                    }
                }

                // Очищаем буфер после спавна всех льдин
                _entityManager.RemoveComponent<SpawnShrapnelCommand>(_playerEntity);
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
            if (!_spriteRenderer) return;

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
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_AddCurrency(int amount)
        {
            NetworkCurrency += amount;
            Debug.Log($"<color=#FFD700>[ЭКОНОМИКА]</color> Игрок {Object.InputAuthority} подобрал {amount} монет! Всего: {NetworkCurrency}");
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_RequestPurchaseUpgrade(NetworkString<_32> upgradeID, int cost)
        {
            if (NetworkCurrency >= cost && !PurchasedUpgrades.Contains(upgradeID))
            {
                NetworkCurrency -= cost;
                PurchasedUpgrades.Add(upgradeID);
                
                // === ПРИМЕНЯЕМ ПЕРК ===
                var upgradeData = LocalShopManager.Instance.GetUpgradeByID(upgradeID.ToString());
                if (upgradeData != null)
                {
                    ApplyUpgradeToECS(upgradeData);
                }
                
                Debug.Log($"<color=#32CD32>[МАГАЗИН]</color> Игрок {Object.InputAuthority} успешно купил {upgradeID}!");
            }
        }

        private void ApplyUpgradeToECS(UpgradeData upgrade)
        {
            if (!_entityManager.Exists(_playerEntity)) return;

            // 1. Изменение не-ECS параметров (например, Здоровье)
            if (upgrade.upgradeType == UpgradeType.MaxHealth)
            {
                var health = GetComponent<Health>();
                if (health != null)
                {
                    float bonus = health.MaxHealth * upgrade.value;
                    health.MaxHealth += bonus;
                    health.CurrentHealth += bonus; 
                }
                return;
            }

            // 2. Изменение ECS-параметров
            var config = _entityManager.GetComponentData<SkillConfigComponent>(_playerEntity);

            switch (upgrade.upgradeType)
            {
                // Базовые
                case UpgradeType.FlatDamage: config.BaseDamage += upgrade.value; break;
                case UpgradeType.CritChance: config.CritChance += upgrade.value; break;
                case UpgradeType.CritDamage: config.CritDamage += upgrade.value; break;
                case UpgradeType.MagnetRadius: config.MagnetRadius += upgrade.value; break;
                
                case UpgradeType.MoveSpeed:
                    var movement = _entityManager.GetComponentData<PlayerMovementComponent>(_playerEntity);
                    movement.MoveSpeed += movement.MoveSpeed * upgrade.value;
                    _entityManager.SetComponentData(_playerEntity, movement);
                    break;
                    
                case UpgradeType.CooldownReduction:
                    config.CooldownReduction += upgrade.value;
                    NetworkMaxCooldown *= (1f - upgrade.value);
                    break;

                // Оружейные
                case UpgradeType.PierceCount: config.PierceCount += (int)upgrade.value; break;
                case UpgradeType.RicochetCount: config.RicochetCount += (int)upgrade.value; break;
                case UpgradeType.ExtraProjectiles: config.ExtraProjectiles += (int)upgrade.value; break;
                
                case UpgradeType.Lifesteal:
                    config.Lifesteal += upgrade.value;
                    if (NetworkArchetypeID == 0) config.FuryLifesteal += upgrade.value; // Бонус для Истерика
                    break;

                // Классовые: Истерик
                case UpgradeType.FuryThreshold: config.FuryHealthThreshold = upgrade.value; break;
                case UpgradeType.ForceFuryOnUltimate: 
                    config.ForceFuryOnUltimate = upgrade.value > 0; 
                    config.OverloadDuration = upgrade.value; // Записываем длительность из Scriptable Object
                    break;
                case UpgradeType.TornadoMultiplier: config.TornadoBulletMultiplier = (int)upgrade.value; break;

                // Классовые: Параноик
                case UpgradeType.ShieldRechargeTime: config.ShieldRechargeTime = upgrade.value; break;
                case UpgradeType.ShieldReflect: config.ShieldReflect += upgrade.value; break;
                case UpgradeType.MaxTurrets: config.MaxTurrets = (int)upgrade.value; break;
                case UpgradeType.TurretExplode: config.TurretExplode = upgrade.value > 0; break;
                case UpgradeType.TurretCryo: config.TurretCryo = upgrade.value > 0; break;
                case UpgradeType.TurretHealAura: config.TurretHealAura = upgrade.value; break;

                // Классовые: Шизоид
                case UpgradeType.MaxInstability: config.MaxInstability = (int)upgrade.value; break;
                case UpgradeType.CloneRadiusMult: config.CloneRadiusMult = upgrade.value; break;
                case UpgradeType.MiniClones: config.MiniClones = (int)upgrade.value; break;
                case UpgradeType.InvisDuration: config.InvisDuration = upgrade.value; break;
                case UpgradeType.CloneToxicCloudDPS: config.CloneToxicCloudDPS = upgrade.value; break;
                case UpgradeType.CloneShootingMult: config.CloneShootingMult = upgrade.value; break;
                case UpgradeType.ShadowStrikeMult: config.ShadowStrikeMult = upgrade.value; break;
                case UpgradeType.ClonePoisonDPS: config.ClonePoisonDPS = upgrade.value; break;
                case UpgradeType.InvisSpeedMult: config.InvisSpeedMult = upgrade.value; break;
                case UpgradeType.KillCooldownReduction: config.KillCooldownReduction = upgrade.value; break;

                // Классовые: Меланхолик
                // === МЕЛАНХОЛИК ===
                case UpgradeType.FreezeDuration: config.FreezeDuration += upgrade.value; break; 
                case UpgradeType.ApathyMaxStacks: config.ApathyMaxStacks -= (int)upgrade.value; break;
                case UpgradeType.ChainTargets: config.ChainTargetsCount += (int)upgrade.value; break; 
                case UpgradeType.ShrapnelDeath: config.ShrapnelDeath = (int)upgrade.value; break;
                case UpgradeType.FrostVulnerability: config.FrostVulnerability = upgrade.value; break;
                case UpgradeType.AuraRadius: config.AuraRadius = upgrade.value; break;
                case UpgradeType.ShieldPerFreeze: config.ShieldPerFreeze = upgrade.value; break;
            }

            // Сохраняем измененные данные обратно в ECS
            _entityManager.SetComponentData(_playerEntity, config);
        }
    }
}