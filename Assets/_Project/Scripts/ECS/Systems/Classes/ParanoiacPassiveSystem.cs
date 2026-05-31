using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Player;
using _Project.Scripts.ECS.Components.Skills;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems.Classes
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct ParanoiacPassiveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // =======================================================================
            // 1. РЕГЕНЕРАЦИЯ ЩИТОВ (Для всех, у кого есть EnergyShieldComponent)
            // =======================================================================
            foreach (var (shield, entity) in SystemAPI.Query<RefRW<EnergyShieldComponent>>().WithEntityAccess())
            {
                // Регенерируем только если щит не полный
                if (shield.ValueRO.CurrentShield < shield.ValueRO.MaxShield)
                {
                    shield.ValueRW.OutOfCombatTimer += deltaTime;
                    
                    // Если сущность - Параноик, берем таймер из его конфига, иначе стандартные 5 сек
                    float requiredRechargeTime = 5f;
                    if (SystemAPI.HasComponent<SkillConfigComponent>(entity))
                    {
                        requiredRechargeTime = SystemAPI.GetComponent<SkillConfigComponent>(entity).ShieldRechargeTime;
                    }

                    if (shield.ValueRO.OutOfCombatTimer >= requiredRechargeTime)
                    {
                        shield.ValueRW.CurrentShield = shield.ValueRO.MaxShield;
                        Debug.Log($"<color=#00FA9A>[ЩИТ]</color> Сущность {entity.Index} восстановила щит ({shield.ValueRO.MaxShield})!");
                    }
                }
            }

            // =======================================================================
            // 2. КОМАНДНАЯ АУРА ПАРАНОИКА (Растягивание барьера на союзников)
            // =======================================================================
            
            // Собираем всех Параноиков на карте
            var paranoiacQuery = SystemAPI.QueryBuilder().WithAll<ParanoiacTag, EnergyShieldComponent, LocalTransform, SkillConfigComponent>().Build();
            if (paranoiacQuery.IsEmpty) 
            {
                ecb.Dispose();
                return; 
            }
            
            var paranoiacEntities = paranoiacQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            var paranoiacTransforms = paranoiacQuery.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            var paranoiacConfigs = paranoiacQuery.ToComponentDataArray<SkillConfigComponent>(Unity.Collections.Allocator.Temp);
            var paranoiacShields = paranoiacQuery.ToComponentDataArray<EnergyShieldComponent>(Unity.Collections.Allocator.Temp);

            // Проверяем остальных игроков
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>().WithNone<ParanoiacTag>().WithEntityAccess())
            {
                bool inAura = false;
                float bonusMaxShield = 0f;

                // Ищем, есть ли рядом хоть один Параноик
                for (int i = 0; i < paranoiacEntities.Length; i++)
                {
                    float dist = math.distance(transform.ValueRO.Position, paranoiacTransforms[i].Position);
                    
                    if (dist <= paranoiacConfigs[i].ShieldAuraRadius)
                    {
                        inAura = true;
                        // Союзник получает 50% от максимального щита Параноика
                        bonusMaxShield = paranoiacShields[i].MaxShield * 0.5f; 
                        break; // Достаточно одного Параноика
                    }
                }

                bool hasShield = SystemAPI.HasComponent<EnergyShieldComponent>(entity);

                if (inAura && !hasShield)
                {
                    // Вошел в ауру — выдаем щит!
                    ecb.AddComponent(entity, new EnergyShieldComponent 
                    { 
                        MaxShield = bonusMaxShield, 
                        CurrentShield = bonusMaxShield, 
                        OutOfCombatTimer = 0f 
                    });
                    Debug.Log($"<color=#1E90FF>[АУРА]</color> Игрок {entity.Index} вошел в ауру Параноика и получил щит {bonusMaxShield}!");
                }
                else if (!inAura && hasShield)
                {
                    // Вышел из ауры — забираем щит
                    ecb.RemoveComponent<EnergyShieldComponent>(entity);
                    Debug.Log($"<color=#4682B4>[АУРА]</color> Игрок {entity.Index} покинул ауру Параноика. Щит деактивирован.");
                }
            }

            paranoiacEntities.Dispose();
            paranoiacTransforms.Dispose();
            paranoiacConfigs.Dispose();
            paranoiacShields.Dispose();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}