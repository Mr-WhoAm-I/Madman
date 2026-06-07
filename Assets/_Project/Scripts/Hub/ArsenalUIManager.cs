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
        [Header("Верхняя Панель (Экономика)")]
        public TextMeshProUGUI crystalsBalanceText;

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
        
        // === КЭШ СОСТОЯНИЯ ===
        private string _pendingWeaponID; 
        private int _pendingActionCost; 
        private bool _isPendingUpgrade;
        
        
        protected virtual void Awake()
        {
            base.Awake();
            upgradeButton.onClick.AddListener(OnUpgradeOrUnlockButtonClicked);
            equipButton.onClick.AddListener(OnEquipButtonClicked);
        }

        protected virtual void OnDestroy()
        {
            upgradeButton.onClick.RemoveListener(OnUpgradeOrUnlockButtonClicked);
            equipButton.onClick.RemoveListener(OnEquipButtonClicked);
        }

        protected override void OnWindowOpened()
        {
            if (ProfileController.Instance)
            {
                _currentArchetypeIndex = ProfileController.Instance.CurrentProfile.LastSelectedArchetypeID;
            }
            SelectClassTab(_currentArchetypeIndex);
            UpdateBalanceUI();
        }

        public void SelectClassTab(int index)
        {
            _currentArchetypeIndex = index;
            slot2Container.SetActive(_currentArchetypeIndex == 0); // Включаем 2-й слот только Истерику

            // Очищаем старый список
            foreach (Transform child in weaponListContainer) Destroy(child.gameObject);

            // Определяем категорию оружия по классу
            var targetCategory = index switch
            {
                0 => WeaponCategory.Handguns,
                1 => WeaponCategory.Shotguns,
                2 => WeaponCategory.SniperRifles,
                3 => WeaponCategory.AssaultRifles,
                _ => WeaponCategory.Handguns
            };

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
            var profile = ProfileController.Instance.CurrentProfile;
            var progression = profile.GetProgressForArchetype(_currentArchetypeIndex);
            var saveInfo = progression.GetWeaponData(weapon.weaponID);

            bool isUnlocked = saveInfo.IsUnlocked || weapon.unlockCost == 0;

            // Кэшируем ID для обработчиков кнопок
            _pendingWeaponID = weapon.weaponID;

            weaponNameText.text = weapon.weaponName;
            levelText.text = isUnlocked ? $"УРОВЕНЬ {saveInfo.Level}" : "ЗАБЛОКИРОВАНО";

            float damageMultiplier = 1f + (saveInfo.Level - 1) * 0.05f;
            damageText.text = (weapon.damage * damageMultiplier).ToString("F1");
            fireRateText.text = weapon.fireRate.ToString("F2");
            magazineText.text = weapon.magazineSize.ToString();

            // 2. БОЛЬШЕ НИКАКИХ RemoveAllListeners И ЗАМЫКАНИЙ
            if (!isUnlocked)
            {
                equipButton.interactable = false;
                equipButtonText.text = "НЕДОСТУПНО";
                
                _pendingActionCost = weapon.unlockCost;
                _isPendingUpgrade = false; // Это покупка
                
                bool canAfford = profile.Crystals >= _pendingActionCost;
                upgradeCostText.text = $"КУПИТЬ: {_pendingActionCost}";
                upgradeButton.interactable = canAfford;
            }
            else
            {
                equipButton.interactable = true;
                equipButtonText.text = IsWeaponEquipped(weapon.weaponID) ? "СНЯТЬ" : "ВЫБРАТЬ";

                if (saveInfo.Level < 30)
                {
                    _pendingActionCost = weapon.upgradeBaseCost * saveInfo.Level;
                    _isPendingUpgrade = true; // Это улучшение
                    
                    bool canAfford = profile.Crystals >= _pendingActionCost;
                    upgradeCostText.text = $"УЛУЧШИТЬ: {_pendingActionCost}";
                    upgradeButton.interactable = canAfford;
                }
                else
                {
                    upgradeCostText.text = "МАКС. УРОВЕНЬ";
                    upgradeButton.interactable = false;
                }
            }
        }
        
        private void OnUpgradeOrUnlockButtonClicked()
        {
            if (string.IsNullOrEmpty(_pendingWeaponID)) return;

            var profile = ProfileController.Instance.CurrentProfile;
            
            if (profile.TrySpendCrystals(_pendingActionCost))
            {
                var progression = profile.GetProgressForArchetype(_currentArchetypeIndex);
                
                if (_isPendingUpgrade)
                    progression.UpgradeWeapon(_pendingWeaponID);
                else
                    progression.UnlockWeapon(_pendingWeaponID);
                
                // 3. ОТЛОЖЕННОЕ СОХРАНЕНИЕ (Или выносим в OnWindowClosed)
                SaveGameState(); 
                
                UpdateBalanceUI();
                
                if (!_isPendingUpgrade)
                    SelectClassTab(_currentArchetypeIndex); // Перерисовываем список, если купили
                else
                    SelectWeapon(_selectedWeapon); // Обновляем статы, если улучшили
            }
        }

        private void OnEquipButtonClicked()
        {
            if (string.IsNullOrEmpty(_pendingWeaponID)) return;
            ToggleEquipWeapon(_pendingWeaponID);
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
        
        private void UpdateBalanceUI()
        {
            if (crystalsBalanceText && ProfileController.Instance)
            {
                crystalsBalanceText.text = ProfileController.Instance.CurrentProfile.Crystals.ToString();
            }
        }
        
        private void SaveGameState()
        {
            ProfileController.Instance.SaveGame(); 
        }
    }
}