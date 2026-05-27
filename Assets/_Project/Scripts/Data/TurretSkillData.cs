using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "CerberusTurretData", menuName = "Madman/Skills/Cerberus Turret")]
    public class TurretSkillData : SkillData
    {
        [Header("Спавн и Лимиты")]
        public NetworkPrefabRef turretPrefab;
        public int maxTurrets = 1; // Задел на будущее улучшение

        [Header("Характеристики")]
        public float baseHealth = 500f;
        public float tauntDuration = 5f;
        public float tauntRadius = 10f;

        [Header("Боевая система")]
        public NetworkPrefabRef bulletPrefab;
        public float fireRate = 1f; // Выстрелов в секунду
        public float attackRadius = 15f; // Дальность стрельбы
    }
}