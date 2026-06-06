using _Project.Scripts.UI;
using Fusion;
using UnityEngine;

namespace _Project.Scripts.Hub
{
    public class MissionUIManager : HubWindowBase
    {
        public static MissionUIManager Instance;
        
        [Header("Настройки перехода")]
        [Tooltip("Индекс боевой сцены в окне Build Settings (обычно 1)")]
        public int gameSceneIndex = 1; 

        protected override void Awake()
        {
            base.Awake(); // Обязательно вызываем логику базового окна!
            Instance = this;
        }
        
        public void StartMission()
        {
            var runner = FindAnyObjectByType<NetworkRunner>();

            if (runner != null && runner.IsServer) 
            {
                Debug.Log("[Сервер] Запуск миссии. Переход всех игроков на боевую сцену...");
                
                // ПРАВИЛЬНЫЙ СИНТАКСИС FUSION 2
                runner.LoadScene(SceneRef.FromIndex(gameSceneIndex));
            }
            else if (runner != null && !runner.IsServer)
            {
                Debug.LogWarning("[Клиент] Только лидер группы (Хост) может запустить миссию!");
            }
        }
    }
}