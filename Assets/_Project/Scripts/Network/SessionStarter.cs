using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Project.Scripts.Network
{
    // INetworkRunnerCallbacks позволяет скрипту слушать события сети (кто зашел, кто вышел)
    public class SessionStarter : MonoBehaviour, INetworkRunnerCallbacks
    {
        private NetworkRunner _runner;
        private PlayerControls _controls;
        
        [FormerlySerializedAs("_playerPrefab")]
        [Header("Настройки спавна")]
        [SerializeField] private NetworkPrefabRef playerPrefab; // Ссылка на наш префаб игрока

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();
        private void Awake()
        {
            // 2. Инициализируем управление при старте
            _controls = new PlayerControls();
            _controls.Enable();
        }
        
        private async void Start()
        {
            Debug.Log("Инициализация сети. Попытка подключения...");

            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            
            // Подписываем этот скрипт на получение сетевых событий
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

        // --- СОБЫТИЯ СЕТИ ---

        // Этот метод срабатывает каждый раз, когда кто-то (включая тебя) заходит в сессию
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (runner.IsServer)
            {
                Debug.Log($"Игрок {player} подключился. Спавним куб!");
                
                // 2. Сохраняем созданный объект в переменную
                var networkPlayerObject = runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, player);
                
                // 3. Записываем в словарь, что этот куб принадлежит этому игроку
                _spawnedCharacters.Add(player, networkPlayerObject);
            }
        }

        // Остальные методы интерфейса INetworkRunnerCallbacks (оставляем пустыми, чтобы не ругался компилятор)
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;
            // Ищем в словаре куб, который принадлежал ушедшему игроку
            if (!_spawnedCharacters.TryGetValue(player, out var networkObject)) return;
            // Сервер физически удаляет куб из игрового мира
            runner.Despawn(networkObject);
            // Удаляем запись из словаря
            _spawnedCharacters.Remove(player);
                    
            Debug.Log($"Игрок {player} отключился. Его куб (призрак) успешно удален.");
        }
        
        void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            OnReliableDataProgress(runner, player, key, progress);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var data = new NetworkInputData
            {
                // Читаем вектор движения
                MovementInput = _controls.Gameplay.Move.ReadValue<Vector2>()
            };
            
            input.Set(data);
        }
        
        private void OnDestroy()
        {
            _controls?.Disable(); // Отключаем инпут при выходе
        }
        
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            OnConnectFailed(runner, remoteAddress, reason);
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {}
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnSceneLoadDone(NetworkRunner runner) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    }
}