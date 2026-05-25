using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.UI
{
    [RequireComponent(typeof(Button))]
    public class UIWindowOpenerButton : MonoBehaviour
    {
        [Header("Настройки Окна")]
        [Tooltip("Какое окно должна открывать/закрывать эта кнопка?")]
        [SerializeField] private UIWindowType targetWindow;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            
            // Подписываемся на клик через код, чтобы не засорять Inspector ручными связями
            _button.onClick.AddListener(OnButtonClicked);
        }

        private void OnDestroy()
        {
            // Отписываемся для предотвращения утечек памяти
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            if (HUDManager.Instance != null)
            {
                // Вызываем уже готовую логику из HUDManager
                Debug.Log("[UIWindowOpenerButton] НАЖАТА");
                HUDManager.Instance.ToggleWindow(targetWindow);
            }
            else
            {
                Debug.LogWarning("[UIWindowOpenerButton] HUDManager не найден на сцене!");
            }
        }
    }
}