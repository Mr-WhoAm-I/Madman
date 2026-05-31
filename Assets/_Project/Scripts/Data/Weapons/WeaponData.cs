using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data.Weapons
{
    // Этот атрибут добавит новую кнопку в меню Unity по правому клику мыши!
    [CreateAssetMenu(fileName = "NewWeapon", menuName = "Madman/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Базовые характеристики")]
        public string weaponName = "Новое оружие";
        public WeaponCategory category;
        public float damage = 25f;
        public float fireRate = 0.3f; // Задержка между выстрелами
        
        [Header("Модификаторы стрельбы")]
        public int pelletCount = 1;
        public float spreadAngle = 0f; 
        public float bulletLifeTime = 1f;
        public float bulletSpeed = 15f;
        [Header("Снаряд")]
        public NetworkPrefabRef bulletPrefab; // Ссылка на то, чем стреляем
        
    }
}