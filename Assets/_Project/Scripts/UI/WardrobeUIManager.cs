using _Project.Scripts.Core;
using TMPro;
using UnityEngine;

namespace _Project.Scripts.UI
{
    public class WardrobeUIManager : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI levelText;

        // Этот метод вызывается, когда ты открываешь окно Гардероба
        public void OpenWardrobe()
        {
            RefreshUI();
        }

        private void RefreshUI()
        {
            var data = ProfileController.Instance.GetActiveArchetypeData();
            levelText.text = $"Уровень: {data.Level}";
        }

        // Вызывается при нажатии на кнопку выбора архетипа в UI
        public void SelectArchetype(int archetypeID)
        {
            ProfileController.Instance.SetActiveArchetype(archetypeID);
            RefreshUI();
            Debug.Log($"Архетип {archetypeID} выбран и сохранен!");
        }

        // Пример кнопки прокачки
        public void UpgradeLevel()
        {
            var data = ProfileController.Instance.GetActiveArchetypeData();
            data.Level++;
            ProfileController.Instance.SaveGame();
            RefreshUI();
        }
    }
}