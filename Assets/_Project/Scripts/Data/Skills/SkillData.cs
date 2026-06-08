using UnityEngine;

namespace _Project.Scripts.Data.Skills
{
    // Базовый класс для всех уникальных навыков
    public abstract class SkillData : ScriptableObject
    {
        [Header("Базовые параметры навыка")]
        [Tooltip("Время перезарядки навыка в секундах")]
        public float cooldown = 5f;
        
        [Tooltip("Максимальное количество зарядов (по умолчанию 1)")]
        public int maxCharges = 1;

        [Tooltip("Дистанция применения (например, дальность рывка или дистанция спавна турели)")]
        public float castDistance = 4f; 
        
        [Tooltip("Радиус действия (если применимо к навыку)")]
        public float effectRadius = 5f;
        
        [Tooltip("Стоимость маны для применения навыка")]
        public float manaCost = 20f;
    }
}