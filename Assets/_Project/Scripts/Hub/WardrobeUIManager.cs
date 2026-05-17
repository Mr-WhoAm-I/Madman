using UnityEngine;
using _Project.Scripts.Network;

namespace _Project.Scripts.Hub
{
    public class WardrobeUIManager : MonoBehaviour
    {
        public static WardrobeUIManager Instance; // Синглтон для быстрого вызова

        [Header("UI Элементы")]
        public CanvasGroup windowGroup;

        private void Awake()
        {
            Instance = this;
            CloseWindow(); // Прячем окно при старте сцены
        }

        public void OpenWindow()
        {
            windowGroup.alpha = 1f;
            windowGroup.interactable = true;
            windowGroup.blocksRaycasts = true;
        }

        public void CloseWindow()
        {
            windowGroup.alpha = 0f;
            windowGroup.interactable = false;
            windowGroup.blocksRaycasts = false;
        }

        // Эти методы мы привяжем к твоим 4-м кнопкам в Unity
        public void OnHystericClicked() => SendChangeRequest(0); // 0 - индекс Истерика в массиве
        public void OnParanoiacClicked() => SendChangeRequest(1);
        public void OnSchizoidClicked() => SendChangeRequest(2);
        public void OnMelancholicClicked() => SendChangeRequest(3);

        private void SendChangeRequest(int classIndex)
        {
            // Используем новый стандарт API (ищем только активные объекты на сцене)
            var allPlayers = FindObjectsByType<PlayerManager>(FindObjectsInactive.Exclude);
            
            foreach (var player in allPlayers)
            {
                // Находим именно НАШЕГО локального персонажа
                if (!player.HasInputAuthority) continue;
                // Отправляем запрос на сервер
                player.Rpc_ChangeArchetype(classIndex);
                break;
            }
            CloseWindow(); // Закрываем интерфейс после выбора
        }
    }
}