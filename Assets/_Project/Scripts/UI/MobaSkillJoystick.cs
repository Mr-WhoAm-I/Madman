using UnityEngine;
using UnityEngine.EventSystems;
using _Project.Scripts.Network;
using _Project.Scripts.Network.Bridges;
using _Project.Scripts.Network.Managers;

namespace _Project.Scripts.UI
{
    public class MobaSkillJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public static MobaSkillJoystick Instance { get; private set; }

        [Header("Настройки")]
        public float maxDragDistance = 100f; // Радиус оттяжки
        public float deadZone = 0.15f; // Если свайпнули меньше этого, сработает автонаведение
        public float autoAimRadius = 15f; // Радиус поиска врага для автонаведения

        [Header("Визуал (Опционально)")]
        [Tooltip("Можно положить сюда CooldownOverlay или создать отдельный кружок-ручку")]
        public RectTransform handle; 
        
        private Vector2 _startPosition;
        private Vector2 _currentAimDirection;
        private int _fireLatchCount; // ИСПРАВЛЕНО: Заменили нестабильный bool на счетчик тиков защелки (Input Latching)
        private bool _isDragging;

        private void Awake()
        {
            Instance = this;
            if (handle != null) _startPosition = handle.anchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // БЛОКИРОВКА: Если игрока нет или скилл в откате (процент > 0) — игнорируем нажатие
            if (PlayerNetworkBridge.LocalPlayer == null || PlayerNetworkBridge.LocalPlayer.GetSkillCooldownPercentage() > 0f)
            {
                return;
            }

            _isDragging = true;
            OnDrag(eventData); // Сразу просчитываем позицию
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            // Считаем смещение от центра кнопки
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out var localPoint);

            // Ограничиваем сдвиг
            var clampedPos = Vector2.ClampMagnitude(localPoint, maxDragDistance);
            
            if (handle != null)
            {
                handle.anchoredPosition = _startPosition + clampedPos;
            }

            // Записываем вектор отклонения (от 0 до 1)
            _currentAimDirection = clampedPos / maxDragDistance;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            
            if (handle != null)
            {
                handle.anchoredPosition = _startPosition; // Возвращаем ручку на место
            }

            // Если это быстрый тап (не вышли за мертвую зону) -> Автонаведение
            if (_currentAimDirection.magnitude < deadZone)
            {
                _currentAimDirection = TryFindAutoAimTarget();
            }
            else
            {
                // Иначе стреляем точно туда, куда свайпнули
                _currentAimDirection = _currentAimDirection.normalized;
            }

            // ИСПРАВЛЕНО: Вместо разового импульса взводим защелку на 3 сетевых опроса Fusion.
            // Это гарантирует избыточность (redundancy) против потери UDP-пакетов в мобильной сети
            // и полностью нейтрализует рассинхрон фреймрейта (120 FPS экрана против фиксированной сети).
            _fireLatchCount = 3;
        }

        private Vector2 TryFindAutoAimTarget()
        {
            if (PlayerNetworkBridge.LocalPlayer == null) return Vector2.zero;

            Vector2 playerPos = PlayerNetworkBridge.LocalPlayer.transform.position;
            var closestDir = Vector2.zero;
            var closestDist = float.MaxValue;

            var swarmManager = EnemySwarmManager.Instance;
            if (swarmManager != null)
            {
                for (var i = 0; i < swarmManager.EnemyStates.Length; i++)
                {
                    var enemy = swarmManager.EnemyStates[i];
                    if (!enemy.IsActive) continue;

                    var dist = Vector2.Distance(playerPos, enemy.Position);
                    if (dist <= autoAimRadius && dist < closestDist)
                    {
                        closestDist = dist;
                        closestDir = (enemy.Position - playerPos).normalized;
                    }
                }
            }

            return closestDir; // Вернет (0,0), если врагов нет
        }

        // Вызывается из NetworkManager каждый сетевой тик опроса инпута
        public bool ConsumeFireEvent(out Vector2 aimDir)
        {
            aimDir = _currentAimDirection;
            
            // ИСПРАВЛЕНО: Пока счетчик защелки активен, удерживаем состояние нажатия навыка во Fusion пакетах
            if (_fireLatchCount > 0)
            {
                _fireLatchCount--;
                return true;
            }
            return false;
        }
    }
}