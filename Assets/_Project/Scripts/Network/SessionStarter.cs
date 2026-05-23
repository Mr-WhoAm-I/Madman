using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Scripts.UI;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace _Project.Scripts.Network
{
    public class SessionStarter : MonoBehaviour, INetworkRunnerCallbacks
    {
        private NetworkRunner _runner;
        private PlayerControls _controls;
        
        [Header("Настройки спавна")]
        [SerializeField] private NetworkPrefabRef playerPrefab; 

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

        private void Awake()
        {
            // 1. Делаем спавнер "бессмертным". Он переедет на Боевую сцену вместе с нами.
            DontDestroyOnLoad(gameObject);

            _controls = new PlayerControls();
            _controls.Enable();
        }
        
        private async void Start()
        {
            // Защита от двойного запуска, если мы вернемся в Хаб
            if (_runner != null) return;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetupHubLayout();
            }
            Debug.Log("Инициализация сети. Попытка подключения...");

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this); 

            var result = await _runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.AutoHostOrClient, 
                SessionName = "Madman_Test_Chamber",
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

            if (!result.Ok)
            {
                Debug.LogError($"Ошибка сети: {result.ShutdownReason}");
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            Debug.Log($"Игрок {player} подключился. Спавним куб!");
                
            var networkPlayerObject = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
                
            // 2. ВАЖНО: Делаем самого ИГРОКА бессмертным при смене сцен!
            // Теперь он не уничтожится, а значит сохранит свое ХП, Скорость и выбранное Оружие.
            DontDestroyOnLoad(networkPlayerObject.gameObject);

            _spawnedCharacters.Add(player, networkPlayerObject);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            if (!_spawnedCharacters.TryGetValue(player, out var networkObject)) return;
            
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
                    
            Debug.Log($"Игрок {player} отключился. Его куб успешно удален.");
        }
        
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            // 3. Сцена загрузилась (Хаб или Бой). Расставляем всех "выживших" игроков!
            if (!runner.IsServer) return;
            foreach (var kvp in _spawnedCharacters.Where(kvp => kvp.Value != null))
            {
                // Скидываем их в центр новой сцены (немного вразброс, чтобы не застряли друг в друге)
                kvp.Value.transform.position = new Vector3(UnityEngine.Random.Range(-2f, 2f), 0, 0);
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var inputData = new NetworkInputData();
            if (HUDManager.Instance != null && HUDManager.Instance.IsInteractionSuspended)
            {
                input.Set(inputData);
                return;               
            }

            var moveInput = _controls.Gameplay.Move.ReadValue<Vector2>();
            inputData.MovementInput = moveInput;

            input.Set(inputData);
        }
        
        private void OnDestroy()
        {
            _controls?.Disable(); 
            // Отписываемся от событий, чтобы не было утечек памяти
            if (_runner != null) _runner.RemoveCallbacks(this); 
        }
        
        // --- ПУСТЫЕ МЕТОДЫ ИНТЕРФЕЙСА ---
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    }
}