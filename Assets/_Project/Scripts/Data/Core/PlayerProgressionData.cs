using System;
using System.Collections.Generic;
using System.Linq;

namespace _Project.Scripts.Data.Core
{
    // Структура сохранения для конкретного ствола
    [Serializable]
    public class WeaponSaveData
    {
        public string WeaponID;
        public int Level;
        public bool IsUnlocked;
    }

    [Serializable]
    public class PlayerProgressionData
    {
        public int Level = 1;
        public float CurrentXP = 0f;
        public float XPToNextLevel = 100f;
        
        // ==========================================
        // СОХРАНЕНИЯ АРСЕНАЛА
        // ==========================================
        public List<WeaponSaveData> WeaponArsenal = new();
        
        // Массив слотов (Индекс 0 = Слот 1, Индекс 1 = Слот 2). Сохраняем ID оружия.
        public string[] EquippedWeaponIDs = new string[2] { "", "" };
        
        // Индекс 0 = Слот Q (или левый D-Pad), Индекс 1 = Слот E (или правый D-Pad)
        public string[] EquippedConsumableIDs = new string[2] { "", "" };

        // Безопасное получение или создание данных об оружии
        public WeaponSaveData GetWeaponData(string weaponID)
        {
            var weapon = WeaponArsenal.FirstOrDefault(w => w.WeaponID == weaponID);
            if (weapon == null)
            {
                // Если пушки еще нет в файле сохранения (например, добавили с патчем),
                // создаем ее с 1-м уровнем в заблокированном состоянии
                weapon = new WeaponSaveData { WeaponID = weaponID, Level = 1, IsUnlocked = false };
                WeaponArsenal.Add(weapon);
            }
            return weapon;
        }

        // Логика экипировки
        public void EquipWeapon(string weaponID, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= EquippedWeaponIDs.Length) return;
            EquippedWeaponIDs[slotIndex] = weaponID;
        }
        
        // Разблокировка нового слота
        public void UnlockWeapon(string weaponID)
        {
            var weapon = GetWeaponData(weaponID);
            weapon.IsUnlocked = true;
        }

        // Повышение уровня
        public void UpgradeWeapon(string weaponID)
        {
            var weapon = GetWeaponData(weaponID);
            weapon.Level++;
        }
        
        public void EquipConsumable(string consumableID, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= EquippedConsumableIDs.Length) return;
            EquippedConsumableIDs[slotIndex] = consumableID;
        }
    }
}