using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics; // Для float3
using UnityEngine;
using _Project.Scripts.Network;
using _Project.Scripts.ECS.Components;

namespace _Project.Scripts.ECS.Systems
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
                int index = 0;
                
                // Читаем данные (RefRO)
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

                // Зачищаем "хвосты" массива, если врагов стало меньше
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
                int index = 0;
                
                // Изменяем данные (RefRW), так как Клиент должен двигать объекты и менять им ХП
                foreach (var (transform, health) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyHealthComponent>>())
                {
                    if (index >= 256) break;

                    var networkState = swarmManager.EnemyStates[index];

                    if (networkState.IsActive)
                    {
                        // 1. Телепортируем "куклу" точно на координаты Сервера
                        transform.ValueRW.Position = new float3(networkState.Position.x, networkState.Position.y, 0);
                        
                        // 2. Синхронизируем здоровье (чтобы мигание/полоски ХП работали у Клиента)
                        health.ValueRW.CurrentHealth = networkState.Health;
                    }
                    else
                    {
                        // Простой трюк (Object Pooling): если Сервер убил врага, мы прячем "куклу" за пределы экрана
                        transform.ValueRW.Position = new float3(0, 9999, 0);
                    }
                    
                    index++;
                }
            }
        }
    }
}