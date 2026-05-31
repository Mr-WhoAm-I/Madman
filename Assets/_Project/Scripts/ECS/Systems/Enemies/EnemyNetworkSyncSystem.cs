using _Project.Scripts.ECS.Authoring;
using _Project.Scripts.ECS.Components.Enemies;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Gameplay;
using _Project.Scripts.Network.Managers;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Обязательно для EnemyPrefabElement

namespace _Project.Scripts.ECS.Systems.Enemies
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyNetworkSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            if (EnemySwarmManager.Instance == null || EnemySwarmManager.Instance.Object == null || !EnemySwarmManager.Instance.Object.IsValid)
                return;

            var swarmManager = EnemySwarmManager.Instance;

            // ---------------------------------------------------------
            // ЛОГИКА СЕРВЕРА (ХОСТА): Упаковка врагов в сетевой массив
            // ---------------------------------------------------------
            if (swarmManager.HasStateAuthority)
            {
                var index = 0;
                
                foreach (var (transform, health) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyHealthComponent>>())
                {
                    if (index >= 256) break;

                    swarmManager.EnemyStates.Set(index, new EnemyNetworkState
                    {
                        IsActive = true,
                        Position = new Vector2(transform.ValueRO.Position.x, transform.ValueRO.Position.y),
                        Health = health.ValueRO.CurrentHealth
                    });
                    
                    index++;
                }

                for (var i = index; i < 256; i++)
                {
                    if (swarmManager.EnemyStates[i].IsActive)
                        swarmManager.EnemyStates.Set(i, new EnemyNetworkState { IsActive = false });
                }
            }
            // ---------------------------------------------------------
            // ЛОГИКА КЛИЕНТА: Покорная отрисовка данных Сервера
            // ---------------------------------------------------------
            else
            {
                var count = 0;
                // Считаем сколько врагов (кукол) уже есть в памяти клиента
                foreach (var _ in SystemAPI.Query<RefRO<EnemyTagComponent>>()) 
                    count++;

                // Если массив кукол еще не заполнен - создаем их из Реестра!
                if (count < 256)
                {
                    var registryQuery = SystemAPI.QueryBuilder().WithAll<EnemyPrefabElement>().Build();
                    if (registryQuery.TryGetSingletonEntity<EnemyPrefabElement>(out var registryEntity))
                    {
                        var buffer = state.EntityManager.GetBuffer<EnemyPrefabElement>(registryEntity);
                        if (buffer.Length > 0)
                        {
                            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
                            var prefab = buffer[0].PrefabEntity; // Берем первый попавшийся префаб для визуала куклы
                            
                            for (var i = count; i < 256; i++)
                            {
                                var newEnemy = ecb.Instantiate(prefab);
                                // Прячем за экран
                                ecb.SetComponent(newEnemy, LocalTransform.FromPosition(new float3(0, 9999, 0)));
                            }
                            ecb.Playback(state.EntityManager);
                            ecb.Dispose();
                        }
                    }
                }

                var index = 0;
                // Синхронизируем готовые куклы с координатами Сервера
                foreach (var (transform, health) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyHealthComponent>>())
                {
                    if (index >= 256) break;

                    var networkState = swarmManager.EnemyStates[index];

                    if (networkState.IsActive)
                    {
                        transform.ValueRW.Position = new float3(networkState.Position.x, networkState.Position.y, 0);
                        health.ValueRW.CurrentHealth = networkState.Health;
                    }
                    else
                    {
                        transform.ValueRW.Position = new float3(0, 9999, 0);
                    }
                    
                    index++;
                }
            }
        }
    }
}