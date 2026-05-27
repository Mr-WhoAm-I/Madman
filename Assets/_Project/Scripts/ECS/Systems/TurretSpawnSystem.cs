using Unity.Entities;
using Fusion;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;
using _Project.Scripts.Core;

namespace _Project.Scripts.ECS.Systems
{
    // Эта система работает только на сервере и переносит запросы из ECS в мир GameObject'ов
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial class TurretSpawnSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (NetworkRunner.Instances.Count == 0) return;
            var runner = NetworkRunner.Instances[0];

            // Спавнить NetworkObject'ы имеет право только Сервер/Хост
            if (!runner.IsServer) return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (request, entity) in SystemAPI.Query<RefRO<SpawnTurretRequest>>().WithEntityAccess())
            {
                var archetypeData = ProfileController.Instance.GetArchetypeAsset(request.ValueRO.ArchetypeID);
                if (archetypeData != null && archetypeData.turretPrefab.IsValid)
                {
                    // Используем OnBeforeSpawned коллбэк Fusion для безопасной передачи ID до вызова Spawned()
                    runner.Spawn(archetypeData.turretPrefab, request.ValueRO.Position, UnityEngine.Quaternion.identity, null, (r, obj) => {
                        var bridge = obj.GetComponent<TurretNetworkBridge>();
                        if (bridge != null)
                        {
                            bridge.OwnerArchetypeID = request.ValueRO.ArchetypeID;
                        }
                    });
                }

                // Запрос обработан — удаляем его
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}