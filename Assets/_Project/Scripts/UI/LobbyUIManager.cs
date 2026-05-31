using UnityEngine;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Managers;

namespace _Project.Scripts.UI
{
    public class LobbyUIManager : MonoBehaviour
    {
        public void OnCreateOnlineHostClicked()
        {
            // Создаем онлайн-сессию
            _ = NetworkManager.Instance.StartNetworkSession(NetworkGameMode.OnlineHost, "MyAwesomeRoom");
        }

        public void OnFindOnlineSessionsClicked()
        {
            // Здесь мы будем вызывать поиск доступных сессий
            // Пока просто тест подключения к конкретному имени
            _ = NetworkManager.Instance.StartNetworkSession(NetworkGameMode.OnlineClient, "MyAwesomeRoom");
        }

        public void OnCreateLanHostClicked()
        {
            // Создаем LAN-сессию
            _ = NetworkManager.Instance.StartNetworkSession(NetworkGameMode.LanHost);
        }

        public void OnJoinLanHostClicked(string ipAddress)
        {
            // Подключаемся по IP
            _ = NetworkManager.Instance.StartNetworkSession(NetworkGameMode.LanClient, ipAddress: ipAddress);
        }
    }
}