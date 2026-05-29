using Fusion;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Data;
using _Project.Scripts.Core; 
using Allocator = Unity.Collections.Allocator;

namespace _Project.Scripts.Network
{
    public class PlayerWeapon : NetworkBehaviour
    {
        [Header("Экипированное оружие")]
        public WeaponData[] equippedWeapons = new WeaponData[2]; //

        [Networked] private TickTimer Timer0 { get; set; }
        [Networked] private TickTimer Timer1 { get; set; }
        
        private EntityQuery _enemyQuery;
        private PlayerManager _playerManager;
        private bool _isClone; // Флаг: является ли этот объект клоном Шизоида

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();

            if (!HasStateAuthority) return;
            _enemyQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(EnemyTagComponent), typeof(LocalTransform));
                
            // Проверяем, кто мы — игрок или клон
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

        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;

            // Клон не обрабатывает ECS-ульты Истерика и не блокирует сам себя в инвизе
            if (!_isClone)
            {
                var bridge = GetComponent<PlayerNetworkBridge>();
                if (bridge != null && bridge.EntityManager != default && bridge.EntityManager.Exists(bridge.PlayerEntity))
                {
                    var em = bridge.EntityManager;
                    var playerE = bridge.PlayerEntity;

                    // БЛОКИРОВКА АВТОАТАКИ В ИНВИЗЕ ДЛЯ НАСТОЯЩЕГО ИГРОКА
                    if (em.HasComponent<InvisibilityStateComponent>(playerE))
                    {
                        return; 
                    }

                    // Логика ульты Истерика (Ураган 360)
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
                            
                            ShootTornado360(bulletsToShoot);
                        }
                        em.RemoveComponent<Trigger360ShootTag>(playerE);
                    }
                }
            }

            if (!HasStateAuthority) return;

            // ЕДИНЫЙ ЦИКЛ СТРЕЛЬБЫ (Работает и для игрока, и для полноценного оружия клона)
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

            // --- ВЫЧИСЛЕНИЕ ДИНАМИЧЕСКОГО УРОНА ---
            var calculatedDamage = weapon.damage;

            if (_isClone)
            {
                // ИСПРАВЛЕНО: Сначала проверяем наличие архетипа, затем безопасно приводим его activeSkillData к типу Шизоида
                if (_playerManager != null && _playerManager.currentArchetype != null)
                {
                    var schizoidSkill = _playerManager.currentArchetype.activeSkillData as SchizoidSkillData;
                    if (schizoidSkill != null)
                    {
                        calculatedDamage = weapon.damage * schizoidSkill.cloneDamagePercentage;
                    }
                }
            }
            else
            {
                // ЛОГИКА НАСТОЯЩЕГО ИГРОКА: применяем стаки Квантовой нестабильности
                var bridge = GetComponent<PlayerNetworkBridge>();
                if (bridge != null && bridge.EntityManager != default && bridge.EntityManager.Exists(bridge.PlayerEntity))
                {
                    var em = bridge.EntityManager;
                    var playerE = bridge.PlayerEntity;

                    if (em.HasComponent<QuantumInstabilityComponent>(playerE) && em.HasComponent<SkillConfigComponent>(playerE))
                    {
                        var instability = em.GetComponentData<QuantumInstabilityComponent>(playerE);
                        var config = em.GetComponentData<SkillConfigComponent>(playerE);

                        if (instability.CurrentStacks > 0)
                        {
                            var multiplier = 1.0f + (instability.CurrentStacks * config.InstabilityDamagePerStack);
                            calculatedDamage *= multiplier;

                            Debug.Log($"<color=#00FFCC>[КВАНТОВАЯ НЕСТАБИЛЬНОСТЬ]</color> Игрок выстрелил! Стаки: {instability.CurrentStacks}. Урон: {weapon.damage} -> {calculatedDamage}");

                            instability.CurrentStacks = 0;
                            instability.Timer = 0f;
                            em.SetComponentData(playerE, instability);
                        }
                    }
                }
            }

            // Спавним столько дробинок/снарядов, сколько прописано в WeaponData полноценного ствола!
            for (var p = 0; p < weapon.pelletCount; p++)
            {
                var randomSpread = UnityEngine.Random.Range(-weapon.spreadAngle, weapon.spreadAngle);
                var finalRotation = baseRotation * Quaternion.Euler(0, 0, randomSpread);

                Runner.Spawn(weapon.bulletPrefab, finalSpawnPos, finalRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        // Сюда улетает честно пересчитанный урон (либо уменьшенный для клона, либо увеличенный для игрока)
                        var bridge = GetComponent<PlayerNetworkBridge>();
                        var shooterEntity = bridge != null ? bridge.PlayerEntity : Entity.Null;
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