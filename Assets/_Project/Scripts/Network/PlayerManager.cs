using System.Collections.Generic;
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
            if (currentArchetype == null) return;

            if (HasStateAuthority)
            {
                ApplyArchetypeStats();
            }
        }
        
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // Когда игрок выходит или умирает, убираем его из списка
            AllActivePlayers.Remove(this);
        
        }

        private void ApplyArchetypeStats()
        {
            var health = GetComponent<Health>();
            if (health != null)
            {
                health.MaxHealth = currentArchetype.maxHealth;
                health.CurrentHealth = currentArchetype.maxHealth;
            }

            var movement = GetComponent<PlayerNetworkMovement>();
            if (movement != null)
            {
                movement.speed = currentArchetype.moveSpeed;
            }
            
            var weaponSystem = GetComponent<PlayerWeapon>();
            if (weaponSystem != null && currentArchetype.defaultWeapon != null)
            {
                // 1. БЕЗОПАСНАЯ ОЧИСТКА: проходимся только по существующим ячейкам
                for (var i = 0; i < weaponSystem.equippedWeapons.Length; i++)
                {
                    weaponSystem.equippedWeapons[i] = null;
                }

                // 2. Заполняем разрешенные слоты дефолтным оружием
                for (var i = 0; i < currentArchetype.weaponSlotsCount; i++)
                {
                    if (i < weaponSystem.equippedWeapons.Length)
                    {
                        weaponSystem.equippedWeapons[i] = currentArchetype.defaultWeapon;
                    }
                }

                // Запускаем валидацию для надежности
                weaponSystem.ValidateWeapons();
            }
            
            Debug.Log($"[Сервер] Игрок загружен как {currentArchetype.archetypeName}");
        }

        // НОВЫЙ МЕТОД: Клиент просит сервер сменить класс (RPC)
        // RpcSources.InputAuthority - кто вызывает (наш клиент)
        // RpcTargets.StateAuthority - кто исполняет (сервер)
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void Rpc_ChangeArchetype(int classIndex)
        {
            if (classIndex < 0 || classIndex >= availableArchetypes.Length) return;
            currentArchetype = availableArchetypes[classIndex];
            ApplyArchetypeStats(); // Сервер меняет ХП и скорость
            Debug.Log($"[Сервер] Игрок сменил класс на {currentArchetype.archetypeName}");
        }
    }
}