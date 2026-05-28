using Fusion;
using UnityEngine;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(fileName = "SchizoidSkillData", menuName = "Madman/Skills/Schizoid Skill Data")]
    public class SchizoidSkillData : SkillData
    {
        [Header("Пассивный навык: Квантовая нестабильность")]
        [Tooltip("Через сколько секунд без получения урона/в инвизе дается 1 стак нестабильности")]
        public float timePerInstabilityStack = 1.0f;
        
        [Tooltip("Максимальное количество стаков нестабильности")]
        public int maxInstabilityStacks = 4;

        [Tooltip("Множитель урона за ОДИН стак нестабильности (например, 0.2f = +20% за стак)")]
        public float damageMultiplierPerStack = 0.2f;

        [Header("Активный навык: Стеклянный лабиринт")]
        [Tooltip("Префаб голографического клона Шизоида")]
        public NetworkPrefabRef clonePrefab;
        
        [Tooltip("Длительность невидимости игрока в секундах")]
        public float invisibilityDuration = 4f;

        [Tooltip("Время жизни клона в секундах до автодетонации")]
        public float cloneDuration = 4f;

        [Tooltip("Скорость бега клона")]
        public float cloneMoveSpeed = 4.5f;
        
        [Tooltip("Радиус взрыва копии при детонации")]
        public float cloneExplosionRadius = 3f;
        
        [Tooltip("Базовый урон от взрыва клона")]
        public float cloneExplosionDamage = 150f;
        
        // Переменная effectRadius (радиус взрыва копии) наследуется из базового класса SkillData!

        [Header("Будущие улучшения (Параметризация перков)")]
        [Tooltip("Дополнительный множитель урона для первого выстрела из инвиза (Удар из тени: +200% = 3.0f)")]
        public float shadowStrikeDamageMultiplier = 3.0f;

        [Tooltip("Множитель скорости бега в инвизе (Паркур: +40% = 1.4f)")]
        public float invisibilitySpeedMultiplier = 1.4f;

        [Tooltip("Множитель урона выстрелов клона (Вооруженная проекция: 20% = 0.2f)")]
        public float cloneDamagePercentage = 0.2f;

        [Tooltip("Урон в секунду от ядовитого облака (Токсичная личность)")]
        public float toxicCloudDamagePerSecond = 25f;
    }
}