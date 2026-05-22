using System.Collections.Generic;
using _Project.Scripts.Core;
using Fusion;
using UnityEngine;
using _Project.Scripts.Data;

namespace _Project.Scripts.Network
{
    public class PlayerManager : NetworkBehaviour
    {
        [Header("База данных: Все 4 класса")]
        public ArchetypeData[] availableArchetypes;

        [Header("Текущий класс Безумца")]
        public ArchetypeData currentArchetype;

        public static readonly List<PlayerManager> AllActivePlayers = new();

        public override void Spawned()
        {
            AllActivePlayers.Add(this);

            // 1. Клиент (InputAuthority) читает СВОЙ локальный файл
            if (HasInputAuthority && ProfileController.Instance != null && ProfileController.Instance.CurrentProfile != null)
            {
                var savedClassIndex = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
                var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(savedClassIndex);
                
                var clientLevel = progression?.Level ?? 1; 

                // Отправляем серверу и индекс класса, и текущий уровень
                Rpc_ChangeArchetype(savedClassIndex, clientLevel);
            }
            // 2. Сервер инициализирует базовые статы, если это не управляемый игроком объект
            else if (HasStateAuthority && currentArchetype != null)
            {
                ApplyArchetypeStats(1); // Для ботов или дефолта ставим 1 уровень
            }
        }
        
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            AllActivePlayers.Remove(this);
        }

        // Сервер берет переданный клиентом уровень и применяет формулы
        private void ApplyArchetypeStats(int level)
        {
            var health = GetComponent<Health>();
            if (health != null)
            {
                // ФОРМУЛА ПРОГРЕССИИ: Базовое ХП + (Уровень * Бонус)
                float calculatedHealth = currentArchetype.maxHealth + (level * 10f);
                health.MaxHealth = calculatedHealth;
                health.CurrentHealth = calculatedHealth;
            }

            var movement = GetComponent<PlayerNetworkMovement>();
            if (movement != null)
            {
                // Пример прогрессии скорости: +0.1f за каждый уровень
                movement.speed = currentArchetype.moveSpeed + (level * 0.1f);
            }
            
            var weaponSystem = GetComponent<PlayerWeapon>();
            if (weaponSystem != null && currentArchetype.defaultWeapon != null)
            {
                for (var i = 0; i < weaponSystem.equippedWeapons.Length; i++)
                {
                    weaponSystem.equippedWeapons[i] = null;
                }

                for (var i = 0; i < currentArchetype.weaponSlotsCount; i++)
                {
                    if (i < weaponSystem.equippedWeapons.Length)
                    {
                        weaponSystem.equippedWeapons[i] = currentArchetype.defaultWeapon;
                    }
                }

                weaponSystem.ValidateWeapons();
            }
            
            Debug.Log($"[Сервер] Игрок {Object.Id} загружен как {currentArchetype.archetypeName} (Уровень: {level})");
        }

        // Обновленный RPC: принимает уровень
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_ChangeArchetype(int classIndex, int clientLevel)
        {
            if (classIndex < 0 || classIndex >= availableArchetypes.Length) return;
            
            currentArchetype = availableArchetypes[classIndex];
            
            // Вызываем применение статов с учетом уровня!
            ApplyArchetypeStats(clientLevel); 
        }
    }
}