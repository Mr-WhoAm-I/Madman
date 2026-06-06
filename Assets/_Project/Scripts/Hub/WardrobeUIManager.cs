using UnityEngine;
using TMPro; // Обязательно для TextMeshPro
using UnityEngine.UI; // Обязательно для UI-слайдеров
using _Project.Scripts.Network;
using _Project.Scripts.Core;
using _Project.Scripts.Network.Managers;
using _Project.Scripts.UI;

namespace _Project.Scripts.Hub
{
    public class WardrobeUIManager : HubWindowBase
    {
        public static WardrobeUIManager Instance;

        [Header("Тексты Уровня (0-Ист, 1-Пар, 2-Шиз, 3-Мел)")]
        public TextMeshProUGUI[] levelTexts; 

        [Header("Тексты Опыта (Опционально)")]
        public TextMeshProUGUI[] expTexts;

        [Header("Полоски Опыта (Опционально)")]
        public Slider[] expSliders;

        protected override void Awake()
        {
            base.Awake(); // Обязательно вызываем логику базового окна!
            Instance = this;
        }
        
        public void OnHystericClicked() => SendChangeRequest(0);
        public void OnParanoiacClicked() => SendChangeRequest(1);
        public void OnSchizoidClicked() => SendChangeRequest(2);
        public void OnMelancholicClicked() => SendChangeRequest(3);

        private void SendChangeRequest(int classIndex)
        {
            var currentLevel = 1; // Уровень по умолчанию, если сейвов вдруг нет

            // 1. Сохраняем локально выбранный архетип
            if (ProfileController.Instance != null && ProfileController.Instance.CurrentProfile != null)
            {
                ProfileController.Instance.SetActiveArchetype(classIndex);
                var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(classIndex);
                if (progression != null) 
                {
                    currentLevel = progression.Level;
                }
            }
            else
            {
                Debug.LogWarning("[WardrobeUIManager] ProfileController не найден!");
            }

            // 2. Ищем локального игрока
            var allPlayers = FindObjectsByType<PlayerManager>(FindObjectsInactive.Exclude);
            
            foreach (var player in allPlayers)
            {
                if (!player.HasInputAuthority) continue;
                
                // 3. Отправляем запрос на сервер вместе с УРОВНЕМ из профиля
                player.Rpc_ChangeArchetype(classIndex, currentLevel);
                break;
            }
        }

        protected override void OnWindowOpened()
        {
            UpdateProgressUI();
        }
        
        private void UpdateProgressUI()
        {
            if (!ProfileController.Instance || ProfileController.Instance.CurrentProfile == null)
            {
                return;
            }

            // У нас 4 архетипа (от 0 до 3)
            for (var i = 0; i < 4; i++)
            {
                // Достаем данные по каждому архетипу
                var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(i);
                if (progression == null) continue;

                // Обновляем текст уровня, если ссылка задана в Unity Инспекторе
                if (levelTexts != null && i < levelTexts.Length && levelTexts[i] != null)
                {
                    levelTexts[i].text = $"УР. {progression.Level}";
                }

                // Обновляем текст опыта (свойства Level и Experience зависят от того, как они названы у тебя в PlayerProgressionData)
                if (expTexts != null && i < expTexts.Length && expTexts[i] != null)
                {
                    expTexts[i].text = $"{progression.CurrentXP:F0} / {progression.XPToNextLevel:F0} XP";
                }

                // Обновляем слайдер опыта (если вы его используете)
                if (expSliders == null || i >= expSliders.Length || expSliders[i] == null) continue;
                expSliders[i].value = progression.CurrentXP / progression.XPToNextLevel;
            }
        }
    }
}