using UnityEngine;
using UnityEngine.EventSystems; // Обязательно для фикса залипания!

namespace _Project.Scripts.Hub
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class HubWindowBase : MonoBehaviour
    {
        protected CanvasGroup _windowGroup;
        public bool IsOpen { get; private set; }

        protected virtual void Awake()
        {
            _windowGroup = GetComponent<CanvasGroup>();
            IsOpen = false;
            _windowGroup.alpha = 0f;
            _windowGroup.interactable = false;
            _windowGroup.blocksRaycasts = false;
        }

        public virtual void Open()
        {
            IsOpen = true;
            _windowGroup.alpha = 1f;
            _windowGroup.interactable = true;
            _windowGroup.blocksRaycasts = true;
            OnWindowOpened();
        }

        public virtual void Close()
        {
            IsOpen = false;
            _windowGroup.alpha = 0f;
            _windowGroup.interactable = false;
            _windowGroup.blocksRaycasts = false;

            // КРИТИЧЕСКИЙ ФИКС БАГА С WASD: Сбрасываем фокус с кнопок UI!
            if (EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            OnWindowClosed();
        }

        protected virtual void OnWindowOpened() { }
        protected virtual void OnWindowClosed() { }
    }
}