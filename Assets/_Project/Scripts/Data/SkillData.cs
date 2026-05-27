using UnityEngine;

namespace _Project.Scripts.Data
{
    // Базовый класс для всех уникальных навыков
    public abstract class SkillData : ScriptableObject
    {
        [Header("Базовые параметры навыка")]
        public float cooldown = 5f;
    }
}