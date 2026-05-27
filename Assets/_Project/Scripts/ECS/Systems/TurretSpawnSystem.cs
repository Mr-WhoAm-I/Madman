using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Fusion;
using System.Linq;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.Network;
using _Project.Scripts.Core;
using _Project.Scripts.Data;

namespace _Project.Scripts.ECS.Systems
{
    [UpdateInGroup(typeof(FusionUpdateGroup))]
    public partial class TurretSpawnSystem : SystemBase
    {
        // Вспомогательная структура для отложенного спавна
        private struct TurretSpawnData
        {
            public TurretSkillData SkillData;
            public float3 Position;
            public PlayerRef Owner;
        }

        protected override void OnUpdate()
        {
            if (NetworkRunner.Instances.Count == 0) return;
            var runner = NetworkRunner.Instances[0];

            if (!runner.IsServer) return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            var spawnList = new List<TurretSpawnData>();

            // 1. СОБИРАЕМ ЗАПРОСЫ В ЦИКЛЕ ECS
            foreach (var (request, entity) in SystemAPI.Query<RefRO<SpawnTurretRequest>>().WithEntityAccess())
            {
                var archetypeData = ProfileController.Instance.GetArchetypeAsset(request.ValueRO.ArchetypeID);
                
                if (archetypeData != null && archetypeData.activeSkillData is TurretSkillData turretData && turretData.turretPrefab.IsValid)
                {
                    // Добавляем в отложенный список
                    spawnList.Add(new TurretSpawnData
                    {
                        SkillData = turretData,
                        Position = request.ValueRO.Position,
                        Owner = request.ValueRO.Owner
                    });
                }

                // Запрос обработан — ставим в очередь на удаление
                ecb.DestroyEntity(entity);
            }

            // 2. ЗАКРЫВАЕМ СТРУКТУРНЫЕ ИЗМЕНЕНИЯ ECS
            ecb.Playback(EntityManager);
            ecb.Dispose();

            // 3. БЕЗОПАСНО ВЫЗЫВАЕМ FUSION (Вне цикла ECS)
            foreach (var spawnReq in spawnList)
            {
                // Ищем существующие турели этого игрока
                var playerTurrets = TurretNetworkBridge.ActiveTurrets
                    .Where(t => t.OwnerPlayer == spawnReq.Owner)
                    .ToList();

                // Если турелей больше или равно лимиту, уничтожаем самую старую (первую в списке)
                if (playerTurrets.Count >= spawnReq.SkillData.maxTurrets)
                {
                    var oldestTurret = playerTurrets[0];
                    runner.Despawn(oldestTurret.Object);
                    // Важно: Despawned() вызовется тут же и сам удалит турель из ActiveTurrets
                }

                // Спавним новую турель
                runner.Spawn(spawnReq.SkillData.turretPrefab, spawnReq.Position, UnityEngine.Quaternion.identity, spawnReq.Owner, (r, obj) => {
                    var bridge = obj.GetComponent<TurretNetworkBridge>();
                    if (bridge != null)
                    {
                        bridge.Initialize(spawnReq.Owner, spawnReq.SkillData);
                    }
                });
            }
        }
    }
}