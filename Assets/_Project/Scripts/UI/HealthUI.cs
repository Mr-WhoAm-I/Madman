using UnityEngine;
using UnityEngine.UI; // Обязательно для работы с UI
using _Project.Scripts.Network;

namespace _Project.Scripts.UI
{
    public class HealthUI : MonoBehaviour
    {
        [Header("Ссылки на интерфейс")]
        public Image healthFillImage; // Сюда мы перетащим нашу картинку кардиограммы

        private const float MaxHealth = 100f; // Максимальное здоровье Безумца по умолчанию

        private void Update()
        {
            // Проверяем, существует ли локальный игрок (мы сохраняли его в статичную переменную)
            if (PlayerNetworkMovement.LocalPlayerHealth)
            {
                // Получаем текущее здоровье с сервера
                float currentHealth = PlayerNetworkMovement.LocalPlayerHealth.CurrentHealth;
                
                // Вычисляем процент заполнения (от 0.0 до 1.0)
                healthFillImage.fillAmount = currentHealth / MaxHealth;
            }
            else
            {
                // Если игрока еще нет (не заспавнился) или он умер - полоска пустая
                healthFillImage.fillAmount = 0f;
            }
        }
    }
}