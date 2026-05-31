using UnityEngine;
using UnityEngine.Events;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Managers;
using _Project.Scripts.UI;      

namespace _Project.Scripts.Hub
{
    public class InteractableObject : MonoBehaviour
    {
        [Header("Настройки")]
        public string interactionName = "Гардероб";
        public UnityEvent onInteract; 

        private bool _isPlayerNear;
        private PlayerControls _controls;

        private void Awake() => _controls = new PlayerControls();
        private void OnEnable() => _controls.Enable();
        private void OnDisable() => _controls.Disable();

        private void Update()
        {
            if (!_isPlayerNear || !_controls.Gameplay.Interact.WasPressedThisFrame()) return;
            // Защита от багов: не даем нажать "Е" или мобильную кнопку, если меню уже открыто
            if (HUDManager.Instance && HUDManager.Instance.IsInteractionSuspended) return;
                
            ExecuteInteraction();
        }

        public void ExecuteInteraction()
        {
            onInteract.Invoke();
            // Временно ПРИОСТАНАВЛИВАЕМ работу кнопки, пока открыто меню
            if (HUDManager.Instance) HUDManager.Instance.SuspendInteractionPrompt();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;

            var playerManager = collision.GetComponent<PlayerManager>();
            if (playerManager == null || !playerManager.HasInputAuthority) return;
            _isPlayerNear = true;
            if (HUDManager.Instance != null) HUDManager.Instance.ShowInteractionPrompt(interactionName);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;

            var playerManager = collision.GetComponent<PlayerManager>();
            if (playerManager == null || !playerManager.HasInputAuthority) return;
            _isPlayerNear = false;
            if (HUDManager.Instance != null) HUDManager.Instance.HideInteractionPrompt();
        }
    }
}