using _Project.Scripts.Data.Weapons;
using _Project.Scripts.ECS.Components.BuffsAndDebuffs;
using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.ECS.Components.Skills;
using System;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems.Combat
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct DamageSystem : ISystem
    {
        // === МОСТ В UI ===
        // Вызывается каждый раз, когда враг получает урон. 
        // Передает: Позицию, Количество урона, Тип стихии (для цвета)
        public static Action<Vector3, float, WeaponElementalType, bool> OnEnemyDamaged;

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =======================================================================
            // 0. ПЕРЕХВАТ УРОНА ЩИТАМИ (Оставил без изменений)
            // =======================================================================
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRW<TakeDamageComponent>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnergyShieldComponent>(entity))
                {
                    var shield = SystemAPI.GetComponent<EnergyShieldComponent>(entity);
                    shield.OutOfCombatTimer = 0f;

                    if (shield.CurrentShield > 0f)
                    {
                        float incomingDamage = takeDamage.ValueRO.Amount;
                        float absorbedDamage = 0f;

                        if (shield.CurrentShield >= incomingDamage)
                        {
                            shield.CurrentShield -= incomingDamage;
                            absorbedDamage = incomingDamage;
                            takeDamage.ValueRW.Amount = 0f; 
                        }
                        else
                        {
                            absorbedDamage = shield.CurrentShield;
                            takeDamage.ValueRW.Amount -= shield.CurrentShield;
                            shield.CurrentShield = 0f;
                        }

                        if (SystemAPI.HasComponent<SkillConfigComponent>(entity))
                        {
                            var config = SystemAPI.GetComponent<SkillConfigComponent>(entity);
                            if (config.ShieldReflect > 0f && takeDamage.ValueRO.SourceEntity != Entity.Null)
                            {
                                float reflectedDamage = absorbedDamage * config.ShieldReflect;
                                ecb.AddComponent(takeDamage.ValueRO.SourceEntity, new TakeDamageComponent
                                {
                                    Amount = reflectedDamage,
                                    SourceEntity = entity,
                                    Element = WeaponElementalType.Physical
                                });
                            }
                        }
                    }
                    SystemAPI.SetComponent(entity, shield);
                }
            }

            // --- ЛОГИКА 1: Урон игрокам/сетевым объектам ---
            foreach (var (takeDamage, healthLink) in SystemAPI.Query<RefRO<TakeDamageComponent>, HealthLinkComponent>())
            {
                if (takeDamage.ValueRO.Amount <= 0f) continue; 

                if (healthLink.Value != null && healthLink.Value.Object != null && healthLink.Value.Object.IsValid)
                {
                    if (healthLink.Value.HasStateAuthority)
                    {
                        healthLink.Value.TakeDamage(takeDamage.ValueRO.Amount);
                    }
                }
            }

            // --- ЛОГИКА 2: Урон врагам, стихии и вызов UI ---
            foreach (var (takeDamage, enemyHealth, transform, entity) in SystemAPI.Query<RefRW<TakeDamageComponent>, RefRW<EnemyHealthComponent>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                float damageAmount = takeDamage.ValueRO.Amount;
                WeaponElementalType element = takeDamage.ValueRO.Element;
                bool isCrit = takeDamage.ValueRO.IsCritical; // ЧИТАЕМ ФЛАГ!
            
                if (damageAmount > 0f)
                {
                    // ВЫЗЫВАЕМ СОБЫТИЕ ДЛЯ UI
                    OnEnemyDamaged?.Invoke(transform.ValueRO.Position, damageAmount, element, isCrit);

                    // === МЕХАНИКА: ХРУПКИЙ ЛЕД ===
                    if (state.EntityManager.HasComponent<FrostVulnerabilityComponent>(entity))
                    {
                        var vuln = state.EntityManager.GetComponentData<FrostVulnerabilityComponent>(entity);
                        damageAmount *= (1.0f + vuln.Multiplier); 
                    }

                    enemyHealth.ValueRW.CurrentHealth -= damageAmount;
                    Entity attacker = takeDamage.ValueRO.SourceEntity;

                    // === НОВОЕ: ЭЛЕМЕНТАЛЬНЫЕ СТАТУСЫ ===
                    if (element == WeaponElementalType.Fire)
                    {
                        // Накладываем статус горения (например, 5 урона каждые 0.5 сек, 6 раз)
                        ecb.AddComponent(entity, new BurningDebuffComponent
                        {
                            Timer = 0.5f,
                            TickRate = 0.5f,
                            DamagePerTick = 5f,
                            TicksRemaining = 6,
                            SourceEntity = attacker
                        });
                    }
                    else if (element == WeaponElementalType.Cryo)
                    {
                        // Заморозка (если у моба еще нет этого компонента)
                        if (!state.EntityManager.HasComponent<CryoDebuffComponent>(entity))
                        {
                            ecb.AddComponent(entity, new CryoDebuffComponent
                            {
                                OriginalSpeed = 1f, // TODO: брать реальную скорость врага
                                Timer = 3f          // Время заморозки
                            });
                        }
                    }
                    
                    // === МЕХАНИКА: ИСТЯЗАНИЕ ===
                    if (attacker != Entity.Null && state.EntityManager.HasComponent<CloneComponent>(attacker))
                    {
                        var cloneComp = state.EntityManager.GetComponentData<CloneComponent>(attacker);
                        if (cloneComp.OwnerEntity != Entity.Null && state.EntityManager.HasComponent<SkillConfigComponent>(cloneComp.OwnerEntity))
                        {
                            var config = state.EntityManager.GetComponentData<SkillConfigComponent>(cloneComp.OwnerEntity);
                            if (config.ClonePoisonDPS > 0f)
                            {
                                ecb.AddComponent(entity, new PoisonDebuffComponent 
                                { 
                                    DPS = config.ClonePoisonDPS,
                                    OwnerEntity = cloneComp.OwnerEntity
                                });
                            }
                        }
                    }

                    // === МЕХАНИКА: КРИО-СНАРЯДЫ ТУРЕЛИ ===
                    if (attacker != Entity.Null && state.EntityManager.HasComponent<TurretComponent>(attacker))
                    {
                        var turretComp = state.EntityManager.GetComponentData<TurretComponent>(attacker);
                        if (turretComp.OwnerEntity != Entity.Null && state.EntityManager.HasComponent<SkillConfigComponent>(turretComp.OwnerEntity))
                        {
                            var config = state.EntityManager.GetComponentData<SkillConfigComponent>(turretComp.OwnerEntity);
                            if (config.TurretCryo)
                            {
                                ecb.AddComponent(entity, new CryoDebuffComponent 
                                { 
                                    OriginalSpeed = turretComp.CryoMultiplier, 
                                    Timer = turretComp.CryoDuration 
                                });
                            }
                        }
                    }
                    
                    // === ААА-МЕХАНИКА: ВАМПИРИЗМ ===
                    if (attacker != Entity.Null && state.EntityManager.HasComponent<SkillConfigComponent>(attacker))
                    {
                        var config = SystemAPI.GetComponent<SkillConfigComponent>(attacker);
                        float totalLifesteal = config.Lifesteal; 

                        if (SystemAPI.HasComponent<HystericFuryStateTag>(attacker))
                        {
                            totalLifesteal += config.FuryLifesteal;
                        }
                        
                        if (totalLifesteal > 0f && state.EntityManager.HasComponent<HealthLinkComponent>(attacker))
                        {
                            var attackerHealthLink = state.EntityManager.GetComponentObject<HealthLinkComponent>(attacker);
                            if (attackerHealthLink.Value != null && attackerHealthLink.Value.HasStateAuthority)
                            {
                                attackerHealthLink.Value.Heal(damageAmount * totalLifesteal);
                            }
                        }
                    }

                    // === СМЕРТЬ ВРАГА И ЭФФЕКТЫ ПРИ СМЕРТИ ===
                    if (enemyHealth.ValueRO.CurrentHealth <= 0)
                    {
                        ecb.AddComponent<DeathTagComponent>(entity);

                        Entity realAttacker = attacker;
                        if (attacker != Entity.Null)
                        {
                            if (state.EntityManager.HasComponent<TurretComponent>(attacker))
                                realAttacker = state.EntityManager.GetComponentData<TurretComponent>(attacker).OwnerEntity;
                            else if (state.EntityManager.HasComponent<CloneComponent>(attacker))
                                realAttacker = state.EntityManager.GetComponentData<CloneComponent>(attacker).OwnerEntity;
                        }

                        if (realAttacker != Entity.Null && state.EntityManager.HasComponent<SkillConfigComponent>(realAttacker))
                        {
                            var config = state.EntityManager.GetComponentData<SkillConfigComponent>(realAttacker);
                            
                            if (config.KillCooldownReduction > 0f && state.EntityManager.HasComponent<SkillStateComponent>(realAttacker))
                            {
                                var skillState = state.EntityManager.GetComponentData<SkillStateComponent>(realAttacker);
                                if (skillState.CurrentCooldown > 0f)
                                {
                                    skillState.CurrentCooldown = Unity.Mathematics.math.max(0f, skillState.CurrentCooldown - config.KillCooldownReduction);
                                    state.EntityManager.SetComponentData(realAttacker, skillState);
                                }
                            }

                            if (config.ShrapnelDeath > 0 && state.EntityManager.HasComponent<ApathyDebuffComponent>(entity))
                            {
                                var apathy = state.EntityManager.GetComponentData<ApathyDebuffComponent>(entity);
                                
                                if (apathy.FreezeTimer > 0f) 
                                {
                                    var deadPos = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                                    
                                    DynamicBuffer<SpawnShrapnelCommand> buffer;
                                    if (!state.EntityManager.HasBuffer<SpawnShrapnelCommand>(realAttacker))
                                        buffer = ecb.AddBuffer<SpawnShrapnelCommand>(realAttacker);
                                    else
                                        buffer = state.EntityManager.GetBuffer<SpawnShrapnelCommand>(realAttacker);

                                    for (int i = 0; i < config.ShrapnelDeath; i++)
                                    {
                                        buffer.Add(new SpawnShrapnelCommand { Position = deadPos, TargetEnemy = Entity.Null });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // --- ОЧИСТКА ---
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>>().WithEntityAccess())
            {
                ecb.RemoveComponent<TakeDamageComponent>(entity);
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}