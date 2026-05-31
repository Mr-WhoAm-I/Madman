using _Project.Scripts.ECS.Components.Classes;
using _Project.Scripts.ECS.Components.Core;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Bridges;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace _Project.Scripts.ECS.Systems.Player
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial struct LootMagnetSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Убеждаемся, что локальный игрок существует в мире
            if (PlayerNetworkBridge.LocalPlayer == null || !PlayerNetworkBridge.LocalPlayer.Object.IsValid) 
                return;

            if (!SystemAPI.TryGetSingleton<NetworkTimeComponent>(out var timeComponent))
                return;

            float deltaTime = timeComponent.DeltaTime;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            // 1. ИЩЕМ СУЩНОСТЬ ЛОКАЛЬНОГО ИГРОКА (Только он подбирает этот лут)
            Entity localPlayerEntity = Entity.Null;
            float3 localPlayerPos = float3.zero;

            foreach (var (transform, playerOwner, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerOwnerComponent>>().WithEntityAccess())
            {
                if (playerOwner.ValueRO.Player == PlayerNetworkBridge.LocalPlayer.Object.InputAuthority)
                {
                    localPlayerEntity = entity;
                    localPlayerPos = transform.ValueRO.Position;
                    break;
                }
            }

            if (localPlayerEntity == Entity.Null)
            {
                ecb.Dispose();
                return;
            }

            // === ЧИТАЕМ ПРОКАЧАННЫЙ РАДИУС МАГНИТА ===
            float pickupRadius = 4f; // Значение по умолчанию
            if (SystemAPI.HasComponent<SkillConfigComponent>(localPlayerEntity))
            {
                var config = SystemAPI.GetComponent<SkillConfigComponent>(localPlayerEntity);
                // Базовый радиус (4f) + все купленные бонусы из магазина
                pickupRadius = 4f + config.MagnetRadius; 
            }

            // 2. ОБРАБАТЫВАЕМ ВЕСЬ ЛУТ НА АРЕНЕ
            foreach (var (transform, loot, magnet, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<LootComponent>, RefRW<MagnetStateComponent>>().WithEntityAccess())
            {
                float dist = math.distance(transform.ValueRO.Position, localPlayerPos);

                // Если лут еще не притянут, проверяем радиус
                if (!magnet.ValueRO.IsPulled)
                {
                    if (dist <= pickupRadius)
                    {
                        magnet.ValueRW.IsPulled = true;
                        magnet.ValueRW.TargetEntity = localPlayerEntity;
                    }
                }
                // Если лут в процессе притяжения — двигаем его к игроку
                else
                {
                    // Проверка дистанции сбора (столкновение)
                    if (dist < 0.5f)
                    {
                        // Подбираем! Отправляем RPC на сервер
                        PlayerNetworkBridge.LocalPlayer.Rpc_AddCurrency(loot.ValueRO.Value);
                        
                        // Удаляем ECS-сущность лута с экрана
                        ecb.DestroyEntity(entity);
                    }
                    else
                    {
                        // Магнитный полет к игроку
                        float3 dir = math.normalize(localPlayerPos - transform.ValueRO.Position);
                        float magnetSpeed = 15f; // Осколок летит быстро
                        transform.ValueRW.Position += dir * magnetSpeed * deltaTime;
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}