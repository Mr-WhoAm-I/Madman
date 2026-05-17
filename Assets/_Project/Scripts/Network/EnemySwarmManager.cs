using Fusion;
using UnityEngine;

namespace _Project.Scripts.Network
{
    public class EnemySwarmManager : NetworkBehaviour
    {
        public static EnemySwarmManager Instance;

        // Создаем сетевой массив на 256 врагов. 
        // [Networked] означает, что Photon сам будет синхронизировать его с Сервера на Клиенты.
        [Networked, Capacity(256)]
        public NetworkArray<EnemyNetworkState> EnemyStates { get; }

        private void Awake()
        {
            Instance = this;
        }

        public override void Spawned()
        {
            Debug.Log(HasStateAuthority
                ? "[Сервер] Менеджер Роя запущен. Готов к синхронизации врагов."
                : "[Клиент] Менеджер Роя подключен. Жду координаты от сервера.");
        }
    }
}