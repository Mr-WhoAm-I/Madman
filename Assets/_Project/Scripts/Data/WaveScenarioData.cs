using System.Collections.Generic;
using UnityEngine;

namespace _Project.Scripts.Data
{
    // 1. ПАЧКА ВРАГОВ: Что спавним, где и сколько
    [System.Serializable]
    public class SpawnBatch
    {
        public string batchName = "Ассасины в Зоне 1"; // Просто для удобства в Инспекторе
        public EnemyDefinitionData enemyDefinition;
        public int spawnZoneID;         // ID зоны на карте (1, 2, 3 и т.д.)
        public int enemyLevel = 1;      // Уровень (влияет на ХП и Урон)
        
        public int totalAmount = 10;    // Сколько ВСЕГО врагов такого типа нужно за волну
        public int spawnAtOnce = 5;     // Сколько спавнить за один раз (порция), чтобы не перегружать арену
        public float spawnDelay = 2f;   // Задержка между порциями
    }

    // 2. ВОЛНА: Набор пачек и настройки передышки
    [System.Serializable]
    public class WaveDefinition
    {
        public string waveName = "Волна 1";
        public List<SpawnBatch> spawnBatches = new(); // Кто спавнится в эту волну
        public float delayBeforeWave = 3f;            // Сколько секунд ждем перед началом волны (после магазина)
        public bool hasShopAfterWave = true;          // Открывать ли магазин после зачистки?
    }

    // 3. СЦЕНАРИЙ УРОВНЯ: Сам файл, который мы будем создавать в проекте
    [CreateAssetMenu(fileName = "New Level Scenario", menuName = "Madman/Level Scenario")]
    public class WaveScenarioData : ScriptableObject
    {
        [Header("Сценарий Арены")]
        public List<WaveDefinition> waves = new();
    }
}