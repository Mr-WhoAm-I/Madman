using UnityEngine;
using _Project.Scripts.Network.Managers;
using _Project.Scripts.UI;

namespace _Project.Scripts.Hub
{
    public class HubBuildingTerminal : MonoBehaviour
    {
        [Header("Настройки здания")]
        public string buildingName = "Здание";
        public HubWindowBase targetWindow;

        private bool _isPlayerNear;
        private PlayerControls _controls;
        private HUDManager _hud; // Кэшируем для оптимизации

        private void Awake() => _controls = new PlayerControls();
        private void OnEnable() => _controls.Enable();
        private void OnDisable() => _controls.Disable();

        private void Start()
        {
            _hud = HUDManager.Instance;
        }

        private void Update()
        {
            if (!_isPlayerNear) return;

            // 1. Проверяем нажатие Esc (Закрываем окно, если оно было открыто)
            if (targetWindow.IsOpen && _controls.UI.Menu.WasPressedThisFrame())
            {
                targetWindow.Close();
                // Возвращаем текст "Нажмите Е", так как игрок всё еще рядом
                if (_hud) _hud.ShowInteractionPrompt(buildingName);
                return; 
            }

            // 2. Проверяем нажатие "Е"
            if (!_controls.Gameplay.Interact.WasPressedThisFrame()) return;
            if (targetWindow.IsOpen)
            {
                targetWindow.Close();
                if (_hud) _hud.ShowInteractionPrompt(buildingName);
            }
            else
            {
                targetWindow.Open();
                if (_hud) _hud.HideInteractionPrompt();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;
            var player = collision.GetComponent<PlayerManager>();
            if (player == null || !player.HasInputAuthority) return;

            _isPlayerNear = true;
            
            // Показываем текст только если окно сейчас закрыто
            if (!targetWindow.IsOpen && _hud != null)
                _hud.ShowInteractionPrompt(buildingName);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;
            var player = collision.GetComponent<PlayerManager>();
            if (player == null || !player.HasInputAuthority) return;

            _isPlayerNear = false;
            if (_hud != null) _hud.HideInteractionPrompt();
        }
    }
}