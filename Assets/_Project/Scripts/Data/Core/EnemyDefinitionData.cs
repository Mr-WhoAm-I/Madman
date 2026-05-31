using UnityEngine;

namespace _Project.Scripts.Data.Core
{
    [CreateAssetMenu(fileName = "New Enemy Def", menuName = "Madman/Enemy Definition")]
    public class EnemyDefinitionData : ScriptableObject
    {
        [Header("Основное")]
        public string enemyName = "Ассасин";
        public GameObject enemyPrefab; // Ссылка на префаб, который мы будем спавнить

        [Header("Базовые характеристики (Уровень 1)")]
        public float baseHealth = 100f;
        public float baseDamage = 10f;
        public float baseSpeed = 3f;
        public int baseBounty = 10;

        [Header("Рост за каждый уровень")]
        [Tooltip("Сколько ХП прибавляется за каждый уровень свыше первого")]
        public float healthPerLevel = 20f; 
        
        [Tooltip("Сколько урона прибавляется за каждый уровень")]
        public float damagePerLevel = 5f;
        
        [Tooltip("На сколько ускоряется враг (обычно мобы не ускоряются сильно, оставим 0)")]
        public float speedPerLevel = 0f;

        // Метод-помощник для расчета финального здоровья
        public float GetHealthForLevel(int level)
        {
            // Если уровень 1, то просто baseHealth. Если уровень 3, то baseHealth + (healthPerLevel * 2)
            return baseHealth + (healthPerLevel * Mathf.Max(0, level - 1));
        }

        // Метод-помощник для расчета финального урона
        public float GetDamageForLevel(int level)
        {
            return baseDamage + (damagePerLevel * Mathf.Max(0, level - 1));
        }
        
        // Метод-помощник для скорости
        public float GetSpeedForLevel(int level)
        {
            return baseSpeed + (speedPerLevel * Mathf.Max(0, level - 1));
        }
    }
}