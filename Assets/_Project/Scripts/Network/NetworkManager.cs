using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using _Project.Scripts.UI;

namespace _Project.Scripts.Network
{
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static NetworkManager Instance;
        public List<SessionInfo> AvailableSessions { get; private set; } = new();
        
        [Header("Настройки спавна")]
        public NetworkPrefabRef playerPrefab;

        private NetworkRunner _networkRunner;
        private PlayerControls _playerControls;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

        private const ushort DefaultLanPort = 27015;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.parent = null;
                DontDestroyOnLoad(gameObject);
                _playerControls = new PlayerControls();
                _playerControls.Enable();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async Task<bool> StartNetworkSession(NetworkGameMode mode, string sessionName = "MadmanSession", string ipAddress = "127.0.0.1")
        {
            if (_networkRunner != null)
            {
                await _networkRunner.Shutdown();
                Destroy(_networkRunner);
            }

            _networkRunner = gameObject.AddComponent<NetworkRunner>();
            gameObject.AddComponent<ECSNetworkTicker>();
            _networkRunner.ProvideInput = true;
            _networkRunner.AddCallbacks(this);

            var startGameArgs = new StartGameArgs
            {
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            };

            switch (mode)
            {
                case NetworkGameMode.Solo:
                    startGameArgs.GameMode = GameMode.Single;
                    startGameArgs.SessionName = "SoloMode";
                    break;

                case NetworkGameMode.LanHost:
                    startGameArgs.GameMode = GameMode.Host;
                    startGameArgs.Address = NetAddress.Any(DefaultLanPort);
                    startGameArgs.SessionName = "LanHostMode";
                    break;

                case NetworkGameMode.LanClient:
                    startGameArgs.GameMode = GameMode.Client;
                    startGameArgs.Address = NetAddress.CreateFromIpPort(ipAddress, DefaultLanPort);
                    break;

                case NetworkGameMode.OnlineHost:
                    startGameArgs.GameMode = GameMode.Host;
                    startGameArgs.SessionName = sessionName;
                    break;

                case NetworkGameMode.OnlineClient:
                    startGameArgs.GameMode = GameMode.Client;
                    startGameArgs.SessionName = sessionName;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            Debug.Log($"[NetworkManager] Попытка запуска сессии: {mode}...");
            var result = await _networkRunner.StartGame(startGameArgs);

            if (result.Ok)
            {
                Debug.Log("[NetworkManager] Сессия успешно инициализирована.");
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.SetupHubLayout();
                }
                return true;
            }

            Debug.LogError($"[NetworkManager] Критическая ошибка запуска: {result.ShutdownReason}");
            return false;
        }

        // --- ИМПЛЕМЕНТАЦИЯ ПОЛУЧЕНИЯ ИНПУТА ---
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var inputData = new NetworkInputData();

            if (HUDManager.Instance != null && HUDManager.Instance.IsInteractionSuspended)
            {
                input.Set(inputData);
                return;
            }

            var moveInput = _playerControls.Gameplay.Move.ReadValue<Vector2>();
            inputData.MovementInput = moveInput;

            input.Set(inputData);
        }

        // --- АВТОРИТАРНЫЙ СПАВН ИГРОКОВ НА СЕРВЕРЕ ---
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return; 

            Debug.Log($"[Сервер] Игрок {player} подключился. Создаем персонажа...");
            
            var spawnPos = Vector3.zero; 
            var playerObject = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
            
            _spawnedCharacters.Add(player, playerObject);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            if (!_spawnedCharacters.TryGetValue(player, out var playerObject)) return;
            Debug.Log($"[Сервер] Игрок {player} отключился. Уничтожаем персонажа.");
            runner.Despawn(playerObject);
            _spawnedCharacters.Remove(player);
        }

        // --- ОБЯЗАТЕЛЬНЫЕ МЕТОДЫ ИНТЕРФЕЙСА FUSION БЕЗ ЛИШНЕГО МУСОРА ---
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) {}
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) {}
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) {}
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            AvailableSessions = sessionList;
            Debug.Log($"[NetworkManager] Найдено сессий: {sessionList.Count}");
    
            // Здесь можно вызвать событие (Action), чтобы обновить UI
            // OnSessionListChanged?.Invoke(sessionList);
        }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnSceneLoadDone(NetworkRunner runner) {}
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
    }
}