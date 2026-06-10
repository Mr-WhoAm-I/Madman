using System.Collections.Generic;
using UnityEngine;
using TMPro;
using _Project.Scripts.Core;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Hub; // Пространство имен для HubWindowBase

namespace _Project.Scripts.UI
{
    // Наследуемся от HubWindowBase, чтобы терминал мог открыть нас
    public class ConsumableShopUIManager : HubWindowBase 
    {
        [Header("Цены и пачки патронов")]
        public int ammoPackSize = 30;
        public int ammoPackCost = 50;

        [Header("UI: Тексты баланса (Профиль)")]
        public TextMeshProUGUI metaCurrencyText; 
        public TextMeshProUGUI fireAmmoText;
        public TextMeshProUGUI cryoAmmoText;
        public TextMeshProUGUI toxicAmmoText;

        [Header("UI: Спавн карточек")]
        public Transform consumablesContainer; // Parent для списка (например, Content внутри ScrollView)
        public ConsumableCardUI consumableCardPrefab;

        private List<ConsumableCardUI> _spawnedCards = new List<ConsumableCardUI>();

        private void Start()
        {
            InitializeCards();
        }

        // Переопределяем метод Open из базового класса UIWindow
        public override void Open() 
        {
            base.Open();
            UpdateUI();
        }

        // ==========================================
        // ИНИЦИАЛИЗАЦИЯ (Паттерн Data-Driven UI)
        // ==========================================
        private void InitializeCards()
        {
            foreach (var card in _spawnedCards) Destroy(card.gameObject);
            _spawnedCards.Clear();

            var allConsumables = ProfileController.Instance.AllConsumables;

            if (allConsumables == null) return;

            foreach (var consumable in allConsumables)
            {
                var card = Instantiate(consumableCardPrefab, consumablesContainer);
                card.Setup(consumable, TryUnlockConsumable, EquipConsumable);
                _spawnedCards.Add(card);
            }
        }

        // ==========================================
        // ЛОГИКА ТРАНЗАКЦИЙ (Мета-валюта)
        // ==========================================
        public void BuyFireAmmo() => TryBuyAmmo(1);
        public void BuyCryoAmmo() => TryBuyAmmo(2);
        public void BuyToxicAmmo() => TryBuyAmmo(3);

        private void TryBuyAmmo(int ammoType)
        {
            var profile = ProfileController.Instance.CurrentProfile;

            // Примечание: Используем TrySpendCrystals, т.к. ты сказал, что не переименовывал их на прошлом этапе[cite: 2]
            if (profile.TrySpendMemoryShards(ammoPackCost))
            {
                // Метод AddAmmo мы добавили в PlayerProfile на Этапе 1
                if (ammoType == 1) profile.AddAmmo(ammoPackSize, 0, 0);
                else if (ammoType == 2) profile.AddAmmo(0, ammoPackSize, 0);
                else if (ammoType == 3) profile.AddAmmo(0, 0, ammoPackSize);

                ProfileController.Instance.SaveGame();
                UpdateUI();
            }
            else
            {
                Debug.LogWarning("[Магазин] Недостаточно осколков памяти!");
            }
        }

        private void TryUnlockConsumable(ConsumableData data)
        {
            var profile = ProfileController.Instance.CurrentProfile;

            if (profile.UnlockedConsumables.Contains(data.consumableID)) return;

            if (profile.TrySpendMemoryShards(data.unlockCost))
            {
                profile.UnlockedConsumables.Add(data.consumableID);
                ProfileController.Instance.SaveGame();
                UpdateUI();
            }
        }

        private void EquipConsumable(ConsumableData data, int slotIndex)
        {
            var profile = ProfileController.Instance.CurrentProfile;
            if (!profile.UnlockedConsumables.Contains(data.consumableID)) return;

            var progression = ProfileController.Instance.GetActiveArchetypeData();
            if (progression != null)
            {
                progression.EquipConsumable(data.consumableID, slotIndex);

                // Защита: Если этот же предмет был во втором слоте, снимаем его оттуда
                int otherSlot = slotIndex == 0 ? 1 : 0;
                if (progression.EquippedConsumableIDs[otherSlot] == data.consumableID)
                {
                    progression.EquipConsumable("", otherSlot);
                }

                ProfileController.Instance.SaveGame();
                UpdateUI();
            }
        }

        // ==========================================
        // ОБНОВЛЕНИЕ ВИЗУАЛА 
        // ==========================================
        private void UpdateUI()
        {
            var profile = ProfileController.Instance.CurrentProfile;
            var progression = ProfileController.Instance.GetActiveArchetypeData();

            // Обновляем тексты
            if (metaCurrencyText) metaCurrencyText.text = profile.MemoryShards.ToString();
            if (fireAmmoText) fireAmmoText.text = profile.FireAmmo.ToString();
            if (cryoAmmoText) cryoAmmoText.text = profile.CryoAmmo.ToString();
            if (toxicAmmoText) toxicAmmoText.text = profile.ToxicAmmo.ToString();

            // Пробегаем по всем заспавненным карточкам и сверяем их состояние с профилем игрока
            for (int i = 0; i < ProfileController.Instance.AllConsumables.Length; i++)
            {
                var data = ProfileController.Instance.AllConsumables[i];
                bool isUnlocked = profile.UnlockedConsumables.Contains(data.consumableID);
                
                bool isSlot1 = progression != null && progression.EquippedConsumableIDs[0] == data.consumableID;
                bool isSlot2 = progression != null && progression.EquippedConsumableIDs[1] == data.consumableID;

                _spawnedCards[i].RefreshState(isUnlocked, isSlot1, isSlot2);
            }
        }
    }
}