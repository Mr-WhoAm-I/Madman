using Unity.Entities;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;

namespace _Project.Scripts.ECS.Systems
{
    // Эта система срабатывает в самом конце сетевого тика Fusion, сохраняя результаты в сеть
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    [UpdateAfter(typeof(HystericSkillSystem))]
    [UpdateAfter(typeof(ParanoiacSkillSystem))]
    [UpdateAfter(typeof(SchizoidActiveSkillSystem))]
    [UpdateAfter(typeof(SkillCooldownSystem))]
    public partial class PlayerNetworkPushSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // Используем SystemBase и Query с управляемым компонентом PlayerBridgeReference
            foreach (var (skillState, bridgeRef, entity) in 
                     SystemAPI.Query<RefRO<SkillStateComponent>, PlayerBridgeReference>().WithAll<PlayerTag>().WithEntityAccess())
            {
                var bridge = bridgeRef.Bridge;
                if (bridge == null) continue;

                // =========================================================================
                // ФАЗА ПУША: ECS -> СЕТЬ (Запись вычислений ECS во Fusion свойства)
                // =========================================================================

                // 1. Сохраняем измененные кулдауны
                bridge.NetworkCurrentCooldown = skillState.ValueRO.CurrentCooldown;
                bridge.NetworkMaxCooldown = skillState.ValueRO.MaxCooldown;
                bridge.NetworkCurrentCharges = skillState.ValueRO.CurrentCharges;
                bridge.NetworkMaxCharges = skillState.ValueRO.MaxCharges;

                // 2. Сохраняем состояние рывка
                if (SystemAPI.HasComponent<DashComponent>(entity))
                {
                    var dashComponent = SystemAPI.GetComponent<DashComponent>(entity);
                    
                    bridge.NetworkIsDashing = true;
                    bridge.NetworkDashDirection = dashComponent.Direction;
                    bridge.NetworkDashSpeed = dashComponent.Speed;
                    bridge.NetworkDashTimeLeft = dashComponent.TimeLeft;
                }
                else
                {
                    bridge.NetworkIsDashing = false;
                    bridge.NetworkDashDirection = Unity.Mathematics.float2.zero;
                    bridge.NetworkDashSpeed = 0f;
                    bridge.NetworkDashTimeLeft = 0f;
                }
            }
        }
    }
}