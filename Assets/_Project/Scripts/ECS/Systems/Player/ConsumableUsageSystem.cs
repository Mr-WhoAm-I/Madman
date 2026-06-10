using _Project.Scripts.Data.Shop;
using _Project.Scripts.ECS.Components.Combat;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Player;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network.Core;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Project.Scripts.ECS.Systems.Player
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial class ConsumableUsageSystem : SystemBase // Используем SystemBase для доступа к Managed-компонентам
    {
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent)) return;
            float deltaTime = timeComponent.DeltaTime;
            
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (input, inventory, entity) in SystemAPI.Query<RefRO<PlayerInputComponent>, RefRW<ConsumableInventoryComponent>>().WithEntityAccess())
            {
                // 1. Охлаждение кулдаунов
                if (inventory.ValueRO.Slot1.CurrentCooldown > 0f) inventory.ValueRW.Slot1.CurrentCooldown -= deltaTime;
                if (inventory.ValueRO.Slot2.CurrentCooldown > 0f) inventory.ValueRW.Slot2.CurrentCooldown -= deltaTime;

                // 2. Проверка нажатия клавиш (Сравниваем текущий кадр с предыдущим, чтобы не выпить всё зажатой кнопкой)
                bool useSlot1 = input.ValueRO.Buttons.IsSet(PlayerInputButtons.UseConsumable1) && !input.ValueRO.PreviousButtons.IsSet(PlayerInputButtons.UseConsumable1);
                bool useSlot2 = input.ValueRO.Buttons.IsSet(PlayerInputButtons.UseConsumable2) && !input.ValueRO.PreviousButtons.IsSet(PlayerInputButtons.UseConsumable2);

                // 3. Попытка применения
                if (useSlot1) TryUseConsumable(ref inventory.ValueRW.Slot1, entity, EntityManager, ecb);
                if (useSlot2) TryUseConsumable(ref inventory.ValueRW.Slot2, entity, EntityManager, ecb);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        private void TryUseConsumable(ref ConsumableSlot slot, Entity entity, EntityManager em, EntityCommandBuffer ecb)
        {
            if (slot.IsEmpty || slot.CurrentCharges <= 0 || slot.CurrentCooldown > 0f) return;

            // Списываем заряд и вешаем кулдаун
            slot.CurrentCharges--;
            slot.CurrentCooldown = slot.MaxCooldown;

            // ПРИМЕНЕНИЕ ЭФФЕКТОВ
            switch (slot.Type)
            {
                case ConsumableType.Heal:
                    // Используем ManagedAPI для доступа к классу HealthLinkComponent
                    if (em.HasComponent<HealthLinkComponent>(entity))
                    {
                        var healthLink = em.GetComponentObject<HealthLinkComponent>(entity);
                        if (healthLink.Value != null && healthLink.Value.HasStateAuthority)
                        {
                            healthLink.Value.Heal(slot.Power);
                        }
                    }
                    break;

                case ConsumableType.Mana:
                    if (em.HasComponent<ManaComponent>(entity) && em.HasComponent<SkillConfigComponent>(entity))
                    {
                        var mana = em.GetComponentData<ManaComponent>(entity);
                        var config = em.GetComponentData<SkillConfigComponent>(entity);
                        mana.CurrentMana = math.min(config.BaseMaxMana, mana.CurrentMana + slot.Power);
                        ecb.SetComponent(entity, mana);
                        Debug.Log($"<color=#00BFFF>[МАНА]</color> Восстановлено {slot.Power} маны!");
                    }
                    break;

                case ConsumableType.Shield:
                    // Если щита нет вообще
                    if (!em.HasComponent<EnergyShieldComponent>(entity))
                    {
                        ecb.AddComponent(entity, new EnergyShieldComponent { MaxShield = slot.Power, CurrentShield = slot.Power, OutOfCombatTimer = 0f });
                        ecb.AddComponent<PermanentShieldTag>(entity);
                    }
                    else
                    {
                        var shield = em.GetComponentData<EnergyShieldComponent>(entity);
                        shield.MaxShield += slot.Power; // Увеличиваем капу
                        shield.CurrentShield = math.min(shield.MaxShield, shield.CurrentShield + slot.Power);
                        ecb.SetComponent(entity, shield);
                        
                        // Если щит был от ауры, делаем его перманентным
                        if (em.HasComponent<AuraShieldTag>(entity)) ecb.RemoveComponent<AuraShieldTag>(entity);
                        ecb.AddComponent<PermanentShieldTag>(entity);
                    }
                    Debug.Log($"<color=#00FA9A>[ЩИТ]</color> Активирован энергобарьер на {slot.Power} ед.!");
                    break;

                case ConsumableType.FuryBuff:
                    // Стандартный подход: вешаем компонент с таймером
                    ecb.AddComponent(entity, new ActiveBuffComponent 
                    { 
                        BuffType = 0, // 0 = Ярость
                        Power = slot.Power, 
                        Timer = 10f // Длительность баффа (можно вынести в ConsumableData)
                    });
                    Debug.Log($"<color=#FF4500>[БАФФ]</color> Получен заряд Ярости!");
                    break;
            }
        }
    }
}