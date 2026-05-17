using _Project.Scripts.Network;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.UI
{
    public class HealthUI : MonoBehaviour
    {
        [Header("UI Элементы")]
        public Image healthFillImage;

        private Health _localPlayerHealth;

        private void Update()
        {
            // 1. Если локальный игрок еще не найден (или он умер и был удален сервером)
            if (_localPlayerHealth == null)
            {
                FindLocalPlayerHealth();
                
                // Если после поиска игрок всё еще не найден, прерываем выполнение (это спасает от ошибки бессмертия)
                if (_localPlayerHealth == null) return; 
            }

            // 2. Игрок жив и найден — обновляем UI
            healthFillImage.fillAmount = _localPlayerHealth.CurrentHealth / _localPlayerHealth.MaxHealth;
        }

        private void FindLocalPlayerHealth()
        {
            // Ищем всех игроков на сцене (исключая выключенные объекты)
            var allHealthComponents = FindObjectsByType<Health>(FindObjectsInactive.Exclude);
            
            foreach (var health in allHealthComponents)
            {
                var networkObject = health.GetComponent<NetworkObject>();
                
                // Проверяем, принадлежит ли этот сетевой объект НАШЕМУ клиенту
                if (networkObject != null && networkObject.HasInputAuthority)
                {
                    _localPlayerHealth = health;
                    Debug.Log("[UI] Локальная полоска здоровья успешно привязана к Безумцу.");
                    break; // Игрок найден, прекращаем поиск
                }
            }
        }
    }
}