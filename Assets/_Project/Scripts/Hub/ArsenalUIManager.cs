using _Project.Scripts.Core;
using _Project.Scripts.Data.Weapons;
using _Project.Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Scripts.Hub
{
    public class ArsenalUIManager : HubWindowBase
    {
        [Header("База Данных")]
        public WeaponCatalogData weaponCatalog;

        [Header("Левая Панель (Слоты)")]
        public Button[] classTabs; // 0-Ист, 1-Пар, 2-Шиз, 3-Мел
        public GameObject slot2Container; // Скрываем для всех, кроме Истерика
        public TextMeshProUGUI slot1Text;
        public TextMeshProUGUI slot2Text;

        [Header("Центр (Список)")]
        public Transform weaponListContainer;
        public ArsenalWeaponCard weaponCardPrefab;

        [Header("Правая Панель (Характеристики)")]
        public TextMeshProUGUI weaponNameText;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI damageText;
        public TextMeshProUGUI fireRateText;
        public TextMeshProUGUI magazineText;

        [Header("Кнопки Действий")]
        public Button upgradeButton;
        public TextMeshProUGUI upgradeCostText;
        public Button equipButton;
        public TextMeshProUGUI equipButtonText;

        private int _currentArchetypeIndex = 0;
        private WeaponData _selectedWeapon;
        
        
        protected override void OnWindowOpened()
        {
            if (ProfileController.Instance)
            {
                _currentArchetypeIndex = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
            }
            SelectClassTab(_currentArchetypeIndex);
        }

        public void SelectClassTab(int index)
        {
            _currentArchetypeIndex = index;
            slot2Container.SetActive(_currentArchetypeIndex == 0); // Включаем 2-й слот только Истерику

            // Очищаем старый список
            foreach (Transform child in weaponListContainer) Destroy(child.gameObject);

            // Определяем категорию оружия по классу
            WeaponCategory targetCategory = WeaponCategory.Handguns;
            switch (index)
            {
                case 0: targetCategory = WeaponCategory.Handguns; break;
                case 1: targetCategory = WeaponCategory.Shotguns; break;
                case 2: targetCategory = WeaponCategory.SniperRifles; break;
                case 3: targetCategory = WeaponCategory.AssaultRifles; break;
            }

            var weapons = weaponCatalog.GetWeaponsByCategory(targetCategory);
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);

            bool firstWeaponSelected = false;

            foreach (var weapon in weapons)
            {
                var card = Instantiate(weaponCardPrefab, weaponListContainer);
                var saveInfo = progression.GetWeaponData(weapon.weaponID);
                
                // Если цена 0, считаем разблокированным по умолчанию
                bool isUnlocked = saveInfo.IsUnlocked || weapon.unlockCost == 0; 
                card.Setup(weapon, isUnlocked, this);

                if (!firstWeaponSelected)
                {
                    SelectWeapon(weapon);
                    firstWeaponSelected = true;
                }
            }

            UpdateEquippedSlotsUI();
        }

        public void SelectWeapon(WeaponData weapon)
        {
            _selectedWeapon = weapon;
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);
            var saveInfo = progression.GetWeaponData(weapon.weaponID);

            bool isUnlocked = saveInfo.IsUnlocked || weapon.unlockCost == 0;

            weaponNameText.text = weapon.weaponName;
            levelText.text = isUnlocked ? $"УРОВЕНЬ {saveInfo.Level}" : "ЗАБЛОКИРОВАНО";

            // === МАТЕМАТИКА ПРОКАЧКИ (ААА-Стандарт) ===
            // Урон растет на 5% за каждый уровень (на 1 уровне множитель 1.0)
            float damageMultiplier = 1f + (saveInfo.Level - 1) * 0.05f;
            float actualDamage = weapon.damage * damageMultiplier;

            damageText.text = actualDamage.ToString("F1");
            fireRateText.text = weapon.fireRate.ToString("F2");
            magazineText.text = weapon.magazineSize.ToString();

            // Настройка кнопок
            upgradeButton.onClick.RemoveAllListeners();
            equipButton.onClick.RemoveAllListeners();

            if (!isUnlocked)
            {
                equipButton.interactable = false;
                equipButtonText.text = "НЕДОСТУПНО";
                
                upgradeCostText.text = $"КУПИТЬ: {weapon.unlockCost}";
                upgradeButton.onClick.AddListener(() => UnlockWeapon(weapon.weaponID, weapon.unlockCost));
            }
            else
            {
                equipButton.interactable = true;
                equipButtonText.text = IsWeaponEquipped(weapon.weaponID) ? "СНЯТЬ" : "ВЫБРАТЬ";
                equipButton.onClick.AddListener(() => ToggleEquipWeapon(weapon.weaponID));

                if (saveInfo.Level < 30)
                {
                    int upgradeCost = weapon.upgradeBaseCost * saveInfo.Level; // Цена растет с уровнем
                    upgradeCostText.text = $"УЛУЧШИТЬ: {upgradeCost}";
                    upgradeButton.onClick.AddListener(() => UpgradeWeapon(weapon.weaponID, upgradeCost));
                    upgradeButton.interactable = true;
                }
                else
                {
                    upgradeCostText.text = "МАКС. УРОВЕНЬ";
                    upgradeButton.interactable = false;
                }
            }
        }

        private void UnlockWeapon(string weaponID, int cost)
        {
            // Здесь должна быть проверка валюты (например, Кристаллов из профиля). 
            // Пока считаем, что денег хватает.
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);
            progression.UnlockWeapon(weaponID);
            ProfileController.Instance.SaveGame();
            
            SelectClassTab(_currentArchetypeIndex); // Перерисовываем UI
        }

        private void UpgradeWeapon(string weaponID, int cost)
        {
            // Здесь проверка валюты
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);
            progression.UpgradeWeapon(weaponID);
            ProfileController.Instance.SaveGame();
            
            SelectWeapon(_selectedWeapon); // Обновляем статы
        }

        private void ToggleEquipWeapon(string weaponID)
        {
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);

            // Если оружие уже надето - снимаем
            if (IsWeaponEquipped(weaponID))
            {
                for (int i = 0; i < progression.EquippedWeaponIDs.Length; i++)
                {
                    if (progression.EquippedWeaponIDs[i] == weaponID) progression.EquippedWeaponIDs[i] = "";
                }
            }
            else
            {
                // Смарт-экипировка
                if (_currentArchetypeIndex == 0) // Истерик
                {
                    if (string.IsNullOrEmpty(progression.EquippedWeaponIDs[0])) 
                        progression.EquipWeapon(weaponID, 0);
                    else 
                        progression.EquipWeapon(weaponID, 1); // Перезапишет 2 слот, если 1 занят
                }
                else // Остальные (только 1 слот)
                {
                    progression.EquipWeapon(weaponID, 0);
                }
            }

            ProfileController.Instance.SaveGame();
            UpdateEquippedSlotsUI();
            SelectWeapon(_selectedWeapon); // Обновляем текст на кнопке
        }

        private bool IsWeaponEquipped(string weaponID)
        {
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);
            return progression.EquippedWeaponIDs[0] == weaponID || progression.EquippedWeaponIDs[1] == weaponID;
        }

        private void UpdateEquippedSlotsUI()
        {
            var progression = ProfileController.Instance.CurrentProfile.GetProgressForArchetype(_currentArchetypeIndex);
            
            // Обновляем текст/иконки в слотах слева (можно заменить на красивые картинки из базы)
            slot1Text.text = string.IsNullOrEmpty(progression.EquippedWeaponIDs[0]) ? "ПУСТО" : weaponCatalog.GetWeaponByID(progression.EquippedWeaponIDs[0]).weaponName;
            
            if (_currentArchetypeIndex == 0)
            {
                slot2Text.text = string.IsNullOrEmpty(progression.EquippedWeaponIDs[1]) ? "ПУСТО" : weaponCatalog.GetWeaponByID(progression.EquippedWeaponIDs[1]).weaponName;
            }
        }
        
        // Вызывать из UnityEvent (Терминала) вместо обычного Open()
    }
}