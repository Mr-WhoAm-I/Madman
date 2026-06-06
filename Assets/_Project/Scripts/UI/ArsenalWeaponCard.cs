using _Project.Scripts.Data.Weapons;
using _Project.Scripts.Hub;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.UI
{
    public class ArsenalWeaponCard : MonoBehaviour
    {
        public Image weaponIcon;      // Иконка оружия (если добавишь позже)
        public TextMeshProUGUI nameText; // Название пушки
        public GameObject lockIcon;   // Иконка замочка
        public Button cardButton;     // Сама кнопка карточки

        private WeaponData _myWeapon;
        private ArsenalUIManager _manager;

        public void Setup(WeaponData weapon, bool isUnlocked, ArsenalUIManager manager)
        {
            _myWeapon = weapon;
            _manager = manager;

            if (nameText != null) nameText.text = weapon.weaponName;
            if (lockIcon != null) lockIcon.SetActive(!isUnlocked);
            
            // Здесь можно будет подставлять weaponIcon.sprite = weapon.icon;

            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => _manager.SelectWeapon(_myWeapon));
        }
    }
}