using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Shop;
using _Project.Scripts.Network;
using _Project.Scripts.ECS.Components;
using _Project.Scripts.ECS.Components.Skills;
using _Project.Scripts.Network.Bridges; // Для получения SkillConfigComponent

namespace _Project.Scripts.Gameplay
{
    // Класс-обертка для карточки на витрине
    public class ShopOffer
    {
        public UpgradeData Data;
        public int FinalCost;
        public int DiscountPercent; // 0, 10, 20, 30, 40, 50
        public bool IsSold; // Куплен ли предмет (чтобы оставлять пустой слот)
    }

    public class LocalShopManager : MonoBehaviour
    {
        public static LocalShopManager Instance;
        
        private List<UpgradeData> _allUpgradesDatabase;
        private Dictionary<string, UpgradeData> _upgradeDict;

        // СОСТОЯНИЕ ВИТРИНЫ
        public List<ShopOffer> CurrentOffers = new();
        public int AvailableRerolls { get; private set; } // Доступные рероллы сейчас

        private void Awake()
        {
            Instance = this;
            _allUpgradesDatabase = Resources.LoadAll<UpgradeData>("Upgrades").ToList();
            
            _upgradeDict = new Dictionary<string, UpgradeData>();
            foreach (var upgrade in _allUpgradesDatabase)
            {
                if (!_upgradeDict.ContainsKey(upgrade.upgradeID))
                    _upgradeDict.Add(upgrade.upgradeID, upgrade);
            }
        }

        public UpgradeData GetUpgradeByID(string id)
        {
            if (_upgradeDict.TryGetValue(id, out var upgrade)) return upgrade;
            return null;
        }

        // Вызывается из WaveManager в начале фазы магазина
        public void OnShopPhaseStarted(int playerMaxRerolls)
        {
            // Начисляем рероллы до максимума
            AvailableRerolls = playerMaxRerolls;

            // Если витрина пуста (самая первая волна или игрок скупил всё) — генерируем новую
            if (CurrentOffers.Count == 0 || CurrentOffers.All(o => o.IsSold))
            {
                GenerateNewOffers();
            }
            // ИНАЧЕ мы НИЧЕГО не делаем — старые некупленные предметы сохранятся!
        }

        // Метод ручного реролла (по кнопке)
        public bool TryReroll()
        {
            if (AvailableRerolls <= 0) return false;
            
            AvailableRerolls--;
            GenerateNewOffers();
            return true;
        }

        // Внутренняя логика генерации (с учетом скидок)
        private void GenerateNewOffers()
        {
            CurrentOffers.Clear();
            var player = PlayerNetworkBridge.LocalPlayer;
            if (player == null) return;

            // Читаем статы игрока (минимальную скидку)
            float playerMinDiscount = 0f;
            if (player.EntityManager.Exists(player.PlayerEntity))
            {
                var config = player.EntityManager.GetComponentData<SkillConfigComponent>(player.PlayerEntity);
                playerMinDiscount = config.MinDiscount;
            }

            // 1. ФИЛЬТРАЦИЯ (как и раньше)
            List<UpgradeData> validUpgrades = new List<UpgradeData>();
            foreach (var upgrade in _allUpgradesDatabase)
            {
                if (upgrade.requiredArchetypeID != -1 && upgrade.requiredArchetypeID != player.NetworkArchetypeID) continue;
                if (player.PurchasedUpgrades.Any(id => id.ToString() == upgrade.upgradeID)) continue;
                if (upgrade.requiredUpgrade != null && !player.PurchasedUpgrades.Any(id => id.ToString() == upgrade.requiredUpgrade.upgradeID)) continue;
                
                validUpgrades.Add(upgrade);
            }

            // 2. ВЗВЕШЕННЫЙ РАНДОМ И СКИДКИ
            int offersCount = Mathf.Min(3, validUpgrades.Count);
            for (int i = 0; i < offersCount; i++)
            {
                int totalWeight = validUpgrades.Sum(u => GetWeight(u.rarity));
                int randomWeight = Random.Range(0, totalWeight);
                int currentWeight = 0;

                for (int j = 0; j < validUpgrades.Count; j++)
                {
                    currentWeight += GetWeight(validUpgrades[j].rarity);
                    if (currentWeight > randomWeight)
                    {
                        var selectedData = validUpgrades[j];
                        
                        // РАСЧЕТ СКИДКИ (Шаги: 10%, 20%...)
                        int discountPercent = CalculateDiscount(playerMinDiscount, selectedData.baseCost);
                        int finalCost = selectedData.baseCost - Mathf.RoundToInt(selectedData.baseCost * (discountPercent / 100f));

                        CurrentOffers.Add(new ShopOffer
                        {
                            Data = selectedData,
                            FinalCost = finalCost,
                            DiscountPercent = discountPercent,
                            IsSold = false
                        });

                        validUpgrades.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        private int CalculateDiscount(float playerMinDiscount, int baseCost)
        {
            // Бесплатные предметы не получают скидок
            if (baseCost == 0) return 0;

            // Возможные шаги скидок (в процентах)
            List<int> possibleDiscounts = new List<int> { 0, 10, 20, 30, 40, 50 };
            
            // Отсекаем всё, что ниже минимальной скидки игрока (если есть перк)
            int minDiscountInt = Mathf.RoundToInt(playerMinDiscount * 100);
            possibleDiscounts.RemoveAll(d => d < minDiscountInt);

            if (possibleDiscounts.Count == 0) return 50; // Кап 50%

            // --- ВЗВЕШЕННЫЙ РАНДОМ СКИДОК ---
            int totalWeight = possibleDiscounts.Sum(d => GetDiscountWeight(d));
            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (int discount in possibleDiscounts)
            {
                currentWeight += GetDiscountWeight(discount);
                if (currentWeight > randomWeight)
                {
                    return discount;
                }
            }

            return possibleDiscounts[0]; // Фолбэк (на всякий случай)
        }

        // Настройка редкости скидок (чем больше вес, тем чаще падает)
        private int GetDiscountWeight(int discount)
        {
            return discount switch
            {
                0 => 750 // 75% шанс, что скидки вообще не будет
                ,
                10 => 150 // 15% шанс на базовую скидку
                ,
                20 => 60 // 6% шанс 
                ,
                30 => 25 // 2.5% шанс
                ,
                40 => 10 // 1% шанс (очень редко)
                ,
                50 => 5 // 0.5% шанс (джекпот)
                ,
                _ => 0
            };
        }

        private int GetWeight(UpgradeRarity rarity)
        {
            return rarity switch
            {
                UpgradeRarity.Ordinary => 100,
                UpgradeRarity.Experimental => 60,
                UpgradeRarity.Banned => 30,
                UpgradeRarity.Anomalous => 10,
                _ => 50
            };
        }
    }
}