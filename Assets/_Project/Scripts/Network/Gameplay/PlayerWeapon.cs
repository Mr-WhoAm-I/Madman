using _Project.Scripts.Core;
using _Project.Scripts.Data.Skills;
using _Project.Scripts.Data.Weapons;
using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Enemies;
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

        [Networked] private TickTimer Timer0 { get; set; }
        [Networked] private TickTimer Timer1 { get; set; }
        
        private EntityQuery _enemyQuery;
        private PlayerManager _playerManager;
        private bool _isClone; 

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();

            if (!HasStateAuthority) return;
            _enemyQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(EnemyTagComponent), typeof(LocalTransform));
                
            _isClone = GetComponent<CloneNetworkBridge>() != null;

            ValidateWeapons();
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

        // --- ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Чтение конфига владельца клона ---
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

            if (_isClone)
            {
                // === МЕХАНИКА: ВООРУЖЕННАЯ ПРОЕКЦИЯ (ШИЗОИД) ===
                var cloneBridge = GetComponent<CloneNetworkBridge>();
                if (cloneBridge != null && GetOwnerConfig(cloneBridge.OwnerPlayer, out var cloneConfig, out _))
                {
                    // Если перк не куплен, клон не имеет права стрелять. Выходим.
                    if (cloneConfig.CloneShootingMult <= 0f) return; 
                }
                else return; 
            }
            else
            {
                var bridge = GetComponent<PlayerNetworkBridge>();
                if (bridge != null && bridge.EntityManager != default && bridge.EntityManager.Exists(bridge.PlayerEntity))
                {
                    var em = bridge.EntityManager;
                    var playerE = bridge.PlayerEntity;

                    // БЛОКИРОВКА АВТОАТАКИ В ИНВИЗЕ
                    if (em.HasComponent<InvisibilityStateComponent>(playerE))
                    {
                        return; 
                    }

                    if (em.HasComponent<Trigger360ShootTag>(playerE))
                    {
                        if (HasStateAuthority)
                        {
                            var bulletsToShoot = 8; 
                            var archetypeData = ProfileController.Instance.GetArchetypeAsset(bridge.NetworkArchetypeID);
                            
                            if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericSkill)
                            {
                                bulletsToShoot = hystericSkill.bulletCount; 
                            }
                            
                            var config = em.GetComponentData<SkillConfigComponent>(playerE);
                            if (config.TornadoBulletMultiplier > 0)
                            {
                                bulletsToShoot *= config.TornadoBulletMultiplier;
                                Debug.Log($"<color=#FF4500>[СМЕРЧ]</color> Ульта усилена! Выпущено {bulletsToShoot} пуль!");
                            }
                            
                            ShootTornado360(bulletsToShoot);
                        }
                        em.RemoveComponent<Trigger360ShootTag>(playerE);
                    }
                }
            }

            if (!HasStateAuthority) return;

            if (equippedWeapons.Length > 0 && equippedWeapons[0] != null && Timer0.ExpiredOrNotRunning(Runner))
            {
                FireWeapon(equippedWeapons[0], 0);
                Timer0 = TickTimer.CreateFromSeconds(Runner, equippedWeapons[0].fireRate);
            }

            if (equippedWeapons.Length <= 1 || equippedWeapons[1] == null || !Timer1.ExpiredOrNotRunning(Runner)) return;
            
            FireWeapon(equippedWeapons[1], 1);
            Timer1 = TickTimer.CreateFromSeconds(Runner, equippedWeapons[1].fireRate);
        }

        private void FireWeapon(WeaponData weapon, int slotIndex)
        {
            var enemyTransforms = _enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            
            if (enemyTransforms.Length == 0)
            {
                enemyTransforms.Dispose();
                return; 
            }

            var shooterPos = new float3(transform.position.x, transform.position.y, 0f);
            var nearestDistSq = float.MaxValue;
            var nearestEnemyPos = float3.zero;

            for (var i = 0; i < enemyTransforms.Length; i++)
            {
                var distSq = math.distancesq(shooterPos, enemyTransforms[i].Position);
                if (!(distSq < nearestDistSq)) continue;
                nearestDistSq = distSq;
                nearestEnemyPos = enemyTransforms[i].Position;
            }
            enemyTransforms.Dispose();

            var direction = ((Vector3)nearestEnemyPos - transform.position).normalized;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            var baseRotation = Quaternion.Euler(0, 0, angle);

            var rightDirection = Vector3.Cross(direction, Vector3.forward);
            var spawnOffset = rightDirection * (slotIndex == 0 ? -0.3f : 0.3f);
            var finalSpawnPos = transform.position + spawnOffset;

            float flatDamageBonus = 0f;
            float critChance = 0f;
            float critDamageMult = 0.5f; 

            var bridge = GetComponent<PlayerNetworkBridge>();
            if (bridge != null && bridge.EntityManager != default && bridge.EntityManager.Exists(bridge.PlayerEntity))
            {
                var em = bridge.EntityManager;
                var playerE = bridge.PlayerEntity;

                if (em.HasComponent<SkillConfigComponent>(playerE))
                {
                    var config = em.GetComponentData<SkillConfigComponent>(playerE);
                    flatDamageBonus = config.BaseDamage;
                    critChance = config.CritChance;
                    critDamageMult += config.CritDamage; 
                }
            }

            var calculatedDamage = weapon.damage + flatDamageBonus; 

            bool isCrit = false;
            if (!_isClone && UnityEngine.Random.value <= critChance)
            {
                isCrit = true;
                calculatedDamage *= (1f + critDamageMult);
            }

            if (_isClone)
            {
                // ЛОГИКА КЛОНА: Урон берется из множителя магазина!
                var cloneBridge = GetComponent<CloneNetworkBridge>();
                if (cloneBridge != null && GetOwnerConfig(cloneBridge.OwnerPlayer, out var cloneConfig, out _))
                {
                    calculatedDamage *= cloneConfig.CloneShootingMult;
                }
            }
            else
            {
                if (bridge != null && bridge.EntityManager != default)
                {
                    var em = bridge.EntityManager;
                    var playerE = bridge.PlayerEntity;

                    if (em.HasComponent<SkillConfigComponent>(playerE))
                    {
                        var config = em.GetComponentData<SkillConfigComponent>(playerE);

                        // 1. Квантовая нестабильность
                        if (em.HasComponent<QuantumInstabilityComponent>(playerE))
                        {
                            var instability = em.GetComponentData<QuantumInstabilityComponent>(playerE);
                            if (instability.CurrentStacks > 0)
                            {
                                var multiplier = 1.0f + (instability.CurrentStacks * config.InstabilityDamagePerStack);
                                calculatedDamage *= multiplier;
                                
                                instability.CurrentStacks = 0;
                                instability.Timer = 0f;
                                em.SetComponentData(playerE, instability);
                            }
                        }

                        // === 2. МЕХАНИКА: УДАР ИЗ ТЕНИ ===
                        if (em.HasComponent<ShadowStrikeBuffComponent>(playerE))
                        {
                            if (config.ShadowStrikeMult > 0f)
                            {
                                calculatedDamage *= config.ShadowStrikeMult;
                                Debug.Log($"<color=#9400D3>[УДАР ИЗ ТЕНИ]</color> Из инвиза! Урон этого выстрела: {calculatedDamage}!");
                            }
                            
                            // Снимаем бафф, чтобы второй выстрел был обычным
                            em.RemoveComponent<ShadowStrikeBuffComponent>(playerE);
                        }
                    }
                }
            }

            for (var p = 0; p < weapon.pelletCount; p++)
            {
                var randomSpread = UnityEngine.Random.Range(-weapon.spreadAngle, weapon.spreadAngle);
                var finalRotation = baseRotation * Quaternion.Euler(0, 0, randomSpread);

                Runner.Spawn(weapon.bulletPrefab, finalSpawnPos, finalRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        Entity shooterEntity = Entity.Null;
                        if (bridge != null) shooterEntity = bridge.PlayerEntity;
                        else if (_isClone)
                        {
                            var cloneBridge = GetComponent<CloneNetworkBridge>();
                            if (cloneBridge != null) shooterEntity = cloneBridge.CloneEntity; // Передаем сущность клона!
                        }
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, calculatedDamage, weapon.bulletSpeed, shooterEntity);
                    }
                });
            }
        }

        public void ShootTornado360(int bulletCount)
        {
            if (equippedWeapons.Length == 0 || equippedWeapons[0] == null) return;
            var weapon = equippedWeapons[0];
            
            var angleStep = 360f / bulletCount;
            
            for (var i = 0; i < bulletCount; i++)
            {
                var rotation = Quaternion.Euler(0, 0, i * angleStep);
                
                Runner.Spawn(weapon.bulletPrefab, transform.position, rotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        var bridge = GetComponent<PlayerNetworkBridge>();
                        var shooterEntity = bridge != null ? bridge.PlayerEntity : Entity.Null;
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, weapon.damage, weapon.bulletSpeed, shooterEntity);
                    }
                });
            }
        }
    }
}