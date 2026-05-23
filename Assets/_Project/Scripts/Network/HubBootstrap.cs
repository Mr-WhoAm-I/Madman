using UnityEngine;

namespace _Project.Scripts.Network
{
    public class HubBootstrap : MonoBehaviour
    {
        private async void Start()
        {
            // Ждем один кадр, чтобы HUDManager и ProfileController успели сделать Awake
            await System.Threading.Tasks.Task.Yield();

            if (NetworkManager.Instance != null)
            {
                Debug.Log("[HubBootstrap] Автоматический запуск Хаба в соло-режиме...");
                
                // По умолчанию запускаем одиночный оффлайн-режим
                await NetworkManager.Instance.StartNetworkSession(NetworkGameMode.Solo);
            }
            else
            {
                Debug.LogError("[HubBootstrap] Ошибка: На сцене не найден NetworkManager!");
            }
        }
    }
}