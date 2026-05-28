using Fusion;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Data;
using _Project.Scripts.Core; // ДОБАВЛЕНО: нужно для ProfileController
using Allocator = Unity.Collections.Allocator;

namespace _Project.Scripts.Network
{
    public class PlayerWeapon : NetworkBehaviour
    {
        [Header("Экипированное оружие")]
        // Массив слотов (максимум 2 по нашему GDD для Истерика)
        public WeaponData[] equippedWeapons = new WeaponData[2];

        // Сетевые таймеры для каждого слота
        [Networked] private TickTimer Timer0 { get; set; }
        [Networked] private TickTimer Timer1 { get; set; }
        
        private EntityQuery _enemyQuery;
        private PlayerManager _playerManager;

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();

            if (!HasStateAuthority) return;
            _enemyQuery = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(EnemyTagComponent), typeof(LocalTransform));
                
            // Важнейший шаг: проверяем оружие по правилам архетипа
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
                    Debug.LogWarning($"[Сервер] Слот {i + 1} заблокирован для класса {_playerManager.currentArchetype.archetypeName}. Оружие изъято.");
                    equippedWeapons[i] = null;
                }
                else if (equippedWeapons[i].category != allowedCategory)
                {
                    Debug.LogWarning($"[Сервер] Класс {_playerManager.currentArchetype.archetypeName} не может использовать категорию {equippedWeapons[i].category}. Оружие изъято.");
                    equippedWeapons[i] = null;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (GetComponent<Health>().IsDead) return;

            // --- НОВАЯ ЛОГИКА АКТИВНЫХ НАВЫКОВ (ИЗОЛИРОВАННАЯ ИЗ МОСТА) ---
            var bridge = GetComponent<PlayerNetworkBridge>();
            if (bridge != null && bridge.EntityManager != default && bridge.EntityManager.Exists(bridge.PlayerEntity))
            {
                // Оружие само проверяет, не появилась ли в ECS команда на выстрел 360
                if (bridge.EntityManager.HasComponent<Trigger360ShootTag>(bridge.PlayerEntity))
                {
                    // Спавн пуль происходит только на сервере (чтобы не было двойных пуль у клиентов)
                    if (HasStateAuthority)
                    {
                        int bulletsToShoot = 8; // Значение по умолчанию
                        
                        // Достаем актуальный архетип
                        var archetypeData = ProfileController.Instance.GetArchetypeAsset(bridge.NetworkArchetypeID);
                        if (archetypeData != null && archetypeData.activeSkillData is HystericSkillData hystericSkill)
                        {
                            bulletsToShoot = hystericSkill.bulletCount; // Берем из ScriptableObject!
                        }
                        
                        ShootTornado360(bulletsToShoot);
                    }
                    
                    // ВАЖНО: Удаляем тег у ВСЕХ (и на сервере, и на клиентах), чтобы клиент тоже очистил свой ECS
                    bridge.EntityManager.RemoveComponent<Trigger360ShootTag>(bridge.PlayerEntity);
                }
            }
            // -------------------------------------------------------------

            if (!HasStateAuthority) return;

            // Логика стрельбы из первого слота (Левая рука)
            if (equippedWeapons.Length > 0 && equippedWeapons[0] != null && Timer0.ExpiredOrNotRunning(Runner))
            {
                FireWeapon(equippedWeapons[0], 0); // Передаем индекс 0
                Timer0 = TickTimer.CreateFromSeconds(Runner, equippedWeapons[0].fireRate);
            }

            // Логика стрельбы из второго слота (Правая рука)
            if (equippedWeapons.Length <= 1 || equippedWeapons[1] == null ||
                !Timer1.ExpiredOrNotRunning(Runner)) return;
            FireWeapon(equippedWeapons[1], 1); // Передаем индекс 1
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

            var playerPos = new float3(transform.position.x, transform.position.y, 0f);
            var nearestDistSq = float.MaxValue;
            var nearestEnemyPos = float3.zero;

            for (var i = 0; i < enemyTransforms.Length; i++)
            {
                var distSq = math.distancesq(playerPos, enemyTransforms[i].Position);
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

            for (var p = 0; p < weapon.pelletCount; p++)
            {
                var randomSpread = UnityEngine.Random.Range(-weapon.spreadAngle, weapon.spreadAngle);
                var finalRotation = baseRotation * Quaternion.Euler(0, 0, randomSpread);

                Runner.Spawn(weapon.bulletPrefab, finalSpawnPos, finalRotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, weapon.damage, weapon.bulletSpeed);
                    }
                });
            }
        }

        public void ShootTornado360(int bulletCount)
        {
            if (equippedWeapons.Length == 0 || equippedWeapons[0] == null) return;
            var weapon = equippedWeapons[0];
            
            float angleStep = 360f / bulletCount;
            
            for (int i = 0; i < bulletCount; i++)
            {
                Quaternion rotation = Quaternion.Euler(0, 0, i * angleStep);
                
                Runner.Spawn(weapon.bulletPrefab, transform.position, rotation, Object.InputAuthority, (runner, obj) =>
                {
                    var bulletMovement = obj.GetComponent<BulletNetworkMovement>();
                    if (bulletMovement != null)
                    {
                        bulletMovement.InitNetworkState(weapon.bulletLifeTime, weapon.damage, weapon.bulletSpeed);
                    }
                });
            }
        }
    }
}