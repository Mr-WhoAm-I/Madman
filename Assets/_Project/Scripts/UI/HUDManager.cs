using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

namespace _Project.Scripts.UI
{
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        [Header("Общие Панели")]
        public GameObject playerStatusPanel;
        public GameObject hubOverlayPanel;
        public GameObject battleOverlayPanel;
        public GameObject skillsPanel;

        [Header("Только для Мобилок")]
        public GameObject mobileMovementPanel;

        [Header("Контекстное Взаимодействие")]
        public GameObject interactionPromptPanel;
        public GameObject mobileInteractButton;
        public TextMeshProUGUI pcInteractText;

        [Header("Настройки")]
        public bool forceMobileUIInEditor = true;

        [Header("Реестр Окон (Новая система)")]
        [Tooltip("Перетащите сюда все UI панели, на которых висит скрипт UIWindow")]
        public List<UIWindow> GameWindows = new List<UIWindow>();

        // --- ПЕРЕМЕННЫЕ ВЗАИМОДЕЙСТВИЯ ---
        public bool IsInteractionSuspended { get; private set; } // Открыто ли сейчас меню?
        private string _cachedInteractionText = "";
        private bool _isInteractionActive = false; // Стоит ли игрок в зоне триггера?

        // --- ПЕРЕМЕННЫЕ ОКОН И ВВОДА ---
        private bool _isMobile;
        private PlayerControls _inputActions;
        private UIWindow _currentOpenWindow;
        private Dictionary<UIWindowType, UIWindow> _windowCache = new Dictionary<UIWindowType, UIWindow>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                transform.parent = null; 
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            CheckPlatform();
            HideInteractionPrompt(); 

            // Инициализация кэша окон для быстрого поиска
            foreach (var window in GameWindows)
            {
                if (window != null && !_windowCache.ContainsKey(window.WindowType))
                {
                    _windowCache.Add(window.WindowType, window);
                    window.Close(); // Гарантируем, что при старте все зарегистрированные окна закрыты
                }
            }
        }

        private void OnEnable()
        {
            _inputActions = new PlayerControls();
            
            // Подписываемся на экшены (Проверь названия экшенов в своем файле .inputactions)
            _inputActions.UI.ToggleNetworkMenu.performed += OnToggleNetworkMenuPressed;
            _inputActions.UI.Menu.performed += OnEscapePressed; 
            
            _inputActions.Enable();
        }

        private void OnDisable()
        {
            if (_inputActions == null) return;
            _inputActions.UI.ToggleNetworkMenu.performed -= OnToggleNetworkMenuPressed;
            _inputActions.UI.Menu.performed -= OnEscapePressed;
            _inputActions.Disable();
        }

        // ==========================================
        // ЛОГИКА ПЛАТФОРМ И ЛЕЙАУТОВ (ТВОЯ СТАРАЯ ЛОГИКА)
        // ==========================================
        private void CheckPlatform()
        {
            _isMobile = false;
#if UNITY_ANDROID || UNITY_IOS
            _isMobile = true;
#endif
#if UNITY_EDITOR
            if (forceMobileUIInEditor) _isMobile = true;
#endif
        }

        public void SetupHubLayout()
        {
            CloseCurrentWindow(); // Закрываем любые окна при смене сцены
            IsInteractionSuspended = false;
            if (playerStatusPanel) playerStatusPanel.SetActive(true);
            if (hubOverlayPanel) hubOverlayPanel.SetActive(true);
            if (battleOverlayPanel) battleOverlayPanel.SetActive(false);
            if (skillsPanel) skillsPanel.SetActive(false); 
            if (mobileMovementPanel) mobileMovementPanel.SetActive(_isMobile);
        }

        public void SetupBattleLayout()
        {
            CloseCurrentWindow(); // Закрываем любые окна при смене сцены
            IsInteractionSuspended = false;
            if (playerStatusPanel) playerStatusPanel.SetActive(true);
            if (hubOverlayPanel) hubOverlayPanel.SetActive(false);
            if (battleOverlayPanel) battleOverlayPanel.SetActive(true);
            if (skillsPanel) skillsPanel.SetActive(true); 
            if (mobileMovementPanel) mobileMovementPanel.SetActive(_isMobile);
        }

        // ==========================================
        // ЛОГИКА ВЗАИМОДЕЙСТВИЙ (PROMPTS)
        // ==========================================
        public void ShowInteractionPrompt(string objectName)
        {
            _cachedInteractionText = objectName;
            _isInteractionActive = true;
            
            if (!IsInteractionSuspended) DrawPrompt();
        }

        public void HideInteractionPrompt()
        {
            _isInteractionActive = false;
            if (interactionPromptPanel) interactionPromptPanel.SetActive(false);
        }

        public void SuspendInteractionPrompt()
        {
            IsInteractionSuspended = true;
            if (interactionPromptPanel) interactionPromptPanel.SetActive(false);
        }

        public void ResumeInteractionPrompt()
        {
            IsInteractionSuspended = false;
            if (_isInteractionActive) DrawPrompt();
        }

        private void DrawPrompt()
        {
            if (interactionPromptPanel == null) return;
            interactionPromptPanel.SetActive(true);

            if (mobileInteractButton) mobileInteractButton.SetActive(_isMobile);
            if (!pcInteractText) return;
            pcInteractText.gameObject.SetActive(!_isMobile);
            pcInteractText.text = $"Нажмите [E] чтобы открыть {_cachedInteractionText}";
        }

        // ==========================================
        // НОВАЯ СИСТЕМА ОКОН (WINDOW MANAGER)
        // ==========================================
        private void OnToggleNetworkMenuPressed(InputAction.CallbackContext context)
        {
            if (IsUserTyping()) return; // Защита от срабатывания во время ввода текста
            ToggleWindow(UIWindowType.NetworkMenu);
        }

        private void OnEscapePressed(InputAction.CallbackContext context)
        {
            if (_currentOpenWindow != null)
            {
                CloseCurrentWindow();
            }
        }

        public void OpenWindow(UIWindowType type)
        {
            if (type == UIWindowType.None) return;

            if (_windowCache.TryGetValue(type, out var targetWindow))
            {
                if (_currentOpenWindow != null && _currentOpenWindow != targetWindow)
                {
                    CloseCurrentWindow();
                }

                _currentOpenWindow = targetWindow;
                _currentOpenWindow.Open();
                
                // СИНЕРГИЯ: Прячем кнопку "Е", потому что игрок открыл окно
                SuspendInteractionPrompt(); 
                
                Debug.Log($"[WindowManager] Открыто окно: {type}");
            }
        }

        public void CloseCurrentWindow()
        {
            if (_currentOpenWindow == null) return;
            
            Debug.Log($"[WindowManager] Закрыто окно: {_currentOpenWindow.WindowType}");
            _currentOpenWindow.Close();
            _currentOpenWindow = null;

            // СИНЕРГИЯ: Возвращаем кнопку "Е" (если игрок всё ещё в триггере)
            ResumeInteractionPrompt();
        }

        public void ToggleWindow(UIWindowType type)
        {
            if (_currentOpenWindow != null && _currentOpenWindow.WindowType == type)
            {
                CloseCurrentWindow();
            }
            else
            {
                OpenWindow(type);
            }
        }

        private bool IsUserTyping()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null) return false;
            return EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
        }
    }
}