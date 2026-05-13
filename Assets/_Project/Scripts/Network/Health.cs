using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    public class Health : NetworkBehaviour
    {
        [Networked] public float CurrentHealth { get; set; } = 100f;
        [Networked] public float MaxHealth { get; set; } = 100f;
        
        // НОВЫЙ ФЛАГ: Синхронизируется по сети, чтобы все знали, что этот игрок мертв
        [Networked] public NetworkBool IsDead { get; set; }

        public void TakeDamage(float damage)
        {
            // Урон проходит только если командует Сервер и игрок еще жив
            if (!HasStateAuthority || IsDead) return;
            CurrentHealth -= damage;
            Debug.Log($"[Сервер] Получен урон: {damage}. Текущее здоровье: {CurrentHealth}");

            if (!(CurrentHealth <= 0f)) return;
            IsDead = true;
            // Сервер вызывает этот метод, но он сработает у ВСЕХ игроков в сессии
            Rpc_OnPlayerDeath();
        }

        // Атрибут RPC: Источник - Сервер, Цель - Все клиенты
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_OnPlayerDeath()
        {
            // 1. Прячем кубик (отключаем отрисовку)
            if (TryGetComponent<SpriteRenderer>(out var sprite)) sprite.enabled = false;
            
            // 2. Отключаем физику, чтобы враги проходили насквозь
            if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;

            // 3. Выводим сообщение конкретно тому игроку, чей это кубик
            if (HasInputAuthority)
            {
                Debug.LogWarning("💀 ВЫ ПОГИБЛИ! Переход в режим наблюдателя...");
                // Позже здесь мы вызовем UI-окно "Game Over"
            }
            else
            {
                Debug.Log($"Игрок {Object.InputAuthority} погиб.");
            }
        }
    }
}