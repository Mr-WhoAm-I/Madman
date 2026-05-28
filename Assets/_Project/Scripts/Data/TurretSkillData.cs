using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "CerberusTurretData", menuName = "Madman/Skills/Cerberus Turret")]
    public class TurretSkillData : SkillData
    {
        [Header("Настройки Турели")]
        public NetworkPrefabRef turretPrefab;
        public int maxTurrets = 1; 
        public float baseHealth = 500f;

        [Header("Агро-система (Taunt)")]
        public float tauntDuration = 5f;
        // tauntRadius берем из базового effectRadius!

        [Header("Боевая система")]
        public NetworkPrefabRef bulletPrefab;
        public float fireRate = 1f; 
        // attackRadius можно оставить тут, если он отличается от effectRadius
        public float attackRadius = 15f; 
    }
}