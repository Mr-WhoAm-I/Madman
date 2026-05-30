using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using _Project.Scripts.Data;
using _Project.Scripts.Network;

namespace _Project.Scripts.Gameplay
{
    public class LocalShopManager : MonoBehaviour
    {
        public static LocalShopManager Instance;
        
        private List<UpgradeData> _allUpgradesDatabase;
        // Быстрый словарь для поиска сервером
        private Dictionary<string, UpgradeData> _upgradeDict;

        private void Awake()
        {
            Instance = this;
            _allUpgradesDatabase = Resources.LoadAll<UpgradeData>("Upgrades").ToList();
            _upgradeDict = new Dictionary<string, UpgradeData>();
            foreach (var upgrade in _allUpgradesDatabase.Where(upgrade => !_upgradeDict.ContainsKey(upgrade.upgradeID)))
            {
                _upgradeDict.Add(upgrade.upgradeID, upgrade);
            }
            Debug.Log($"[МАГАЗИН] Загружено {_allUpgradesDatabase.Count} улучшений из базы данных.");
        }

        public List<UpgradeData> GenerateShopOffers(PlayerNetworkBridge player)
        {
            List<UpgradeData> validUpgrades = new List<UpgradeData>();

            // --- 1. ФИЛЬТРАЦИЯ (Отсеиваем недоступное) ---
            foreach (var upgrade in _allUpgradesDatabase)
            {
                if (upgrade.requiredArchetypeID != -1 && upgrade.requiredArchetypeID != player.NetworkArchetypeID)
                    continue;

                bool alreadyBought = false;
                foreach (var purchasedId in player.PurchasedUpgrades)
                {
                    if (purchasedId.ToString() == upgrade.upgradeID)
                    {
                        alreadyBought = true;
                        break;
                    }
                }
                if (alreadyBought) continue;

                if (upgrade.requiredUpgrade != null)
                {
                    bool hasRequirement = false;
                    foreach (var purchasedId in player.PurchasedUpgrades)
                    {
                        if (purchasedId.ToString() == upgrade.requiredUpgrade.upgradeID)
                        {
                            hasRequirement = true;
                            break;
                        }
                    }
                    if (!hasRequirement) continue; 
                }

                validUpgrades.Add(upgrade);
            }

            // --- 2. ВЗВЕШЕННЫЙ РАНДОМ (Формируем витрину) ---
            List<UpgradeData> finalOffers = new List<UpgradeData>();
            int offersCount = Mathf.Min(3, validUpgrades.Count);

            for (int i = 0; i < offersCount; i++)
            {
                // Считаем общий вес всех оставшихся доступных карточек
                int totalWeight = validUpgrades.Sum(u => GetWeight(u.rarity));
                
                // Кидаем "кубик"
                int randomWeight = Random.Range(0, totalWeight);
                int currentWeight = 0;

                for (int j = 0; j < validUpgrades.Count; j++)
                {
                    currentWeight += GetWeight(validUpgrades[j].rarity);
                    
                    // Если накопительный вес превысил бросок кубика — забираем эту карточку
                    if (currentWeight > randomWeight)
                    {
                        finalOffers.Add(validUpgrades[j]);
                        validUpgrades.RemoveAt(j); // Удаляем из пула, чтобы не предложить ее дважды
                        break;
                    }
                }
            }

            return finalOffers;
        }

        // Метод для определения "веса" редкости
        private int GetWeight(UpgradeRarity rarity)
        {
            switch (rarity)
            {
                case UpgradeRarity.Ordinary: return 100;
                case UpgradeRarity.Experimental: return 60;
                case UpgradeRarity.Banned: return 30;
                case UpgradeRarity.Anomalous: return 10;
                default: return 50;
            }
        }
        
        public UpgradeData GetUpgradeByID(string id)
        {
            if (_upgradeDict.TryGetValue(id, out var upgrade))
                return upgrade;
                
            Debug.LogError($"[МАГАЗИН] Улучшение с ID {id} не найдено в базе!");
            return null;
        }
    }
}