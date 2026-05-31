using Unity.Entities;
using UnityEngine; // Добавлено для дебаг логов
using _Project.Scripts.ECS.Components;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct DamageSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =======================================================================
            // 0. ПЕРЕХВАТ УРОНА ЩИТАМИ (Работает для ЛЮБОЙ сущности с щитом)
            // =======================================================================
            foreach (var (takeDamage, entity) in SystemAPI.Query<RefRW<TakeDamageComponent>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<EnergyShieldComponent>(entity))
                {
                    var shield = SystemAPI.GetComponent<EnergyShieldComponent>(entity);
                    
                    // Сбрасываем таймер "Вне боя" при любом попадании
                    shield.OutOfCombatTimer = 0f;

                    if (shield.CurrentShield > 0f)
                    {
                        float incomingDamage = takeDamage.ValueRO.Amount;
                        float absorbedDamage = 0f;

                        if (shield.CurrentShield >= incomingDamage)
                        {
                            // Щит выдержал весь урон
                            shield.CurrentShield -= incomingDamage;
                            absorbedDamage = incomingDamage;
                            takeDamage.ValueRW.Amount = 0f; // Обнуляем урон
                            
                            Debug.Log($"<color=#00FFFF>[ЩИТ]</color> Урон {incomingDamage} полностью поглощен! Остаток щита: {shield.CurrentShield}");
                        }
                        else
                        {
                            // Щит пробит!
                            absorbedDamage = shield.CurrentShield;
                            takeDamage.ValueRW.Amount -= shield.CurrentShield;
                            shield.CurrentShield = 0f;
                            
                            Debug.Log($"<color=#00FFFF>[ЩИТ ПРОБИТ]</color> Поглощено {absorbedDamage} урона. В здоровье прошло: {takeDamage.ValueRO.Amount}");
                        }

                        // === МЕХАНИКА: ШИПОВАННЫЙ БАРЬЕР (ВОЗВРАТ УРОНА) ===
                        if (SystemAPI.HasComponent<SkillConfigComponent>(entity))
                        {
                            var config = SystemAPI.GetComponent<SkillConfigComponent>(entity);
                            if (config.ShieldReflect > 0f && takeDamage.ValueRO.SourceEntity != Entity.Null)
                            {
                                float reflectedDamage = absorbedDamage * config.ShieldReflect;
                                
                                // Создаем новую команду урона для атакующего
                                ecb.AddComponent(takeDamage.ValueRO.SourceEntity, new TakeDamageComponent
                                {
                                    Amount = reflectedDamage,
                                    SourceEntity = entity // Источник - владелец щита
                                });
                                
                                Debug.Log($"<color=#DA70D6>[ШИПОВАННЫЙ БАРЬЕР]</color> Отражено {reflectedDamage} урона обратно во врага!");
                            }
                        }
                    }
                    
                    SystemAPI.SetComponent(entity, shield);
                }
            }

            // --- ЛОГИКА 1: Урон игрокам/сетевым объектам ---
            foreach (var (takeDamage, healthLink) in SystemAPI.Query<RefRO<TakeDamageComponent>, HealthLinkComponent>())
            {
                // ИГНОРИРУЕМ, ЕСЛИ УРОН БЫЛ ПОГЛОЩЕН ЩИТОМ
                if (takeDamage.ValueRO.Amount <= 0f) continue; 

                if (healthLink.Value != null && healthLink.Value.Object != null && healthLink.Value.Object.IsValid)
                {
                    if (healthLink.Value.HasStateAuthority)
                    {
                        healthLink.Value.TakeDamage(takeDamage.ValueRO.Amount);
                    }
                }
            }

            // --- ЛОГИКА 2: Урон врагам и ВАМПИРИЗМ ---
            foreach (var (takeDamage, enemyHealth, entity) in SystemAPI.Query<RefRO<TakeDamageComponent>, RefRW<EnemyHealthComponent>>().WithEntityAccess())
            {
                float damageAmount = takeDamage.ValueRO.Amount;
                
                if (damageAmount > 0f)
                {
                    // === МЕХАНИКА: ХРУПКИЙ ЛЕД ===
                    if (state.EntityManager.HasComponent<FrostVulnerabilityComponent>(entity))
                    {
                        var vuln = state.EntityManager.GetComponentData<FrostVulnerabilityComponent>(entity);
                        damageAmount *= (1.0f + vuln.Multiplier); // Урон увеличивается на 50%
                    }

                    enemyHealth.ValueRW.CurrentHealth -= damageAmount;
                    Entity attacker = takeDamage.ValueRO.SourceEntity;
                    
                    // === МЕХАНИКА: ИСТЯЗАНИЕ (Ядовитые пули клона) ===
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
                                    SpeedMultiplier = turretComp.CryoMultiplier, 
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
                            
                            // 1. КРОВАВАЯ ЖАТВА (ШИЗОИД)
                            if (config.KillCooldownReduction > 0f && state.EntityManager.HasComponent<SkillStateComponent>(realAttacker))
                            {
                                var skillState = state.EntityManager.GetComponentData<SkillStateComponent>(realAttacker);
                                if (skillState.CurrentCooldown > 0f)
                                {
                                    skillState.CurrentCooldown = Unity.Mathematics.math.max(0f, skillState.CurrentCooldown - config.KillCooldownReduction);
                                    state.EntityManager.SetComponentData(realAttacker, skillState);
                                }
                            }

                            // 2. ОСКОЛОЧНЫЙ ВЗРЫВ (МЕЛАНХОЛИК)
                            if (config.ShrapnelDeath > 0 && state.EntityManager.HasComponent<ApathyDebuffComponent>(entity))
                            {
                                var apathy = state.EntityManager.GetComponentData<ApathyDebuffComponent>(entity);
                                
                                // Проверяем, что враг был ПОЛНОСТЬЮ заморожен в момент смерти
                                if (apathy.FreezeTimer > 0f) 
                                {
                                    var deadPos = state.EntityManager.GetComponentData<LocalTransform>(entity).Position;
                                    
                                    // Добавляем команду в буфер стрелка
                                    DynamicBuffer<SpawnShrapnelCommand> buffer;
                                    if (!state.EntityManager.HasBuffer<SpawnShrapnelCommand>(realAttacker))
                                        buffer = ecb.AddBuffer<SpawnShrapnelCommand>(realAttacker);
                                    else
                                        buffer = state.EntityManager.GetBuffer<SpawnShrapnelCommand>(realAttacker);

                                    // Добавляем N осколков в очередь на спавн
                                    for (int i = 0; i < config.ShrapnelDeath; i++)
                                    {
                                        buffer.Add(new SpawnShrapnelCommand { Position = deadPos, TargetEnemy = Entity.Null });
                                    }
                                    
                                    Debug.Log($"<color=#E0FFFF>[ОСКОЛОЧНЫЙ ВЗРЫВ]</color> Моб разорван на {config.ShrapnelDeath} льдин(ы)!");
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