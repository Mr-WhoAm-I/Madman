using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Project.Scripts.Network.Core;
using _Project.Scripts.UI;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace _Project.Scripts.Network.Managers
{
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public static NetworkManager Instance;
        public List<SessionInfo> AvailableSessions { get; private set; } = new();
        public event Action<List<SessionInfo>> OnSessionListUpdatedEvent;
        public event Action<byte> OnAmmoChoiceChanged;
        
        [Header("Настройки спавна")]
        public NetworkPrefabRef playerPrefab;

        private NetworkRunner _networkRunner;
        private GameObject _runnerObject;
        private PlayerControls _playerControls;
        private byte _currentAmmoChoice = 0;
        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();
        private bool _isIntentionalShutdown = false;
        private bool _isRecovering = false;
        

        private const ushort DefaultLanPort = 27015;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.parent = null;
                DontDestroyOnLoad(gameObject);
                _playerControls = new PlayerControls();
                
                // Подписываемся через явные методы
                _playerControls.Gameplay.SelectAmmo1.performed += OnSelectAmmo1;
                _playerControls.Gameplay.SelectAmmo2.performed += OnSelectAmmo2;
                _playerControls.Gameplay.SelectAmmo3.performed += OnSelectAmmo3;
                _playerControls.Gameplay.SelectAmmo4.performed += OnSelectAmmo4;
                
                _playerControls.Enable();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // --- ЯВНЫЕ ОБРАБОТЧИКИ ДЛЯ ИЗБЕЖАНИЯ УТЕЧЕК ПАМЯТИ ---
        private void OnSelectAmmo1(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SetAmmoChoice(0);
        private void OnSelectAmmo2(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SetAmmoChoice(1);
        private void OnSelectAmmo3(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SetAmmoChoice(2);
        private void OnSelectAmmo4(UnityEngine.InputSystem.InputAction.CallbackContext ctx) => SetAmmoChoice(3);

        public void SetAmmoChoice(byte ammoType)
        {
            _currentAmmoChoice = ammoType;
            OnAmmoChoiceChanged?.Invoke(ammoType); // Сигнализируем UI
        }

        private async Task ShutdownCurrentSession()
        {
            if (_networkRunner != null)
            {
                Debug.Log("[NetworkManager] Выключаем текущую сессию и очищаем память...");
                
                _isIntentionalShutdown = true; 
                
                await _networkRunner.Shutdown();
                
                if (_runnerObject != null)
                {
                    Destroy(_runnerObject);
                }
        
                _networkRunner = null;
                _runnerObject = null;
                _spawnedCharacters.Clear(); 
                
                await Task.Delay(50); 
                
                // Снимаем флаг
                _isIntentionalShutdown = false; 
            }
        }

        public async Task<bool> StartNetworkSession(NetworkGameMode mode, string sessionName = "MadmanSession", string ipAddress = "127.0.0.1")
        {
            await ShutdownCurrentSession();

            // Создаем отдельный объект-носитель для Fusion
            _runnerObject = new GameObject("FusionNetworkRunner_Instance");
            
            DontDestroyOnLoad(_runnerObject);

            _networkRunner = _runnerObject.AddComponent<NetworkRunner>();
            _runnerObject.AddComponent<ECSNetworkTicker>();
            
            _networkRunner.ProvideInput = true;
            
            // ВАЖНО: Коллбеки всё равно направляем в наш синглтон (this)
            _networkRunner.AddCallbacks(this); 

            var startGameArgs = new StartGameArgs
            {
                SceneManager = _runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                PlayerCount = 4
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

        public async Task HostOnlineGame(string roomName)
        {
            Debug.Log($"[NetworkManager] Миграция: Создаем онлайн-комнату '{roomName}'");
            // Закрываем текущую сессию и запускаем OnlineHost
            await StartNetworkSession(NetworkGameMode.OnlineHost, roomName);
        }

        public async Task JoinOnlineGame(string roomName)
        {
            Debug.Log($"[NetworkManager] Миграция: Подключаемся к '{roomName}'");
            // Закрываем текущую сессию и подключаемся к чужой
            await StartNetworkSession(NetworkGameMode.OnlineClient, roomName);
        }

        public async Task BrowseOnlineGames()
        {
            Debug.Log("[NetworkManager] Миграция: Вход в лобби для поиска...");
            
            // 1. Убиваем текущую сессию (соло или любую другую)
            await ShutdownCurrentSession();

            // 2. Создаем чистый объект ТОЛЬКО для лобби
            _runnerObject = new GameObject("FusionNetworkRunner_Lobby");
            DontDestroyOnLoad(_runnerObject);

            _networkRunner = _runnerObject.AddComponent<NetworkRunner>();
            _networkRunner.ProvideInput = true;
            _networkRunner.AddCallbacks(this);

            // 3. ВАЖНО: Вызываем подключение к лобби, а НЕ StartGame!
            var result = await _networkRunner.JoinSessionLobby(SessionLobby.ClientServer);

            if (result.Ok)
            {
                Debug.Log("[NetworkManager] Успешно зашли в Лобби. Ожидаем списки серверов...");
            }
            else
            {
                Debug.LogWarning($"[NetworkManager] Ошибка входа в лобби: {result.ShutdownReason}");
            }
        }
        
        // --- ИМПЛЕМЕНТАЦИЯ ПОЛУЧЕНИЯ ИНПУТА ---
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var inputData = new NetworkInputData();

            if (UIState.IsInputBlocked)
            {
                input.Set(inputData);
                return;
            }

            inputData.MovementInput = _playerControls.Gameplay.Move.ReadValue<Vector2>();

            // Базовое направление (мышь ПК или правый стик прицеливания)
            var baseAimDirection = Vector2.up;
            var aimValue = _playerControls.Gameplay.Aim.ReadValue<Vector2>();
            
            if (aimValue.magnitude > 1f && Camera.main != null) 
            {
                var worldMousePos = Camera.main.ScreenToWorldPoint(new Vector3(aimValue.x, aimValue.y, -Camera.main.transform.position.z));
                var screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
                var worldCenter = Camera.main.ScreenToWorldPoint(new Vector3(screenCenter.x, screenCenter.y, -Camera.main.transform.position.z));
                baseAimDirection = (new Vector2(worldMousePos.x, worldMousePos.y) - new Vector2(worldCenter.x, worldCenter.y)).normalized;
            }
            else if (aimValue.magnitude > 0.1f)
            {
                baseAimDirection = aimValue.normalized;
            }

            // --- КРОССПЛАТФОРМЕННАЯ ЛОГИКА НАВЫКА ---
            var isSkillFired = false;

            // 1. Проверяем мобильный MOBA-джойстик (присутствует ли он и был ли отпущен палец)
            if (MobaSkillJoystick.Instance != null && MobaSkillJoystick.Instance.ConsumeFireEvent(out var joystickAim))
            {
                isSkillFired = true;
                
                // Если автонаведение никого не нашло (вернуло 0,0), стреляем в сторону базового прицела/движения
                inputData.AimDirection = joystickAim != Vector2.zero ? joystickAim : baseAimDirection;
            }
            else
            {
                // 2. Классический ПК-инпут: кнопка нажата СЕЙЧАС
                isSkillFired = _playerControls.Gameplay.Skill.IsPressed();
                inputData.AimDirection = baseAimDirection; // Целимся туда, куда смотрит мышь
            }
            
            bool useConsumable1 = _playerControls.Gameplay.UseConsumable1.IsPressed();
            bool useConsumable2 = _playerControls.Gameplay.UseConsumable2.IsPressed();

            inputData.Buttons.Set(PlayerInputButtons.Skill, isSkillFired);
            inputData.Buttons.Set(PlayerInputButtons.UseConsumable1, useConsumable1);
            inputData.Buttons.Set(PlayerInputButtons.UseConsumable2, useConsumable2);
            inputData.SelectedAmmoType = _currentAmmoChoice;

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
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
        {
            Debug.Log($"[NetworkManager] Остановка раннера. Причина: {shutdownReason}");

            if (_isIntentionalShutdown || shutdownReason == ShutdownReason.Ok) return;
            
            Debug.LogError("[NetworkManager] Внезапная потеря связи (Shutdown)! Аварийное возвращение в соло-режим...");
            _ = RecoverToSoloAsync();
        }
        public void OnConnectedToServer(NetworkRunner runner) {}
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) 
        {
            Debug.LogWarning($"[NetworkManager] Отключены от сервера. Причина: {reason}");
            
            // Если мы не выходили сами, значит хост отвалился или пропал интернет
            if (!_isIntentionalShutdown)
            {
                Debug.LogError("[NetworkManager] Таймаут/потеря хоста! Аварийное возвращение в соло-режим...");
                _ = RecoverToSoloAsync();
            }
        }

        private async Task RecoverToSoloAsync()
        {
            if (_isRecovering) return; // Блокируем, если процесс спасения уже запущен
            _isRecovering = true;

            // Ждем полсекунды, пока Fusion полностью освободит ресурсы (защита от Race Condition)
            await Task.Delay(500); 
            UnityEngine.SceneManagement.SceneManager.LoadScene("HubScene");
            // StartNetworkSession сам корректно убьет зависший раннер и загрузит нас в Solo
            await StartNetworkSession(NetworkGameMode.Solo);

            _isRecovering = false;
        }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) {}
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) {}
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) {}
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            AvailableSessions = sessionList;
            Debug.Log($"[NetworkManager] Найдено сессий: {sessionList.Count}");
    
            // Вызываем событие, чтобы передать список в наш UI
            OnSessionListUpdatedEvent?.Invoke(sessionList);
        }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) {}
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) {}
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            // Этот метод вызывается автоматически Fusion, когда сцена загружена
            if (!runner.IsServer) return;
            Debug.Log("[NetworkManager] Сцена загружена. Проверяем состояние игроков...");
        
            // Определяем точку спавна на новой сцене
            var spawnPos = Vector3.zero;
        
            // Ищем пустой объект-точку спавна на новой сцене (если он у вас есть)
            var spawnPoint = GameObject.FindWithTag("SpawnPoint");
            if (spawnPoint != null)
            {
                spawnPos = spawnPoint.transform.position;
            }

            foreach (var player in runner.ActivePlayers)
            {
                // ИСПРАВЛЕНИЕ:
                // Пытаемся достать объект из словаря. Если игрока там нет ИЛИ 
                // объект равен null (был уничтожен Unity при выгрузке сцены Хаба) — спавним его заново!
                if (!_spawnedCharacters.TryGetValue(player, out var networkObject) || networkObject == null)
                {
                    Debug.Log($"[NetworkManager] Игрок {player} уничтожен или не найден на новой сцене. Спавним персонажа в {spawnPos}...");
                
                    var playerObject = runner.Spawn(playerPrefab, spawnPos, Quaternion.identity, player);
                
                    // Перезаписываем или добавляем живую ссылку на новый GameObject в словарь
                    if (_spawnedCharacters.ContainsKey(player))
                    {
                        _spawnedCharacters[player] = playerObject;
                    }
                    else
                    {
                        _spawnedCharacters.Add(player, playerObject);
                    }
                }
                else
                {
                    Debug.Log($"[NetworkManager] Игрок {player} уже имеет живой объект на сцене. Пропускаем спавн.");
                }
            }
        }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) {}
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) {}
        public void OnSceneLoadStart(NetworkRunner runner) {}
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) {}
        
        private void OnDestroy()
        {
            if (Instance != this || _playerControls == null) return;
            // Корректная отписка
            _playerControls.Gameplay.SelectAmmo1.performed -= OnSelectAmmo1;
            _playerControls.Gameplay.SelectAmmo2.performed -= OnSelectAmmo2;
            _playerControls.Gameplay.SelectAmmo3.performed -= OnSelectAmmo3;
            _playerControls.Gameplay.SelectAmmo4.performed -= OnSelectAmmo4;
        }
    }
}