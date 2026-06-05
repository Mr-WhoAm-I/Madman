using UnityEngine;
using UnityEngine.UI;
using _Project.Scripts.Network.Managers; // Путь к NetworkManager

namespace _Project.Scripts.UI.Battle
{
    public class AmmoSelectionUI : MonoBehaviour
    {
        [Header("Кнопки выбора патронов")]
        [SerializeField] private Button _btnPhysical;
        [SerializeField] private Button _btnFire;
        [SerializeField] private Button _btnCryo;
        [SerializeField] private Button _btnToxic;

        [Header("Визуальное выделение (Рамки)")]
        [SerializeField] private Image _highlightPhysical;
        [SerializeField] private Image _highlightFire;
        [SerializeField] private Image _highlightCryo;
        [SerializeField] private Image _highlightToxic;

        private void Start()
        {
            _btnPhysical.onClick.AddListener(() => SelectAmmo(0));
            _btnFire.onClick.AddListener(() => SelectAmmo(1));
            _btnCryo.onClick.AddListener(() => SelectAmmo(2));
            _btnToxic.onClick.AddListener(() => SelectAmmo(3));

            // Подписываемся на изменения извне (от сервера или инпута)
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnAmmoChoiceChanged += UpdateVisuals;
            }

            UpdateVisuals(0);
        }

        private void SelectAmmo(byte ammoType)
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SetAmmoChoice(ammoType);
            }
            // UpdateVisuals(ammoType); убираем отсюда, так как ивент сам вызовет обновление
        }

        private void OnDestroy()
        {
            _btnPhysical.onClick.RemoveAllListeners();
            _btnFire.onClick.RemoveAllListeners();
            _btnCryo.onClick.RemoveAllListeners();
            _btnToxic.onClick.RemoveAllListeners();

            // Отписываемся
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnAmmoChoiceChanged -= UpdateVisuals;
            }
        }

        private void UpdateVisuals(byte ammoType)
        {
            if (_highlightPhysical) _highlightPhysical.enabled = (ammoType == 0);
            if (_highlightFire) _highlightFire.enabled = (ammoType == 1);
            if (_highlightCryo) _highlightCryo.enabled = (ammoType == 2);
            if (_highlightToxic) _highlightToxic.enabled = (ammoType == 3);
        }
    }
}