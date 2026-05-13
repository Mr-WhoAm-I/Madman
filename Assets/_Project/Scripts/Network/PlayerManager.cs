using Fusion;
using UnityEngine;
using _Project.Scripts.Data;
using UnityEngine.Serialization; // Подключаем наши Scriptable Objects

namespace _Project.Scripts.Network
{
    public class PlayerManager : NetworkBehaviour
    {
        [Header("Текущий класс Безумца")]
        public ArchetypeData currentArchetype;

        public override void Spawned()
        {
            // Убеждаемся, что файл класса назначен
            if (currentArchetype == null)
            {
                Debug.LogError("На игроке не назначен ArchetypeData!");
                return;
            }

            // Настройку производим только на Сервере (Хосте), чтобы не было рассинхрона
            if (HasStateAuthority)
            {
                ApplyArchetypeStats();
            }
        }

        private void ApplyArchetypeStats()
        {
            // 1. Применяем Здоровье
            var health = GetComponent<Health>();
            if (health != null)
            {
                // Присваиваем стартовые значения из файла
                health.MaxHealth = currentArchetype.maxHealth;
                health.CurrentHealth = currentArchetype.maxHealth;
            }

            // 2. Применяем Скорость (у нас скрипт движения висит на GameObject)
            var movement = GetComponent<PlayerNetworkMovement>();
            if (movement != null)
            {
                movement.speed = currentArchetype.moveSpeed;
            }
            
            Debug.Log($"[Сервер] Игрок загружен как {currentArchetype.archetypeName}. HP: {health.MaxHealth}, Скорость: {movement.speed}");
        }
    }
}