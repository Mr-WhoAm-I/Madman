// Путь: Assets/_Project/Scripts/Core/SaveManager.cs
using System.IO;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Core;

namespace _Project.Scripts.Core
{
    public static class SaveManager
    {
        private static readonly string FilePath = Path.Combine(Application.persistentDataPath, "player_progress.json");

        public static void SaveProfile(PlayerProfile profile)
        {
            var json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(FilePath, json);
            Debug.Log($"[SaveManager] Профиль сохранен по пути: {FilePath}");
        }

        public static PlayerProfile LoadProfile()
        {
            if (!File.Exists(FilePath))
            {
                Debug.Log("[SaveManager] Файл сохранения не найден, создаем новый.");
                return new PlayerProfile();
            }

            var json = File.ReadAllText(FilePath);
            return JsonUtility.FromJson<PlayerProfile>(json);
        }
    }
}