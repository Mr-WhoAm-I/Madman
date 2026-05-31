using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "CerberusTurretData", menuName = "Madman/Skills/Cerberus Turret Settings")]
    public class TurretSkillData : ScriptableObject
    {
        [Header("Настройки Турели")]
        public NetworkPrefabRef turretPrefab;
        public float baseHealth = 500f;

        [Header("Агро-система (Taunt)")]
        public float tauntDuration = 5f;

        [Header("Боевая система")]
        public NetworkPrefabRef bulletPrefab;
        public float fireRate = 1f; 
        public float attackRadius = 15f; 

        [Header("Снаряды Турели (Урон)")]
        public float bulletDamage = 15f;
        public float bulletSpeed = 10f;
        public float bulletLifeTime = 2f;

        [Header("Параметры Перков")]
        [Tooltip("Множитель ХП при взятии 'Запасные детали'")]
        public float sparePartsHealthMult = 0.7f;
        
        [Tooltip("Радиус лечения 'Полевой медик'")]
        public float healAuraRadius = 3f;
        
        [Tooltip("Радиус детонации 'Взрывной реактор'")]
        public float explosionRadius = 4f;
        
        [Tooltip("Урон детонации 'Взрывной реактор'")]
        public float explosionDamage = 150f;
        
        [Tooltip("Множитель скорости для 'Крио-снаряды' (0.7 = 70% от нормы)")]
        public float cryoSlowMultiplier = 0.7f;
        
        [Tooltip("Длительность заморозки от 'Крио-снарядов'")]
        public float cryoDuration = 3f;
    }
}