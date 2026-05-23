using UnityEngine;
using _Project.Scripts.Data;

namespace _Project.Scripts.Core
{
    public class ProfileController : MonoBehaviour
    {
        public static ProfileController Instance { get; private set; }
        
        public PlayerProfile CurrentProfile { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.parent = null;
                DontDestroyOnLoad(gameObject); // Чтобы профиль не удалялся при смене сцен
                LoadGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadGame()
        {
            // Используем наш SaveManager, который мы создали ранее
            CurrentProfile = SaveManager.LoadProfile();
            Debug.Log("[ProfileController] Профиль успешно загружен.");
        }

        public void SaveGame()
        {
            SaveManager.SaveProfile(CurrentProfile);
        }
        
        public void SetActiveArchetype(int archetypeID)
        {
            CurrentProfile.LastSelectedArchetypeID = archetypeID;
            SaveGame(); // Сразу сохраняем выбор
        }

        public PlayerProgressionData GetActiveArchetypeData()
        {
            return CurrentProfile.GetProgressForArchetype(CurrentProfile.LastSelectedArchetypeID);
        }
        
        public void AddExperience(float amount)
        {
            var progression = GetActiveArchetypeData();
            if (progression == null) return;

            progression.CurrentXP += amount;

            // Проверяем, не апнули ли мы уровень (цикл на случай, если дали ОЧЕНЬ много опыта)
            var leveledUp = false;
            while (progression.CurrentXP >= progression.XPToNextLevel)
            {
                progression.CurrentXP -= progression.XPToNextLevel;
                progression.Level++;
        
                // Увеличиваем требование к следующему уровню (например, на 20% больше)
                progression.XPToNextLevel *= 1.2f; 
                leveledUp = true;
            }

            if (leveledUp)
            {
                Debug.Log($"[ProfileController] УРОВЕНЬ ПОВЫШЕН! Текущий уровень: {progression.Level}");
                // Здесь потом можно будет вызывать ивент для красивых партиклов LevelUp на игроке
            }

            // Сохраняем прогресс на диск
            SaveGame();
        }
    }
}