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
    }
}