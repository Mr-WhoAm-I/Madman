using _Project.Scripts.Core;
using _Project.Scripts.Data.Skills;
using _Project.Scripts.Data.Weapons;
using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.ECS.Components.Player;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network.Bridges;
using _Project.Scripts.Network.Managers;
using Fusion;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Allocator = Unity.Collections.Allocator;

namespace _Project.Scripts.Network.Gameplay
{
    public class PlayerWeapon : NetworkBehaviour
    {
        [Header("Экипированное оружие")]
        public WeaponData[] equippedWeapons = new WeaponData[2];
        [Networked, Capacity(2)] public NetworkArray<NetworkString<_32>> EquippedWeaponIDs { get; }
        
        [Header("Визуал оружия")]
        public WeaponCatalogData weaponCatalog; // Ссылка на глобальный каталог пушек
        public Transform[] handSlots = new Transform[2]; // Точки (пустышки) рук персонажа
        private GameObject[,] _spawnedVisuals = new GameObject[2, 2];
        private GameObject[] _activeWeaponModels = new GameObject[2];
        
        // ИЗМЕНЕНИЕ: Сетевой массив таймеров для поддержки любого количества слотов
        [Networked, Capacity(4)] private NetworkArray<TickTimer> WeaponCooldowns { get; }

        [Networked, Capacity(4)] private NetworkArray<int> CurrentAmmo { get; }
        [Networked, Capacity(4)] private NetworkArray<TickTimer> ReloadTimers { get; }
        [Networked, Capacity(4)] private NetworkArray<byte> LoadedAmmoType { get; }

        private EntityQuery _enemyQuery;
        private PlayerManager _playerManager;
        private PlayerNetworkBridge _bridge;
        private ChangeDetector _changeDetector;
        private bool _isClone;

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();
            _bridge = GetComponent<PlayerNetworkBridge>();
            _isClone = GetComponent<CloneNetworkBridge>() != null;

            if (HasStateAuthority)
            {
                _enemyQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(EnemyTagComponent), typeof(LocalTransform));
                
                // === ФИКС 3: ЧИТАЕМ СОХРАНЕНИЯ ОРУЖИЯ ИЗ ХАБА ===
                if (ProfileController.Instance != null && weaponCatalog != null)
                {
                    var profile = ProfileController.Instance.CurrentProfile;
                    var prog = profile.GetProgressForArchetype(profile.LastSelectedArchetypeID);
                    
                    for (int i = 0; i < 2; i++)
                    {
                        string savedID = prog.EquippedWeaponIDs[i];
                        if (!string.IsNullOrEmpty(savedID))
                        {
                            equippedWeapons[i] = weaponCatalog.GetWeaponByID(savedID);
                        }
                    }
                }

                ValidateWeapons();
                
                // Записываем обновленные пушки в сеть
                for (var i = 0; i < equippedWeapons.Length; i++)
                {
                    if (equippedWeapons[i] != null)
                    {
                        EquippedWeaponIDs.Set(i, equippedWeapons[i].weaponID);
                        
                        if (equippedWeapons[i].ammoSystem == AmmoType.Magazine)
                        {
                            LoadedAmmoType.Set(i, 0);
                            CurrentAmmo.Set(i, equippedWeapons[i].magazineSize);
                        }
                    }
                }
            }

            // Инициализация графики для всех клиентов
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            UpdateWeaponVisuals();
        }

        // === ВИЗУАЛЬНАЯ ЛОГИКА ===
        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(EquippedWeaponIDs))
                {
                    UpdateWeaponVisuals();
                }
            }

            UpdateWeaponsRotation();
        }

        private void UpdateWeaponVisuals()
        {
            if (weaponCatalog == null) return;

            for (int i = 0; i < EquippedWeaponIDs.Length; i++)
            {
                string weaponID = EquippedWeaponIDs.Get(i).ToString();
                if (string.IsNullOrEmpty(weaponID)) continue;

                var data = weaponCatalog.GetWeaponByID(weaponID);
                if (data == null || data.visualPrefabRight == null || data.visualPrefabLeft == null) continue;

                if (_spawnedVisuals[i, 0] != null && _spawnedVisuals[i, 0].name == data.visualPrefabRight.name + "(Clone)")
                    continue;

                if (_spawnedVisuals[i, 0] != null) Destroy(_spawnedVisuals[i, 0]);
                if (_spawnedVisuals[i, 1] != null) Destroy(_spawnedVisuals[i, 1]);

                if (handSlots[i] != null)
                {
                    _spawnedVisuals[i, 0] = Instantiate(data.visualPrefabRight, handSlots[i]);
                    _spawnedVisuals[i, 1] = Instantiate(data.visualPrefabLeft, handSlots[i]);

                    _spawnedVisuals[i, 0].transform.localPosition = Vector3.zero;
                    _spawnedVisuals[i, 1].transform.localPosition = Vector3.zero;

                    _spawnedVisuals[i, 0].SetActive(true);
                    _spawnedVisuals[i, 1].SetActive(false);
                }
            }
        }

        private void UpdateWeaponsRotation()
        {
            if (!TryGetNearestEnemy(out float3 nearestEnemyPos)) return;
            Vector3 targetPos = nearestEnemyPos;

            for (int i = 0; i < handSlots.Length; i++)
            {
                var rightPrefab = _spawnedVisuals[i, 0];
                var leftPrefab = _spawnedVisuals[i, 1];

                if (rightPrefab == null || leftPrefab == null || handSlots[i] == null) continue;

                Vector3 aimDirection = (targetPos - handSlots[i].position).normalized;
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

                bool isAimingLeft = Mathf.Abs(angle) > 90f;

                if (isAimingLeft)
                {
                    rightPrefab.SetActive(false);
                    leftPrefab.SetActive(true);
                    leftPrefab.transform.rotation = Quaternion.Euler(0, 0, angle - 180f);
                }
                else
                {
                    leftPrefab.SetActive(false);
                    rightPrefab.SetActive(true);
                    rightPrefab.transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
        }

        public void ValidateWeapons()
        {
            if (_playerManager == null || _playerManager.currentArchetype == null) return;

            var allowedSlots = _playerManager.currentArchetype.weaponSlotsCount;
            var allowedCategory = _playerManager.currentArchetype.allowedWeaponCategory;

            for (var i = 0; i < equippedWeapons.Length; i++)
            {
                if (equippedWeapons[i] == null) continue;
                if (i >= allowedSlots)
                {
                    Debug.LogWarning($"[Сервер] Слот {i + 1} заблокирован. Оружие изъято.");
                    equippedWeapons[i] = null;
                }
                else if (equippedWeapons[i].category != allowedCategory)
                {
                    Debug.LogWarning($"[Сервер] Оружие изъято из-за несоответствия категории.");
                    equippedWeapons[i] = null;
                }
            }
        }

        private bool GetOwnerConfig(PlayerRef owner, out SkillConfigComponent config, out Entity ownerEntity)
        {
            config = default;
            ownerEntity = Entity.Null;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            if (em == default) return false; 
            
            var query = em.CreateEntityQuery(typeof(PlayerOwnerComponent), typeof(SkillConfigComponent));
            using var owners = query.ToComponentDataArray<PlayerOwnerComponent>(Allocator.Temp);
            using var configs = query.ToComponentDataArray<SkillConfigComponent>(Allocator.Temp);
            using var entities = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i].Player == owner)
                {
                    config = configs[i];
                    ownerEntity = entities[i];
                    return true;
                }
            }
            return false;
        }

        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;

            if (!CanShoot()) return;

            HandleUltimateAbilities();

            if (!HasStateAuthority) return;

            HandleWeaponFiring();
        }

        // ИЗМЕНЕНИЕ: Изолированная логика блокировок стрельбы
        private bool CanShoot()
        {
            if (_isClone)
            {
                var cloneBridge = GetComponent<CloneNetworkBridge>();
                if (cloneBridge != null && GetOwnerConfig(cloneBridge.OwnerPlayer, out var cloneConfig, out _))
                {
                    return cloneConfig.CloneShootingMult > 0f;
                }
                return false;
            }
            
            if (_bridge != null && _bridge.EntityManager != default && _bridge.EntityManager.Exists(_bridge.PlayerEntity))
            {
                var em = _bridge.EntityManager;
                if (em.HasComponent<InvisibilityStateComponent>(_bridge.PlayerEntity)) return false;
            }
            return true;
        }

        // ИЗМЕНЕНИЕ: Изолированная логика ультимейта
        private void HandleUltimateAbilities()
        {
            if (_isClone || _bridge == null || _bridge.EntityManager == default || !_bridge.EntityManager.Exists(_bridge.PlayerEntity)) return;

            var em = _bridge.EntityManager;
            var playerE = _bridge.PlayerEntity;

            if (em.HasComponent<Trigger360ShootTag>(playerE))
            {
                if (HasStateAuthority)
                {
                    int bulletsToShoot = CalculateUltimateBulletCount(em, playerE);
                    ShootTornado360(bulletsToShoot);
                }
                em.RemoveComponent<Trigger360ShootTag>(playerE);
            }
        }

        private int CalculateUltimateBulletCount(EntityManager em, Entity playerE)
        {
            int bulletsToShoot = 8;
            var archetypeData = ProfileController.Instance.GetArchetypeAsset(_bridge.NetworkArchetypeID);
            
            if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericSkill)
            {
                bulletsToShoot = hystericSkill.bulletCount;
            }
            
            if (em.HasComponent<SkillConfigComponent>(playerE))
            {
                var config = em.GetComponentData<SkillConfigComponent>(playerE);
                if (config.TornadoBulletMultiplier > 0)
                {
                    bulletsToShoot *= config.TornadoBulletMultiplier;
                    Debug.Log($"<color=#FF4500>[СМЕРЧ]</color> Ульта усилена! Выпущено {bulletsToShoot} пуль!");
                }
            }
            return bulletsToShoot;
        }

        // ИЗМЕНЕНИЕ: Автоматическая обработка любого количества оружия
        private void HandleWeaponFiring()
        {
            // Читаем, что нажал игрок
            byte desiredAmmoType = 0;
            if (_bridge.EntityManager.HasComponent<PlayerInputComponent>(_bridge.PlayerEntity))
            {
                desiredAmmoType = _bridge.EntityManager.GetComponentData<PlayerInputComponent>(_bridge.PlayerEntity).SelectedAmmoType;
            }

            for (int i = 0; i < equippedWeapons.Length; i++)
            {
                var weapon = equippedWeapons[i];
                if (weapon == null) continue;

                // --- 1. ПРОВЕРКА СМЕНЫ ПАТРОНОВ ---
                if (LoadedAmmoType.Get(i) != desiredAmmoType)
                {
                    // === ААА-ФИКС: ПРОВЕРКА НА ПУСТОЙ РЮКЗАК ДО ПЕРЕЗАРЯДКИ ===
                    if (desiredAmmoType != 0 && GetBackpackAmmo(desiredAmmoType) <= 0)
                    {
                        Debug.LogWarning($"[Weapon] Патронов типа {desiredAmmoType} нет! Переключение заблокировано.");
                        
                        // Принудительно сбрасываем инпут в ECS обратно на то, что заряжено сейчас,
                        // чтобы не застрять в бесконечном цикле попыток переключиться
                        if (_bridge.EntityManager.HasComponent<PlayerInputComponent>(_bridge.PlayerEntity))
                        {
                            var inputComp = _bridge.EntityManager.GetComponentData<PlayerInputComponent>(_bridge.PlayerEntity);
                            inputComp.SelectedAmmoType = LoadedAmmoType.Get(i);
                            _bridge.EntityManager.SetComponentData(_bridge.PlayerEntity, inputComp);
                        }
                        
                        desiredAmmoType = LoadedAmmoType.Get(i); // Продолжаем стрелять тем, что есть
                        continue; 
                    }

                    // Если патроны есть (или это бесконечная физика) — начинаем честную перезарядку
                    RefundAmmoToBackpack(LoadedAmmoType.Get(i), CurrentAmmo.Get(i));
                    
                    LoadedAmmoType.Set(i, desiredAmmoType);
                    CurrentAmmo.Set(i, 0); // Обнуляем обойму
                    ReloadTimers.Set(i, TickTimer.CreateFromSeconds(Runner, weapon.reloadTime));
                    
                    Debug.Log($"[Weapon] Смена типа патронов на {desiredAmmoType}. Перезарядка...");
                    continue; // Ждем окончания перезарядки
                }

                // --- 2. ПРОВЕРКА ПЕРЕЗАРЯДКИ ---
                var reloadTimer = ReloadTimers.Get(i);
                if (reloadTimer.IsRunning)
                {
                    if (reloadTimer.Expired(Runner))
                    {
                        // Пытаемся достать нужные патроны из рюкзака
                        int loadedAmount = TryLoadAmmoFromBackpack(LoadedAmmoType.Get(i), weapon.magazineSize);
                        CurrentAmmo.Set(i, loadedAmount);
                        ReloadTimers.Set(i, TickTimer.None);

                        if (loadedAmount == 0 && LoadedAmmoType.Get(i) != 0)
                        {
                            Debug.Log($"[Weapon] Патроны типа {LoadedAmmoType.Get(i)} закончились! Обойма пуста.");
                        }
                    }
                    continue; 
                }

                // --- 3. ПРОВЕРКА КУЛДАУНА И ОБОЙМЫ ---
                var cooldownTimer = WeaponCooldowns.Get(i);
                if (cooldownTimer.ExpiredOrNotRunning(Runner))
                {
                    if (weapon.ammoSystem == AmmoType.Magazine && CurrentAmmo.Get(i) <= 0)
                    {
                        if (LoadedAmmoType.Get(i) != 0 && GetBackpackAmmo(LoadedAmmoType.Get(i)) <= 0)
                        {
                            // === АВТОМАТИЧЕСКИЙ ВОЗВРАТ К ФИЗИКЕ ===
                            if (Runner.IsForward) Debug.Log($"<color=#FFFF00>[Weapon]</color> Слот {i}: Патроны кончились. Авто-возврат к Физике.");
                            
                            LoadedAmmoType.Set(i, 0);
                            CurrentAmmo.Set(i, 0);
                            ReloadTimers.Set(i, TickTimer.CreateFromSeconds(Runner, weapon.reloadTime));
                            
                            // Принудительно меняем инпут в ECS
                            if (_bridge.EntityManager.HasComponent<PlayerInputComponent>(_bridge.PlayerEntity))
                            {
                                var inputComp = _bridge.EntityManager.GetComponentData<PlayerInputComponent>(_bridge.PlayerEntity);
                                inputComp.SelectedAmmoType = 0;
                                _bridge.EntityManager.SetComponentData(_bridge.PlayerEntity, inputComp);
                            }

                            // Вызываем RPC, чтобы переключить UI на клиенте!
                            _bridge.Rpc_ForceAmmoUIUpdate(0);
                            
                            continue;
                        }

                        ReloadTimers.Set(i, TickTimer.CreateFromSeconds(Runner, weapon.reloadTime));
                        continue;
                    }

                    // --- 4. ВЫСТРЕЛ ---
                    // Вычисляем финальный элемент с учетом патрона и базового элемента оружия
                    var currentElement = GetCurrentElement(weapon, LoadedAmmoType.Get(i));
                    
                    if (TryFireWeapon(weapon, i, currentElement))
                    {
                        WeaponCooldowns.Set(i, TickTimer.CreateFromSeconds(Runner, weapon.fireRate));
                        
                        if (weapon.ammoSystem == AmmoType.Magazine)
                        {
                            CurrentAmmo.Set(i, CurrentAmmo.Get(i) - 1);
                        }
                    }
                }
            }
        }

        // === ИНВЕНТАРНАЯ ЛОГИКА ===
        private int GetBackpackAmmo(byte ammoType)
        {
            if (ammoType == 1) return _bridge.NetworkFireAmmo;
            if (ammoType == 2) return _bridge.NetworkCryoAmmo;
            if (ammoType == 3) return _bridge.NetworkToxicAmmo;
            return 0; 
        }

        private void RefundAmmoToBackpack(byte ammoType, int amount)
        {
            if (ammoType == 0 || amount <= 0) return; // Физику не возвращаем
            
            if (ammoType == 1) _bridge.NetworkFireAmmo += amount;
            else if (ammoType == 2) _bridge.NetworkCryoAmmo += amount;
            else if (ammoType == 3) _bridge.NetworkToxicAmmo += amount;
        }

        private int TryLoadAmmoFromBackpack(byte ammoType, int magazineSize)
        {
            if (ammoType == 0) return magazineSize; // Физика бесконечная

            int available = GetBackpackAmmo(ammoType);
            
            // --- ААА-ФИКС ДЛЯ ИСТЕРИКА: Умное разделение патронов ---
            int needingWeapons = 0;
            for (int k = 0; k < equippedWeapons.Length; k++)
            {
                if (equippedWeapons[k] != null && LoadedAmmoType.Get(k) == ammoType)
                {
                    // Считаем пушки, которым СЕЙЧАС нужны эти патроны
                    if (CurrentAmmo.Get(k) < equippedWeapons[k].magazineSize || ReloadTimers.Get(k).IsRunning) 
                        needingWeapons++;
                }
            }

            int amountToLoad = magazineSize;
            // Если пушек несколько, а патронов мало - делим "по-братски" (с округлением вверх)
            if (needingWeapons > 1 && available < magazineSize * needingWeapons)
            {
                amountToLoad = Mathf.CeilToInt((float)available / needingWeapons);
            }
            amountToLoad = math.min(amountToLoad, available);

            // Списываем из инвентаря
            if (ammoType == 1) _bridge.NetworkFireAmmo -= amountToLoad;
            else if (ammoType == 2) _bridge.NetworkCryoAmmo -= amountToLoad;
            else if (ammoType == 3) _bridge.NetworkToxicAmmo -= amountToLoad;

            return amountToLoad;
        }

        private WeaponElementalType GetCurrentElement(WeaponData weapon, byte ammoType)
        {
            return ammoType switch
            {
                1 => WeaponElementalType.Fire,
                2 => WeaponElementalType.Cryo,
                3 => WeaponElementalType.Toxic,
                _ => weapon.innateElement // Физика оставляет родной элемент оружия
            };
        }

        // === ОБНОВЛЕННЫЕ МЕТОДЫ ВЫСТРЕЛА (Передаем currentElement) ===
       private bool TryFireWeapon(WeaponData weapon, int slotIndex, WeaponElementalType currentElement)
        {
            if (!TryGetNearestEnemy(out float3 nearestEnemyPos)) return false;

            var direction = ((Vector3)nearestEnemyPos - transform.position).normalized;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var baseRotation = Quaternion.Euler(0, 0, angle);

            Vector3 finalSpawnPos = transform.position; // Позиция по умолчанию

            // === ИЩЕМ ДУЛО ОРУЖИЯ ===
            GameObject activeVisual = null;
            
            // Проверяем, какой префаб (правый или левый) сейчас включен в этом слоте
            if (_spawnedVisuals[slotIndex, 0] != null && _spawnedVisuals[slotIndex, 0].activeSelf)
                activeVisual = _spawnedVisuals[slotIndex, 0];
            else if (_spawnedVisuals[slotIndex, 1] != null && _spawnedVisuals[slotIndex, 1].activeSelf)
                activeVisual = _spawnedVisuals[slotIndex, 1];

            if (activeVisual != null)
            {
                // Ищем наш маячок внутри включенного префаба
                var muzzle = activeVisual.GetComponentInChildren<WeaponMuzzle>();
                if (muzzle != null)
                {
                    // Если дуло найдено — пуля вылетит ровно оттуда!
                    finalSpawnPos = muzzle.transform.position;
                }
                else
                {
                    // Фолбек: если забыли повесить WeaponMuzzle, стреляем со сдвигом как раньше
                    var rightDirection = Vector3.Cross(direction, Vector3.forward);
                    var spawnOffset = rightDirection * (slotIndex == 0 ? -0.3f : 0.3f);
                    finalSpawnPos = transform.position + spawnOffset;
                }
            }

            var (finalDamage, isCrit) = CalculateDamage(weapon);
            Entity shooterEntity = GetShooterEntity();

            SpawnProjectiles(weapon, finalSpawnPos, baseRotation, finalDamage, shooterEntity, isCrit, currentElement);
            return true;
        }

        // ИЗМЕНЕНИЕ: Безопасный поиск с автоматической очисткой массива (using)
        private bool TryGetNearestEnemy(out float3 nearestEnemyPos)
        {
            nearestEnemyPos = float3.zero;
            using var enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            
            if (enemyTransforms.Length == 0) return false;

            var shooterPos = new float3(transform.position.x, transform.position.y, 0f);
            var nearestDistSq = float.MaxValue;
            bool found = false;

            for (var i = 0; i < enemyTransforms.Length; i++)
            {
                var distSq = math.distancesq(shooterPos, enemyTransforms[i].Position);
                if (distSq < nearestDistSq)
                {
                    nearestDistSq = distSq;
                    nearestEnemyPos = enemyTransforms[i].Position;
                    found = true;
                }
            }
            return found;
        }

        // ИЗМЕНЕНИЕ: Вся логика модификаторов урона собрана в одном месте
        // Возвращает кортеж (урон, был ли крит)
        private (float damage, bool isCrit) CalculateDamage(WeaponData weapon)
        {
            float calculatedDamage = weapon.damage;
            bool isCrit = false;

            if (_isClone)
            {
                var cloneBridge = GetComponent<CloneNetworkBridge>();
                if (cloneBridge != null && GetOwnerConfig(cloneBridge.OwnerPlayer, out var cloneConfig, out _))
                {
                    return (calculatedDamage * cloneConfig.CloneShootingMult, false); 
                }
                return (calculatedDamage, false);
            }

            if (_bridge != null && _bridge.EntityManager != default && _bridge.EntityManager.Exists(_bridge.PlayerEntity))
            {
                var em = _bridge.EntityManager;
                var playerE = _bridge.PlayerEntity;

                if (em.HasComponent<SkillConfigComponent>(playerE) && em.HasComponent<CritStateComponent>(playerE))
                {
                    var config = em.GetComponentData<SkillConfigComponent>(playerE);
                    var critState = em.GetComponentData<CritStateComponent>(playerE);
                    
                    calculatedDamage += config.BaseDamage;

                    // ААА-МЕХАНИКА: Bad Luck Protection (PRD)
                    // Базовый крит 5% + бонус от магазина
                    float baseCritChance = 0.05f + config.CritChance; 
                    
                    // Каждый выстрел без крита увеличивает шанс на 50% от базового
                    // Например, при базе 30%: 1-й промах = 30%, 2-й = 45%, 3-й = 60%, 4-й = 75%
                    float dynamicCritChance = baseCritChance + (critState.NonCritStreak * (baseCritChance * 0.5f));
                    
                    // Ограничиваем кап шанса на 100%
                    dynamicCritChance = Mathf.Clamp01(dynamicCritChance);

                    if (UnityEngine.Random.value <= dynamicCritChance)
                    {
                        // УСПЕХ: Крит сработал!
                        isCrit = true;
                        float critMultiplier = 1.5f + config.CritDamage;
                        calculatedDamage *= critMultiplier;
                        
                        // Сбрасываем счетчик неудач
                        critState.NonCritStreak = 0;
                    }
                    else
                    {
                        // НЕУДАЧА: Увеличиваем счетчик для следующего выстрела
                        critState.NonCritStreak++;
                    }

                    // Сохраняем обновленный стрик обратно в ECS
                    em.SetComponentData(playerE, critState);

                    // ДЕБАГ: Раскомментируй эту строку, чтобы проверить реальные цифры на сервере, если криты снова пропадут
                    // Debug.Log($"[КРИТ СЕРВЕР] База: {baseCritChance * 100}%, Динамический шанс: {dynamicCritChance * 100}%, Стрик неудач: {critState.NonCritStreak}");

                    if (em.HasComponent<QuantumInstabilityComponent>(playerE))
                    {
                        var instability = em.GetComponentData<QuantumInstabilityComponent>(playerE);
                        if (instability.CurrentStacks > 0)
                        {
                            calculatedDamage *= 1.0f + (instability.CurrentStacks * config.InstabilityDamagePerStack);
                            instability.CurrentStacks = 0;
                            instability.Timer = 0f;
                            em.SetComponentData(playerE, instability);
                        }
                    }

                    if (!em.HasComponent<ShadowStrikeBuffComponent>(playerE)) return (calculatedDamage, isCrit);
                    if (config.ShadowStrikeMult > 0f)
                    {
                        calculatedDamage *= config.ShadowStrikeMult;
                        isCrit = true; 
                    }
                    em.RemoveComponent<ShadowStrikeBuffComponent>(playerE);
                }
            }
            return (calculatedDamage, isCrit);
        }

        private Entity GetShooterEntity()
        {
            if (_bridge != null) return _bridge.PlayerEntity;
            if (!_isClone) return Entity.Null;
            var cloneBridge = GetComponent<CloneNetworkBridge>();
            return cloneBridge != null ? cloneBridge.CloneEntity : Entity.Null;
        }

        // ИЗМЕНЕНИЕ: Изолированный спавн
        private void SpawnProjectiles(WeaponData weapon, Vector3 spawnPos, Quaternion baseRotation, float damage, Entity shooterEntity, bool isCrit, WeaponElementalType currentElement)
        {
            for (var p = 0; p < weapon.pelletCount; p++)
            {
                var randomSpread = UnityEngine.Random.Range(-weapon.spreadAngle, weapon.spreadAngle);
                var finalRotation = baseRotation * Quaternion.Euler(0, 0, randomSpread);

                Runner.Spawn(weapon.bulletPrefab, spawnPos, finalRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        // Передаем правильную стихию пуле!
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, damage, weapon.bulletSpeed, shooterEntity, weapon.pierceEnemies, currentElement, isCrit);
                    }
                });
            }
        }
        
        public void ShootTornado360(int bulletCount)
        {
            if (equippedWeapons.Length == 0 || equippedWeapons[0] == null) return;
            var weapon = equippedWeapons[0];
            
            var angleStep = 360f / bulletCount;
            // Ульта стреляет теми же патронами, что заряжены в первый слот!
            var element = GetCurrentElement(weapon, LoadedAmmoType.Get(0));
            
            for (var i = 0; i < bulletCount; i++)
            {
                var rotation = Quaternion.Euler(0, 0, i * angleStep);
                
                Runner.Spawn(weapon.bulletPrefab, transform.position, rotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, weapon.damage, weapon.bulletSpeed, GetShooterEntity(), weapon.pierceEnemies, element, false);
                    }
                });
            }
        }
    }
}