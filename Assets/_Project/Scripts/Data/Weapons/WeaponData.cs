using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data.Weapons
{
    public enum AmmoType { Infinite, Magazine }
    public enum WeaponElementalType { Physical, Fire, Cryo, Toxic } // Задел на будущее для делюзий

    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Madman/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Базовые характеристики")]
        public string weaponName = "Новое оружие";
        public WeaponCategory category;
        public float damage = 25f;
        public float fireRate = 0.3f; // Задержка между выстрелами
        
        [Header("Система патронов")]
        public AmmoType ammoSystem = AmmoType.Magazine;
        public int magazineSize = 10;       // Размер обоймы
        public float reloadTime = 1.5f;     // Время перезарядки
        
        [Header("Модификаторы стрельбы")]
        public int pelletCount = 1;
        public float spreadAngle = 0f; 
        public float bulletLifeTime = 1f;
        public float bulletSpeed = 15f;
        public bool pierceEnemies = false;  // Пробивает ли врагов насквозь?

        [Header("Снаряд")]
        public NetworkPrefabRef bulletPrefab;
        public WeaponElementalType innateElement = WeaponElementalType.Physical;
    }
}