using UnityEngine;
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

        // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ЛОГИКИ ВЗАИМОДЕЙСТВИЯ ---
        public bool IsInteractionSuspended { get; private set; } // Открыто ли сейчас меню?
        private string _cachedInteractionText = "";
        private bool _isInteractionActive = false; // Стоит ли игрок в зоне триггера?
        // ---------------------------------------------------

        private bool _isMobile;

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
        }

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
            IsInteractionSuspended = false;
            if (playerStatusPanel) playerStatusPanel.SetActive(true);
            if (hubOverlayPanel) hubOverlayPanel.SetActive(true);
            if (battleOverlayPanel) battleOverlayPanel.SetActive(false);
            if (skillsPanel) skillsPanel.SetActive(false); 
            if (mobileMovementPanel) mobileMovementPanel.SetActive(_isMobile);
        }

        public void SetupBattleLayout()
        {
            IsInteractionSuspended = false;
            if (playerStatusPanel) playerStatusPanel.SetActive(true);
            if (hubOverlayPanel) hubOverlayPanel.SetActive(false);
            if (battleOverlayPanel) battleOverlayPanel.SetActive(true);
            if (skillsPanel) skillsPanel.SetActive(true); 
            if (mobileMovementPanel) mobileMovementPanel.SetActive(_isMobile);
        }

        // 1. Игрок вошел в триггер
        public void ShowInteractionPrompt(string objectName)
        {
            _cachedInteractionText = objectName;
            _isInteractionActive = true;
            
            if (!IsInteractionSuspended) DrawPrompt();
        }

        // 2. Игрок вышел из триггера
        public void HideInteractionPrompt()
        {
            _isInteractionActive = false;
            if (interactionPromptPanel) interactionPromptPanel.SetActive(false);
        }

        // 3. Игрок открыл меню (Прячем кнопку временно)
        public void SuspendInteractionPrompt()
        {
            IsInteractionSuspended = true;
            if (interactionPromptPanel) interactionPromptPanel.SetActive(false);
        }

        // 4. Игрок закрыл меню (Возвращаем кнопку, если он еще не отошел)
        public void ResumeInteractionPrompt()
        {
            IsInteractionSuspended = false;
            if (_isInteractionActive) DrawPrompt();
        }

        // Внутренний метод отрисовки
        private void DrawPrompt()
        {
            if (interactionPromptPanel == null) return;
            interactionPromptPanel.SetActive(true);

            if (mobileInteractButton) mobileInteractButton.SetActive(_isMobile);
            if (!pcInteractText) return;
            pcInteractText.gameObject.SetActive(!_isMobile);
            pcInteractText.text = $"Нажмите [E] чтобы открыть {_cachedInteractionText}";
        }
    }
}