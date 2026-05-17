using UnityEngine;
using UnityEngine.Events; // Нужно для событий
using UnityEngine.InputSystem;

namespace _Project.Scripts.Hub
{
    public class InteractableObject : MonoBehaviour
    {
        [Header("Настройки")]
        public string interactionName = "Объект";
        
        [Header("Событие при нажатии 'E'")]
        public UnityEvent onInteract; 

        private bool _isPlayerNear;

        private void Update()
        {
            if (_isPlayerNear && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                // Просто запускаем событие, настроенное в Инспекторе
                onInteract.Invoke();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Player")) _isPlayerNear = true;
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Player")) _isPlayerNear = false;
        }
    }
}