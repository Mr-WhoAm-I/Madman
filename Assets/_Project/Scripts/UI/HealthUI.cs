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

        private void Start()
        {
            FindLocalPlayerHealth();
        }

        private void Update()
        {
            // ЗАЩИТА: Проверяем не только на null, но и на то, жив ли сетевой объект (IsValid)
            if (!_localPlayerHealth || !_localPlayerHealth.Object || !_localPlayerHealth.Object.IsValid)
            {
                _localPlayerHealth = null; // Сбрасываем сломанную ссылку

                // Если игрок мертв или мы отключились — просто обнуляем полоску и прерываем Update
                if (!_localPlayerHealth) 
                {
                    healthFillImage.fillAmount = 0f; 
                    return; 
                }
            }
            
            // Если мы дошли сюда, значит объект 100% жив и валиден
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
                if (networkObject == null || !networkObject.HasInputAuthority) continue;
                _localPlayerHealth = health;
                Debug.Log("[UI] Локальная полоска здоровья успешно привязана к Безумцу.");
                break; // Игрок найден, прекращаем поиск
            }
        }
    }
}