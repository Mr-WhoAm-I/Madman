using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _Project.Scripts.Data.Weapons
{
    [CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Game Data/Weapon Catalog", order = 0)]
    public class WeaponCatalogData : ScriptableObject
    {
        [Header("База всего оружия в игре")]
        public List<WeaponData> AllWeapons = new();

        /// <summary>
        /// Возвращает список оружия для конкретного класса (категории)
        /// </summary>
        public List<WeaponData> GetWeaponsByCategory(WeaponCategory targetCategory)
        {
            return AllWeapons.Where(w => w.category == targetCategory).ToList();
        }

        /// <summary>
        /// Ищет конкретное оружие по его ID (нужно для загрузки сохранений)
        /// </summary>
        public WeaponData GetWeaponByID(string id)
        {
            return AllWeapons.FirstOrDefault(w => w.weaponID == id);
        }
    }
}