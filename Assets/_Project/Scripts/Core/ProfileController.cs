using System; // <-- Добавить обязательно
using System.Linq;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Core;
using _Project.Scripts.Data.Shop;

namespace _Project.Scripts.Core
{
    public class ProfileController : MonoBehaviour
    {
        public static ProfileController Instance { get; private set; }
        
        public PlayerProfile CurrentProfile { get; private set; }
        
        [Header("База данных")]
        public ArchetypeData[] AllArchetypes;
        public ConsumableData[] AllConsumables;

        // НОВОЕ: Событие, которое срабатывает при смене класса
        public event Action<int> OnArchetypeChanged;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.parent = null;
                DontDestroyOnLoad(gameObject);
                LoadGame();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadGame()
        {
            CurrentProfile = SaveManager.LoadProfile();
        }

        public void SaveGame()
        {
            SaveManager.SaveProfile(CurrentProfile);
        }
        
        public void SetActiveArchetype(int archetypeID)
        {
            CurrentProfile.LastSelectedArchetypeID = archetypeID;
            SaveGame(); 
            
            // ВЫЗЫВАЕМ СОБЫТИЕ: Все, кто подписан, узнают о смене класса
            OnArchetypeChanged?.Invoke(archetypeID);
        }
        
        public void SetNickname(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            CurrentProfile.Nickname = newName;
            SaveGame();
        }

        public PlayerProgressionData GetActiveArchetypeData()
        {
            return CurrentProfile.GetProgressForArchetype(CurrentProfile.LastSelectedArchetypeID);
        }

        public ArchetypeData GetArchetypeAsset(int id)
        {
            if (AllArchetypes == null || AllArchetypes.Length == 0) return null;
            return AllArchetypes.FirstOrDefault(a => a.archetypeID == id) ?? AllArchetypes[0];
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
        
        public ConsumableData GetConsumableAsset(string id)
        {
            if (AllConsumables == null || string.IsNullOrEmpty(id)) return null;
            return AllConsumables.FirstOrDefault(c => c.consumableID == id);
        }
    }
}